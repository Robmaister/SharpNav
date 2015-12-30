// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

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
		private Area[] areas;

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
		/// <param name="settings">The settings to build with.</param>
		public CompactHeightfield(Heightfield field, NavMeshGenerationSettings settings)
			: this(field, settings.VoxelAgentHeight, settings.VoxelMaxClimb)
		{
		}

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
			areas = new Area[spanCount];

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
					if (s.Area.IsWalkable)
					{
						CompactSpan.FromMinMax(s.Maximum, fs[j + 1].Minimum, out spans[spanIndex]);
						areas[spanIndex] = s.Area;
						spanIndex++;
						c.Count++;
					}
				}

				//the last closed span that has an "infinite" height
				var lastS = fs[lastInd];
				if (lastS.Area.IsWalkable)
				{
					spans[spanIndex] = new CompactSpan(fs[lastInd].Maximum, int.MaxValue);
					areas[spanIndex] = lastS.Area;
					spanIndex++;
					c.Count++;
				}

				cells[i] = c;
			}

			//set neighbor connections
			for (int z = 0; z < length; z++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[z * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];

						for (var dir = Direction.West; dir <= Direction.South; dir++)
						{
							int dx = x + dir.GetHorizontalOffset();
							int dz = z + dir.GetVerticalOffset();

							if (dx < 0 || dz < 0 || dx >= width || dz >= length)
								continue;

							CompactCell dc = cells[dz * width + dx];
							for (int j = dc.StartIndex, cellEnd = dc.StartIndex + dc.Count; j < cellEnd; j++)
							{
								CompactSpan ds = spans[j];

								int overlapBottom, overlapTop;
								CompactSpan.OverlapMin(ref s, ref ds, out overlapBottom);
								CompactSpan.OverlapMax(ref s, ref ds, out overlapTop);

								//Make sure that the agent can walk to the next span and that the span isn't a huge drop or climb
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
		public Area[] Areas
		{
			get
			{
				return areas;
			}
		}

		/// <summary>
		/// Gets an <see cref="IEnumerable{T}"/> of <see cref="CompactSpan"/> of the spans at a specified coordiante.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> of <see cref="CompactSpan"/>.</returns>
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
		/// Gets an <see cref="IEnumerable{T}"/> of <see cref="CompactSpan"/>s at a specified index.
		/// </summary>
		/// <param name="i">The index.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> of <see cref="CompactSpan"/>.</returns>
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
					areas[i] = Area.Null;

			//marking areas as null changes the distance field, so recalculate it.
			if (distances != null)
				BuildDistanceField();
		}

		/// <summary>
		/// The central method for building regions, which consists of connected, non-overlapping walkable spans.
		/// </summary>
		/// <param name="borderSize">The border size</param>
		/// <param name="minRegionArea">If smaller than this value, region will be null</param>
		/// <param name="mergeRegionArea">Reduce unneccesarily small regions</param>
		public void BuildRegions(int borderSize, int minRegionArea, int mergeRegionArea)
		{
			if (distances == null)
				BuildDistanceField();

			const int LogStackCount = 3;
			const int StackCount = 1 << LogStackCount;
			List<CompactSpanReference>[] stacks = new List<CompactSpanReference>[StackCount];
			for (int i = 0; i < stacks.Length; i++)
				stacks[i] = new List<CompactSpanReference>(1024);

			RegionId[] regions = new RegionId[spans.Length];
			int[] floodDistances = new int[spans.Length];

			RegionId[] regionBuffer = new RegionId[spans.Length];
			int[] distanceBuffer = new int[spans.Length];

			int regionIndex = 1;
			int level = ((maxDistance + 1) / 2) * 2;
			 
			const int ExpandIters = 8;

			if (borderSize > 0)
			{
				//make sure border doesn't overflow
				int borderWidth = Math.Min(width, borderSize);
				int borderHeight = Math.Min(length, borderSize);

				//fill regions
				FillRectangleRegion(regions, new RegionId(regionIndex++, RegionFlags.Border), 0, borderWidth, 0, length);
				FillRectangleRegion(regions, new RegionId(regionIndex++, RegionFlags.Border), width - borderWidth, width, 0, length);
				FillRectangleRegion(regions, new RegionId(regionIndex++, RegionFlags.Border), 0, width, 0, borderHeight);
				FillRectangleRegion(regions, new RegionId(regionIndex++, RegionFlags.Border), 0, width, length - borderHeight, length);

				this.borderSize = borderSize;
			}

			int stackId = -1;
			while (level > 0)
			{
				level = level >= 2 ? level - 2 : 0;
				stackId = (stackId + 1) & (StackCount - 1);

				if (stackId == 0)
					SortCellsByLevel(regions, stacks, level, StackCount, 1);
				else
					AppendStacks(stacks[stackId - 1], stacks[stackId], regions);

				//expand current regions until no new empty connected cells found
				ExpandRegions(regions, floodDistances, ExpandIters, level, stacks[stackId], regionBuffer, distanceBuffer);

				//mark new regions with ids
				for (int j = 0; j < stacks[stackId].Count; j++)
				{
					var spanRef = stacks[stackId][j];
					if (spanRef.Index >= 0 && regions[spanRef.Index] == 0)
						if (FloodRegion(regions, floodDistances, regionIndex, level, ref spanRef))
							regionIndex++;
				}
			}

			//expand current regions until no new empty connected cells found
			ExpandRegions(regions, floodDistances, ExpandIters * 8, 0, null, regionBuffer, distanceBuffer);

			//filter out small regions
			this.maxRegions = FilterSmallRegions(regions, minRegionArea, mergeRegionArea, regionIndex);

			//write the result out
			for (int i = 0; i < spans.Length; i++)
				spans[i].Region = regions[i];
		}

		/// <summary>
		/// Merge two stacks to get a single stack.
		/// </summary>
		/// <param name="source">The original stack</param>
		/// <param name="destination">The new stack</param>
		/// <param name="regions">Region ids</param>
		private static void AppendStacks(List<CompactSpanReference> source, List<CompactSpanReference> destination, RegionId[] regions)
		{
			for (int j = 0; j < source.Count; j++)
			{
				var spanRef = source[j];
				if (spanRef.Index < 0 || regions[spanRef.Index] != 0)
					continue;

				destination.Add(spanRef);
			}
		}

		/// <summary>
		/// Discards regions that are too small. 
		/// </summary>
		/// <param name="regionIds">region data</param>
		/// <param name="minRegionArea">The minimum area a region can have</param>
		/// <param name="mergeRegionSize">The size of the regions after merging</param>
		/// <param name="maxRegionId">determines the number of regions available</param>
		/// <returns>The reduced number of regions.</returns>
		private int FilterSmallRegions(RegionId[] regionIds, int minRegionArea, int mergeRegionSize, int maxRegionId)
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
						int r = (int)regionIds[i];
						if (r <= 0 || (int)r >= numRegions)
							continue;

						Region reg = regions[(int)r];
						reg.SpanCount++;

						//update floors
						for (int j = c.StartIndex; j < end; j++)
						{
							if (i == j) continue;
							RegionId floorId = regionIds[j];
							if (floorId == 0 || (int)floorId >= numRegions)
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
			Stack<RegionId> stack = new Stack<RegionId>();
			List<RegionId> trace = new List<RegionId>();
			for (int i = 0; i < numRegions; i++)
			{
				Region reg = regions[i];
				if (reg.IsBorderOrNull || reg.SpanCount == 0 || reg.Visited)
					continue;

				//count the total size of all connected regions
				//also keep track of the regions connections to a tile border
				bool connectsToBorder = false;
				int spanCount = 0;
				stack.Clear();
				trace.Clear();

				reg.Visited = true;
				stack.Push(reg.Id);

				while (stack.Count > 0)
				{
					//pop
					RegionId ri = stack.Pop();

					Region creg = regions[(int)ri];

					spanCount += creg.SpanCount;
					trace.Add(ri);

					for (int j = 0; j < creg.Connections.Count; j++)
					{
						if (RegionId.HasFlags(creg.Connections[j], RegionFlags.Border))
						{
							connectsToBorder = true;
							continue;
						}

						Region neiReg = regions[(int)creg.Connections[j]];
						if (neiReg.Visited || neiReg.IsBorderOrNull)
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
						int index = (int)trace[j];

						regions[index].SpanCount = 0;
						regions[index].Id = RegionId.Null;
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
					if (reg.IsBorderOrNull || reg.SpanCount == 0)
						continue;

					//check to see if region should be merged
					if (reg.SpanCount > mergeRegionSize && reg.IsConnectedToBorder())
						continue;

					//small region with more than one connection or region which is not connected to border at all
					//find smallest neighbor that connects to this one
					int smallest = int.MaxValue;
					RegionId mergeId = reg.Id;
					for (int j = 0; j < reg.Connections.Count; j++)
					{
						if (RegionId.HasFlags(reg.Connections[j], RegionFlags.Border))
							continue;

						Region mreg = regions[(int)reg.Connections[j]];
						if (mreg.IsBorderOrNull)
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
						RegionId oldId = reg.Id;
						Region target = regions[(int)mergeId];

						//merge regions
						if (target.MergeWithRegion(reg))
						{
							//fix regions pointing to current region
							for (int j = 0; j < numRegions; j++)
							{
								if (regions[j].IsBorderOrNull)
									continue;

								//if another regions was already merged into current region
								//change the nid of the previous region too
								if (regions[j].Id == oldId)
									regions[j].Id = mergeId;

								//replace current region with new one if current region is neighbor
								regions[j].ReplaceNeighbor(oldId, mergeId);
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

				if (regions[i].IsBorderOrNull)
					continue;

				regions[i].Remap = true;
			}

			int regIdGen = 0;
			for (int i = 0; i < numRegions; i++)
			{
				if (!regions[i].Remap)
					continue;

				RegionId oldId = regions[i].Id;
				RegionId newId = new RegionId(++regIdGen);
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
				if (!RegionId.HasFlags(regionIds[i], RegionFlags.Border))
					regionIds[i] = regions[(int)regionIds[i]].Id;
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
						Area area = areas[i];

						bool isBoundary = false;
						if (s.ConnectionCount != 4)
							isBoundary = true;
						else
						{
							for (var dir = Direction.West; dir <= Direction.South; dir++)
							{
								int dx = x + dir.GetHorizontalOffset();
								int dy = y + dir.GetVerticalOffset();
								int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, dir);
								if (area != areas[di])
								{
									isBoundary = true;
									break;
								}
							}
						}

						if (isBoundary)
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
		/// <param name="distances">The original distances.</param>
		/// <param name="threshold">The distance threshold below which no blurring occurs.</param>
		/// <param name="buffer">A buffer that is at least the same length as <see cref="distances"/> for working memory.</param>
		private void BoxBlur(int[] distances, int threshold, int[] buffer = null)
		{
			threshold *= 2;

			//if the optional buffer parameter wasn't passed in, or is too small, make a new one.
			if (buffer == null || buffer.Length < distances.Length)
				buffer = new int[distances.Length];

			Buffer.BlockCopy(distances, 0, buffer, 0, distances.Length * sizeof(int)); 

			//horizontal pass
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
						for (Direction dir = Direction.West; dir <= Direction.South; dir++)
						{
							if (s.IsConnected(dir))
							{
								int dx = x + dir.GetHorizontalOffset();
								int dy = y + dir.GetVerticalOffset();
								int di = cells[dy * width + dx].StartIndex + CompactSpan.GetConnection(ref s, dir);

								d += buffer[di];

								CompactSpan ds = spans[di];
								Direction dir2 = dir.NextClockwise();
								if (ds.IsConnected(dir2))
								{
									int dx2 = dx + dir2.GetHorizontalOffset();
									int dy2 = dy + dir2.GetVerticalOffset();
									int di2 = cells[dy2 * width + dx2].StartIndex + CompactSpan.GetConnection(ref ds, dir2);

									d += buffer[di2];
								}
								else
								{
									d += cellDist;
								}
							}
							else
							{
								//add the center span if there's no connection.
								d += cellDist * 2;
							}
						}

						//save new value to destination
						distances[i] = (d + 5) / 9;
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
		/// <param name="stack">A stack of span references that are being expanded.</param>
		/// <param name="regionBuffer">A buffer to store region IDs. Must be at least the same size as <c>regions</c>.</param>
		/// <param name="distanceBuffer">A buffer to store flood distances. Must be at least the same size as <c>floodDistances</c>.</param>
		private void ExpandRegions(RegionId[] regions, int[] floodDistances, int maxIterations, int level, List<CompactSpanReference> stack = null, RegionId[] regionBuffer = null, int[] distanceBuffer = null)
		{
			//generate buffers if they're not passed in or if they're too small.
			if (regionBuffer == null || regionBuffer.Length < regions.Length)
				regionBuffer = new RegionId[regions.Length];

			if (distanceBuffer == null || distanceBuffer.Length < floodDistances.Length)
				distanceBuffer = new int[floodDistances.Length];

			//copy existing data into the buffers.
			Array.Copy(regions, 0, regionBuffer, 0, regions.Length);
			Array.Copy(floodDistances, 0, distanceBuffer, 0, floodDistances.Length);

			//find cells that are being expanded to.
			if (stack == null)
			{
				stack = new List<CompactSpanReference>();
				for (int y = 0; y < length; y++)
				{
					for (int x = 0; x < width; x++)
					{
						CompactCell c = cells[x + y * width];
						for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
						{
							//a cell is being expanded to if it's distance is greater than the current level,
							//but no region has been asigned yet. It must also not be in a null area.
							if (this.distances[i] >= level && regions[i] == 0 && areas[i].IsWalkable)
								stack.Add(new CompactSpanReference(x, y, i));
						}
					}
				}
			}
			else
			{
				for (int j = 0; j < stack.Count; j++)
				{
					if (regions[stack[j].Index] != 0)
						stack[j] = CompactSpanReference.Null;
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

					RegionId r = regions[i];
					Area area = areas[i];
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
						RegionId ri = regions[di];
						int dist = floodDistances[di];
						if (!(ri.IsNull || RegionId.HasFlags(ri, RegionFlags.Border)))
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
						stack[j] = CompactSpanReference.Null;
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
				Array.Copy(regionBuffer, 0, regions, 0, regions.Length);
				Array.Copy(distanceBuffer, 0, floodDistances, 0, floodDistances.Length);

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
		/// <param name="regions">source region</param>
		/// <param name="floodDistances">source distances</param>
		/// <param name="regionIndex">region id</param>
		/// <param name="level">current level</param>
		/// <param name="start">A reference to the starting span.</param>
		/// <returns>Always true.</returns>
		private bool FloodRegion(RegionId[] regions, int[] floodDistances, int regionIndex, int level, ref CompactSpanReference start)
		{
			//TODO this method should always return true, make it not return a bool?
			//flood fill mark region
			Stack<CompactSpanReference> stack = new Stack<CompactSpanReference>();
			stack.Push(start);

			Area area = areas[start.Index];
			regions[start.Index] = new RegionId(regionIndex);
			floodDistances[start.Index] = 0;

			int lev = level >= 2 ? level - 2 : 0;
			int count = 0;

			while (stack.Count > 0)
			{
				CompactSpanReference cell = stack.Pop();
				CompactSpan cs = spans[cell.Index];

				//check if any of the neighbors already have a valid reigon set
				RegionId ar = RegionId.Null;
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

						RegionId nr = regions[di];

						if (RegionId.HasFlags(nr, RegionFlags.Border)) //skip borders
							continue;

						if (nr != 0 && nr != regionIndex)
						{
							ar = nr;
							break;
						}

						CompactSpan ds = spans[di];
						Direction dir2 = dir.NextClockwise();
						if (ds.IsConnected(dir2))
						{
							int dx2 = dx + dir2.GetHorizontalOffset();
							int dy2 = dy + dir2.GetVerticalOffset();
							int di2 = cells[dx2 + dy2 * width].StartIndex + CompactSpan.GetConnection(ref ds, dir2);

							if (areas[di2] != area)
								continue;

							RegionId nr2 = regions[di2];
							if (nr2 != 0 && nr2 != regionIndex)
							{
								ar = nr2;
								break;
							}
						}
					}
				}

				if (ar != 0)
				{
					regions[cell.Index] = RegionId.Null;
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
							regions[di] = new RegionId(regionIndex);
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
		private bool IsSolidEdge(RegionId[] regions, ref CompactSpanReference spanRef, Direction dir)
		{
			CompactSpan s = spans[spanRef.Index];
			RegionId r = RegionId.Null;

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
		/// <param name="spanRef">The span to start walking from.</param>
		/// <param name="dir">The direction to start walking in.</param>
		/// <param name="cont">A collection of regions to append to.</param>
		private void WalkContour(RegionId[] regions, CompactSpanReference spanRef, Direction dir, List<RegionId> cont)
		{
			Direction startDir = dir;
			int starti = spanRef.Index;

			CompactSpan ss = spans[starti];
			RegionId curReg = RegionId.Null;

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
					RegionId r = RegionId.Null;
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
		private void FillRectangleRegion(RegionId[] regions, RegionId newRegionId, int left, int right, int bottom, int top)
		{
			for (int y = bottom; y < top; y++)
			{
				for (int x = left; x < right; x++)
				{
					CompactCell c = cells[x + y * width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						if (areas[i].IsWalkable)
							regions[i] = newRegionId;
					}
				}
			}
		}

		/// <summary>
		/// Sort the compact spans
		/// </summary>
		/// <param name="regions">Region data</param>
		/// <param name="stacks">Temporary stack of CompactSpanReference values</param>
		/// <param name="startlevel">Starting level</param>
		/// <param name="numStacks">The number of layers</param>
		/// <param name="logLevelsPerStack">log base 2 of stack levels</param>
		private void SortCellsByLevel(RegionId[] regions, List<CompactSpanReference>[] stacks, int startlevel, int numStacks, int logLevelsPerStack)
		{
			startlevel = startlevel >> logLevelsPerStack;
			for (int j = 0; j < numStacks; j++)
				stacks[j].Clear();

			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						if (!areas[i].IsWalkable || !regions[i].IsNull)
							continue;

						int level = distances[i] >> logLevelsPerStack;
						int sId = startlevel - level;
						if (sId >= numStacks)
							continue;
						if (sId < 0)
							sId = 0;

						stacks[sId].Add(new CompactSpanReference(x, y, i));
					}
				}
			}
		}

		/// <summary>
		/// Builds a set of <see cref="Contour"/>s around the generated regions. Must be called after regions are generated.
		/// </summary>
		/// <param name="settings">Settings for building the <see cref="ContourSet"/>.</param>
		/// <returns>A <see cref="ContourSet"/> containing one contour per region.</returns>
		public ContourSet BuildContourSet(NavMeshGenerationSettings settings)
		{
			return BuildContourSet(settings.MaxEdgeError, settings.MaxEdgeLength, settings.ContourFlags);
		}

		/// <summary>
		/// Builds a set of <see cref="Contour"/>s around the generated regions. Must be called after regions are generated.
		/// </summary>
		/// <param name="maxError">The maximum allowed deviation in a simplified contour from a raw one.</param>
		/// <param name="maxEdgeLength">The maximum edge length.</param>
		/// <param name="buildFlags">Flags that change settings for the build process.</param>
		/// <returns>A <see cref="ContourSet"/> containing one contour per region.</returns>
		public ContourSet BuildContourSet(float maxError, int maxEdgeLength, ContourBuildFlags buildFlags)
		{
			BBox3 contourSetBounds = bounds;
			if (borderSize > 0)
			{
				//remove offset
				float pad = borderSize * cellSize;
				contourSetBounds.Min.X += pad;
				contourSetBounds.Min.Z += pad;
				contourSetBounds.Max.X -= pad;
				contourSetBounds.Max.Z -= pad;
			}

			int contourSetWidth = width - borderSize * 2;
			int contourSetLength = length - borderSize * 2;

			int maxContours = Math.Max(maxRegions, 8);
			var contours = new List<Contour>(maxContours);

			EdgeFlags[] flags = new EdgeFlags[spans.Length];

			//Modify flags array by using the CompactHeightfield data
			//mark boundaries
			for (int z = 0; z < length; z++)
			{
				for (int x = 0; x < width; x++)
				{
					//loop through all the spans in the cell
					CompactCell c = cells[x + z * width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];

						//set the flag to 0 if the region is a border region or null.
						if (s.Region.IsNull || RegionId.HasFlags(s.Region, RegionFlags.Border))
						{
							flags[i] = 0;
							continue;
						}

						//go through all the neighboring cells
						for (var dir = Direction.West; dir <= Direction.South; dir++)
						{
							//obtain region id
							RegionId r = RegionId.Null;
							if (s.IsConnected(dir))
							{
								int dx = x + dir.GetHorizontalOffset();
								int dz = z + dir.GetVerticalOffset();
								int di = cells[dx + dz * width].StartIndex + CompactSpan.GetConnection(ref s, dir);
								r = spans[di].Region;
							}

							//region ids are equal
							if (r == s.Region)
							{
								//res marks all the internal edges
								EdgeFlagsHelper.AddEdge(ref flags[i], dir);
							}
						}

						//flags represents all the nonconnected edges, edges that are only internal
						//the edges need to be between different regions
						EdgeFlagsHelper.FlipEdges(ref flags[i]);
					}
				}
			}

			var verts = new List<ContourVertex>();
			var simplified = new List<ContourVertex>();

			for (int z = 0; z < length; z++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[x + z * width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						//flags is either 0000 or 1111
						//in other words, not connected at all 
						//or has all connections, which means span is in the middle and thus not an edge.
						if (flags[i] == EdgeFlags.None || flags[i] == EdgeFlags.All)
						{
							flags[i] = EdgeFlags.None;
							continue;
						}

						var spanRef = new CompactSpanReference(x, z, i);
						RegionId reg = this[spanRef].Region;
						if (reg.IsNull || RegionId.HasFlags(reg, RegionFlags.Border))
							continue;

						//reset each iteration
						verts.Clear();
						simplified.Clear();

						//Walk along a contour, then build it
						WalkContour(spanRef, flags, verts);
						Contour.Simplify(verts, simplified, maxError, maxEdgeLength, buildFlags);
						Contour.RemoveDegenerateSegments(simplified);
						Contour contour = new Contour(simplified, reg, areas[i], borderSize);

						if (!contour.IsNull)
							contours.Add(contour);
					}
				}
			}

			//Check and merge bad contours
			for (int i = 0; i < contours.Count; i++)
			{
				Contour cont = contours[i];

				//Check if contour is backwards
				if (cont.Area2D < 0)
				{
					//Find another contour to merge with
					int mergeIndex = -1;
					for (int j = 0; j < contours.Count; j++)
					{
						if (i == j)
							continue;

						//Must have at least one vertex, the same region ID, and be going forwards.
						Contour contj = contours[j];
						if (contj.Vertices.Length != 0 && contj.RegionId == cont.RegionId && contj.Area2D > 0)
						{
							mergeIndex = j;
							break;
						}
					}

					//Merge if found.
					if (mergeIndex != -1)
					{
						contours[mergeIndex].MergeWith(cont);
						contours.RemoveAt(i);
						i--;
					}
				}
			}

			return new ContourSet(contours, contourSetBounds, contourSetWidth, contourSetLength);
		}

		/// <summary>
		/// Initial generation of the contours
		/// </summary>
		/// <param name="spanReference">A referecne to the span to start walking from.</param>
		/// <param name="flags">An array of flags determinining </param>
		/// <param name="points">The vertices of a contour.</param>
		private void WalkContour(CompactSpanReference spanReference, EdgeFlags[] flags, List<ContourVertex> points)
		{
			Direction dir = Direction.West;

			//find the first direction that has a connection 
			while (!EdgeFlagsHelper.IsConnected(ref flags[spanReference.Index], dir))
				dir++;

			Direction startDir = dir;
			int startIndex = spanReference.Index;

			Area area = areas[startIndex];

			//TODO make the max iterations value a variable
			int iter = 0;
			while (++iter < 40000)
			{
				// this direction is connected
				if (EdgeFlagsHelper.IsConnected(ref flags[spanReference.Index], dir))
				{
					// choose the edge corner
					bool isBorderVertex;
					bool isAreaBorder = false;

					int px = spanReference.X;
					int py = GetCornerHeight(spanReference, dir, out isBorderVertex);
					int pz = spanReference.Y;

					switch (dir)
					{
						case Direction.West:
							pz++;
							break;
						case Direction.North:
							px++;
							pz++;
							break;
						case Direction.East:
							px++;
							break;
					}

					RegionId r = RegionId.Null;
					CompactSpan s = this[spanReference];
					if (s.IsConnected(dir))
					{
						int dx = spanReference.X + dir.GetHorizontalOffset();
						int dy = spanReference.Y + dir.GetVerticalOffset();
						int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, dir);
						r = spans[di].Region;
						if (area != areas[di])
							isAreaBorder = true;
					}

					// apply flags if neccessary
					if (isBorderVertex)
						r = RegionId.WithFlags(r, RegionFlags.VertexBorder);

					if (isAreaBorder)
						r = RegionId.WithFlags(r, RegionFlags.AreaBorder);

					//save the point
					points.Add(new ContourVertex(px, py, pz, r));

					EdgeFlagsHelper.RemoveEdge(ref flags[spanReference.Index], dir);	// remove visited edges
					dir = dir.NextClockwise();			// rotate clockwise
				}
				else
				{
					//get a new cell(x, y) and span index(i)
					int di = -1;
					int dx = spanReference.X + dir.GetHorizontalOffset();
					int dy = spanReference.Y + dir.GetVerticalOffset();

					CompactSpan s = this[spanReference];
					if (s.IsConnected(dir))
					{
						CompactCell dc = cells[dx + dy * width];
						di = dc.StartIndex + CompactSpan.GetConnection(ref s, dir);
					}

					if (di == -1)
					{
						// shouldn't happen
						// TODO if this shouldn't happen, this check shouldn't be necessary.
						throw new InvalidOperationException("Something went wrong");
					}

					spanReference = new CompactSpanReference(dx, dy, di);
					dir = dir.NextCounterClockwise(); // rotate counterclockwise
				}

				if (startIndex == spanReference.Index && startDir == dir)
					break;
			}
		}

		/// <summary>
		/// Helper method for WalkContour function
		/// </summary>
		/// <param name="sr">The span to get the corner height for.</param>
		/// <param name="dir">The direction to get the corner height from.</param>
		/// <param name="isBorderVertex">Determine whether the vertex is a border or not.</param>
		/// <returns>The corner height.</returns>
		private int GetCornerHeight(CompactSpanReference sr, Direction dir, out bool isBorderVertex)
		{
			isBorderVertex = false;

			CompactSpan s = this[sr];
			int cornerHeight = s.Minimum;
			Direction dirp = dir.NextClockwise(); //new clockwise direction

			RegionId[] cornerRegs = new RegionId[4];
			Area[] cornerAreas = new Area[4];

			//combine region and area codes in order to prevent border vertices, which are in between two areas, to be removed 
			cornerRegs[0] = s.Region;
			cornerAreas[0] = areas[sr.Index];

			if (s.IsConnected(dir))
			{
				//get neighbor span
				int dx = sr.X + dir.GetHorizontalOffset();
				int dy = sr.Y + dir.GetVerticalOffset();
				int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, dir);
				CompactSpan ds = spans[di];

				cornerHeight = Math.Max(cornerHeight, ds.Minimum);
				cornerRegs[1] = spans[di].Region;
				cornerAreas[1] = areas[di];

				//get neighbor of neighbor's span
				if (ds.IsConnected(dirp))
				{
					int dx2 = dx + dirp.GetHorizontalOffset();
					int dy2 = dy + dirp.GetVerticalOffset();
					int di2 = cells[dx2 + dy2 * width].StartIndex + CompactSpan.GetConnection(ref ds, dirp);
					CompactSpan ds2 = spans[di2];

					cornerHeight = Math.Max(cornerHeight, ds2.Minimum);
					cornerRegs[2] = ds2.Region;
					cornerAreas[2] = areas[di2];
				}
			}

			//get neighbor span
			if (s.IsConnected(dirp))
			{
				int dx = sr.X + dirp.GetHorizontalOffset();
				int dy = sr.Y + dirp.GetVerticalOffset();
				int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(ref s, dirp);
				CompactSpan ds = spans[di];

				cornerHeight = Math.Max(cornerHeight, ds.Minimum);
				cornerRegs[3] = ds.Region;
				cornerAreas[3] = areas[di];

				//get neighbor of neighbor's span
				if (ds.IsConnected(dir))
				{
					int dx2 = dx + dir.GetHorizontalOffset();
					int dy2 = dy + dir.GetVerticalOffset();
					int di2 = cells[dx2 + dy2 * width].StartIndex + CompactSpan.GetConnection(ref ds, dir);
					CompactSpan ds2 = spans[di2];

					cornerHeight = Math.Max(cornerHeight, ds2.Minimum);
					cornerRegs[2] = ds2.Region;
					cornerAreas[2] = areas[di2];
				}
			}

			//check if vertex is special edge vertex
			//if so, these vertices will be removed later
			for (int j = 0; j < 4; j++)
			{
				int a = j;
				int b = (j + 1) % 4;
				int c = (j + 2) % 4;
				int d = (j + 3) % 4;

				RegionId ra = cornerRegs[a], rb = cornerRegs[b], rc = cornerRegs[c], rd = cornerRegs[d];
				Area aa = cornerAreas[a], ab = cornerAreas[b], ac = cornerAreas[c], ad = cornerAreas[d];

				//the vertex is a border vertex if:
				//two same exterior cells in a row followed by two interior cells and none of the regions are out of bounds
				bool twoSameExteriors = RegionId.HasFlags(ra, RegionFlags.Border) && RegionId.HasFlags(rb, RegionFlags.Border) && (ra == rb && aa == ab);
				bool twoSameInteriors = !(RegionId.HasFlags(rc, RegionFlags.Border) || RegionId.HasFlags(rd, RegionFlags.Border));
				bool intsSameArea = ac == ad;
				bool noZeros = ra != 0 && rb != 0 && rc != 0 && rd != 0 && aa != 0 && ab != 0 && ac != 0 && ad != 0;
				if (twoSameExteriors && twoSameInteriors && intsSameArea && noZeros)
				{
					isBorderVertex = true;
					break;
				}
			}

			return cornerHeight;
		}
	}
}
