// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;

using SharpNav.Geometry;

namespace SharpNav
{
	//TODO should this be ISet<Contour>? Are the extra methods useful?
	
	/// <summary>
	/// A set of contours around the regions of a <see cref="CompactHeightfield"/>, used as the edges of a
	/// <see cref="PolyMesh"/>.
	/// </summary>
	public class ContourSet : ICollection<Contour>
	{
		private List<Contour> contours;
		private BBox3 bounds;
		private int width;
		private int height;

		/// <summary>
		/// Initializes a new instance of the <see cref="ContourSet"/> class.
		/// </summary>
		/// <param name="contours">A collection of <see cref="Contour"/>s.</param>
		/// <param name="bounds">The bounding box that contains all of the <see cref="Contour"/>s.</param>
		/// <param name="width">The width, in voxel units, of the world.</param>
		/// <param name="height">The height, in voxel units, of the world.</param>
		public ContourSet(IEnumerable<Contour> contours, BBox3 bounds, int width, int height)
		{
			this.contours = contours.ToList();
			this.bounds = bounds;
			this.width = width;
			this.height = height;
		}

		/// <summary>
		/// Gets the number of <see cref="Contour"/>s in the set.
		/// </summary>
		public int Count
		{
			get
			{
				return contours.Count;
			}
		}

		/// <summary>
		/// Gets the world-space bounding box of the set.
		/// </summary>
		public BBox3 Bounds
		{
			get
			{
				return bounds;
			}
		}

		/// <summary>
		/// Gets the width of the set, not including the border size specified in <see cref="CompactHeightfield"/>.
		/// </summary>
		public int Width
		{
			get
			{
				return width;
			}
		}

		/// <summary>
		/// Gets the height of the set, not including the border size specified in <see cref="CompactHeightfield"/>.
		/// </summary>
		public int Height
		{
			get
			{
				return height;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the <see cref="ContourSet"/> is read-only.
		/// </summary>
		bool ICollection<Contour>.IsReadOnly
		{
			get { return true; }
		}

		/// <summary>
		/// Checks if a specified <see cref="ContourSet"/> is contained in the <see cref="ContourSet"/>.
		/// </summary>
		/// <param name="item">A contour.</param>
		/// <returns>A value indicating whether the set contains the specified contour.</returns>
		public bool Contains(Contour item)
		{
			return contours.Contains(item);
		}

		/// <summary>
		/// Copies the <see cref="Contour"/>s in the set to an array.
		/// </summary>
		/// <param name="array">The array to copy to.</param>
		/// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
		public void CopyTo(Contour[] array, int arrayIndex)
		{
			contours.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Returns an enumerator that iterates through the entire <see cref="ContourSet"/>.
		/// </summary>
		/// <returns>An enumerator.</returns>
		public IEnumerator<Contour> GetEnumerator()
		{
			return contours.GetEnumerator();
		}

		//TODO support the extra ICollection methods later?

		/// <summary>
		/// Add a new contour to the set
		/// </summary>
		/// <param name="item">The contour to add</param>
		void ICollection<Contour>.Add(Contour item)
		{
			throw new InvalidOperationException();
		}
		
		/// <summary>
		/// (Not implemented) Clear the list
		/// </summary>
		void ICollection<Contour>.Clear()
		{
			throw new InvalidOperationException();
		}

		/// <summary>
		/// (Not implemented) Remove a contour from the set
		/// </summary>
		/// <param name="item">The contour to remove</param>
		/// <returns>throw InvalidOperatorException</returns>
		bool ICollection<Contour>.Remove(Contour item)
		{
			throw new InvalidOperationException();
		}

		/// <summary>
		/// Gets an enumerator that iterates through the set
		/// </summary>
		/// <returns>The enumerator</returns>
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
