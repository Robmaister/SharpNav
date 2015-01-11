// Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

namespace SharpNav.Collections.Generic
{
	/// <summary>
	/// An interface that defines a class containing a cost associated with the instance.
	/// Used in <see cref="PriorityQueue{T}"/>
	/// </summary>
	public interface IValueWithCost
	{
		/// <summary>
		/// Gets the cost of this instance.
		/// </summary>
		float Cost { get; }
	}
}
