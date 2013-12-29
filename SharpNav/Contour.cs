#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav
{
	/// <summary>
	/// A contour is formed from a region.
	/// </summary>
	public class Contour
	{
		//TODO properly encapsulate

		//simplified vertices have much less edges
		public ContourSet.SimplifiedVertex[] Vertices;

		//raw vertices derived directly from CompactHeightfield
		public ContourSet.RawVertex[] RawVertices;

		public int RegionId;
		public AreaFlags Area;

		//flags used in the build process
		private const int BORDER_VERTEX = 0x10000;
		private const int AREA_BORDER = 0x20000;

		//applied to region id field of contour vertices in order to extract region id
		private const int CONTOUR_REG_MASK = 0xffff;

		public static void SetBorderVertex(ref int region)
		{
			region |= BORDER_VERTEX;
		}

		public static void SetAreaBorder(ref int region)
		{
			region |= AREA_BORDER;
		}

		public static bool IsBorderVertex(int r)
		{
			return (r & BORDER_VERTEX) != 0;
		}

		public static bool IsAreaBorder(int r)
		{
			return (r & AREA_BORDER) != 0;
		}

		public static bool IsSameArea(int region1, int region2)
		{
			return (region1 & AREA_BORDER) == (region2 & AREA_BORDER);
		}

		public static int ExtractRegionId(int r)
		{
			return r & CONTOUR_REG_MASK;
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
			return (region1 & (CONTOUR_REG_MASK | AREA_BORDER)) | (region2 & BORDER_VERTEX);
		}
	}
}
