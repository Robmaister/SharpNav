// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using SharpNav.Geometry;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Vector3);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			//with help from http://stackoverflow.com/a/21632292/1122135
			if (reader.TokenType == JsonToken.Null)
				return null;

			JObject jObject = JObject.Load(reader);
			return new Vector3(jObject["X"].ToObject<float>(serializer), jObject["Y"].ToObject<float>(serializer), jObject["Z"].ToObject<float>(serializer));
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
	}
}
