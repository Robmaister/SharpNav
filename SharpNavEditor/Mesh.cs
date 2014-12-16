#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenTK;

using SharpNav.Geometry;

using SharpNavEditor.IO;

#if !OPENTK
using Vector3 = OpenTK.Vector3;
using SVector3 = SharpNav.Geometry.Vector3;
#else
using SVector3 = OpenTK.Vector3;
#endif

namespace SharpNavEditor
{
	public class Mesh
	{
		private IModelData modelData;
		private Transform transform;

		public Mesh(string name, IModelData data)
		{
			Name = name;
			modelData = data;
			transform = Transform.Identity;
		}

		public string Name { get; set; }

		public IModelData Model { get { return modelData; } }

		public Transform Transform { get { return transform; } set { transform = value; } }

		public IEnumerable<Triangle3> GetTransformedTris()
		{
			Matrix4 m = Transform.Matrix;

			if (modelData.Indices != null)
			{
				var pos = modelData.Positions;
				var ind = modelData.Indices;

				for (int i = 0; i < ind.Length / 9; i++)
				{
					Triangle3 tri;

					int indStart = i * 9;
					Vector3 tmp = new Vector3(pos[ind[indStart]], pos[ind[indStart + 1]], pos[ind[indStart + 2]]);
					tmp = Vector3.Transform(tmp, m);
					tri.A = new SVector3(tmp.X, tmp.Y, tmp.Z);

					indStart += 3;
					tmp = new Vector3(pos[ind[indStart]], pos[ind[indStart + 1]], pos[ind[indStart + 2]]);
					tmp = Vector3.Transform(tmp, m);
					tri.B = new SVector3(tmp.X, tmp.Y, tmp.Z);

					indStart += 3;
					tmp = new Vector3(pos[ind[indStart]], pos[ind[indStart + 1]], pos[ind[indStart + 2]]);
					tmp = Vector3.Transform(tmp, m);
					tri.C = new SVector3(tmp.X, tmp.Y, tmp.Z);

					yield return tri;
				}
			}
			else
			{
				var pos = modelData.Positions;

				for (int i = 0; i < pos.Length / 9; i++)
				{
					Triangle3 tri;

					int posStart = i * 9;
					Vector3 tmp = new Vector3(pos[posStart], pos[posStart + 1], pos[posStart + 2]);
					tmp = Vector3.Transform(tmp, m);
					tri.A = new SVector3(tmp.X, tmp.Y, tmp.Z);

					posStart += 3;
					tmp = new Vector3(pos[posStart], pos[posStart + 1], pos[posStart + 2]);
					tmp = Vector3.Transform(tmp, m);
					tri.B = new SVector3(tmp.X, tmp.Y, tmp.Z);

					posStart += 3;
					tmp = new Vector3(pos[posStart], pos[posStart + 1], pos[posStart + 2]);
					tmp = Vector3.Transform(tmp, m);
					tri.C = new SVector3(tmp.X, tmp.Y, tmp.Z);

					yield return tri;
				}
			}
		}
	}
}
