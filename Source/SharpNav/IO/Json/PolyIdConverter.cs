// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using SharpNav.Pathfinding;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SharpNav.IO.Json
{
	class PolyIdConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(PolyId);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return null;

			return new PolyId(serializer.Deserialize<int>(reader));
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var polyId = (value as PolyId?).Value;
			serializer.Serialize(writer, (int)polyId.Id);
		}
	}
}
