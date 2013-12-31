#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// A contour is formed from a region.
	/// </summary>
	public class Contour
	{
		//TODO properly encapsulate

		//simplified vertices have much less edges
		public SimplifiedVertex[] Vertices;

		//raw vertices derived directly from CompactHeightfield
		public RawVertex[] RawVertices;

		public int RegionId;
		public AreaFlags Area;

		//flags used in the build process
		private const int VertexBorderFlag = 0x10000;
		private const int AreaBorderFlag = 0x20000;

		//applied to region id field of contour vertices in order to extract region id
		private const int ContourRegionMask = 0xffff;

		public static void SetBorderVertex(ref int region)
		{
			region |= VertexBorderFlag;
		}

		public static void SetAreaBorder(ref int region)
		{
			region |= AreaBorderFlag;
		}

		public static bool IsBorderVertex(int r)
		{
			return (r & VertexBorderFlag) != 0;
		}

		public static bool IsAreaBorder(int r)
		{
			return (r & AreaBorderFlag) != 0;
		}

		public static bool IsSameArea(int region1, int region2)
		{
			return (region1 & AreaBorderFlag) == (region2 & AreaBorderFlag);
		}

		public static int ExtractRegionId(int r)
		{
			return r & ContourRegionMask;
		}

		public static bool IsSameRegion(int region1, int region2)
		{
			return ExtractRegionId(region1) == ExtractRegionId(region2);
		}

		public static bool CanTessellateWallEdges(ContourBuildFlags buildFlags)
		{
			return (buildFlags & ContourBuildFlags.TessellateWallEdges) != 0;
		}

		public static bool CanTessellateAreaEdges(ContourBuildFlags buildFlags)
		{
			return (buildFlags & ContourBuildFlags.TessellateAreaEdges) != 0;
		}

		public static bool CanTessellateEitherWallOrAreaEdges(ContourBuildFlags buildFlags)
		{
			return (buildFlags & (ContourBuildFlags.TessellateWallEdges | ContourBuildFlags.TessellateAreaEdges)) != 0;
		}

		public static int GetNewRegion(int region1, int region2)
		{
			return (region1 & (ContourRegionMask | AreaBorderFlag)) | (region2 & VertexBorderFlag);
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct RawVertex
		{
			public int X;
			public int Y;
			public int Z;
			public int RegionId;

			public RawVertex(int x, int y, int z, int region)
			{
				this.X = x;
				this.Y = y;
				this.Z = z;
				this.RegionId = region;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SimplifiedVertex
		{
			public int X;
			public int Y;
			public int Z;
			public int RawVertexIndex;

			public SimplifiedVertex(int x, int y, int z, int rawVertex)
			{
				this.X = x;
				this.Y = y;
				this.Z = z;
				this.RawVertexIndex = rawVertex;
			}

			public SimplifiedVertex(RawVertex rawVert, int index)
			{
				this.X = rawVert.X;
				this.Y = rawVert.Y;
				this.Z = rawVert.Z;
				this.RawVertexIndex = index;
			}
		}
	}
}
