#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNavEditor.IO
{
	public interface IModelData
	{
		bool HasPositions { get; }
		bool HasTextureCoordinates { get; }
		bool HasNormals { get; }
		bool HasTangents { get; }
		bool HasBitangents { get; }
		bool HasColors { get; }
		bool HasAnimation { get; }
		bool HasSkeleton { get; }
		bool HasIndices { get; }
		int CustomVertexDataTypesCount { get; }

		int PositionVertexSize { get; }
		int TextureCoordinateVertexSize { get; }
		int NormalVertexSize { get; }
		int TangentVertexSize { get; }
		int BitangentVertexSize { get; }
		int ColorVertexSize { get; }

		float[] Positions { get; }
		float[] TextureCoordinates { get; }
		float[] Normals { get; }
		float[] Tangents { get; }
		float[] Bitangents { get; }
		float[] Colors { get; }

		//TODO animation/skeleton handlers

		int[] Indices { get; }
	}
}
