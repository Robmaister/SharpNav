// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav.CLI
{
	/// <summary>
	/// An enumeration of different levels of output.
	/// </summary>
	public enum VerbosityLevel
	{
		/// <summary>
		/// Program outputs nothing.
		/// </summary>
		Silent,

		/// <summary>
		/// Program only outputs the bare minimum. Great for automation.
		/// </summary>
		Minimal,
		
		/// <summary>
		/// Standard verbosity level.
		/// </summary>
		Normal,

		/// <summary>
		/// Provides more details than most users would need.
		/// </summary>
		Verbose,

		/// <summary>
		/// Outputs everything. Great for tracking down a bug.
		/// </summary>
		Debug
	}

	/// <summary>
	/// Helpers for the <see cref="VerbosityLevel"/> enumeration.
	/// </summary>
	public static class Verbosity
	{
		/// <summary>
		/// Parses a <see cref="VerbosityLevel"/> from a string.
		/// </summary>
		/// <param name="level">The level as a string.</param>
		/// <returns>A value from the <see cref="VerbosityLevel"/> enumeration.</returns>
		public static VerbosityLevel Parse(string level)
		{
			switch (level.ToLowerInvariant())
			{
				case "s":
				case "silent:":
					return VerbosityLevel.Silent;
				case "m":
				case "minimal":
					return VerbosityLevel.Minimal;
				case "n":
				case "normal":
					return VerbosityLevel.Normal;
				case "v":
				case "verbose":
					return VerbosityLevel.Verbose;
				case "d":
				case "debug":
					return VerbosityLevel.Debug;
				default:
					Console.WriteLine("[ERROR] Cannot parse verbosity level \"" + level + "\". Setting to Normal.");
					return VerbosityLevel.Normal;
			}
		}
	}
}
