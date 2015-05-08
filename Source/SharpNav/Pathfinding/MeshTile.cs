// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using SharpNav.Collections;
using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// The MeshTile contains the map data for pathfinding
	/// </summary>
	public class MeshTile
	{
		/// <summary>
		/// Gets or sets the counter describing modifications to the tile
		/// </summary>
		public int Salt { get; set; }

		/// <summary>
		/// Gets or sets the index to the next free link
		/// </summary>
		public int LinksFreeList { get; set; }

		/// <summary>
		/// Gets or sets the header
		/// </summary>
		public PathfindingCommon.NavMeshInfo Header { get; set; }

		/// <summary>
		/// Gets or sets the PolyMesh polygons
		/// </summary>
		public Poly[] Polys { get; set; }

		/// <summary>
		/// Gets or sets the PolyMesh vertices
		/// </summary>
		public Vector3[] Verts { get; set; }

		/// <summary>
		/// Gets or sets the links between polygons
		/// </summary>
		public Link[] Links { get; set; }

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

		/// <summary>
		/// Gets or sets the bounding volume tree
		/// </summary>
		public BVTree BVTree { get; set; }

		/// <summary>
		/// Gets or sets the NavMeshBuilder data
		/// </summary>
		public NavMeshBuilder Data { get; set; }

		/// <summary>
		/// Gets or sets the next MeshTile
		/// </summary>
		public MeshTile Next { get; set; }

		/// <summary>
		/// Given a point, find the closest point on that poly.
		/// </summary>
		/// <param name="poly">The current polygon.</param>
		/// <param name="pos">The current position</param>
		/// <param name="closest">Reference to the closest point</param>
		public void ClosestPointOnPoly(Poly poly, Vector3 pos, ref Vector3 closest)
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
			Poly poly = Polys[indexPoly];

			//Off-mesh connections don't have detail polygons
			if (Polys[indexPoly].PolyType == PolygonType.OffMeshConnection)
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
		public void ClosestPointOnPolyBoundary(Poly poly, Vector3 pos, out Vector3 closest)
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
			Poly poly = Polys[indexPoly];
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
		public void ClosestPointOnPolyOffMeshConnection(Poly poly, Vector3 pos, out Vector3 closest)
		{
			Vector3 v0 = Verts[poly.Verts[0]];
			Vector3 v1 = Verts[poly.Verts[1]];
			float d0 = (pos - v0).Length();
			float d1 = (pos - v1).Length();
			float u = d0 / (d0 + d1);
			closest = Vector3.Lerp(v0, v1, u);
		}
	}
}
