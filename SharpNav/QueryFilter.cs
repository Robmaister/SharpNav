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
	public class QueryFilter
	{
		public float[] m_areaCost; //cost per area type
		public int m_includeFlags; //flags for polygons that can be visited
		public int m_excludeFlags; //flags for polygons that shouldn't be visited

		public QueryFilter()
		{
			m_includeFlags = 0xffff;
			m_excludeFlags = 0;

			m_areaCost = new float[PathfinderCommon.MAX_AREAS];
			for (int i = 0; i < PathfinderCommon.MAX_AREAS; i++)
				m_areaCost[i] = 1.0f;
		}

		public bool PassFilter(PathfinderCommon.Poly poly)
		{
			return (poly.flags & m_includeFlags) != 0 && (poly.flags & m_excludeFlags) == 0;
		}

		public float GetCost(Vector3 pa, Vector3 pb, PathfinderCommon.Poly curPoly)
		{
			return (new Vector3(pa - pb).Length) * m_areaCost[curPoly.GetArea()];
		}
	}
}
