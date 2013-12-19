using System;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	internal static class Vector3Extensions
	{
#if OPENTK
		internal static float Length(this Vector3 v)
		{
			return v.Length;
		}

		internal static float LengthSquared(this Vector3 v)
		{
			return v.LengthSquared;
		}
#endif
	}
}
