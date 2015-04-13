// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Gwen.Control;

namespace SharpNav.Examples
{
	/// <summary>
	/// A helper class for writing values to a <see cref="ListBox"/>.
	/// </summary>
	/// <remarks>
	/// This exists because <see cref="ListBox"/> only lets you write strings.
	/// </remarks>
	public class GwenTextWriter : TextWriter
	{
		private ListBox outputBox;

		/// <summary>
		/// Initializes a new instance of the <see cref="GwenTextWriter"/> class.
		/// </summary>
		/// <param name="outputBox"></param>
		public GwenTextWriter(ListBox outputBox)
		{
			this.outputBox = outputBox;
		}

		/// <summary>
		/// Gets the encoding.
		/// </summary>
		public override Encoding Encoding
		{
			get { return Encoding.Default; }
		}

		/// <summary>
		/// Writes a boolean value.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void Write(bool value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes a char value.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void Write(char value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes a char buffer.
		/// </summary>
		/// <param name="buffer">The array to write.</param>
		public override void Write(char[] buffer)
		{
			Write(new string(buffer));
		}

		/// <summary>
		/// Writes a substring of a char buffer.
		/// </summary>
		/// <param name="buffer">The array to write.</param>
		/// <param name="index">The starting index.</param>
		/// <param name="count">The number of chars to write.</param>
		public override void Write(char[] buffer, int index, int count)
		{
			Write(new string(buffer, index, count));
		}

		/// <summary>
		/// Writes a decimal value.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void Write(decimal value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes a double value.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void Write(double value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes a float value.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void Write(float value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes an int value.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void Write(int value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes a long value.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void Write(long value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes an object.
		/// </summary>
		/// <param name="value">The object to write.</param>
		public override void Write(object value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes a formatted string.
		/// </summary>
		/// <param name="format">The string to write.</param>
		/// <param name="arg0">The object to format.</param>
		public override void Write(string format, object arg0)
		{
			Write(string.Format(format, arg0));
		}

		/// <summary>
		/// Writes a formatted string.
		/// </summary>
		/// <param name="format">The string to write.</param>
		/// <param name="arg0">The object to format.</param>
		/// <param name="arg1">The second object to format.</param>
		public override void Write(string format, object arg0, object arg1)
		{
			Write(string.Format(format, arg0, arg1));
		}

		/// <summary>
		/// Writes a formatted string.
		/// </summary>
		/// <param name="format">The string to write.</param>
		/// <param name="arg0">The object to format.</param>
		/// <param name="arg1">The second object to format.</param>
		/// <param name="arg2">The third object to format.</param>
		public override void Write(string format, object arg0, object arg1, object arg2)
		{
			Write(string.Format(format, arg0, arg1, arg2));
		}

		/// <summary>
		/// Writes a formatted string.
		/// </summary>
		/// <param name="format">The string to write.</param>
		/// <param name="arg">The objects to format.</param>
		public override void Write(string format, params object[] arg)
		{
			Write(string.Format(format, arg));
		}

		/// <summary>
		/// Writes a string.
		/// </summary>
		/// <param name="value">The string to write.</param>
		public override void Write(string value)
		{
			if (outputBox.RowCount == 0)
				outputBox.AddRow(value);
			else
				outputBox[outputBox.RowCount - 1].Text += value;

			outputBox.ScrollToBottom();
		}

		/// <summary>
		/// Writes a uint value.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void Write(uint value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes a ulong value.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void Write(ulong value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// Writes a newline character.
		/// </summary>
		public override void WriteLine()
		{
			WriteLine("");
		}

		/// <summary>
		/// Writes a boolean value followed by a newline.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void WriteLine(bool value)
		{
			WriteLine(value.ToString());
		}

		/// <summary>
		/// Writes a char value followed by a newline.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void WriteLine(char value)
		{
			WriteLine(value.ToString());
		}

		/// <summary>
		/// Writes a char buffer.
		/// </summary>
		/// <param name="buffer">The array to write.</param>
		public override void WriteLine(char[] buffer)
		{
			WriteLine(new string(buffer));
		}

		/// <summary>
		/// Writes a substring of a char buffer.
		/// </summary>
		/// <param name="buffer">The array to write.</param>
		/// <param name="index">The starting index.</param>
		/// <param name="count">The number of chars to write.</param>
		public override void WriteLine(char[] buffer, int index, int count)
		{
			WriteLine(new string(buffer, index, count));
		}

		/// <summary>
		/// Writes a decimal value followed by a newline.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void WriteLine(decimal value)
		{
			WriteLine(value.ToString());
		}

		/// <summary>
		/// Writes a double value followed by a newline.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void WriteLine(double value)
		{
			WriteLine(value.ToString());
		}

		/// <summary>
		/// Writes a float value followed by a newline.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void WriteLine(float value)
		{
			WriteLine(value.ToString());
		}

		/// <summary>
		/// Writes an int value followed by a newline.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void WriteLine(int value)
		{
			WriteLine(value.ToString());
		}

		/// <summary>
		/// Writes a long value followed by a newline.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void WriteLine(long value)
		{
			WriteLine(value.ToString());
		}

		/// <summary>
		/// Writes an object followed by a newline.
		/// </summary>
		/// <param name="value">The object to write.</param>
		public override void WriteLine(object value)
		{
			WriteLine(value.ToString());
		}

		/// <summary>
		/// Writes a formatted string.
		/// </summary>
		/// <param name="format">The string to write.</param>
		/// <param name="arg0">The object to format.</param>
		public override void WriteLine(string format, object arg0)
		{
			WriteLine(string.Format(format, arg0));
		}

		/// <summary>
		/// Writes a formatted string.
		/// </summary>
		/// <param name="format">The string to write.</param>
		/// <param name="arg0">The object to format.</param>
		/// <param name="arg1">The second object to format.</param>
		public override void WriteLine(string format, object arg0, object arg1)
		{
			WriteLine(string.Format(format, arg0, arg1));
		}

		/// <summary>
		/// Writes a formatted string.
		/// </summary>
		/// <param name="format">The string to write.</param>
		/// <param name="arg0">The object to format.</param>
		/// <param name="arg1">The second object to format.</param>
		/// <param name="arg2">The third object to format.</param>
		public override void WriteLine(string format, object arg0, object arg1, object arg2)
		{
			WriteLine(string.Format(format, arg0, arg1, arg2));
		}

		/// <summary>
		/// Writes a formatted string.
		/// </summary>
		/// <param name="format">The string to write.</param>
		/// <param name="arg">The objects to format.</param>
		public override void WriteLine(string format, params object[] arg)
		{
			WriteLine(string.Format(format, arg));
		}

		/// <summary>
		/// Writes a string followed by a newline.
		/// </summary>
		/// <param name="value">The string to write.</param>
		public override void WriteLine(string value)
		{
			outputBox.AddRow(value);
			outputBox.ScrollToBottom();
		}

		/// <summary>
		/// Writes a uint value followed by a newline.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void WriteLine(uint value)
		{
			WriteLine(value.ToString());
		}

		/// <summary>
		/// Writes a ulong value followed by a newline.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public override void WriteLine(ulong value)
		{
			WriteLine(value.ToString());
		}
	}
}
