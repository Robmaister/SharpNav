#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNavEditor
{
	class EntryPoint
	{
		static void Main(string[] args)
		{
			using (var window = new EditorWindow())
				window.Run();
		}
	}
}
