#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	public class AreaFlagsGenerator
	{
		private IEnumerable<Triangle3> tris;
		private int triCount;
		private List<Tuple<Func<Triangle3, bool>, AreaFlags>> conditions;
		private AreaFlags defaultArea;

		private AreaFlagsGenerator(IEnumerable<Triangle3> verts, int triCount, AreaFlags defaultArea)
		{
			this.tris = verts;
			this.triCount = triCount;
			this.defaultArea = defaultArea;
			conditions = new List<Tuple<Func<Triangle3, bool>, AreaFlags>>();
		}

		public static AreaFlagsGenerator From(Triangle3[] tris, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromTriangle(tris, 0, tris.Length), tris.Length, area);
		}

		public static AreaFlagsGenerator From(Triangle3[] tris, int triOffset, int triCount, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromTriangle(tris, triOffset, triCount), triCount, area);
		}

		public static AreaFlagsGenerator From(Vector3[] verts, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromVector3(verts, 0, 1, verts.Length / 3), verts.Length / 3, area);
		}

		public static AreaFlagsGenerator From(Vector3[] verts, int vertOffset, int vertStride, int triCount, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromVector3(verts, vertOffset, vertStride, triCount), triCount, area);
		}

		public static AreaFlagsGenerator From(float[] verts, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromFloat(verts, 0, 3, verts.Length / 9), verts.Length / 9, area);
		}

		public static AreaFlagsGenerator From(float[] verts, int floatOffset, int floatStride, int triCount, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromFloat(verts, floatOffset, floatStride, triCount), triCount, area);
		}

		public static AreaFlagsGenerator From(Vector3[] verts, int[] inds, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromIndexedVector3(verts, inds, 0, 1, 0, inds.Length / 3), inds.Length / 3, area);
		}

		public static AreaFlagsGenerator From(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromIndexedVector3(verts, inds, vertOffset, vertStride, indexOffset, triCount), triCount, area);
		}

		public static AreaFlagsGenerator From(float[] verts, int[] inds, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromIndexedFloat(verts, inds, 0, 3, 0, inds.Length / 3), inds.Length / 3, area);
		}

		public static AreaFlagsGenerator From(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount, AreaFlags area)
		{
			return new AreaFlagsGenerator(TriangleEnumerable.FromIndexedFloat(verts, inds, floatOffset, floatStride, indexOffset, triCount), triCount, area);
		}

		public AreaFlags[] ToArray()
		{
			AreaFlags[] areas = new AreaFlags[triCount];

			int i = 0;
			foreach (var tri in tris)
			{
				areas[i] = defaultArea;

				foreach (var condition in conditions)
					if (condition.Item1(tri))
						areas[i] = condition.Item2;

				i++;
			}

			return areas;
		}

		public AreaFlagsGenerator MarkAboveSlope(float angle, AreaFlags area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaFlags>(tri =>
			{
				Vector3 n = tri.Normal;
				return Vector3.Dot(n, Vector3.UnitY) <= angle;
			}, area));

			return this;
		}

		public AreaFlagsGenerator MarkBelowSlope(float angle, AreaFlags area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaFlags>(tri =>
			{
				Vector3 n = tri.Normal;
				return Vector3.Dot(n, Vector3.UnitY) >= angle;
			}, area));

			return this;
		}

		public AreaFlagsGenerator MarkAtSlope(float angle, float range, AreaFlags area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaFlags>(tri =>
			{
				Vector3 n = tri.Normal;
				float ang = Vector3.Dot(n, Vector3.UnitY);
				return ang >= angle - range && ang <= angle + range;
			}, area));

			return this;
		}

		public AreaFlagsGenerator MarkBelowHeight(float y, AreaFlags area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaFlags>(tri =>
			{
				if (tri.A.Y <= y || tri.B.Y <= y || tri.C.Y <= y)
					return true;

				return false;
			}, area));

			return this;
		}

		public AreaFlagsGenerator MarkAtHeight(float y, float radius, AreaFlags area)
		{
			throw new NotImplementedException();
		}

		public AreaFlagsGenerator MarkAboveHeight(float y, AreaFlags area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaFlags>(tri =>
			{
				if (tri.A.Y >= y || tri.B.Y >= y || tri.C.Y >= y)
					return true;

				return false;
			}, area));

			return this;
		}

		public AreaFlagsGenerator MarkCustomFilter(Func<Triangle3, bool> func, AreaFlags area)
		{
			conditions.Add(Tuple.Create(func, area));

			return this;
		}

		private static class TriangleEnumerable
		{
			public static IEnumerable<Triangle3> FromTriangle(Triangle3[] tris, int triOffset, int triCount)
			{
				for (int i = 0; i < triCount; i++)
					yield return tris[triOffset + i];
			}

			public static IEnumerable<Triangle3> FromVector3(Vector3[] verts, int vertOffset, int vertStride, int triCount)
			{
				Triangle3 tri;

				for (int i = 0; i < triCount; i++)
				{
					tri.A = verts[vertOffset + i * vertStride * 3];
					tri.B = verts[vertOffset + i * vertStride * 6];
					tri.C = verts[vertOffset + i * vertStride * 9];

					yield return tri;
				}
			}

			public static IEnumerable<Triangle3> FromFloat(float[] verts, int floatOffset, int floatStride, int triCount)
			{
				Triangle3 tri;

				for (int i = 0; i < triCount; i++)
				{
					int indA = floatOffset + i * floatStride * 3;
					int indB = indA + floatStride;
					int indC = indB + floatStride;

					tri.A.X = verts[indA];
					tri.A.Y = verts[indA + 1];
					tri.A.Z = verts[indA + 2];

					tri.B.X = verts[indB];
					tri.B.Y = verts[indB + 1];
					tri.B.Z = verts[indB + 2];

					tri.C.X = verts[indC];
					tri.C.Y = verts[indC + 1];
					tri.C.Z = verts[indC + 2];

					yield return tri;
				}
			}

			public static IEnumerable<Triangle3> FromIndexedVector3(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount)
			{
				Triangle3 tri;

				for (int i = 0; i < triCount; i++)
				{
					int indA = vertOffset + inds[indexOffset + i * 3] * vertStride;
					int indB = vertOffset + inds[indexOffset + i * 3 + 1] * vertStride;
					int indC = vertOffset + inds[indexOffset + i * 3 + 2] * vertStride;

					tri.A = verts[indA];
					tri.B = verts[indB];
					tri.C = verts[indC];

					yield return tri;
				}
			}

			public static IEnumerable<Triangle3> FromIndexedFloat(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount)
			{
				Triangle3 tri;

				for (int i = 0; i < triCount; i++)
				{
					int indA = floatOffset + inds[indexOffset + i * 3] * floatStride;
					int indB = floatOffset + inds[indexOffset + i * 3 + 1] * floatStride;
					int indC = floatOffset + inds[indexOffset + i * 3 + 2] * floatStride;

					tri.A.X = verts[indA];
					tri.A.Y = verts[indA + 1];
					tri.A.Z = verts[indA + 2];

					tri.B.X = verts[indB];
					tri.B.Y = verts[indB + 1];
					tri.B.Z = verts[indB + 2];

					tri.C.X = verts[indC];
					tri.C.Y = verts[indC + 1];
					tri.C.Z = verts[indC + 2];

					yield return tri;
				}
			}
		}
	}
}
