// Copyright (c) 2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

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
	public class NavQueryFilter
	{
		private float[] areaCost;

		public NavQueryFilter()
		{
			areaCost = new float[Area.MaxValues];
			for (int i = 0; i < areaCost.Length; i++)
				areaCost[i] = 1;
		}

		public virtual bool PassFilter(NavPolyId polyId, NavTile tile, NavPoly poly)
		{
			return true;
		}

		public virtual float GetCost(Vector3 a, Vector3 b,
			NavPolyId prevRef, NavTile prevTile, NavPoly prevPoly,
			NavPolyId curRef, NavTile curTile, NavPoly curPoly,
			NavPolyId nextRef, NavTile nextTile, NavPoly nextPoly)
		{
			return (a - b).Length() * areaCost[(int)curPoly.Area.Id];
		}

		public float GetAreaCost(Area area)
		{
			return areaCost[area.Id];
		}

		public void SetAreaCost(Area area, float value)
		{
			areaCost[area.Id] = value;
		}
	}
}
