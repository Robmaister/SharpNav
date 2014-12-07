#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// A link is formed between two polygons in a TiledNavMesh
	/// </summary>
	public class Link
	{
		/// <summary>
		/// Gets or sets the neighbor reference (the one it's linked to)
		/// </summary>
		public int Reference { get; set; }

		/// <summary>
		/// Gets or sets the index of next link
		/// </summary>
		public int Next { get; set; }

		/// <summary>
		/// Gets or sets the index of polygon edge
		/// </summary>
		public int Edge { get; set; }

		/// <summary>
		/// Gets or sets the polygon side
		/// </summary>
		public int Side { get; set; }

		/// <summary>
		/// Gets or sets the minimum Vector3 of the bounding box
		/// </summary>
		public int BMin { get; set; }

		/// <summary>
		/// Gets or sets the maximum Vector3 of the bounding box
		/// </summary>
		public int BMax { get; set; }

		/// <summary>
		/// Serialized JSON object
		/// </summary>
		/*public JObject JSONObject
		{
			get
			{
				return new JObject(
					new JProperty("Reference", Reference),
					new JProperty("Next", Next),
					new JProperty("Edge", Edge),
					new JProperty("Side", Side),
					new JProperty("BMin", BMin),
					new JProperty("BMax", BMax)
				);
			}
		}*/
	}
}
