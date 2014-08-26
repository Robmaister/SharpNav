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

		public int CustomVertexDataTypesCount { get {return 0;} }

		public IModelData LoadModel(string path)
		{
			var position = new List<Vector3>();
			var faces = new List<List<PlyIndex>>();
			bool end_header = false;

			foreach (string line in File.ReadAllLines(path))
			{
				int sizeofFaces = 0;
				int sizeofVerts = 0;
				string trimmedLine = line;
				trimmedLine = trimmedLine.Trim();
				string[] lineData = trimmedLine.Split(lineSplitChars, StringSplitOptions.RemoveEmptyEntries);
				if (lineData == null || lineData.Length == 0) 
					continue; 

				switch (lineData [0]) 
				{
				case "element":
					if (lineData [1].CompareTo ("vertex") == 0) 
					{
						int.TryParse (lineData [2], out sizeofVerts);
					} 
					else if (lineData [1].CompareTo ("face") == 0) 
					{
						int.TryParse (lineData [2], out sizeofFaces);
					}
					break;
				case "end_header":
					end_header = true;
					continue;
				}
				if (end_header) 
				{
					if (sizeofVerts != 0) 
					{
						if (lineData.Length < 3)
							continue;
						Vector3 v;
						if (!TryParseVec3(lineData, 0, 1, 2, out v))
							continue;

						position.Add(v);
						sizeofVerts--;
						continue;
					}
					else if (sizeofFaces != 0) 
					{
						if (lineData.Length < 3)
							continue;
						var faceIndices = new List<PlyIndex>();

						bool error = false;
						for (int i = 0; i < lineData.Length; i++)
						{
							PlyIndex plyInd;
							if (!PlyIndex.TryParse(lineData[i], out plyInd))
							{
								error = true;
								break;
							}

							faceIndices.Add(plyInd);
						}
						if (error)
							continue;

						faces.Add(faceIndices);
						sizeofFaces--;
						continue;
					}
				}


			}
			if (faces.Count == 0)
				return null;

			var pos = new List<float>();
			bool hasPos = (faces[0][0].PositionIndex != -1);
			foreach (var f in faces) 
			{
				PlyIndex first = f [0];
				for (int i = 1; i < f.Count - 1; i++) 
				{
					PlyIndex a = f [i], b = f [i + 1];
					if (hasPos)
					{
						Vector3 pf = position[first.PositionIndex], pa = position[a.PositionIndex], pb = position[b.PositionIndex];
						pos.Add(pf.X);
						pos.Add(pf.Y);
						pos.Add(pf.Z);
						pos.Add(pa.X);
						pos.Add(pa.Y);
						pos.Add(pa.Z);
						pos.Add(pb.X);
						pos.Add(pb.Y);
						pos.Add(pb.Z);
					}
				}
			}
			return new PlyData ((hasPos ? pos.ToArray () : null));
		
		}

		private bool TryParseVec3(string[] values, int x, int y, int z, out Vector3 v)
		{
			v = Vector3.Zero;

			if (!float.TryParse(values[x], NumberStyles.Any, CultureInfo.InvariantCulture, out v.X))
				return false;
			if (!float.TryParse(values[y], NumberStyles.Any, CultureInfo.InvariantCulture, out v.Y))
				return false;
			if (!float.TryParse(values[z], NumberStyles.Any, CultureInfo.InvariantCulture, out v.Z))
				return false;

			return true;
		}

		private bool TryParseVec2(string[] values, int x, int y, out Vector2 v)
		{
			v = Vector2.Zero;

			if (!float.TryParse(values[x], NumberStyles.Any, CultureInfo.InvariantCulture, out v.X))
				return false;
			if (!float.TryParse(values[y], NumberStyles.Any, CultureInfo.InvariantCulture, out v.Y))
				return false;

			return true;
		}

		private struct PlyIndex
		{
			public static readonly PlyIndex Error = new PlyIndex(-1);

			private static readonly char[] indSplitChars = { ' ' };

			private int positionIndex;
			public PlyIndex(int pos)
			{
				positionIndex = pos;
			}

			public int PositionIndex
			{
				get
				{
					return positionIndex;
				}
			}
				

			public static bool TryParse(string value, out PlyIndex index)
			{
				int p = -1;

				string[] inds = value.Split(indSplitChars);

				if (inds == null || inds.Length == 0)
				{
					index = Error;
					return false;
				}

				//parse position index
				if (!int.TryParse(value, out p))
				{
					index = Error;
					return false;
				}

				index = new PlyIndex(p);
				return true;
			}
		}
	}
}

