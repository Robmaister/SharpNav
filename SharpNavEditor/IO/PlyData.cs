using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNavEditor.IO
{
	public class PlyData : IModelData
	{
		public PlyData (float[] pos)
		{
			Positions = pos;

		}

		public int CustomVertexDataTypesCount { get { return 0; } }

		public int PositionVertexSize { get { return 3; } }
		public int TextureCoordinateVertexSize { get { return 0; } }
		public int NormalVertexSize { get { return 0; } }
		public int TangentVertexSize { get { return 0; } }
		public int BitangentVertexSize { get { return 0; } }
		public int ColorVertexSize { get { return 0; } }

		public float[] Positions { get; private set; }
		public float[] TextureCoordinates { get { return null; } }
		public float[] Normals { get { return null; } }
		public float[] Tangents { get { return null; } }
		public float[] Bitangents { get { return null; } }
		public float[] Colors { get { return null; } }

		//TODO animation/skeleton handlers

		public int[] Indices { get { return null; } }
	}
}

