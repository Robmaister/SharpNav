// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav.CLI
{
	//TODO fix strings longer than one line or including newlines


	public static class Log
	{
		public static VerbosityLevel Verbosity { get; set; }

		static Log()
		{
			Verbosity = VerbosityLevel.Normal;
		}

		public static void Write(string value)
		{
			if (Log.Verbosity < VerbosityLevel.Minimal)
				return;

			Console.WriteLine(value);
		}

		public static void Error(string value)
		{
			if (Log.Verbosity < VerbosityLevel.Minimal)
				return;

			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("[ERR] " + value);
			Console.ResetColor();
		}

		public static void Warning(string value)
		{
			if (Log.Verbosity < VerbosityLevel.Normal)
				return;

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("[WRN] " + value);
			Console.ResetColor();
		}

		public static void Info(string value)
		{
			if (Log.Verbosity < VerbosityLevel.Verbose)
				return;

			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine("[INF] " + value);
			Console.ResetColor();
		}

		public static void Debug(string value)
		{
			Log.Debug(value, 0);
		}

		public static void Debug(string value, int tabLevel)
		{
			if (Log.Verbosity < VerbosityLevel.Debug)
				return;

			Console.ForegroundColor = ConsoleColor.DarkGreen;
			Console.Write("[DBG] ");
			for (int i = 0; i < tabLevel; i++)
				Console.Write("  ");

			Console.WriteLine(value);
			Console.ResetColor();
		}
	}
}
