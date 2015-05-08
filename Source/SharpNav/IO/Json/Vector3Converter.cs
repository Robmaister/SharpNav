// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using Newtonsoft.Json;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.IO.Json
{
	public class Vector3Converter : JsonConverter
	{
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var vec = new Vector3();
			serializer.Populate(reader, vec);
			return vec;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var vec = (value as Vector3?).Value;
			writer.WriteStartObject();
			writer.WritePropertyName("X");
			serializer.Serialize(writer, vec.X);
			writer.WritePropertyName("Y");
			serializer.Serialize(writer, vec.Y);
			writer.WritePropertyName("Z");
			serializer.Serialize(writer, vec.Z);
			writer.WriteEndObject();
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Vector3);
		}
	}
}
