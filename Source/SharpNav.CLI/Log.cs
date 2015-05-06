// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Text;

namespace SharpNav.CLI
{
	/// <summary>
	/// Logs console output based on the <see cref="VerbosityLevel"/> chosen by the user.
	/// </summary>
	/// <remarks>
	/// <see cref="Log.Write"/> and <see cref="WriteLine"/> will output only with a verbosity higher than <see cref="VerbosityLevel.Silent"/>.
	/// <see cref="Log.Error"/> will output only with a verbosity higher than <see cref="VerbosityLevel.Silent"/>.
	/// <see cref="Log.Warning"/> will output only with a verbosity higher than <see cref="VerbosityLevel.Minimal"/>.
	/// <see cref="Log.Info"/> will output only with a verbosity higher than <see cref="VerbosityLevel.Normal"/>.
	/// <see cref="Log.Debug"/> will output only with a verbosity higher than <see cref="VerbosityLevel.Verbose"/>.
	/// </remarks>
	public static class Log
	{
		static Log()
		{
			Verbosity = VerbosityLevel.Normal;

			ErrorColor = ConsoleColor.Red;
			WarningColor = ConsoleColor.Yellow;
			InfoColor = ConsoleColor.DarkGray;
			DebugColor = ConsoleColor.DarkGreen;

			Tab = "  ";
		}

		/// <summary>
		/// Gets or sets the verbosity level. This determines which types of messages get printed.
		/// </summary>
		public static VerbosityLevel Verbosity { get; set; }

		/// <summary>
		/// Gets or sets the color of messages from <see cref="Log.Error"/>.
		/// </summary>
		public static ConsoleColor ErrorColor { get; set; }

		/// <summary>
		/// Gets or sets the color of messages from <see cref="Log.Warning"/>.
		/// </summary>
		public static ConsoleColor WarningColor { get; set; }

		/// <summary>
		/// Gets or sets the color of messages from <see cref="Log.Info"/>.
		/// </summary>
		public static ConsoleColor InfoColor { get; set; }

		/// <summary>
		/// Gets or sets the color of messages from <see cref="Log.Debug"/>.
		/// </summary>
		public static ConsoleColor DebugColor { get; set; }

		/// <summary>
		/// Gets or sets the string used as a tab.
		/// </summary>
		public static string Tab { get; set; }

		/// <summary>
		/// Writes a string to the log.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public static void Write(string value)
		{
			if (Log.Verbosity < VerbosityLevel.Minimal)
				return;

			Console.Write(value);
		}

		/// <summary>
		/// Writes a string, followed by the current line terminator, to the log.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public static void WriteLine(string value)
		{
			if (Log.Verbosity < VerbosityLevel.Minimal)
				return;

			Console.WriteLine(value);
		}

		/// <summary>
		/// Writes an error message to the log.
		/// </summary>
		/// <param name="value">The message to write.</param>
		public static void Error(string value)
		{
			if (Log.Verbosity < VerbosityLevel.Minimal)
				return;

			Console.ForegroundColor = Log.ErrorColor;
			Console.WriteLine(BuildLogString("[ERR] ", value, Console.WindowWidth));
			Console.ResetColor();
		}

		/// <summary>
		/// Writes a warning message to the log.
		/// </summary>
		/// <param name="value">The message to write.</param>
		public static void Warning(string value)
		{
			if (Log.Verbosity < VerbosityLevel.Normal)
				return;

			Console.ForegroundColor = Log.WarningColor;
			Console.WriteLine(BuildLogString("[WRN] ", value, Console.WindowWidth));
			Console.ResetColor();
		}

		/// <summary>
		/// Writes verbose information to the log.
		/// </summary>
		/// <param name="value">The message to write.</param>
		public static void Info(string value)
		{
			if (Log.Verbosity < VerbosityLevel.Verbose)
				return;

			Console.ForegroundColor = Log.InfoColor;
			Console.WriteLine(BuildLogString("[INF] ", value, Console.WindowWidth));
			Console.ResetColor();
		}

		/// <summary>
		/// Writes a debug message to the log.
		/// </summary>
		/// <param name="value">The message to write.</param>
		public static void Debug(string value)
		{
			Log.Debug(value, 0);
		}

		/// <summary>
		/// Writes a debug message to the log tabbed in to a specified depth.
		/// </summary>
		/// <param name="value">The message to write.</param>
		/// <param name="tabLevel">The number of tabs to indent by.</param>
		public static void Debug(string value, int tabLevel)
		{
			if (Log.Verbosity < VerbosityLevel.Debug)
				return;

			string tag = "[DBG] ";
			for (int i = 0; i < tabLevel; i++)
				tag += Log.Tab;

			Console.ForegroundColor = Log.DebugColor;
			Console.WriteLine(BuildLogString(tag, value, Console.WindowWidth));
			Console.ResetColor();
		}

		/// <summary>
		/// Converts a bare string into one formatted for a log.
		/// </summary>
		/// <example>
		/// In a console window with a width of 10, the following string:
		/// <code>"abcdefg"</code>
		/// with the following log tag:
		/// <code>"[ERR] "</code>
		/// becomes this string:
		/// <code>"[ERR] abcd\n[ERR] efg"</code>
		/// </example>
		/// <param name="tag">The tag that's added to the start of each line.</param>
		/// <param name="value">The string value to format.</param>
		/// <param name="outputWidth">The width of the console.</param>
		/// <returns>A properly formatted string.</returns>
		private static string BuildLogString(string tag, string value, int outputWidth)
		{
			StringBuilder builder = new StringBuilder();
			int width = outputWidth - tag.Length;

			// Convert DOS then Mac line endings to Unix.
			value = value.Replace("\r\n", "\n");
			value = value.Replace('\r', '\n');

			bool lineBroken = false;
			while (!string.IsNullOrEmpty(value))
			{
				builder.Append(tag);

				// Replace string's spacing with our own
				value = value.TrimStart('\n', '\t', ' ');

				// Tab in lines that were broken up by the formatting.
				if (lineBroken)
				{
					value = Log.Tab + value;
					lineBroken = false;
				}

				// Split based on location of next newline relative to line width
				int newlineIndex = value.IndexOf('\n');
				if (newlineIndex >= width)
				{
					builder.Append(value.Substring(0, width));
					value = value.Substring(width);

					//If the next character is the newline, it will be trimmed
					//but also tabbed in as though it were a continuation of
					//the current line.
					if (newlineIndex == width)
						continue;

					lineBroken = true;
				}
				else if (newlineIndex == -1)
				{
					if (value.Length >= width)
					{
						builder.Append(value.Substring(0, width));
						value = value.Substring(width);
						lineBroken = true;
					}
					else
					{
						builder.Append(value);
						break;
					}
				}
				else
				{
					builder.AppendLine(value.Substring(0, newlineIndex));
					value = value.Substring(newlineIndex + 1);
				}
			}

			return builder.ToString();
		}
	}
}
