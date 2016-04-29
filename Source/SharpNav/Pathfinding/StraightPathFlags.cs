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
	/// <summary>
	/// Flags returned by NavMeshQuery.FindStraightPath.
	/// </summary>
	[Flags]
	public enum StraightPathFlags
	{
		None = 0,

		/// <summary> The vertex is the start position of the path.</summary>
		Start = 0x01,

		/// <summary> The vertex is the end position of the path.</summary>
		End = 0x02,

		/// <summary> The vertex is the start of an off-mesh connection.</summary>
		OffMeshConnection = 0x04
	}

	public struct StraightPathVertex
	{
		public NavPoint Point;
		public StraightPathFlags Flags;

		public StraightPathVertex(NavPoint point, StraightPathFlags flags)
		{
			Point = point;
			Flags = flags;
		}
	}

	public class StraightPath
	{
		private List<StraightPathVertex> verts;

		public StraightPath()
		{
			verts = new List<StraightPathVertex>();
		}

		public int Count { get { return verts.Count; } }

		public StraightPathVertex this[int i]
		{
			get { return verts[i]; }
			set { verts[i] = value; }
		}

		public void Clear()
		{
			verts.Clear();
		}

		public bool AppendVertex(StraightPathVertex vert)
		{
			bool equalToLast = false;
			if (Count > 0)
			{
				//can only be done if at least one vertex in path
				Vector3 lastStraightPath = verts[Count - 1].Point.Position;
				Vector3 pos = vert.Point.Position;
				equalToLast = Vector3Extensions.AlmostEqual(ref lastStraightPath, ref pos);
			}

			if (equalToLast)
			{
				//the vertices are equal, update flags and polys
				verts[Count - 1] = vert;
			}
			else
			{
				//append new vertex
				verts.Add(vert);

				if (vert.Flags == StraightPathFlags.End)
				{
					return false;
				}
			}

			return true;
		}

		public void RemoveAt(int index)
		{
			verts.RemoveAt(index);
		}

		public void RemoveRange(int index, int count)
		{
			verts.RemoveRange(index, count);
		}
	}
}
