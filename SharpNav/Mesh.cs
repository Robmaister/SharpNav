#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using SharpNav.Geometry;

namespace SharpNav
{
	public class Mesh
	{
		private int nverts;
		private int npolys;

		private int[] verts;
		private int[] polys;
		private int[] regionIds;
		private int[] flags; //flags for a polygon
		private AreaFlags[] areas;
		
		private int maxPolys;
		private int numVertsPerPoly;

		//copied data from OpenHeightfield
		private BBox3 bounds;
		private float cellSize;
		private float cellHeight;
		private int borderSize;

		private const int VERTEX_BUCKET_COUNT = (1 << 12);

		/// <summary>
		/// Create polygons out of a set of contours
		/// </summary>
		/// <param name="contSet">The ContourSet to use</param>
		/// <param name="numVertsPerPoly">Number vertices per polygon</param>
		public Mesh(ContourSet contSet, int numVertsPerPoly)
		{
			this.bounds = contSet.Bounds;
			this.cellSize = contSet.CellSize;
			this.cellHeight = contSet.CellHeight;
			this.borderSize = contSet.BorderSize;

			int maxVertices = 0;
			int maxTris = 0;
			int maxVertsPerCont = 0;
			for (int i = 0; i < contSet.Contours.Count; i++)
			{
				//skip null contours
				if (contSet.Contours[i].vertices.Count / 4 < 3) continue;
				maxVertices += contSet.Contours[i].vertices.Count / 4;
				maxTris += contSet.Contours[i].vertices.Count / 4 - 2;
				maxVertsPerCont += Math.Max(maxVertsPerCont, contSet.Contours[i].vertices.Count / 4);
			}

			int[] vFlags = new int[maxVertices];
			for (int i = 0; i < vFlags.Length; i++)
				vFlags[i] = 0;

			this.verts = new int[maxVertices * 3];
			this.polys = new int[maxTris * numVertsPerPoly * 2];
			this.regionIds = new int[maxTris];
			this.areas = new AreaFlags[maxTris];

			this.nverts = 0;
			this.npolys = 0;
			this.numVertsPerPoly = numVertsPerPoly;
			this.maxPolys = maxTris;

			for (int i = 0; i < this.verts.Length; i++)
				this.verts[i] = 0;
			for (int i = 0; i < this.polys.Length; i++)
				this.polys[i] = 0xff;
			for (int i = 0; i < this.regionIds.Length; i++)
				this.regionIds[i] = 0;
			for (int i = 0; i < this.areas.Length; i++)
				this.areas[i] = AreaFlags.Null;

			int[] nextVert = new int[maxVertices];
			for (int i = 0; i < nextVert.Length; i++)
				nextVert[i] = 0;

			int[] firstVert = new int[VERTEX_BUCKET_COUNT];
			for (int i = 0; i < firstVert.Length; i++)
				firstVert[i] = -1;

			int[] indices = new int[maxVertsPerCont];
			int[] tris = new int[maxVertsPerCont * 3];
			int[] polys = new int[(maxVertsPerCont + 1) * numVertsPerPoly];

			for (int i = 0; i < contSet.Contours.Count; i++)
			{
				ContourSet.Contour cont = contSet.Contours[i];

				//skip null contours
				if (cont.vertices.Count / 4 < 3)
					continue;

				//triangulate contours
				for (int j = 0; j < cont.vertices.Count; j++)
					indices[j] = j;

				int ntris = 0; //insert triangule function
				if (ntris <= 0)
				{
					//shouldn't happen 
					ntris = -ntris;
				}

				//add and merge vertices
				for (int j = 0; j < cont.vertices.Count / 4; j++)
				{
					int v = j * 4;
					indices[j] = 0; //insert addVertex function

					if ((cont.vertices[v + 3] & ContourSet.BORDER_VERTEX) != 0)
					{
						//the vertex should be removed
						vFlags[indices[j]] = 1;
					}
				}
				
				//builds initial polygons
				int npolys = 0;
				for (int j = 0; j < polys.Length; j++)
					polys[i] = 0xff;
				for (int j = 0; j < ntris; j++)
				{
					int t = j * 3;
					if (tris[t + 0] != tris[t + 1] && tris[t + 0] != tris[t + 2] && tris[t + 1] != tris[t + 2])
					{
						polys[npolys * numVertsPerPoly + 0] = indices[tris[t + 0]];
						polys[npolys * numVertsPerPoly + 1] = indices[tris[t + 1]];
						polys[npolys * numVertsPerPoly + 2] = indices[tris[t + 2]];
						npolys++;
					}
				}
				if (npolys == 0)
					continue;

				//merge polygons
				if (numVertsPerPoly > 3)
				{
					for (; ; )
					{
						//find best polygons
						int bestMergeVal = 0;
						int bestPa = 0, bestPb = 0, bestEa = 0, bestEb = 0;

						for (int j = 0; j < npolys - 1; j++)
						{
							int pj = j * numVertsPerPoly;
							for (int k = j + 1; k < npolys; k++)
							{
								int pk = k * numVertsPerPoly;
								int ea = 0, eb = 0;
								int v = 0; //call getPolyMergeValue function
								if (v > bestMergeVal)
								{
									bestMergeVal = v;
									bestPa = j;
									bestPb = k;
									bestEa = ea;
									bestEb = eb;
								}
							}
						}

						if (bestMergeVal > 0)
						{
							int pa = bestPa * numVertsPerPoly;
							int pb = bestPb * numVertsPerPoly;
							//call mergePolys method
							int lastPoly = (npolys - 1) * numVertsPerPoly;
							if (pb != lastPoly)
								pb = lastPoly;

							npolys--;
						}
						else
						{
							//no more merging
							break;
						}
					}
				}

				//store polygons
				for (int j = 0; j < npolys; j++)
				{
					int p = this.npolys * numVertsPerPoly * 2;
					int q = j * numVertsPerPoly;
					for (int k = 0; k < numVertsPerPoly; k++)
						this.polys[p + k] = polys[q + k];

					this.regionIds[this.npolys] = cont.regionId;
					this.areas[this.npolys] = cont.area;
					this.npolys++;
				}
			}

			//TODO: remove edge vertices
			//...
			
			//TODO: calculate adjacency
			//...
			
			//TODO: find portal edges
			//...
		}
	}
}
