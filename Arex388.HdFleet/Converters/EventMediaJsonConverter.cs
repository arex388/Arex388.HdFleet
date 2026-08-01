using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class EventMediaJsonConverter :
	JsonConverter<EventMedia> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _forwardVideoUrl => "forwardVideoUrl"u8;
	private static ReadOnlySpan<byte> _inwardVideoUrl => "inwardVideoUrl"u8;
	private static ReadOnlySpan<byte> _items => "items"u8;

	public override EventMedia? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		string? forwardVideoUrl = null;
		string? inwardVideoUrl = null;
		List<EventMediaItem>? items = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_forwardVideoUrl)) {
				reader.Read();

				forwardVideoUrl = reader.TokenType == JsonTokenType.String
					? reader.GetString()
					: null;
			} else if (reader.ValueTextEquals(_inwardVideoUrl)) {
				reader.Read();

				inwardVideoUrl = reader.TokenType == JsonTokenType.String
					? reader.GetString()
					: null;
			} else if (reader.ValueTextEquals(_items)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.StartArray) {
					reader.Skip();

					continue;
				}

				var converter = (JsonConverter<EventMediaItem>)options.GetConverter(typeof(EventMediaItem));

				items = [];

				while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
					var item = converter.Read(ref reader, typeof(EventMediaItem), options);

					if (item is not null) {
						items.Add(item);
					}
				}
			} else {
				reader.Skip();
			}
		}

		return new EventMedia {
			ForwardVideoUrl = forwardVideoUrl,
			InwardVideoUrl = inwardVideoUrl,
			Items = items ?? []
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		EventMedia value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
