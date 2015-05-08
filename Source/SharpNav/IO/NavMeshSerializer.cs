// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Reflection;
using SharpNav;

namespace SharpNav.IO
{
	//TODO make an interface if it doesn't need to be extended

    /// <summary>
    /// Abstract class for nav mesh serializers
    /// </summary>
	public abstract class NavMeshSerializer
	{
        /// <summary>
        /// Serialize navigation mesh into external file
        /// </summary>
        /// <param name="path">path of file to serialize into</param>
        /// <param name="mesh">mesh to serialize</param>
		public abstract void Serialize(string path, TiledNavMesh mesh);

        /// <summary>
        /// Deserialize navigation mesh from external file
        /// </summary>
        /// <param name="path">file to deserialize from</param>
        /// <returns>deserialized mesh</returns>
		public abstract TiledNavMesh Deserialize(string path);

        /// <summary>
        /// Get value of private field using reflection
        /// </summary>
        /// <param name="obj">object</param>
        /// <param name="name">name of field</param>
        /// <returns>value of field</returns>
	    protected object GetPrivateField(object obj, string name)
	    {
	        return GetPrivateField(obj, obj.GetType(), name);
	    }

        /// <summary>
        /// Get value of private field using reflection
        /// </summary>
        /// <param name="obj">object</param>
        /// <param name="type">type of object</param>
        /// <param name="name">name of field</param>
        /// <returns>value of field</returns>
        protected object GetPrivateField(object obj, Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            return field.GetValue(obj);
        }

        /// <summary>
        /// Set value of private field
        /// </summary>
        /// <param name="obj">object</param>
        /// <param name="name">name of field</param>
        /// <param name="value">value to set</param>
	    protected void SetPrivateField(object obj, string name, object value)
	    {
            SetPrivateField(obj, obj.GetType(), name, value);
	    }

        /// <summary>
        /// Set value of private field
        /// </summary>
        /// <param name="obj">object</param>
        /// <param name="type">type of object</param>
        /// <param name="name">name of field</param>
        /// <param name="value">value to set</param>
	    protected void SetPrivateField(object obj, Type type, string name, object value)
	    {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.SetField | BindingFlags.Instance);
            field.SetValue(obj, value);
	    }
	}
}
