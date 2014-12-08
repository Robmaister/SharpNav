#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// A contour is formed from a region.
	/// </summary>
	public class Contour
	{
		//simplified vertices have much less edges
		private ContourVertex[] vertices;

		//raw vertices derived directly from CompactHeightfield
		private ContourVertex[] rawVertices;

		private RegionId regionId;
		private AreaId area;

		/// <summary>
		/// Initializes a new instance of the <see cref="Contour"/> class.
		/// </summary>
		/// <param name="simplified">The simplified vertices of the contour.</param>
		/// <param name="verts">The raw vertices of the contour.</param>
		/// <param name="reg">The region ID of the contour.</param>
		/// <param name="area">The area ID of the contour.</param>
		/// <param name="borderSize">The size of the border.</param>
		public Contour(IEnumerable<ContourVertex> simplified, IEnumerable<ContourVertex> verts, RegionId reg, AreaId area, int borderSize, bool storeRaw = false)
		{
			vertices = simplified.ToArray();

			if (storeRaw)
				rawVertices = verts.ToArray();

			regionId = reg;
			this.area = area;

			//remove offset
			if (borderSize > 0)
			{
				for (int j = 0; j < vertices.Length; j++)
				{
					vertices[j].X -= borderSize;
					vertices[j].Z -= borderSize;
				}

				if (storeRaw)
				{
					for (int j = 0; j < rawVertices.Length; j++)
					{
						rawVertices[j].X -= borderSize;
						rawVertices[j].Z -= borderSize;
					}
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
		/// Gets the raw vertices of the contour.
		/// </summary>
		public ContourVertex[] RawVertices
		{
			get
			{
				return rawVertices;
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
				if (vertices.Length < 3)
					return true;

				return false;
			}
		}

		/// <summary>
		/// Gets the area ID of the contour.
		/// </summary>
		public AreaId Area
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
	}
}
