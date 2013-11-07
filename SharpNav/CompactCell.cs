using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav
{
	public struct CompactCell
	{
		public int StartIndex;
		public int Count;

		public CompactCell(int start, int count)
		{
			StartIndex = start;
			Count = count;
		}
	}
}
