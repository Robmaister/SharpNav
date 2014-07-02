#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNavEditor.IO
{
	[AttributeUsage(AttributeTargets.Class)]
	public class FileLoaderAttribute : Attribute
	{
		public string Extension { get; private set; }

		public FileLoaderAttribute(string extension)
		{
			Extension = extension;
		}
	}
}
