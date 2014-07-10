#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Runtime.InteropServices;

using OpenTK;

namespace SharpNavEditor
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct Transform
	{
		public static readonly Transform Identity = new Transform(Vector3.Zero, Quaternion.Identity, 1);

		public Vector3 Translation;
		public Quaternion Rotation;
		public float Scale;

		public Transform(Vector3 translation, Quaternion rotation, float scale)
		{
			Translation = translation;
			Rotation = rotation;
			Scale = scale;
		}

		public Matrix4 Matrix
		{
			get
			{
				Matrix4 mat, tmp;
				Matrix4.CreateScale(Scale, out mat);
				Matrix4.CreateFromQuaternion(ref Rotation, out tmp);
				Matrix4.Mult(ref mat, ref tmp, out mat);
				Matrix4.CreateTranslation(ref Translation, out tmp);
				Matrix4.Mult(ref mat, ref tmp, out mat);

				return mat;
			}
		}
	}
}
