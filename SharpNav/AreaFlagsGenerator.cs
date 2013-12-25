using System;
using System.Collections.Generic;
using System.Linq;

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
	public static class AreaFlagsGenerator
	{
		public static AreaFlagsQuery From(Triangle3[] tris)
		{
			return new FromQuery(null, new TriangleCollection(tris, 0, tris.Length));
		}

		public static AreaFlagsQuery From(Triangle3[] tris, int triOffset, int triCount)
		{
			return new FromQuery(null, new TriangleCollection(tris, triOffset, triCount));
		}

		public static AreaFlagsQuery From(Vector3[] verts)
		{
			return new FromQuery(null, new Vector3Collection(verts, 0, 1, verts.Length / 3));
		}

		public static AreaFlagsQuery From(Vector3[] verts, int vertOffset, int vertStride, int triCount)
		{
			return new FromQuery(null, new Vector3Collection(verts, vertOffset, vertStride, triCount));
		}

		public static AreaFlagsQuery From(float[] verts)
		{
			return new FromQuery(null, new FloatCollection(verts, 0, 3, verts.Length / 9));
		}

		public static AreaFlagsQuery From(float[] verts, int floatOffset, int floatStride, int triCount)
		{
			return new FromQuery(null, new FloatCollection(verts, floatOffset, floatStride, triCount));
		}

		public static AreaFlagsQuery From(Vector3[] verts, int[] inds)
		{
			return new FromQuery(null, new IndexedVector3Collection(verts, inds, 0, 1, 0, inds.Length / 3));
		}

		public static AreaFlagsQuery From(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount)
		{
			return new FromQuery(null, new IndexedVector3Collection(verts, inds, vertOffset, vertStride, indexOffset, triCount));
		}

		public static AreaFlagsQuery From(float[] verts, int[] inds)
		{
			return new FromQuery(null, new IndexedFloatCollection(verts, inds, 0, 3, 0, inds.Length / 3));
		}

		public static AreaFlagsQuery From(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount)
		{
			return new FromQuery(null, new IndexedFloatCollection(verts, inds, floatOffset, floatStride, indexOffset, triCount));
		}

		public abstract class AreaFlagsQuery
		{
			protected AreaFlagsQuery parent;
			protected IEnumerable<Tuple<int, Triangle3>> tris;

			protected AreaFlagsQuery(AreaFlagsQuery parent, IEnumerable<Tuple<int, Triangle3>> tris)
			{
				this.parent = parent;
				this.tris = tris;
			}

			internal abstract IEnumerable<Tuple<int, Triangle3>> Execute(AreaFlags[] flags);

			public AreaFlags[] Create()
			{
				//calling Count() does not iterate over all tris, since tris will always
				//be a subclass of ICollection<Tuple<int, Triangle3>>.
				AreaFlags[] flags = new AreaFlags[tris.Count()];

				//run the query on the list and return the modified array.
				foreach (var tri in Execute(flags))
				{
				}

				return flags;
			}

			public AreaFlagsQuery And()
			{
				return new AndQuery(this, tris);
			}

			public AreaFlagsQuery SetArea(AreaFlags area)
			{
				return new SetAreaQuery(this, tris, area);
			}

			public AreaFlagsQuery IsWalkable()
			{
				return new SetAreaQuery(this, tris, AreaFlags.Walkable);
			}

			public AreaFlagsQuery IsUnwalkable()
			{
				return new SetAreaQuery(this, tris, AreaFlags.Null);
			}

			public AreaFlagsQuery Where(Predicate<Triangle3> condition)
			{
				return new WhereQuery(this, tris, condition);
			}

			public AreaFlagsQuery Where(Func<BBox3, bool> condition)
			{
				Predicate<Triangle3> pred = t =>
				{
					BBox3 bbox;
					Triangle3.GetBoundingBox(ref t, out bbox);
					return condition(bbox);
				};

				return new WhereQuery(this, tris, pred);
			}

			public AreaFlagsQuery Where(Func<Vector3, Vector3, Vector3, bool> condition)
			{
				Predicate<Triangle3> pred = t => condition(t.A, t.B, t.C);

				return new WhereQuery(this, tris, pred);
			}
		}

		public class FromQuery : AreaFlagsQuery
		{
			public FromQuery(AreaFlagsQuery parent, IEnumerable<Tuple<int, Triangle3>> tris)
				: base(parent, tris)
			{
			}

			internal override IEnumerable<Tuple<int, Triangle3>> Execute(AreaFlags[] flags)
			{
				return tris;
			}
		}

		public class WhereQuery : AreaFlagsQuery
		{
			private Predicate<Triangle3> condition;

			public WhereQuery(AreaFlagsQuery parent, IEnumerable<Tuple<int, Triangle3>> tris, Predicate<Triangle3> condition)
				: base(parent, tris)
			{
				this.condition = condition;
			}

			internal override IEnumerable<Tuple<int, Triangle3>> Execute(AreaFlags[] flags)
			{
				foreach (var tri in parent.Execute(flags))
				{
					if (condition(tri.Item2))
						yield return tri;
				}
			}
		}

		public class SetAreaQuery : AreaFlagsQuery
		{
			private AreaFlags area;

			public SetAreaQuery(AreaFlagsQuery parent, IEnumerable<Tuple<int, Triangle3>> tris, AreaFlags area)
				: base(parent, tris)
			{
				this.area = area;
			}

			internal override IEnumerable<Tuple<int, Triangle3>> Execute(AreaFlags[] flags)
			{
				foreach (var tri in parent.Execute(flags))
				{
					flags[tri.Item1] = area;

					yield return tri;
				}
			}
		}

		public class AndQuery : AreaFlagsQuery
		{
			public AndQuery(AreaFlagsQuery parent, IEnumerable<Tuple<int, Triangle3>> tris)
				: base(parent, tris)
			{
			}

			internal override IEnumerable<Tuple<int, Triangle3>> Execute(AreaFlags[] flags)
			{
				foreach (var tri in parent.Execute(flags))
				{
				}

				foreach (var tri in tris)
					yield return tri;
			}
		}

		private abstract class VertCollection : ICollection<Tuple<int, Triangle3>>
		{
			public abstract IEnumerator<Tuple<int, Triangle3>> GetEnumerator();

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			public void Add(Tuple<int, Triangle3> item)
			{
				throw new InvalidOperationException();
			}

			public void Clear()
			{
				throw new InvalidOperationException();
			}

			public bool Contains(Tuple<int, Triangle3> item)
			{
				throw new InvalidOperationException();
			}

			public void CopyTo(Tuple<int, Triangle3>[] array, int arrayIndex)
			{
				throw new InvalidOperationException();
			}

			public abstract int Count { get; }

			public bool IsReadOnly
			{
				get { return true; }
			}

			public bool Remove(Tuple<int, Triangle3> item)
			{
				throw new InvalidOperationException();
			}
		}

		private class TriangleCollection : VertCollection
		{
			private Triangle3[] tris;
			private int triOffset, triCount;

			public TriangleCollection(Triangle3[] tris, int triOffset, int triCount)
			{
				this.tris = tris;
				this.triOffset = triOffset;
				this.triCount = triCount;
			}

			public override IEnumerator<Tuple<int, Triangle3>> GetEnumerator()
			{
				for (int i = 0; i < triCount; i++)
					yield return Tuple.Create(i, tris[triOffset + i]);

			}

			public override int Count
			{
				get
				{
					return triCount;
				}
			}
		}

		private class Vector3Collection : VertCollection
		{
			private Vector3[] verts;
			private int vertOffset, vertStride, triCount;

			public Vector3Collection(Vector3[] verts, int vertOffset, int vertStride, int triCount)
			{
				this.verts = verts;
				this.vertOffset = vertOffset;
				this.vertStride = vertStride;
				this.triCount = triCount;
			}

			public override IEnumerator<Tuple<int, Triangle3>> GetEnumerator()
			{
				Triangle3 tri;

				for (int i = 0; i < triCount; i++)
				{
					tri.A = verts[vertOffset + i * vertStride * 3];
					tri.B = verts[vertOffset + i * vertStride * 6];
					tri.C = verts[vertOffset + i * vertStride * 9];

					yield return Tuple.Create(i, tri);
				}
			}

			public override int Count
			{
				get { return triCount; }
			}
		}

		private class FloatCollection : VertCollection
		{
			private float[] verts;
			private int floatOffset, floatStride, triCount;

			public FloatCollection(float[] verts, int floatOffset, int floatStride, int triCount)
			{
				this.verts = verts;
				this.floatOffset = floatOffset;
				this.floatStride = floatStride;
				this.triCount = triCount;
			}

			public override IEnumerator<Tuple<int, Triangle3>> GetEnumerator()
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

					yield return Tuple.Create(i, tri);
				}
			}

			public override int Count
			{
				get { return triCount; }
			}
		}

		private class IndexedVector3Collection : VertCollection
		{
			private Vector3[] verts;
			private int[] inds;
			private int vertOffset, vertStride, indexOffset, triCount;

			public IndexedVector3Collection(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount)
			{
				this.verts = verts;
				this.inds = inds;
				this.vertOffset = vertOffset;
				this.vertStride = vertStride;
				this.indexOffset = indexOffset;
				this.triCount = triCount;
			}

			public override IEnumerator<Tuple<int, Triangle3>> GetEnumerator()
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

					yield return Tuple.Create(i, tri);
				}
			}

			public override int Count
			{
				get { return triCount; }
			}
		}

		private class IndexedFloatCollection : VertCollection
		{
			private float[] verts;
			private int[] inds;
			private int floatOffset, floatStride, indexOffset, triCount;

			public IndexedFloatCollection(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount)
			{
				this.verts = verts;
				this.inds = inds;
				this.floatOffset = floatOffset;
				this.floatStride = floatStride;
				this.indexOffset = indexOffset;
				this.triCount = triCount;
			}

			public override IEnumerator<Tuple<int, Triangle3>> GetEnumerator()
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

					yield return Tuple.Create(i, tri);
				}
			}

			public override int Count
			{
				get { return triCount; }
			}
		}
	}
}
