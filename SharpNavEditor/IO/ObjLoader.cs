using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using OpenTK;
using System.Diagnostics;

namespace SharpNavEditor.IO
{
	public class ObjLoader : IModelLoader
	{
		private static readonly char[] lineSplitChars = { ' ' };

		public bool SupportsPositions { get { return true; } }

		public bool SupportsTextureCoordinates { get { return true; } }

		public bool SupportsNormals { get { return true; } }

		public bool SupportsTangents { get { return false; } }

		public bool SupportsBitangents { get { return false; } }

		public bool SupportsColors { get { return false; } }

		public bool SupportsAnimation { get { return false; } }

		public bool SupportsSkeleton { get { return false; } }

		public bool SupportsIndexing { get { return false; } }

		public int CustomVertexDataTypesCount { get { return 0; } }

		public IModelData LoadModel(string path)
		{
			var position = new List<Vector3>();
			var texcoord = new List<Vector2>();
			var normal = new List<Vector3>();
			var faces = new List<List<ObjIndex>>();

			foreach (string line in File.ReadAllLines(path))
			{
				string trimmedLine = line;
				int commentStart = line.IndexOf('#');
				if (commentStart != -1)
					trimmedLine = trimmedLine.Substring(0, commentStart);

				trimmedLine = trimmedLine.Trim();

				string[] lineData = trimmedLine.Split(lineSplitChars, StringSplitOptions.RemoveEmptyEntries);
                if (lineData == null || lineData.Length == 0) 
                    continue; 
					

				switch (lineData[0])
				{
					case "v":
						if (lineData.Length < 4)
							continue;

						Vector3 v;
						if (!TryParseVec3(lineData, 1, 2, 3, out v))
							continue;

						position.Add(v);
						break;
					case "vt":
						if (lineData.Length < 3)
							continue;
						else if (lineData.Length == 3)
						{
							Vector2 vt;
							if (!TryParseVec2(lineData, 1, 2, out vt))
								continue;

							texcoord.Add(vt);
						}
						else if (lineData.Length == 4)
							throw new NotSupportedException("3D texture coordinates are not supported.");

						break;
					case "vn":
						if (lineData.Length < 4)
							continue;

						Vector3 vn;
						if (!TryParseVec3(lineData, 1, 2, 3, out vn))
							continue;

						normal.Add(vn);
						break;
					case "f":
						if (lineData.Length < 4)
							continue;

						var faceIndices = new List<ObjIndex>();

						bool error = false;
						for (int i = 1; i < lineData.Length; i++)
						{
							ObjIndex objInd;
							if (!ObjIndex.TryParse(lineData[i], position.Count, texcoord.Count, normal.Count, out objInd))
							{
								error = true;
								break;
							}

							faceIndices.Add(objInd);
						}

						if (error)
							continue;

						faces.Add(faceIndices);
						break;
					case "p":
					case "l":
					case "g":
					case "s":
					case "o":
					case "bevel":
					case "c_interp":
					case "d_interp":
					case "lod":
					case "maplib":
					case "usemap":
					case "usemtl":
					case "mtllib":
					case "shadow_obj":
					case "trace_obj":
						//these are not unsupported per-se, just useless for dealing with navigation meshes.
						break;
					case "call":
					case "scmp":
					case "csh":
						throw new NotSupportedException("Calling other files is not supported.");
					case "vp":
					case "cstype":
					case "deg":
					case "bmat":
					case "step":
					case "curv":
					case "curv2":
					case "surf":
					case "parm":
					case "trim":
					case "hole":
					case "scrv":
					case "sp":
					case "end":
					case "con":
					case "mg":
					case "ctech":
					case "stech":
					case "bsp":
					case "bzp":
					case "cdc":
					case "cdp":
					case "res":
						throw new NotSupportedException("Free-form geometry in OBJ files is not supported.");
					default:
						break;
				}
			}

			if (faces.Count == 0)
				return null;

			//TODO split on groups/objects and return multiple models, in case some have different properties (missing or added texcoords, norms)
			var pos = new List<float>();
			var texc = new List<float>();
			var norm = new List<float>();

			bool hasPos = (faces[0][0].PositionIndex != -1);
			bool hasTexc = (faces[0][0].TexcoordIndex != -1);
			bool hasNorm = (faces[0][0].NormalIndex != -1);

			foreach (var f in faces)
			{
				ObjIndex first = f[0];

				for (int i = 1; i < f.Count - 1; i++)
				{
					ObjIndex a = f[i], b = f[i + 1];

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

					if (hasTexc)
					{
						Vector2 tf = texcoord[first.TexcoordIndex], ta = texcoord[a.TexcoordIndex], tb = texcoord[b.TexcoordIndex];
						texc.Add(tf.X);
						texc.Add(tf.Y);
						texc.Add(ta.X);
						texc.Add(ta.Y);
						texc.Add(tb.X);
						texc.Add(tb.Y);
					}

					if (hasNorm)
					{
						Vector3 nf = normal[first.NormalIndex], na = normal[a.NormalIndex], nb = normal[b.NormalIndex];
						norm.Add(nf.X);
						norm.Add(nf.Y);
						norm.Add(nf.Z);
						norm.Add(na.X);
						norm.Add(na.Y);
						norm.Add(na.Z);
						norm.Add(nb.X);
						norm.Add(nb.Y);
						norm.Add(nb.Z);
					}
				}
			}

			return new ObjData((hasPos ? pos.ToArray() : null), (hasTexc ? texc.ToArray() : null), (hasNorm ? norm.ToArray() : null));
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

		private struct ObjIndex
		{
			public static readonly ObjIndex Error = new ObjIndex(-1, -1, -1);

			private static readonly char[] indSplitChars = { '/' };

			private int positionIndex;
			private int texcoordIndex;
			private int normalIndex;

			public ObjIndex(int pos, int texcoord, int norm)
			{
				positionIndex = pos;
				texcoordIndex = texcoord;
				normalIndex = norm;
			}

			public int PositionIndex
			{
				get
				{
					return positionIndex;
				}
			}

			public int TexcoordIndex
			{
				get
				{
					return texcoordIndex;
				}
			}

			public int NormalIndex
			{
				get
				{
					return normalIndex;
				}
			}

			public static bool TryParse(string value, int posCount, int texcoordCount, int normCount, out ObjIndex index)
			{
				int p = -1, t = -1, n = -1;

				string[] inds = value.Split(indSplitChars);

				if (inds == null || inds.Length == 0)
				{
					index = Error;
					return false;
				}

				//parse position index
				if (!int.TryParse(inds[0], out p))
				{
					index = Error;
					return false;
				}
				else if (p == -1)
					p += posCount;
				else
					p--;

				//parse texcoord index
				//it's ok to define an index as "3//5" as long as there are no texcoords
				if (inds.Length >= 2 && inds[1] != "")
				{
					if (!int.TryParse(inds[1], out t))
					{
						index = Error;
						return false;
					}
					else if (t == -1)
						t += texcoordCount;
					else
						t--;
				}
				
				//parse normal index
				if (inds.Length >= 3)
				{
					if (!int.TryParse(inds[2], out n))
					{
						index = Error;
						return false;
					}
					else if (n == -1)
						n += normCount;
					else
						n--;
				}

				index = new ObjIndex(p, t, n);
				return true;
			}
		}
	}
}
