using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class PageJsonConverterFactory :
	JsonConverterFactory {
	public override bool CanConvert(
		Type typeToConvert) => typeToConvert.IsGenericType
		&& typeToConvert.GetGenericTypeDefinition() == typeof(Page<>);

	public override JsonConverter CreateConverter(
		Type typeToConvert,
		JsonSerializerOptions options) => (JsonConverter)Activator.CreateInstance(
		typeof(PageJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

	//	============================================================================
	//	Page converter
	//	============================================================================

	private sealed class PageJsonConverter<T> :
		JsonConverter<Page<T>> {
		//	Property names (camelCase as returned by the API)
		private static ReadOnlySpan<byte> _data => "data"u8;
		private static ReadOnlySpan<byte> _nextCursor => "nextCursor"u8;

		public override Page<T>? Read(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options) {
			if (reader.TokenType == JsonTokenType.Null) {
				return null;
			}

			if (reader.TokenType != JsonTokenType.StartObject) {
				throw new JsonException("Expected StartObject token");
			}

			var converter = (JsonConverter<T>)options.GetConverter(typeof(T));

			List<T>? data = null;

			//	A null or absent nextCursor both mean exhaustion — the local
			//	stays null unless a non-null cursor value is actually read.
			string? nextCursor = null;

			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndObject) {
					break;
				}

				if (reader.TokenType != JsonTokenType.PropertyName) {
					continue;
				}

				if (reader.ValueTextEquals(_data)) {
					reader.Read();

					if (reader.TokenType != JsonTokenType.StartArray) {
						reader.Skip();

						continue;
					}

					data = [];

					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
						var item = converter.Read(ref reader, typeof(T), options);

						if (item is not null) {
							data.Add(item);
						}
					}
				} else if (reader.ValueTextEquals(_nextCursor)) {
					reader.Read();

					nextCursor = reader.TokenType == JsonTokenType.String
						? reader.GetString()
						: null;
				} else {
					reader.Skip();
				}
			}

			return new Page<T> {
				Data = data ?? [],
				NextCursor = nextCursor
			};
		}

		public override void Write(
			Utf8JsonWriter writer,
			Page<T> value,
			JsonSerializerOptions options) => throw new NotImplementedException();
	}
}
