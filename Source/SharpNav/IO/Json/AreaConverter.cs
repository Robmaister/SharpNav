// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using SharpNav;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SharpNav.IO.Json
{
	public class AreaConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Area);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return null;

			return new Area(serializer.Deserialize<byte>(reader));
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var area = (value as Area?).Value;
			serializer.Serialize(writer, (int)area.Id);
		}
	}
}
