using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNavEditor.IO
{
	public class ObjLoader : IModelLoader
	{
		public bool SupportsPositions
		{
			get { throw new NotImplementedException(); }
		}

		public bool SupportsTextureCoordinates
		{
			get { throw new NotImplementedException(); }
		}

		public bool SupportsNormals
		{
			get { throw new NotImplementedException(); }
		}

		public bool SupportsTangents
		{
			get { throw new NotImplementedException(); }
		}

		public bool SupportsBitangents
		{
			get { throw new NotImplementedException(); }
		}

		public bool SupportsColors
		{
			get { throw new NotImplementedException(); }
		}

		public bool SupportsAnimation
		{
			get { throw new NotImplementedException(); }
		}

		public bool SupportsSkeleton
		{
			get { throw new NotImplementedException(); }
		}

		public bool SupportsIndexing
		{
			get { throw new NotImplementedException(); }
		}

		public int CustomVertexDataTypesCount
		{
			get { throw new NotImplementedException(); }
		}

		public IModelData LoadModel(string path)
		{
			throw new NotImplementedException();
		}
	}
}
