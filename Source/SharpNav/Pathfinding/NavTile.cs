// Copyright (c) 2014-2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using SharpNav.Collections;
using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
using Vector2 = Microsoft.Xna.Framework.Vector2;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
using Vector2 = OpenTK.Vector2;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
using Vector2 = SharpDX.Vector2;
#endif

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// The MeshTile contains the map data for pathfinding
	/// </summary>
	public class NavTile : IEquatable<NavTile>
	{
		private NavPolyIdManager idManager;
		private NavPolyId baseRef;

		public NavTile(int x, int y, int layer, NavPolyIdManager manager, NavPolyId baseRef)
			:this(new Vector2i(x, y), layer, manager, baseRef)
		{
		}

		public NavTile(Vector2i location, int layer, NavPolyIdManager manager, NavPolyId baseRef)
		{
			this.Location = location;
			this.Layer = layer;
			this.idManager = manager;
			this.baseRef = baseRef;
		}

		public Vector2i Location { get; private set; }

		public int Layer { get; private set; }

		/// <summary>
		/// Gets or sets the counter describing modifications to the tile
		/// </summary>
		public int Salt { get; set; }

		/// <summary>
		/// Gets or sets the PolyMesh polygons
		/// </summary>
		public NavPoly[] Polys { get; set; }

		public int PolyCount { get; set; }

		/// <summary>
		/// Gets or sets the PolyMesh vertices
		/// </summary>
		public Vector3[] Verts { get; set; }

		/// <summary>
		/// Gets or sets the PolyMeshDetail data
		/// </summary>
		public PolyMeshDetail.MeshData[] DetailMeshes { get; set; }

		/// <summary>
		/// Gets or sets the PolyMeshDetail vertices
		/// </summary>
		public Vector3[] DetailVerts { get; set; }

		/// <summary>
		/// Gets or sets the PolyMeshDetail triangles
		/// </summary>
		public PolyMeshDetail.TriangleData[] DetailTris { get; set; }

		/// <summary>
		/// Gets or sets the OffmeshConnections
		/// </summary>
		public OffMeshConnection[] OffMeshConnections { get; set; }

		public int OffMeshConnectionCount { get; set; }

		/// <summary>
		/// Gets or sets the bounding volume tree
		/// </summary>
		public BVTree BVTree { get; set; }

		public float BvQuantFactor { get; set; }

		public int BvNodeCount { get; set; }

		public BBox3 Bounds { get; set; }

		public float WalkableClimb { get; set; }

		/// <summary>
		/// Allocate links for each of the tile's polygons' vertices
		/// </summary>
		public void ConnectIntLinks()
		{
			//Iterate through all the polygons
			for (int i = 0; i < PolyCount; i++)
			{
				NavPoly p = Polys[i];

				//Avoid Off-Mesh Connection polygons
				if (p.PolyType == NavPolyType.OffMeshConnection)
					continue;

				//Build edge links
				for (int j = p.VertCount - 1; j >= 0; j--)
				{
					//Skip hard and non-internal edges
					if (p.Neis[j] == 0 || Link.IsExternal(p.Neis[j]))
						continue;

					NavPolyId id;
					idManager.SetPolyIndex(ref baseRef, p.Neis[j] - 1, out id);

					//Initialize a new link
					Link link = new Link();
					link.Reference = id;
					link.Edge = j;
					link.Side = BoundarySide.Internal;
					link.BMin = link.BMax = 0;
					p.Links.Add(link);
				}
			}
		}

		/// <summary>
		/// Begin creating off-mesh links between the tile polygons.
		/// </summary>
		public void BaseOffMeshLinks()
		{
			//Base off-mesh connection start points
			for (int i = 0; i < OffMeshConnectionCount; i++)
			{
				int con = i;
				OffMeshConnection omc = OffMeshConnections[con];

				Vector3 extents = new Vector3(omc.Radius, WalkableClimb, omc.Radius);

				//Find polygon to connect to
				Vector3 p = omc.Pos0;
				Vector3 nearestPt = new Vector3();
				NavPolyId reference = FindNearestPoly(p, extents, ref nearestPt);
				if (reference == NavPolyId.Null)
					continue;

				//Do extra checks
				if ((nearestPt.X - p.X) * (nearestPt.X - p.X) + (nearestPt.Z - p.Z) * (nearestPt.Z - p.Z) >
					OffMeshConnections[con].Radius * OffMeshConnections[con].Radius)
					continue;

				NavPoly poly = this.Polys[omc.Poly];

				//Make sure location is on current mesh
				Verts[poly.Verts[0]] = nearestPt;

				Link link = new Link();
				link.Reference = reference;
				link.Edge = 0;
				link.Side = BoundarySide.Internal;
				poly.Links.Add(link);

				//Start end-point always conects back to off-mesh connection
				int landPolyIdx = idManager.DecodePolyIndex(ref reference);
				NavPolyId id;
				idManager.SetPolyIndex(ref baseRef, OffMeshConnections[con].Poly, out id);

				Link link2 = new Link();
				link2.Reference = id;
				link2.Edge = 0xff;
				link2.Side = BoundarySide.Internal;
				Polys[landPolyIdx].Links.Add(link2);
			}
		}

		/// <summary>
		/// Connect polygons from two different tiles.
		/// </summary>
		/// <param name="target">Target Tile</param>
		/// <param name="side">Polygon edge</param>
		public void ConnectExtLinks(NavTile target, BoundarySide side)
		{
			//Connect border links
			for (int i = 0; i < PolyCount; i++)
			{
				int numPolyVerts = Polys[i].VertCount;

				for (int j = 0; j < numPolyVerts; j++)
				{
					//Skip non-portal edges
					if ((Polys[i].Neis[j] & Link.External) == 0)
						continue;

					BoundarySide dir = (BoundarySide)(Polys[i].Neis[j] & 0xff);
					if (side != BoundarySide.Internal && dir != side)
						continue;

					//Create new links
					Vector3 va = Verts[Polys[i].Verts[j]];
					Vector3 vb = Verts[Polys[i].Verts[(j + 1) % numPolyVerts]];
					List<NavPolyId> nei = new List<NavPolyId>(4);
					List<float> neia = new List<float>(4 * 2);
					target.FindConnectingPolys(va, vb, dir.GetOpposite(), nei, neia);

					//Iterate through neighbors
					for (int k = 0; k < nei.Count; k++)
					{
						Link link = new Link();
						link.Reference = nei[k];
						link.Edge = j;
						link.Side = dir;
						Polys[i].Links.Add(link);

						//Compress portal limits to a value
						if (dir == BoundarySide.PlusX || dir == BoundarySide.MinusX)
						{
							float tmin = (neia[k * 2 + 0] - va.Z) / (vb.Z - va.Z);
							float tmax = (neia[k * 2 + 1] - va.Z) / (vb.Z - va.Z);

							if (tmin > tmax)
							{
								float temp = tmin;
								tmin = tmax;
								tmax = temp;
							}

							link.BMin = (int)(MathHelper.Clamp(tmin, 0.0f, 1.0f) * 255.0f);
							link.BMax = (int)(MathHelper.Clamp(tmax, 0.0f, 1.0f) * 255.0f);
						}
						else if (dir == BoundarySide.PlusZ || dir == BoundarySide.MinusZ)
						{
							float tmin = (neia[k * 2 + 0] - va.X) / (vb.X - va.X);
							float tmax = (neia[k * 2 + 1] - va.X) / (vb.X - va.X);

							if (tmin > tmax)
							{
								float temp = tmin;
								tmin = tmax;
								tmax = temp;
							}

							link.BMin = (int)(MathHelper.Clamp(tmin, 0.0f, 1.0f) * 255.0f);
							link.BMax = (int)(MathHelper.Clamp(tmax, 0.0f, 1.0f) * 255.0f);
						}
					}
				}
			}
		}

		/// <summary>
		/// Connect Off-Mesh links between polygons from two different tiles.
		/// </summary>
		/// <param name="target">Target Tile</param>
		/// <param name="side">Polygon edge</param>
		public void ConnectExtOffMeshLinks(NavTile target, BoundarySide side)
		{
			//Connect off-mesh links, specifically links which land from target tile to this tile
			BoundarySide oppositeSide = side.GetOpposite();

			//Iterate through all the off-mesh connections of target tile
			for (int i = 0; i < target.OffMeshConnectionCount; i++)
			{
				OffMeshConnection targetCon = target.OffMeshConnections[i];
				if (targetCon.Side != oppositeSide)
					continue;

				NavPoly targetPoly = target.Polys[targetCon.Poly];

				//Skip off-mesh connections which start location could not be connected at all
				if (targetPoly.Links.Count == 0)
					continue;

				Vector3 extents = new Vector3(targetCon.Radius, target.WalkableClimb, targetCon.Radius);

				//Find polygon to connect to
				Vector3 p = targetCon.Pos1;
				Vector3 nearestPt = new Vector3();
				NavPolyId reference = FindNearestPoly(p, extents, ref nearestPt);
				if (reference == NavPolyId.Null)
					continue;

				//Further checks
				if ((nearestPt.X - p.X) * (nearestPt.X - p.X) + (nearestPt.Z - p.Z) * (nearestPt.Z - p.Z) >
					(targetCon.Radius * targetCon.Radius))
					continue;

				//Make sure the location is on the current mesh
				target.Verts[targetPoly.Verts[1]] = nearestPt;

				//Link off-mesh connection to target poly
				Link link = new Link();
				link.Reference = reference;
				link.Edge = i;
				link.Side = oppositeSide;
				target.Polys[i].Links.Add(link);

				//link target poly to off-mesh connection
				if ((targetCon.Flags & OffMeshConnectionFlags.Bidirectional) != 0)
				{
					int landPolyIdx = idManager.DecodePolyIndex(ref reference);
					NavPolyId id;
					id = target.baseRef;
					idManager.SetPolyIndex(ref id, targetCon.Poly, out id);

					Link bidiLink = new Link();
					bidiLink.Reference = id;
					bidiLink.Edge = 0xff;
					bidiLink.Side = side;
					Polys[landPolyIdx].Links.Add(bidiLink);
				}
			}
		}

		/// <summary>
		/// Search for neighbor polygons in the tile.
		/// </summary>
		/// <param name="va">Vertex A</param>
		/// <param name="vb">Vertex B</param>
		/// <param name="side">Polygon edge</param>
		/// <param name="con">Resulting Connection polygon</param>
		/// <param name="conarea">Resulting Connection area</param>
		public void FindConnectingPolys(Vector3 va, Vector3 vb, BoundarySide side, List<NavPolyId> con, List<float> conarea)
		{
			Vector2 amin = Vector2.Zero;
			Vector2 amax = Vector2.Zero;
			CalcSlabEndPoints(va, vb, amin, amax, side);
			float apos = GetSlabCoord(va, side);

			//Remove links pointing to 'side' and compact the links array
			Vector2 bmin = Vector2.Zero;
			Vector2 bmax = Vector2.Zero;

			//Iterate through all the tile's polygons
			for (int i = 0; i < PolyCount; i++)
			{
				int numPolyVerts = Polys[i].VertCount;

				//Iterate through all the vertices
				for (int j = 0; j < numPolyVerts; j++)
				{
					//Skip edges which do not point to the right side
					if (Polys[i].Neis[j] != (Link.External | (int)side))
						continue;

					//Grab two adjacent vertices
					Vector3 vc = Verts[Polys[i].Verts[j]];
					Vector3 vd = Verts[Polys[i].Verts[(j + 1) % numPolyVerts]];
					float bpos = GetSlabCoord(vc, side);

					//Segments are not close enough
					if (Math.Abs(apos - bpos) > 0.01f)
						continue;

					//Check if the segments touch
					CalcSlabEndPoints(vc, vd, bmin, bmax, side);

					//Skip if slabs don't overlap
					if (!OverlapSlabs(amin, amax, bmin, bmax, 0.01f, WalkableClimb))
						continue;

					//Add return value
					if (con.Count < con.Capacity)
					{
						conarea.Add(Math.Max(amin.X, bmin.X));
						conarea.Add(Math.Min(amax.X, bmax.X));

						NavPolyId id;
						idManager.SetPolyIndex(ref baseRef, i, out id);
						con.Add(id);
					}

					break;
				}
			}
		}

		/// <summary>
		/// Find the closest polygon possible in the tile under certain constraints.
		/// </summary>
		/// <param name="tile">Current tile</param>
		/// <param name="center">Center starting point</param>
		/// <param name="extents">Range of search</param>
		/// <param name="nearestPt">Resulting nearest point</param>
		/// <returns>Polygon Reference which contains nearest point</returns>
		public NavPolyId FindNearestPoly(Vector3 center, Vector3 extents, ref Vector3 nearestPt)
		{
			BBox3 bounds;
			bounds.Min = center - extents;
			bounds.Max = center + extents;

			//Get nearby polygons from proximity grid
			List<NavPolyId> polys = new List<NavPolyId>(128);
			int polyCount = this.QueryPolygons(bounds, polys);

			//Find nearest polygon amongst the nearby polygons
			NavPolyId nearest = NavPolyId.Null;
			float nearestDistanceSqr = float.MaxValue;

			//Iterate throuh all the polygons
			for (int i = 0; i < polyCount; i++)
			{
				NavPolyId reference = polys[i];
				Vector3 closestPtPoly = new Vector3();
				ClosestPointOnPoly(idManager.DecodePolyIndex(ref reference), center, ref closestPtPoly);
				float d = (center - closestPtPoly).LengthSquared();
				if (d < nearestDistanceSqr)
				{
					nearestPt = closestPtPoly;
					nearestDistanceSqr = d;
					nearest = reference;
				}
			}

			return nearest;
		}

		/// <summary>
		/// Find all the polygons within a certain bounding box.
		/// </summary>
		/// <param name="tile">Current tile</param>
		/// <param name="qbounds">The bounds</param>
		/// <param name="polys">List of polygons</param>
		/// <returns>Number of polygons found</returns>
		public int QueryPolygons(BBox3 qbounds, List<NavPolyId> polys)
		{
			if (BVTree.Count != 0)
			{
				int node = 0;
				int end = BvNodeCount;
				Vector3 tbmin = Bounds.Min;
				Vector3 tbmax = Bounds.Max;

				//Clamp query box to world box
				Vector3 qbmin = qbounds.Min;
				Vector3 qbmax = qbounds.Max;
				PolyBounds b;
				float bminx = MathHelper.Clamp(qbmin.X, tbmin.X, tbmax.X) - tbmin.X;
				float bminy = MathHelper.Clamp(qbmin.Y, tbmin.Y, tbmax.Y) - tbmin.Y;
				float bminz = MathHelper.Clamp(qbmin.Z, tbmin.Z, tbmax.Z) - tbmin.Z;
				float bmaxx = MathHelper.Clamp(qbmax.X, tbmin.X, tbmax.X) - tbmin.X;
				float bmaxy = MathHelper.Clamp(qbmax.Y, tbmin.Y, tbmax.Y) - tbmin.Y;
				float bmaxz = MathHelper.Clamp(qbmax.Z, tbmin.Z, tbmax.Z) - tbmin.Z;

				const int MinMask = unchecked((int)0xfffffffe);

				b.Min.X = (int)(bminx * BvQuantFactor) & MinMask;
				b.Min.Y = (int)(bminy * BvQuantFactor) & MinMask;
				b.Min.Z = (int)(bminz * BvQuantFactor) & MinMask;
				b.Max.X = (int)(bmaxx * BvQuantFactor + 1) | 1;
				b.Max.Y = (int)(bmaxy * BvQuantFactor + 1) | 1;
				b.Max.Z = (int)(bmaxz * BvQuantFactor + 1) | 1;

				//traverse tree
				while (node < end)
				{
					BVTree.Node bvNode = BVTree[node];
					bool overlap = PolyBounds.Overlapping(ref b, ref bvNode.Bounds);
					bool isLeafNode = bvNode.Index >= 0;

					if (isLeafNode && overlap)
					{
						NavPolyId polyRef;
						idManager.SetPolyIndex(ref baseRef, bvNode.Index, out polyRef);
						polys.Add(polyRef);
					}

					if (overlap || isLeafNode)
					{
						node++;
					}
					else
					{
						int escapeIndex = -bvNode.Index;
						node += escapeIndex;
					}
				}

				return polys.Count;
			}
			else
			{
				BBox3 b;

				for (int i = 0; i < PolyCount; i++)
				{
					var poly = Polys[i];

					//don't return off-mesh connection polygons
					if (poly.PolyType == NavPolyType.OffMeshConnection)
						continue;

					//calculate polygon bounds
					b.Max = b.Min = Verts[poly.Verts[0]];
					for (int j = 1; j < poly.VertCount; j++)
					{
						Vector3 v = Verts[poly.Verts[j]];
						Vector3Extensions.ComponentMin(ref b.Min, ref v, out b.Min);
						Vector3Extensions.ComponentMax(ref b.Max, ref v, out b.Max);
					}

					if (BBox3.Overlapping(ref qbounds, ref b))
					{
						NavPolyId polyRef;
						idManager.SetPolyIndex(ref baseRef, i, out polyRef);
						polys.Add(polyRef);
					}
				}

				return polys.Count;
			}
		}

		/// <summary>
		/// Find the slab endpoints based off of the 'side' value.
		/// </summary>
		/// <param name="va">Vertex A</param>
		/// <param name="vb">Vertex B</param>
		/// <param name="bmin">Minimum bounds</param>
		/// <param name="bmax">Maximum bounds</param>
		/// <param name="side">The side</param>
		public static void CalcSlabEndPoints(Vector3 va, Vector3 vb, Vector2 bmin, Vector2 bmax, BoundarySide side)
		{
			if (side == BoundarySide.PlusX || side == BoundarySide.MinusX)
			{
				if (va.Z < vb.Z)
				{
					bmin.X = va.Z;
					bmin.Y = va.Y;

					bmax.X = vb.Z;
					bmax.Y = vb.Y;
				}
				else
				{
					bmin.X = vb.Z;
					bmin.Y = vb.Y;

					bmax.X = va.Z;
					bmax.Y = va.Y;
				}
			}
			else if (side == BoundarySide.PlusZ || side == BoundarySide.MinusZ)
			{
				if (va.X < vb.X)
				{
					bmin.X = va.X;
					bmin.Y = va.Y;

					bmax.X = vb.X;
					bmax.Y = vb.Y;
				}
				else
				{
					bmin.X = vb.X;
					bmin.Y = vb.Y;

					bmax.X = va.X;
					bmax.Y = va.Y;
				}
			}
		}

		/// <summary>
		/// Return the proper slab coordinate value depending on the 'side' value.
		/// </summary>
		/// <param name="va">Vertex A</param>
		/// <param name="side">The side</param>
		/// <returns>Slab coordinate value</returns>
		public static float GetSlabCoord(Vector3 va, BoundarySide side)
		{
			if (side == BoundarySide.PlusX || side == BoundarySide.MinusX)
				return va.X;
			else if (side == BoundarySide.PlusZ || side == BoundarySide.MinusZ)
				return va.Z;

			return 0;
		}

		/// <summary>
		/// Check if two slabs overlap.
		/// </summary>
		/// <param name="amin">Minimum bounds A</param>
		/// <param name="amax">Maximum bounds A</param>
		/// <param name="bmin">Minimum bounds B</param>
		/// <param name="bmax">Maximum bounds B</param>
		/// <param name="px">Point's x</param>
		/// <param name="py">Point's y</param>
		/// <returns>True if slabs overlap</returns>
		public static bool OverlapSlabs(Vector2 amin, Vector2 amax, Vector2 bmin, Vector2 bmax, float px, float py)
		{
			//Check for horizontal overlap
			//Segment shrunk a little so that slabs which touch at endpoints aren't connected
			float minX = Math.Max(amin.X + px, bmin.X + px);
			float maxX = Math.Min(amax.X - px, bmax.X - px);
			if (minX > maxX)
				return false;

			//Check vertical overlap
			float leftSlope = (amax.Y - amin.Y) / (amax.X - amin.X);
			float leftConstant = amin.Y - leftSlope * amin.X;
			float rightSlope = (bmax.Y - bmin.Y) / (bmax.X - bmin.X);
			float rightConstant = bmin.Y - rightSlope * bmin.X;
			float leftMinY = leftSlope * minX + leftConstant;
			float leftMaxY = leftSlope * maxX + leftConstant;
			float rightMinY = rightSlope * minX + rightConstant;
			float rightMaxY = rightSlope * maxX + rightConstant;
			float dmin = rightMinY - leftMinY;
			float dmax = rightMaxY - leftMaxY;

			//Crossing segments always overlap
			if (dmin * dmax < 0)
				return true;

			//Check for overlap at endpoints
			float threshold = (py * 2) * (py * 2);
			if (dmin * dmin <= threshold || dmax * dmax <= threshold)
				return true;

			return false;
		}

		/// <summary>
		/// Given a point, find the closest point on that poly.
		/// </summary>
		/// <param name="poly">The current polygon.</param>
		/// <param name="pos">The current position</param>
		/// <param name="closest">Reference to the closest point</param>
		public void ClosestPointOnPoly(NavPoly poly, Vector3 pos, ref Vector3 closest)
		{
			int indexPoly = 0;
			for (int i = 0; i < Polys.Length; i++)
			{
				if (Polys[i] == poly)
				{
					indexPoly = i;
					break;
				}
			}

			ClosestPointOnPoly(indexPoly, pos, ref closest);
		}

		/// <summary>
		/// Given a point, find the closest point on that poly.
		/// </summary>
		/// <param name="indexPoly">The current poly's index</param>
		/// <param name="pos">The current position</param>
		/// <param name="closest">Reference to the closest point</param>
		public void ClosestPointOnPoly(int indexPoly, Vector3 pos, ref Vector3 closest)
		{
			NavPoly poly = Polys[indexPoly];

			//Off-mesh connections don't have detail polygons
			if (Polys[indexPoly].PolyType == NavPolyType.OffMeshConnection)
			{
				ClosestPointOnPolyOffMeshConnection(poly, pos, out closest);
				return;
			}

			ClosestPointOnPolyBoundary(poly, pos, out closest);

			float h;
			if (ClosestHeight(indexPoly, pos, out h))
				closest.Y = h;
		}

		/// <summary>
		/// Given a point, find the closest point on that poly.
		/// </summary>
		/// <param name="poly">The current polygon.</param>
		/// <param name="pos">The current position</param>
		/// <param name="closest">Reference to the closest point</param>
		public void ClosestPointOnPolyBoundary(NavPoly poly, Vector3 pos, out Vector3 closest)
		{
			//Clamp point to be inside the polygon
			Vector3[] verts = new Vector3[PathfindingCommon.VERTS_PER_POLYGON];
			float[] edgeDistance = new float[PathfindingCommon.VERTS_PER_POLYGON];
			float[] edgeT = new float[PathfindingCommon.VERTS_PER_POLYGON];
			int numPolyVerts = poly.VertCount;
			for (int i = 0; i < numPolyVerts; i++)
				verts[i] = Verts[poly.Verts[i]];

			bool inside = Distance.PointToPolygonEdgeSquared(pos, verts, numPolyVerts, edgeDistance, edgeT);
			if (inside)
			{
				//Point is inside the polygon
				closest = pos;
			}
			else
			{
				//Point is outside the polygon
				//Clamp to nearest edge
				float minDistance = float.MaxValue;
				int minIndex = -1;
				for (int i = 0; i < numPolyVerts; i++)
				{
					if (edgeDistance[i] < minDistance)
					{
						minDistance = edgeDistance[i];
						minIndex = i;
					}
				}

				Vector3 va = verts[minIndex];
				Vector3 vb = verts[(minIndex + 1) % numPolyVerts];
				closest = Vector3.Lerp(va, vb, edgeT[minIndex]);
			}
		}

		/// <summary>
		/// Find the distance from a point to a triangle.
		/// </summary>
		/// <param name="indexPoly">Current polygon's index</param>
		/// <param name="pos">Current position</param>
		/// <param name="h">Resulting height</param>
		/// <returns>True, if a height is found. False, if otherwise.</returns>
		public bool ClosestHeight(int indexPoly, Vector3 pos, out float h)
		{
			NavPoly poly = Polys[indexPoly];
			PolyMeshDetail.MeshData pd = DetailMeshes[indexPoly];

			//find height at the location
			for (int j = 0; j < DetailMeshes[indexPoly].TriangleCount; j++)
			{
				PolyMeshDetail.TriangleData t = DetailTris[pd.TriangleIndex + j];
				Vector3[] v = new Vector3[3];

				for (int k = 0; k < 3; k++)
				{
					if (t[k] < poly.VertCount)
						v[k] = Verts[poly.Verts[t[k]]];
					else
						v[k] = DetailVerts[pd.VertexIndex + (t[k] - poly.VertCount)];
				}

				if (Distance.PointToTriangle(pos, v[0], v[1], v[2], out h))
					return true;
			}

			h = float.MaxValue;
			return false;
		}

		/// <summary>
		/// Find the closest point on an offmesh connection, which is in between the two points.
		/// </summary>
		/// <param name="poly">Current polygon</param>
		/// <param name="pos">Current position</param>
		/// <param name="closest">Resulting point that is closest.</param>
		public void ClosestPointOnPolyOffMeshConnection(NavPoly poly, Vector3 pos, out Vector3 closest)
		{
			Vector3 v0 = Verts[poly.Verts[0]];
			Vector3 v1 = Verts[poly.Verts[1]];
			float d0 = (pos - v0).Length();
			float d1 = (pos - v1).Length();
			float u = d0 / (d0 + d1);
			closest = Vector3.Lerp(v0, v1, u);
		}

		public static bool operator==(NavTile left, NavTile right)
		{
			if (object.ReferenceEquals(left, right))
				return true;
			
			if (((object)left == null) || ((object)right == null))
				return false;

			return left.Equals(right);
		}

		public static bool operator !=(NavTile left, NavTile right)
		{
			return !(left == right);
		}

		public bool Equals(NavTile other)
		{
			//TODO use more for equals?
			return this.Location == other.Location && this.Layer == other.Layer;
		}

		public override bool Equals(object obj)
		{
			NavTile other = obj as NavTile;
			if (other != null)
				return this.Equals(other);
			else
				return false;
		}

		public override int GetHashCode()
		{
			//FNV hash
			unchecked
			{
				int h1 = (int)2166136261;
				int h2 = (int)16777619;
				h1 = (h1 * h2) ^ Location.X;
				h1 = (h1 * h2) ^ Location.Y;
				h1 = (h1 * h2) ^ Layer;
				return h1;
			}
		}

		public override string ToString()
		{
			return "{ Location: " + Location.ToString() + ", Layer: " + Layer.ToString() + "}";
		}
	}
}
