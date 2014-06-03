#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpNav;
using SharpNav.Geometry;

#if OPENTK
using OpenTK;
#endif

namespace Examples
{
	public class PlyModel
	{
		string[] type_name = new string[] 
		{
			"invalid",
			"int8",
			"int16",
			"int32",
			"uint8",
			"uint16",
			"uint32",
			"float32",
			"float64",
			"list",
			"char",
			"uchar",
		};
		string[] old_type_name = new string[]
		{
			"invalid",
			"char", "short", "int", "uchar", "ushort", "uint", "float", "double",
		};

		int[] ply_type_size = new int[] 
		{
			0, 1, 2, 4, 1, 2, 4, 4, 8
		};

//		string type_vertex = "int";
//		string type_face = "int";
//		string type_size_face = "int";
		private static readonly char[] lineSplitChars = { ' ' };

		private List<Triangle3> tris;
//		private List<Vector3> norms;
//
//		private Vector3 bboxOffset;

		public PlyModel (string path)
		{
			tris = new List<Triangle3>();
			//norms = new List<Vector3>();
			List<Vector3> tempVerts = new List<Vector3>();
			//List<Vector3> tempNorms = new List<Vector3>();

			using (StreamReader reader = new StreamReader (path)) 
			{
				string file = reader.ReadToEnd();
				int sizeofFaces = 0;
				int sizeofVerts = 0;
				foreach (string l in file.Split('\n')) 
				{
					string tl = l;
					tl = tl.Trim ();
					string[] line = tl.Split(lineSplitChars, StringSplitOptions.RemoveEmptyEntries);
					Vector3 v;
					float x; //X-coordinate
					float s;
					if (line [0] == "element") {
						switch (line [1]) {
						case "vertex":	//when the word after element is vertex
							if (line.Length < 3)
								continue;
							if (!int.TryParse (line [2], out sizeofVerts))
								continue;
							break;
						case "face":  //when the word after element is face
							if (line.Length < 3)
								continue;
							if (!int.TryParse (line [2], out sizeofFaces))
								continue;
							break;
						default:
							Console.WriteLine ("Not a valid word");
							break;
						}
						continue;
					} 
//					else if (line [0] == "property") 
//					{
//						switch (line [1]) 
//						{
//							case type_name[1]:
//								type_vertex = "int";
//								break;
//							case type_name[2]:
//								type_vertex = "int";
//								break;
//							case type_name[3]:
//								type_vertex = "int";
//								break;
//							case type_name[4]:
//								type_vertex = "uint";
//								break;
//							case type_name[5]:
//								type_vertex = "uint";
//								break;
//							case type_name[6]:
//								type_vertex = "uint";
//								break;
//							case type_name[7]:
//								type_vertex = "float";
//								break;
//							case type_name[8]:
//								type_vertex = "float";
//								break;
//							case type_name[9]:
//								break;
//							default:
//								Console.WriteLine ("Not a valid input");
//								break;
//						}

						//TODO:figure out the size of polygon
//						switch (line [2]) 
//						{
//						case type_name[1]:
//							type_size_face = "int";
//							break;
//						case type_name[2]:
//							type_size_face = "int";
//							break;
//						case type_name[3]:
//							type_size_face = "int";
//							break;
//						case type_name[4]:
//							type_size_face = "uint";
//							break;
//						case type_name[5]:
//							type_size_face = "uint";
//							break;
//						case type_name[6]:
//							type_size_face = "uint";
//							break;
//						case type_name[7]:
//							type_size_face = "float";
//							break;
//						case type_name[8]:
//							type_size_face = "float";
//							break;
//						case type_name[9]:
//							break;
//						default:
//							Console.WriteLine ("Not a valid input");
//							break;
//						}

//						switch (line [3]) 
//						{
//
//						}
//					}
					else if (float.TryParse (line [0], out x) && line.Length == 3) {
						if (TryParseVec (line, 0, 1, 2, out v)) {
							tempVerts.Add (v);
						}
						continue;
					} 
					else if (float.TryParse (line [0], out s) && s == sizeofFaces && line.Length > 3) 
					{
						if (line.Length == 4) {
							int v0, v1, v2;
							if (!int.TryParse (line [1], out v0))
								continue;
							if (!int.TryParse (line [2], out v1))
								continue;
							if (!int.TryParse (line [3], out v2))
								continue;
							tris.Add (new Triangle3 (tempVerts [v0], tempVerts [v1], tempVerts [v2]));
						} 
						else if (line.Length > 4) 
						{
							int v0;
							if (!int.TryParse(line[1], out v0)) continue;

							for (int i = 2; i < line.Length - 1; i++)
							{
								int vi, vii;
								if (!int.TryParse(line[i], out vi)) continue;
								if (!int.TryParse(line[i + 1], out vii)) continue;


								vi -= 1;
								vii -= 1;

								tris.Add(new Triangle3(tempVerts[v0], tempVerts[vi], tempVerts[vii]));
							}
						}
					}

				}
			}
		}


		/// <summary>
		/// Tries the parse vec.
		/// </summary>
		/// <returns><c>true</c>, if parse vec_float was tryed, <c>false</c> otherwise.</returns>
		/// <param name="values">Values.</param>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="z">The z coordinate.</param>
		/// <param name="v">V.</param>
		private bool TryParseVec(string[] values, int x, int y, int z, out Vector3 v)
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
	}
}

