// Copyright (c) 2013, 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav.Examples
{
	class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			#if OPENTK || STANDALONE
			using (ExampleWindow ex = new ExampleWindow())
			{
				ex.Run();
			}
			#else
			Console.WriteLine("SharpNav.Examples does not support this configuration of SharpNav.");
			#endif
		}
	}
}
