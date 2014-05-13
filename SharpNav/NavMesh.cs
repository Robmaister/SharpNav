using System;
using System.Collections.Generic;

using SharpNav.Geometry;

namespace SharpNav
{

	//TODO right now this is basically an alias for TiledNavMesh. Fix this in the future.
	public class NavMesh : TiledNavMesh
	{
		public NavMesh(NavMeshBuilder builder)
			: base(builder)
		{
		}

		/// <summary>
		/// Generates a <see cref="NavMesh"/> given a collection of triangles and some settings.
		/// </summary>
		/// <param name="triangles">The triangles that form the level.</param>
		/// <param name="settings">The settings to generate with.</param>
		/// <returns>A <see cref="NavMesh"/>.</returns>
		public static NavMesh Generate(IEnumerable<Triangle3> triangles, NavMeshGenerationSettings settings)
		{
			BBox3 bounds = triangles.GetBoundingBox();
			var hf = new Heightfield(bounds, settings);
			hf.RasterizeTriangles(triangles);
			hf.FilterLedgeSpans(settings.VoxelAgentHeight, settings.VoxelMaxClimb);
			hf.FilterLowHangingWalkableObstacles(settings.VoxelMaxClimb);
			hf.FilterWalkableLowHeightSpans(settings.VoxelAgentHeight);

			var chf = new CompactHeightfield(hf, settings);
			chf.Erode(settings.VoxelAgentWidth);
			chf.BuildDistanceField();
			chf.BuildRegions(2, settings.MinRegionSize, settings.MergedRegionSize);

			var cont = new ContourSet(chf, settings);

			var polyMesh = new PolyMesh(cont, settings);

			var polyMeshDetail = new PolyMeshDetail(polyMesh, chf, settings);

			var buildData = new NavMeshBuilder(polyMesh, polyMeshDetail, new Pathfinding.OffMeshConnection[0], settings);

			var navMesh = new NavMesh(buildData);
			return navMesh;
		}
	}
}
