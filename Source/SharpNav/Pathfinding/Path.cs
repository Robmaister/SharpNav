// Copyright (c) 2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System.Collections.Generic;

namespace SharpNav.Pathfinding
{
	public class Path
	{
		private List<NavPolyId> Polys;
		private float cost;

		public Path()
		{
			Polys = new List<NavPolyId>();
			cost = 0;
		}

		public Path(Path otherPath)
			: this()
		{
			Polys.AddRange(otherPath.Polys);
			cost = otherPath.Cost;
		}

		public NavPolyId this[int i]
		{
			get
			{
				return Polys[i];
			}
			set
			{
				Polys[i] = value;
			}
		}

		public int Count { get { return Polys.Count; } }

		public float Cost { get { return cost; } }

		public void Clear()
		{
			Polys.Clear();
			cost = 0;
		}

		public void Add(NavPolyId poly)
		{
			Polys.Add(poly);
		}

		public void AddRange(IEnumerable<NavPolyId> polys)
		{
			Polys.AddRange(polys);
		}

		public void AppendPath(Path other)
		{
			Polys.AddRange(other.Polys);
		}

		public void AddCost(float cost)
		{
			this.cost += cost;
		}

		public void Reverse()
		{
			Polys.Reverse();
		}

		public void RemoveTrackbacks()
		{
			for (int j = 0; j < Polys.Count; j++)
			{
				if (j - 1 >= 0 && j + 1 < Polys.Count)
				{
					if (Polys[j - 1] == Polys[j + 1])
					{
						Polys.RemoveRange(j - 1, 2);
						j -= 2;
					}
				}
			}
		}

		public void RemoveAt(int index)
		{
			Polys.RemoveAt(index);
		}

		public void RemoveRange(int index, int count)
		{
			Polys.RemoveRange(index, count);
		}
	}
}
