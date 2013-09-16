#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav
{
	internal static class MathHelper
	{
		internal static int Clamp(int val, int min, int max)
		{
			return val < min ? min : (val > max ? max : val);
		}

		internal static void Clamp(ref int val, int min, int max)
		{
			val = val < min ? min : (val > max ? max : val);
		}

		internal static uint Clamp(uint val, uint min, uint max)
		{
			return val < min ? min : (val > max ? max : val);
		}

		internal static void Clamp(ref uint val, uint min, uint max)
		{
			val = val < min ? min : (val > max ? max : val);
		}
	}
}
