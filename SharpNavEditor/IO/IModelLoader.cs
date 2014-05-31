#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNavEditor.IO
{
	public interface IModelLoader
	{
		bool SupportsPositions { get; }
		bool SupportsTextureCoordinates { get; }
		bool SupportsNormals { get; }
		bool SupportsTangents { get; }
		bool SupportsBitangents { get; }
		bool SupportsColors { get; }
		bool SupportsAnimation { get; }
		bool SupportsSkeleton { get; }
		bool SupportsIndexing { get; }

		int CustomVertexDataTypesCount { get; }

		IModelData LoadModel(string path);
	}
}
