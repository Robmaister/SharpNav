using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using OpenTK;
using System.Diagnostics;

namespace SharpNavEditor.IO
{
	[FileLoader(".ply")]
	public class PlyLoader:IModelLoader
	{

		private static readonly char[] lineSplitChars = { ' ' };

		public bool SupportsPositions { get {return true;} }
		public bool SupportsTextureCoordinates { get {return false;} }
		public bool SupportsNormals { get {return false;} }
		public bool SupportsTangents { get {return false;} }
		public bool SupportsBitangents { get {return false;} }
		public bool SupportsColors { get {return false;} }
		public bool SupportsAnimation { get {return false;} }
		public bool SupportsSkeleton { get {return false;} }
		public bool SupportsIndexing { get {return false;} }

		int CustomVertexDataTypesCount { get {return 0;} }

		IModelData LoadModel(string path)
		{

		}
		
	}
}

