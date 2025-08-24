// -------------------------------------------------------------------------------------------------
// <copyright file="TimeSpanSecondsConverter.cs" company="Martin">
//   Copyright (c) 2025 Martin. All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Helldivers2Accessibility;

public class TimeSpanSecondsConverter : JsonConverter<TimeSpan>
{
	public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var seconds = reader.GetInt32();
		return TimeSpan.FromSeconds(seconds: seconds);
	}

	public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
		throw new InvalidOperationException();
}