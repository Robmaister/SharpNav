#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Geometry;

namespace SharpNav
{
	/// <summary>
	/// A more memory-compact heightfield that stores open spans of voxels instead of closed ones.
	/// </summary>
	public class CompactHeightfield
	{
		private BBox3 bounds;

		private int width, height, length;
		private float cellSize, cellHeight;

		private CompactCell[] cells;
		private CompactSpan[] spans;
		private AreaFlags[] areas;

		//distance field
		private int[] distances;
		private int maxDistance;

		//region
		private int maxRegions;
		private int borderSize;
		
		/// <summary>
		/// Initializes a new instance of the <see cref="CompactHeightfield"/> class.
		/// </summary>
		/// <param name="field">A <see cref="Heightfield"/> to build from.</param>
		/// <param name="walkableHeight">The maximum difference in height to filter.</param>
		/// <param name="walkableClimb">The maximum difference in slope to filter.</param>
		public CompactHeightfield(Heightfield field, int walkableHeight, int walkableClimb)
		{
			this.bounds = field.Bounds;
			this.width = field.Width;
			this.height = field.Height;
			this.length = field.Length;
			this.cellSize = field.CellSizeXZ;
			this.cellHeight = field.CellHeight;

			int spanCount = field.SpanCount;
			cells = new CompactCell[width * length];
			spans = new CompactSpan[spanCount];
			areas = new AreaFlags[spanCount];

			//iterate over the Heightfield's cells
			int spanIndex = 0;
			for (int i = 0; i < cells.Length; i++)
			{
				//get the heightfield span list, skip if empty
				var fs = field[i].Spans;
				if (fs.Count == 0)
					continue;

				CompactCell c = new CompactCell(spanIndex, 0);

				//convert the closed spans to open spans
				int lastInd = fs.Count - 1;
				for (int j = 0; j < lastInd; j++)
				{
					var s = fs[j];
					if (s.Area != AreaFlags.Null)
					{
						CompactSpan.FromMinMax(s.Maximum, fs[j + 1].Minimum, out spans[spanIndex]);
						areas[spanIndex] = s.Area;
						spanIndex++;
						c.Count++;
					}
				}

				//the last closed span that has an "infinite" height
				var lastS = fs[lastInd];
				if (lastS.Area != AreaFlags.Null)
				{
					spans[spanIndex] = new CompactSpan(fs[lastInd].Maximum, int.MaxValue);
					areas[spanIndex] = lastS.Area;
					spanIndex++;
					c.Count++;
				}

				cells[i] = c;
			}

			//set neighbor connections
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];

						for (var dir = Direction.West; dir <= Direction.South; dir++)
						{
							int dx = x + dir.GetHorizontalOffset();
							int dy = y + dir.GetVerticalOffset();

							if (dx < 0 || dy < 0 || dx >= width || dy >= length)
								continue;

							CompactCell dc = cells[dy * width + dx];
							for (int j = dc.StartIndex, cellEnd = dc.StartIndex + dc.Count; j < cellEnd; j++)
							{
								CompactSpan ds = spans[j];

								int overlapBottom, overlapTop;
								CompactSpan.OverlapMin(ref s, ref ds, out overlapBottom);
								CompactSpan.OverlapMax(ref s, ref ds, out overlapTop);

								if ((overlapTop - overlapBottom) >= walkableHeight && Math.Abs(ds.Minimum - s.Minimum) <= walkableClimb)
								{
									int con = j - dc.StartIndex;
									CompactSpan.SetConnection(dir, con, ref spans[i]);
									break;
								}
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Gets the width of the <see cref="CompactHeightfield"/> in voxel units.
		/// </summary>
		public int Width
		{
			get
			{
				return width;
			}
		}

		/// <summary>
		/// Gets the height of the <see cref="CompactHeightfield"/> in voxel units.
		/// </summary>
		public int Height
		{
			get
			{
				return height;
			}
		}

		/// <summary>
		/// Gets the length of the <see cref="CompactHeightfield"/> in voxel units.
		/// </summary>
		public int Length
		{
			get
			{
				return length;
			}
		}

		/// <summary>
		/// Gets the world-space bounding box.
		/// </summary>
		public BBox3 Bounds
		{
			get
			{
				return bounds;
			}
		}

		/// <summary>
		/// Gets the world-space size of a cell in the XZ plane.
		/// </summary>
		public float CellSize
		{
			get
			{
				return cellSize;
			}
		}

		/// <summary>
		/// Gets the world-space size of a cell in the Y direction.
		/// </summary>
		public float CellHeight
		{
			get
			{
				return cellHeight;
			}
		}

		/// <summary>
		/// Gets the maximum distance to a border based on the distance field. This value is undefined prior to
		/// calling <see cref="BuildDistanceField"/>.
		/// </summary>
		public int MaxDistance
		{
			get
			{
				return maxDistance;
			}
		}

		/// <summary>
		/// Gets an array of distances from a span to the nearest border. This value is undefined prior to calling
		/// <see cref="BuildDistanceField"/>.
		/// </summary>
		public int[] Distances
		{
			get
			{
				return distances;
			}
		}

		/// <summary>
		/// Gets the size of the border.
		/// </summary>
		public int BorderSize
		{
			get
			{
				return borderSize;
			}
		}

		/// <summary>
		/// Gets the maximum number of allowed regions.
		/// </summary>
		public int MaxRegions
		{
			get
			{
				return maxRegions;
			}
		}

		/// <summary>
		/// Gets the cells.
		/// </summary>
		public CompactCell[] Cells
		{
			get
			{
				return cells;
			}
		}

		/// <summary>
		/// Gets the spans.
		/// </summary>
		public CompactSpan[] Spans
		{
			get
			{
				return spans;
			}
		}

		/// <summary>
		/// Gets the area flags.
		/// </summary>
		public AreaFlags[] Areas
		{
			get
			{
				return areas;
			}
		}

		/// <summary>
		/// Gets an <see cref="IEnumerable<CompactSpan>"/> of the spans at a specified coordiante.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <returns>An <see cref="IEnumerable<CompactSpan>"/>.</returns>
		public IEnumerable<CompactSpan> this[int x, int y]
		{
			get
			{
				if (x < 0 || x >= width || y < 0 || y >= length)
					throw new IndexOutOfRangeException();

				CompactCell c = cells[y * width + x];

				int end = c.StartIndex + c.Count;
				for (int i = c.StartIndex; i < end; i++)
					yield return spans[i];
			}
		}

		/// <summary>
		/// Gets an <see cref="IEnumerable<CompactSpan>"/> of the spans at a specified index.
		/// </summary>
		/// <param name="i">The index.</param>
		/// <returns>An <see cref="IEnumerable<CompactSpan"/>.</returns>
		public IEnumerable<CompactSpan> this[int i]
		{
			get
			{
				CompactCell c = cells[i];

				int end = c.StartIndex + c.Count;
				for (int j = c.StartIndex; j < end; j++)
					yield return spans[j];
			}
		}

		/// <summary>
		/// Gets the <see cref="CompactSpan"/> specified by the reference.
		/// </summary>
		/// <param name="spanRef">A reference to a span in this <see cref="CompactHeightfield"/>.</param>
		/// <returns>The referenced span.</returns>
		public CompactSpan this[CompactSpanReference spanRef]
		{
			get
			{
				return spans[spanRef.Index];
			}
		}

		/// <summary>
		/// Builds a distance field, or the distance to the nearest unwalkable area.
		/// </summary>
		public void BuildDistanceField()
		{
			if (distances == null)
				distances = new int[spans.Length];

			//fill up all the values in src
			CalculateDistanceField(distances);

			//blur the distances
			BoxBlur(distances, 1);

			//find the maximum distance
			this.maxDistance = 0;
			for (int i = 0; i < distances.Length; i++)
				this.maxDistance = Math.Max(distances[i], this.maxDistance);
		}

		/// <summary>
		/// Erodes the walkable areas in the map.
		/// </summary>
		/// <remarks>
		/// If you have already called <see cref="BuildDistanceField"/>, it will automatically be called again after
		/// erosion because it needs to be recalculated.
		/// </remarks>
		/// <param name="radius">The radius to erode from unwalkable areas.</param>
		public void Erode(int radius)
		{
			radius *= 2;

			//get a distance field
			int[] dists = new int[spans.Length];
			CalculateDistanceField(dists);

			//erode close-to-null areas to null areas.
			for (int i = 0; i < spans.Length; i++)
				if (dists[i] < radius)
					areas[i] = AreaFlags.Null;

			//marking areas as null changes the distance field, so recalculate it.
			if (distances != null)
				BuildDistanceField();
		}

		/// <summary>
		/// The central method for building regions, which consists of connected, non-overlapping walkable spans.
		/// </summary>
		/// <param name="borderSize"></param>
		/// <param name="minRegionArea">If smaller than this value, region will be null</param>
		/// <param name="mergeRegionArea">Reduce unneccesarily small regions</param>
		public void BuildRegions(int borderSize, int minRegionArea, int mergeRegionArea)
		{
			//TODO should we just chain off to BuildDistanceField() here?
			//Pros: avoids exceptions 
			if (distances == null)
				throw new InvalidOperationException("BuildRegions requires a distance field to be created first. Call BuildDistanceField() first.");

			int[] regions = new int[spans.Length];
			int[] floodDistances = new int[spans.Length];

			int[] regionBuffer = new int[spans.Length];
			int[] distanceBuffer = new int[spans.Length];

			int regionId = 1;
			int level = ((maxDistance + 1) / 2) * 2;

			const int ExpandIters = 8;

			if (borderSize > 0)
			{
				//make sure border doesn't overflow
				int borderWidth = Math.Min(width, borderSize);
				int borderHeight = Math.Min(length, borderSize);

				int baseRegionId = Region.IdWithBorderFlag(regionId);

				//fill regions
				FillRectangleRegion(regions, baseRegionId + 0, 0, borderWidth, 0, length);
				FillRectangleRegion(regions, baseRegionId + 1, width - borderWidth, width, 0, length);
				FillRectangleRegion(regions, baseRegionId + 2, 0, width, 0, borderHeight);
				FillRectangleRegion(regions, baseRegionId + 3, 0, width, length - borderHeight, length);
				regionId += 4;

				this.borderSize = borderSize;
			}

			while (level > 0)
			{
				level = level >= 2 ? level - 2 : 0;

				//expand current regions until no new empty connected cells found
				ExpandRegions(regions, floodDistances, ExpandIters, level, regionBuffer, distanceBuffer);

				//mark new regions with ids
				for (int y = 0; y < length; y++)
				{
					for (int x = 0; x < width; x++)
					{
						CompactCell c = cells[x + y * width];
						for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
						{
							if (distances[i] < level || regions[i] != 0 || areas[i] == AreaFlags.Null)
								continue;

							if (FloodRegion(regions, floodDistances, regionId, level, new CompactSpanReference(x, y, i)))
								regionId++;
						}
					}
				}
			}

			//expand current regions until no new empty connected cells found
			ExpandRegions(regions, floodDistances, ExpandIters * 8, 0, regionBuffer, distanceBuffer);

			//filter out small regions
			this.maxRegions = FilterSmallRegions(regions, minRegionArea, mergeRegionArea, regionId);

			//write the result out
			for (int i = 0; i < spans.Length; i++)
				spans[i].Region = regions[i];
		}

		/// <summary>
		/// Discards regions that are too small. 
		/// </summary>
		/// <param name="regionIds">region data</param>
		/// <param name="minRegionArea"></param>
		/// <param name="mergeRegionSize"></param>
		/// <param name="maxRegionId">determines the number of regions available</param>
		/// <returns>The reduced number of regions.</returns>
		private int FilterSmallRegions(int[] regionIds, int minRegionArea, int mergeRegionSize, int maxRegionId)
		{
			int numRegions = maxRegionId + 1;
			Region[] regions = new Region[numRegions];

			//construct regions
			for (int i = 0; i < numRegions; i++)
				regions[i] = new Region(i);

			//find edge of a region and find connections around a contour
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[x + y * width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpanReference spanRef = new CompactSpanReference(x, y, i);

						//HACK since the border region flag makes r negative, I changed r == 0 to r <= 0. Figure out exactly what maxRegionId's purpose is and see if Region.IsBorderOrNull is all we need.
						int r = regionIds[i];
						if (r <= 0 || r >= numRegions)
							continue;

						Region reg = regions[r];
						reg.SpanCount++;

						//update floors
						for (int j = c.StartIndex; j < end; j++)
						{
							if (i == j) continue;
							int floorId = regionIds[j];
							if (floorId == 0 || floorId >= numRegions)
								continue;
							reg.AddUniqueFloorRegion(floorId);
						}

						//have found contour
						if (reg.Connections.Count > 0)
							continue;

						reg.AreaType = areas[i];

						//check if this cell is next to a border
						for (var dir = Direction.West; dir <= Direction.South; dir++)
						{
							if (IsSolidEdge(regionIds, ref spanRef, dir))
							{
								//The cell is at a border. 
								//Walk around contour to find all neighbors
								WalkContour(regionIds, spanRef, dir, reg.Connections);
								break;
							}
						}
					}
				}
			}

			//Remove too small regions
			Stack<int> stack = new Stack<int>();
			List<int> trace = new List<int>();
			for (int i = 0; i < numRegions; i++)
			{
				Region reg = regions[i];
				if (reg.IsBorderOrNull() || reg.SpanCount == 0 || reg.Visited)
					continue;

				//count the total size of all connected regions
				//also keep track of the regions connections to a tile border
				bool connectsToBorder = false;
				int spanCount = 0;
				stack.Clear();
				trace.Clear();

				reg.Visited = true;
				stack.Push(i);

				while (stack.Count > 0)
				{
					//pop
					int ri = stack.Pop();

					Region creg = regions[ri];

					spanCount += creg.SpanCount;
					trace.Add(ri);

					for (int j = 0; j < creg.Connections.Count; j++)
					{
						if (Region.IsBorder(creg.Connections[j]))
						{
							connectsToBorder = true;
							continue;
						}

						Region neiReg = regions[creg.Connections[j]];
						if (neiReg.Visited || neiReg.IsBorderOrNull())
							continue;

						//visit
						stack.Push(neiReg.Id);
						neiReg.Visited = true;
					}
				}

				//if the accumulated region size is too small, remove it
				//do not remove areas which connect to tile borders as their size can't be estimated correctly
				//and removing them can potentially remove necessary areas
				if (spanCount < minRegionArea && !connectsToBorder)
				{
					//kill all visited regions
					for (int j = 0; j < trace.Count; j++)
					{
						int index = trace[j];

						regions[index].SpanCount = 0;
						regions[index].Id = 0;
					}
				}
			}

			//Merge too small regions to neighbor regions
			int mergeCount = 0;
			do
			{
				mergeCount = 0;
				for (int i = 0; i < numRegions; i++)
				{
					Region reg = regions[i];
					if (reg.IsBorderOrNull() || reg.SpanCount == 0)
						continue;

					//check to see if region should be merged
					if (reg.SpanCount > mergeRegionSize && reg.IsConnectedToBorder())
						continue;

					//small region with more than one connection or region which is not connected to border at all
					//find smallest neighbor that connects to this one
					int smallest = int.MaxValue;
					int mergeId = reg.Id;
					for (int j = 0; j < reg.Connections.Count; j++)
					{
						if (Region.IsBorder(reg.Connections[j]))
							continue;

						Region mreg = regions[reg.Connections[j]];
						if (mreg.IsBorderOrNull())
							continue;

						if (mreg.SpanCount < smallest && reg.CanMergeWith(mreg) && mreg.CanMergeWith(reg))
						{
							smallest = mreg.SpanCount;
							mergeId = mreg.Id;
						}
					}

					//found new id
					if (mergeId != reg.Id)
					{
						int oldId = reg.Id;
						Region target = regions[mergeId];

						//merge regions
						if (target.MergeWithRegion(reg))
						{
							//fix regions pointing to current region
							for (int j = 0; j < numRegions; j++)
							{
								if (regions[j].IsBorderOrNull())
									continue;

								//if another regions was already merged into current region
								//change the nid of the previous region too
								if (regions[j].Id == oldId)
									regions[j].Id = mergeId;

								//replace current region with new one if current region is neighbor
								regions[j].ReplaceNeighbour(oldId, mergeId);
							}

							mergeCount++;
						}
					}
				}
			}
			while (mergeCount > 0);

			//Compress region ids
			for (int i = 0; i < numRegions; i++)
			{
				regions[i].Remap = false;

				if (regions[i].IsBorderOrNull())
					continue;

				regions[i].Remap = true;
			}

			int regIdGen = 0;
			for (int i = 0; i < numRegions; i++)
			{
				if (!regions[i].Remap)
					continue;

				int oldId = regions[i].Id;
				int newId = ++regIdGen;
				for (int j = i; j < numRegions; j++)
				{
					if (regions[j].Id == oldId)
					{
						regions[j].Id = newId;
						regions[j].Remap = false;
					}
				}
			}

			//Remap regions
			for (int i = 0; i < spans.Length; i++)
			{
				if (!Region.IsBorder(regionIds[i]))
					regionIds[i] = regions[regionIds[i]].Id;
			}

			return regIdGen;
		}

		/// <summary>
		/// A distance field estimates how far each span is from its nearest border span. This data is needed for region generation.
		/// </summary>
		/// <param name="src">Array of values, each corresponding to an individual span</param>
		private void CalculateDistanceField(int[] src)
		{
			//initialize distance and points
			for (int i = 0; i < spans.Length; i++)
				src[i] = int.MaxValue;

			//mark boundary cells
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];
						AreaFlags area = areas[i];

						int numConnections = 0;
						for (var dir = Direction.West; dir <= Direction.South; dir++)
						{
							if (s.IsConnected(dir))
							{
								int dx = x + dir.GetHorizontalOffset();
								int dy = y + dir.GetVerticalOffset();
								int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, dir);
								if (area == areas[di])
									numConnections++;
							}
						}

						if (numConnections != 4)
							src[i] = 0;
					}
				}
			}

			//pass 1
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];

						if (s.IsConnected(Direction.West))
						{
							//(-1, 0)
							int dx = x + Direction.West.GetHorizontalOffset();
							int dy = y + Direction.West.GetVerticalOffset();
							int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, Direction.West);
							CompactSpan ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = src[di] + 2;

							//(-1, -1)
							if (ds.IsConnected(Direction.South))
							{
								int ddx = dx + Direction.South.GetHorizontalOffset();
								int ddy = dy + Direction.South.GetVerticalOffset();
								int ddi = cells[ddx + ddy * width].StartIndex + CompactSpan.GetConnection(ref ds, Direction.South);
								if (src[ddi] + 3 < src[i])
									src[i] = src[ddi] + 3;
							}
						}

						if (s.IsConnected(Direction.South))
						{
							//(0, -1)
							int dx = x + Direction.South.GetHorizontalOffset();
							int dy = y + Direction.South.GetVerticalOffset();
							int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, Direction.South);
							CompactSpan ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = src[di] + 2;

							//(1, -1)
							if (ds.IsConnected(Direction.East))
							{
								int ddx = dx + Direction.East.GetHorizontalOffset();
								int ddy = dy + Direction.East.GetVerticalOffset();
								int ddi = cells[ddx + ddy * width].StartIndex + CompactSpan.GetConnection(ref ds, Direction.East);
								if (src[ddi] + 3 < src[i])
									src[i] = src[ddi] + 3;
							}
						}
					}
				}
			}

			//pass 2
			for (int y = length - 1; y >= 0; y--)
			{
				for (int x = width - 1; x >= 0; x--)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];

						if (s.IsConnected(Direction.East))
						{
							//(1, 0)
							int dx = x + Direction.East.GetHorizontalOffset();
							int dy = y + Direction.East.GetVerticalOffset();
							int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, Direction.East);
							CompactSpan ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = src[di] + 2;

							//(1, 1)
							if (ds.IsConnected(Direction.North))
							{
								int ddx = dx + Direction.North.GetHorizontalOffset();
								int ddy = dy + Direction.North.GetVerticalOffset();
								int ddi = cells[ddx + ddy * width].StartIndex + CompactSpan.GetConnection(ref ds, Direction.North);
								if (src[ddi] + 3 < src[i])
									src[i] = src[ddi] + 3;
							}
						}

						if (s.IsConnected(Direction.North))
						{
							//(0, 1)
							int dx = x + Direction.North.GetHorizontalOffset();
							int dy = y + Direction.North.GetVerticalOffset();
							int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, Direction.North);
							CompactSpan ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = src[di] + 2;

							//(-1, 1)
							if (ds.IsConnected(Direction.West))
							{
								int ddx = dx + Direction.West.GetHorizontalOffset();
								int ddy = dy + Direction.West.GetVerticalOffset();
								int ddi = cells[ddx + ddy * width].StartIndex + CompactSpan.GetConnection(ref ds, Direction.West);
								if (src[ddi] + 3 < src[i])
									src[i] = src[ddi] + 3;
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Part of building the distance field. It may or may not return an array equal to src.
		/// </summary>
		/// <param name="threshold">The distance threshold below which no blurring occurs.</param>
		private void BoxBlur(int[] distances, int threshold, int[] buffer = null)
		{
			threshold *= 2;

			//if the optional buffer parameter wasn't passed in, or is too small, make a new one.
			if (buffer == null || buffer.Length < distances.Length)
				buffer = new int[distances.Length];

			//horizontal pass
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];
						int cellDist = distances[i];

						//if the distance is below the threshold, skip the span.
						if (cellDist <= threshold)
							continue;

						//iterate the full neighborhood of 8 spans.
						int d = cellDist;
						if (s.IsConnected(Direction.West))
						{
							int dx = x - 1;
							int di = cells[y * width + dx].StartIndex + s.ConnectionWest;

							d += distances[di];
						}
						else
						{
							//add the center span if there's no connection.
							d += cellDist;
						}

						if (s.IsConnected(Direction.East))
						{
							int dx = x + 1;
							int di = cells[y * width + dx].StartIndex + s.ConnectionEast;

							d += distances[di];
						}
						else
						{
							//add the center span if there's no connection.
							d += cellDist;
						}

						//save new value to destination
						buffer[i] = (d + 1) / 3;
					}
				}
			}

			//vertical pass
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];
						int cellDist = buffer[i];

						//if the distance is below the threshold, skip the span.
						if (cellDist <= threshold)
							continue;

						//iterate the full neighborhood of 8 spans.
						int d = cellDist;
						if (s.IsConnected(Direction.North))
						{
							int dy = y + 1;
							int di = cells[dy * width + x].StartIndex + s.ConnectionNorth;

							d += buffer[di];
						}
						else
						{
							//add the center span if there's no connection.
							d += cellDist;
						}

						if (s.IsConnected(Direction.South))
						{
							int dy = y - 1;
							int di = cells[dy * width + x].StartIndex + s.ConnectionSouth;

							d += buffer[di];
						}
						else
						{
							//add the center span if there's no connection.
							d += cellDist;
						}

						//save new value to destination
						distances[i] = (d + 1) / 3;
					}
				}
			}
		}

		/// <summary>
		/// Expands regions to include spans above a specified water level.
		/// </summary>
		/// <param name="regions">The array of region IDs.</param>
		/// <param name="floodDistances">The array of flooding distances.</param>
		/// <param name="maxIterations">The maximum number of allowed iterations before breaking.</param>
		/// <param name="level">The current water level.</param>
		/// <param name="regionBuffer">A buffer to store region IDs. Must be at least the same size as <see cref="regions"/>.</param>
		/// <param name="distanceBuffer">A buffer to store flood distances. Must be at least the same size as <see cref="floodDistances"/>.</param>
		private void ExpandRegions(int[] regions, int[] floodDistances, int maxIterations, int level, int[] regionBuffer = null, int[] distanceBuffer = null)
		{
			//generate buffers if they're not passed in or if they're too small.
			if (regionBuffer == null || regionBuffer.Length < regions.Length)
				regionBuffer = new int[regions.Length];

			if (distanceBuffer == null || distanceBuffer.Length < floodDistances.Length)
				distanceBuffer = new int[floodDistances.Length];

			//copy existing data into the buffers.
			Buffer.BlockCopy(regions, 0, regionBuffer, 0, regions.Length * sizeof(int));
			Buffer.BlockCopy(floodDistances, 0, distanceBuffer, 0, floodDistances.Length * sizeof(int));

			//find cells that are being expanded to.
			List<CompactSpanReference> stack = new List<CompactSpanReference>();
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[x + y * width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						//a cell is being expanded to if it's distance is greater than the current level,
						//but no region has been asigned yet. It must also not be in a null area.
						if (this.distances[i] >= level && regions[i] == 0 && areas[i] != AreaFlags.Null)
							stack.Add(new CompactSpanReference(x, y, i));
					}
				}
			}

			//assign regions to all the cells that are being expanded to.
			//will run until it's done or it runs maxIterations times.
			int iter = 0;
			while (stack.Count > 0)
			{
				//spans in the stack that are skipped:
				// - assigned a region ID in an earlier iteration
				// - not neighboring any spans with region IDs
				int skipped = 0;

				for (int j = 0; j < stack.Count; j++)
				{
					CompactSpanReference spanRef = stack[j];
					int x = spanRef.X;
					int y = spanRef.Y;
					int i = spanRef.Index;

					//skip regions already assigned to
					if (i < 0)
					{
						skipped++;
						continue;
					}

					int r = regions[i];
					AreaFlags area = areas[i];
					CompactSpan s = spans[i];

					//search direct neighbors for the one with the smallest distance value
					int minDist = int.MaxValue;
					for (var dir = Direction.West; dir <= Direction.South; dir++)
					{
						if (!s.IsConnected(dir))
							continue;

						int dx = x + dir.GetHorizontalOffset();
						int dy = y + dir.GetVerticalOffset();
						int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, dir);

						if (areas[di] != area)
							continue;

						//compare distance to previous best
						int ri = regions[di];
						int dist = floodDistances[di];
						if (!Region.IsBorderOrNull(ri))
						{
							//set region and distance if better
							if (dist + 2 < minDist)
							{
								r = ri;
								minDist = dist + 2;
							}
						}
					}

					if (r != 0)
					{
						//set the region and distance for this span
						regionBuffer[i] = r;
						distanceBuffer[i] = minDist;

						//mark this item in the stack as assigned for the next iteration.
						stack[j] = new CompactSpanReference(x, y, -1);
					}
					else
					{
						//skip spans that don't neighbor any regions
						skipped++;
					}
				}

				//if the entire stack is being skipped, we're done.
				if (skipped == stack.Count)
					break;

				//Copy from the buffers back to the original arrays. This is done after each iteration
				//because changing it in-place has some side effects for the other spans in the stack.
				Buffer.BlockCopy(regionBuffer, 0, regions, 0, regions.Length * sizeof(int));
				Buffer.BlockCopy(distanceBuffer, 0, floodDistances, 0, floodDistances.Length * sizeof(int));

				if (level > 0)
				{
					//if we hit maxIterations before expansion is done, break out anyways.
					++iter;
					if (iter >= maxIterations)
						break;
				}
			}
		}

		/// <summary>
		/// Floods the regions at a certain level
		/// </summary>
		/// <param name="x">starting x</param>
		/// <param name="y">starting y</param>
		/// <param name="i">span index</param>
		/// <param name="level">current level</param>
		/// <param name="region">region id</param>
		/// <param name="regions">source region</param>
		/// <param name="floodDistances">source distances</param>
		/// <returns></returns>
		private bool FloodRegion(int[] regions, int[] floodDistances, int region, int level, CompactSpanReference start)
		{
			//flood fill mark region
			Stack<CompactSpanReference> stack = new Stack<CompactSpanReference>();
			stack.Push(start);

			AreaFlags area = areas[start.Index];
			regions[start.Index] = region;
			floodDistances[start.Index] = 0;

			int lev = level >= 2 ? level - 2 : 0;
			int count = 0;

			while (stack.Count > 0)
			{
				CompactSpanReference cell = stack.Pop();
				CompactSpan cs = spans[cell.Index];

				//check if any of the neighbors already have a valid reigon set
				int ar = 0;
				for (var dir = Direction.West; dir <= Direction.South; dir++)
				{
					//8 connected
					if (cs.IsConnected(dir))
					{
						int dx = cell.X + dir.GetHorizontalOffset();
						int dy = cell.Y + dir.GetVerticalOffset();
						int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref cs, dir);

						if (areas[di] != area)
							continue;

						int nr = regions[di];

						if (Region.IsBorder(nr)) //skip borders
							continue;

						if (nr != 0 && nr != region)
							ar = nr;

						CompactSpan ds = spans[di];
						Direction dir2 = dir.NextClockwise();
						if (ds.IsConnected(dir2))
						{
							int dx2 = dx + dir2.GetHorizontalOffset();
							int dy2 = dy + dir2.GetVerticalOffset();
							int di2 = cells[dx2 + dy2 * width].StartIndex + CompactSpan.GetConnection(ref ds, dir2);

							if (areas[di2] != area)
								continue;

							int nr2 = regions[di2];
							if (nr2 != 0 && nr2 != region)
								ar = nr2;
						}
					}
				}

				if (ar != 0)
				{
					regions[cell.Index] = 0;
					continue;
				}

				count++;

				//expand neighbors
				for (var dir = Direction.West; dir <= Direction.South; dir++)
				{
					if (cs.IsConnected(dir))
					{
						int dx = cell.X + dir.GetHorizontalOffset();
						int dy = cell.Y + dir.GetVerticalOffset();
						int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref cs, dir);

						if (areas[di] != area)
							continue;

						if (distances[di] >= lev && regions[di] == 0)
						{
							regions[di] = region;
							floodDistances[di] = 0;
							stack.Push(new CompactSpanReference(dx, dy, di));
						}
					}
				}
			}

			return count > 0;
		}

		/// <summary>
		/// Checks whether the edge from a span in a direction is a solid edge.
		/// A solid edge is an edge between two regions.
		/// </summary>
		/// <param name="regions">The region ID array.</param>
		/// <param name="spanRef">A reference to the span connected to the edge.</param>
		/// <param name="dir">The direction of the edge.</param>
		/// <returns>A value indicating whether the described edge is solid.</returns>
		private bool IsSolidEdge(int[] regions, ref CompactSpanReference spanRef, Direction dir)
		{
			CompactSpan s = spans[spanRef.Index];
			int r = 0;

			if (s.IsConnected(dir))
			{
				int dx = spanRef.X + dir.GetHorizontalOffset();
				int dy = spanRef.Y + dir.GetVerticalOffset();
				int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, dir);
				r = regions[di];
			}

			if (r == regions[spanRef.Index])
				return false;

			return true;
		}

		/// <summary>
		/// Try to visit all the spans. May be needed in filtering small regions. 
		/// </summary>
		/// <param name="regions">an array of region values</param>
		/// <param name="x">cell x-coordinate</param>
		/// <param name="y">cell y-coordinate</param>
		/// <param name="i">index of span</param>
		/// <param name="dir">direction</param>
		/// <param name="cont">list of ints</param>
		private void WalkContour(int[] regions, CompactSpanReference spanRef, Direction dir, List<int> cont)
		{
			Direction startDir = dir;
			int starti = spanRef.Index;

			CompactSpan ss = spans[starti];
			int curReg = 0;

			if (ss.IsConnected(dir))
			{
				int dx = spanRef.X + dir.GetHorizontalOffset();
				int dy = spanRef.Y + dir.GetVerticalOffset();
				int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref ss, dir);
				curReg = regions[di];
			}

			cont.Add(curReg);

			int iter = 0;
			while (++iter < 40000)
			{
				CompactSpan s = spans[spanRef.Index];

				if (IsSolidEdge(regions, ref spanRef, dir))
				{
					//choose the edge corner
					int r = 0;
					if (s.IsConnected(dir))
					{
						int dx = spanRef.X + dir.GetHorizontalOffset();
						int dy = spanRef.Y + dir.GetVerticalOffset();
						int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, dir);
						r = regions[di];
					}

					if (r != curReg)
					{
						curReg = r;
						cont.Add(curReg);
					}

					dir = dir.NextClockwise(); //rotate clockwise
				}
				else
				{
					int di = -1;
					int dx = spanRef.X + dir.GetHorizontalOffset();
					int dy = spanRef.Y + dir.GetVerticalOffset();

					if (s.IsConnected(dir))
					{
						CompactCell dc = cells[dx + dy * width];
						di = dc.StartIndex + CompactSpan.GetConnection(ref s, dir);
					}

					if (di == -1)
					{
						//shouldn't happen
						return;
					}

					spanRef = new CompactSpanReference(dx, dy, di);
					dir = dir.NextCounterClockwise(); //rotate counterclockwise
				}

				if (starti == spanRef.Index && startDir == dir)
					break;
			}

			//remove adjacent duplicates
			if (cont.Count > 1)
			{
				for (int j = 0; j < cont.Count;)
				{
					//next element
					int nj = (j + 1) % cont.Count;

					//adjacent duplicate found
					if (cont[j] == cont[nj]) 
						cont.RemoveAt(j);
					else
						j++; 
				}
			}
		}

		/// <summary>
		/// Fill in a rectangular area with a region ID. Spans in a null area are skipped.
		/// </summary>
		/// <param name="regions">The region ID array.</param>
		/// <param name="newRegionId">The ID to fill in.</param>
		/// <param name="left">The left edge of the rectangle.</param>
		/// <param name="right">The right edge of the rectangle.</param>
		/// <param name="bottom">The bottom edge of the rectangle.</param>
		/// <param name="top">The top edge of the rectangle.</param>
		private void FillRectangleRegion(int[] regions, int newRegionId, int left, int right, int bottom, int top)
		{
			for (int y = bottom; y < top; y++)
			{
				for (int x = left; x < right; x++)
				{
					CompactCell c = cells[x + y * width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						if (areas[i] != AreaFlags.Null)
							regions[i] = newRegionId;
					}
				}
			}
		}
	}
}
