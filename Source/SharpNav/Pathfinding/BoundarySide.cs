// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// An enumeration of the different places a point can be relative to a rectangular boundary on the XZ plane.
	/// </summary>
	public enum BoundarySide : byte
	{
		/// <summary>
		/// Not outside of the defined boundary.
		/// </summary>
		Internal = 0xff,

		/// <summary>
		/// Only outside of the defined bondary on the X axis, in the positive direction.
		/// </summary>
		PlusX = 0,

		/// <summary>
		/// Outside of the defined boundary on both the X and Z axes, both in the positive direction.
		/// </summary>
		PlusXPlusZ = 1,

		/// <summary>
		/// Only outside of the defined bondary on the Z axis, in the positive direction.
		/// </summary>
		PlusZ = 2,

		/// <summary>
		/// Outside of the defined boundary on both the X and Z axes, in the negative and positive directions respectively.
		/// </summary>
		MinusXPlusZ = 3,

		/// <summary>
		/// Only outside of the defined bondary on the X axis, in the negative direction.
		/// </summary>
		MinusX = 4,

		/// <summary>
		/// Outside of the defined boundary on both the X and Z axes, both in the negative direction.
		/// </summary>
		MinusXMinusZ = 5,

		/// <summary>
		/// Only outside of the defined bondary on the Z axis, in the negative direction.
		/// </summary>
		MinusZ = 6,

		/// <summary>
		/// Outside of the defined boundary on both the X and Z axes, in the positive and negative directions respectively.
		/// </summary>
		PlusXMinusZ = 7
	}

	/// <summary>
	/// Extension methods for the <see cref="BoundarySide"/> enumeration.
	/// </summary>
	public static class BoundarySideExtensions
	{
		/// <summary>
		/// Gets the side in the exact opposite direction as a specified side.
		/// </summary>
		/// <remarks>
		/// The value <see cref="BoundarySide.Internal"/> will always return <see cref="BoundarySide.Internal"/>.
		/// </remarks>
		/// <param name="side">A side.</param>
		/// <returns>The opposite side.</returns>
		public static BoundarySide GetOpposite(this BoundarySide side)
		{
			if (side == BoundarySide.Internal)
				return BoundarySide.Internal;

			return (BoundarySide)((int)(side + 4) % 8);
		}

		/// <summary>
		/// Gets the boundary side of a point relative to a bounding box.
		/// </summary>
		/// <param name="pt">A point.</param>
		/// <param name="bounds">A bounding box.</param>
		/// <returns>The point's position relative to the bounding box.</returns>
		public static BoundarySide FromPoint(Vector3 pt, BBox3 bounds)
		{
			const int PlusX = 0x1;
			const int PlusZ = 0x2;
			const int MinusX = 0x4;
			const int MinusZ = 0x8;

			int outcode = 0;
			outcode |= (pt.X >= bounds.Max.X) ? PlusX : 0;
			outcode |= (pt.Z >= bounds.Max.Z) ? PlusZ : 0;
			outcode |= (pt.X < bounds.Min.X) ? MinusX : 0;
			outcode |= (pt.Z < bounds.Min.Z) ? MinusZ : 0;

			switch (outcode)
			{
				case PlusX:
					return BoundarySide.PlusX;

				case PlusX | PlusZ:
					return BoundarySide.PlusXPlusZ;

				case PlusZ:
					return BoundarySide.PlusZ;

				case MinusX | PlusZ:
					return BoundarySide.MinusXPlusZ;

				case MinusX:
					return BoundarySide.MinusX;

				case MinusX | MinusZ:
					return BoundarySide.MinusXMinusZ;

				case MinusZ:
					return BoundarySide.MinusZ;

				case PlusX | MinusZ:
					return BoundarySide.PlusXMinusZ;

				default:
					return BoundarySide.Internal;
			}
		}
	}
}
