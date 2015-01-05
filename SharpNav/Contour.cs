// Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System.Collections.Generic;

using SharpNav.Geometry;

namespace SharpNav
{
	/// <summary>
	/// A contour is formed from a region.
	/// </summary>
	public class Contour
	{
		//simplified vertices have much less edges
		private ContourVertex[] vertices;
		private RegionId regionId;
		private Area area;

		/// <summary>
		/// Initializes a new instance of the <see cref="Contour"/> class.
		/// </summary>
		/// <param name="verts">The raw vertices of the contour.</param>
		/// <param name="region">The region ID of the contour.</param>
		/// <param name="area">The area ID of the contour.</param>
		/// <param name="borderSize">The size of the border.</param>
		public Contour(List<ContourVertex> verts, RegionId region, Area area, int borderSize)
		{
			this.vertices = verts.ToArray();
			this.regionId = region;
			this.area = area;

			//remove offset
			if (borderSize > 0)
			{
				for (int j = 0; j < vertices.Length; j++)
				{
					vertices[j].X -= borderSize;
					vertices[j].Z -= borderSize;
				}
			}
		}

		/// <summary>
		/// Gets the simplified vertices of the contour.
		/// </summary>
		public ContourVertex[] Vertices
		{
			get
			{
				return vertices;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the contour is "null" (has less than 3 vertices).
		/// </summary>
		public bool IsNull
		{
			get
			{
				//TODO operator overload == and != with null?
				if (vertices == null || vertices.Length < 3)
					return true;

				return false;
			}
		}

		/// <summary>
		/// Gets the area ID of the contour.
		/// </summary>
		public Area Area
		{
			get
			{
				return area;
			}
		}

		/// <summary>
		/// Gets the region ID of the contour.
		/// </summary>
		public RegionId RegionId
		{
			get
			{
				return regionId;
			}
		}

		/// <summary>
		/// Gets the 2D area of the contour. A positive area means the contour is going forwards, a negative
		/// area maens it is going backwards.
		/// </summary>
		public int Area2D
		{
			get
			{
				int area = 0;
				for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
				{
					ContourVertex vi = vertices[i], vj = vertices[j];
					area += vi.X * vj.Z - vj.X * vi.Z;
				}

				return (area + 1) / 2; 
			}
		}

		/// <summary>
		/// Merges another contour into this instance.
		/// </summary>
		/// <param name="contour">The contour to merge.</param>
		public void MergeWith(Contour contour)
		{
			int lengthA = vertices.Length;
			int lengthB = contour.vertices.Length;

			int ia, ib;
			GetClosestIndices(this, contour, out ia, out ib);

			//create a list with the capacity set to the max number of possible verts to avoid expanding the list.
			var newVerts = new List<ContourVertex>(vertices.Length + contour.vertices.Length + 2);

			//copy contour A
			for (int i = 0; i <= lengthA; i++)
				newVerts.Add(vertices[(ia + i) % lengthA]);

			//add contour B (other contour) to contour A (this contour)
			for (int i = 0; i <= lengthB; i++)
				newVerts.Add(contour.vertices[(ib + i) % lengthB]);

			vertices = newVerts.ToArray();
			
			//delete the other contour
			contour.vertices = null;
		}

		/// <summary>
		/// Finds the closest indices between two contours. Useful for merging contours.
		/// </summary>
		/// <param name="a">A contour.</param>
		/// <param name="b">Another contour.</param>
		/// <param name="indexA">The nearest index on contour A.</param>
		/// <param name="indexB">The nearest index on contour B.</param>
		private static void GetClosestIndices(Contour a, Contour b, out int indexA, out int indexB)
		{
			int closestDistance = int.MaxValue;
			int lengthA = a.vertices.Length;
			int lengthB = b.vertices.Length;

			indexA = -1;
			indexB = -1;

			for (int i = 0; i < lengthA; i++)
			{
				int vertA = i;
				int vertANext = (i + 1) % lengthA;
				int vertAPrev = (i + lengthA - 1) % lengthA;

				for (int j = 0; j < lengthB; j++)
				{
					int vertB = j;

					//vertB must be infront of vertA
					if (ContourVertex.IsLeft(ref a.vertices[vertAPrev], ref a.vertices[vertA], ref b.vertices[vertB]) &&
						ContourVertex.IsLeft(ref a.vertices[vertA], ref a.vertices[vertANext], ref b.vertices[vertB]))
					{
						int dx = b.vertices[vertB].X - a.vertices[vertA].X;
						int dz = b.vertices[vertB].Z - a.vertices[vertA].Z;
						int tempDist = dx * dx + dz * dz;
						if (tempDist < closestDistance)
						{
							indexA = i;
							indexB = j;
							closestDistance = tempDist;
						}
					}
				}
			}
		}

		/// <summary>
		/// Simplify the contours by reducing the number of edges
		/// </summary>
		/// <param name="rawVerts">Initial vertices</param>
		/// <param name="simplified">New and simplified vertices</param>
		/// <param name="maxError">Maximum error allowed</param>
		/// <param name="maxEdgeLen">The maximum edge length allowed</param>
		/// <param name="buildFlags">Flags determines how to split the long edges</param>
		public static void Simplify(List<ContourVertex> rawVerts, List<ContourVertex> simplified, float maxError, int maxEdgeLen, ContourBuildFlags buildFlags)
		{
			bool tesselateWallEdges = (buildFlags & ContourBuildFlags.TessellateWallEdges) == ContourBuildFlags.TessellateWallEdges;
			bool tesselateAreaEdges = (buildFlags & ContourBuildFlags.TessellateAreaEdges) == ContourBuildFlags.TessellateAreaEdges;

			//add initial points
			bool hasConnections = false;
			for (int i = 0; i < rawVerts.Count; i++)
			{
				if (rawVerts[i].RegionId.Id != 0)
				{
					hasConnections = true;
					break;
				}
			}

			if (hasConnections)
			{
				//contour has some portals to other regions
				//add new point to every location where region changes
				for (int i = 0, end = rawVerts.Count; i < end; i++)
				{
					int ii = (i + 1) % end;
					bool differentRegions = rawVerts[i].RegionId.Id != rawVerts[ii].RegionId.Id;
					bool areaBorders = RegionId.HasFlags(rawVerts[i].RegionId, RegionFlags.AreaBorder) != RegionId.HasFlags(rawVerts[ii].RegionId, RegionFlags.AreaBorder);

					if (differentRegions || areaBorders)
					{
						simplified.Add(new ContourVertex(rawVerts[i], i));
					}
				}
			}

			//add some points if thhere are no connections
			if (simplified.Count == 0)
			{
				//find lower-left and upper-right vertices of contour
				int lowerLeftX = rawVerts[0].X;
				int lowerLeftY = rawVerts[0].Y;
				int lowerLeftZ = rawVerts[0].Z;
				RegionId lowerLeftI = RegionId.Null;

				int upperRightX = rawVerts[0].X;
				int upperRightY = rawVerts[0].Y;
				int upperRightZ = rawVerts[0].Z;
				RegionId upperRightI = RegionId.Null;

				//iterate through points
				for (int i = 0; i < rawVerts.Count; i++)
				{
					int x = rawVerts[i].X;
					int y = rawVerts[i].Y;
					int z = rawVerts[i].Z;

					if (x < lowerLeftX || (x == lowerLeftX && z < lowerLeftZ))
					{
						lowerLeftX = x;
						lowerLeftY = y;
						lowerLeftZ = z;
						lowerLeftI = new RegionId(i);
					}

					if (x > upperRightX || (x == upperRightX && z > upperRightZ))
					{
						upperRightX = x;
						upperRightY = y;
						upperRightZ = z;
						upperRightI = new RegionId(i);
					}
				}

				//save the points
				simplified.Add(new ContourVertex(lowerLeftX, lowerLeftY, lowerLeftZ, lowerLeftI));
				simplified.Add(new ContourVertex(upperRightX, upperRightY, upperRightZ, upperRightI));
			}

			//add points until all points are within error tolerance of simplified slope
			int numPoints = rawVerts.Count;
			for (int i = 0; i < simplified.Count;)
			{
				int ii = (i + 1) % simplified.Count;

				//obtain (x, z) coordinates, along with region id
				int ax = simplified[i].X;
				int az = simplified[i].Z;
				int ai = (int)simplified[i].RegionId;

				int bx = simplified[ii].X;
				int bz = simplified[ii].Z;
				int bi = (int)simplified[ii].RegionId;

				float maxDeviation = 0;
				int maxi = -1;
				int ci, countIncrement, endi;

				//traverse segment in lexilogical order (try to go from smallest to largest coordinates?)
				if (bx > ax || (bx == ax && bz > az))
				{
					countIncrement = 1;
					ci = (int)(ai + countIncrement) % numPoints;
					endi = (int)bi;
				}
				else
				{
					countIncrement = numPoints - 1;
					ci = (int)(bi + countIncrement) % numPoints;
					endi = (int)ai;
				}

				//tessellate only outer edges or edges between areas
				if (rawVerts[ci].RegionId.Id == 0 || RegionId.HasFlags(rawVerts[ci].RegionId, RegionFlags.AreaBorder))
				{
					//find the maximum deviation
					while (ci != endi)
					{
						float deviation = Distance.PointToSegment2DSquared(rawVerts[ci].X, rawVerts[ci].Z, ax, az, bx, bz);

						if (deviation > maxDeviation)
						{
							maxDeviation = deviation;
							maxi = ci;
						}

						ci = (ci + countIncrement) % numPoints;
					}
				}

				//If max deviation is larger than accepted error, add new point
				if (maxi != -1 && maxDeviation > (maxError * maxError))
				{
					simplified.Insert(i + 1, new ContourVertex(rawVerts[maxi], maxi));
				}
				else
				{
					i++;
				}
			}

			//split too long edges
			if (maxEdgeLen > 0 && (tesselateAreaEdges || tesselateWallEdges))
			{
				for (int i = 0; i < simplified.Count;)
				{
					int ii = (i + 1) % simplified.Count;

					//get (x, z) coordinates along with region id
					int ax = simplified[i].X;
					int az = simplified[i].Z;
					int ai = (int)simplified[i].RegionId;

					int bx = simplified[ii].X;
					int bz = simplified[ii].Z;
					int bi = (int)simplified[ii].RegionId;

					//find maximum deviation from segment
					int maxi = -1;
					int ci = (int)(ai + 1) % numPoints;

					//tessellate only outer edges or edges between areas
					bool tess = false;

					//wall edges
					if (tesselateWallEdges && rawVerts[ci].RegionId.Id == 0)
						tess = true;

					//edges between areas
					if (tesselateAreaEdges && RegionId.HasFlags(rawVerts[ci].RegionId, RegionFlags.AreaBorder))
						tess = true;

					if (tess)
					{
						int dx = bx - ax;
						int dz = bz - az;
						if (dx * dx + dz * dz > maxEdgeLen * maxEdgeLen)
						{
							//round based on lexilogical direction (smallest to largest cooridinates, first by x.
							//if x coordinates are equal, then compare z coordinates)
							int n = bi < ai ? (bi + numPoints - ai) : (bi - ai);

							if (n > 1)
							{
								if (bx > ax || (bx == ax && bz > az))
									maxi = (int)(ai + n / 2) % numPoints;
								else
									maxi = (int)(ai + (n + 1) / 2) % numPoints;
							}
						}
					}

					//add new point
					if (maxi != -1)
					{
						simplified.Insert(i + 1, new ContourVertex(rawVerts[maxi], maxi));
					}
					else
					{
						i++;
					}
				}
			}

			for (int i = 0; i < simplified.Count; i++)
			{
				ContourVertex sv = simplified[i];

				//take edge vertex flag from current raw point and neighbor region from next raw point
				int ai = ((int)sv.RegionId + 1) % numPoints;
				RegionId bi = sv.RegionId;

				//save new region id
				sv.RegionId = RegionId.FromRawBits(((int)rawVerts[ai].RegionId & (RegionId.MaskId | (int)RegionFlags.AreaBorder)) | ((int)rawVerts[(int)bi].RegionId & (int)RegionFlags.VertexBorder));

				simplified[i] = sv;
			}
		}

		/// <summary>
		/// Removes degenerate segments from a simplified contour.
		/// </summary>
		/// <param name="simplified">The simplified contour.</param>
		public static void RemoveDegenerateSegments(List<ContourVertex> simplified)
		{
			//remove adjacent vertices which are equal on the xz-plane
			for (int i = 0; i < simplified.Count; i++)
			{
				int ni = i + 1;
				if (ni >= simplified.Count)
					ni = 0;

				if (simplified[i].X == simplified[ni].X &&
					simplified[i].Z == simplified[ni].Z)
				{
					//remove degenerate segment
					simplified.RemoveAt(i);
					i--;
				}
			}
		}
	}
}
