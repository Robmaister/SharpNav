#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav
{
	[Serializable]
	public struct Vector2i : IEquatable<Vector2i>
	{
		public static readonly Vector2i Min = new Vector2i(int.MinValue, int.MinValue);
		public static readonly Vector2i Max = new Vector2i(int.MaxValue, int.MaxValue);
		public static readonly Vector2i Zero = new Vector2i(0, 0);

		public int X;
		public int Y;

		public Vector2i(int x, int y)
		{
			X = x;
			Y = y;
		}

		public static bool operator ==(Vector2i left, Vector2i right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(Vector2i left, Vector2i right)
		{
			return !(left == right);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public override string ToString()
		{
			return "{ X: " + X.ToString() + ", Y: " + Y.ToString() + " }";
		}

		public override bool Equals(object obj)
		{
			Vector2i? objV = obj as Vector2i?;
			if (objV != null)
				return Equals(objV);

			return false;
		}

		public bool Equals(Vector2i other)
		{
			return X == other.X && Y == other.Y;
		}
	}
}
