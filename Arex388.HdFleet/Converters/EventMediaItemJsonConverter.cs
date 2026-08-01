using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class EventMediaItemJsonConverter :
	JsonConverter<EventMediaItem> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _cameraNumber => "cameraNumber"u8;
	private static ReadOnlySpan<byte> _endAt => "endAt"u8;
	private static ReadOnlySpan<byte> _mediaStatus => "mediaStatus"u8;
	private static ReadOnlySpan<byte> _mediaType => "mediaType"u8;
	private static ReadOnlySpan<byte> _startAt => "startAt"u8;
	private static ReadOnlySpan<byte> _url => "url"u8;

	public override EventMediaItem? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		int? cameraNumber = null;
		DateTimeOffset? endAt = null;
		string? mediaStatus = null;
		string? mediaType = null;
		DateTimeOffset? startAt = null;
		string? url = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_cameraNumber)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					cameraNumber = reader.TokenType == JsonTokenType.String
						? int.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture)
						: reader.GetInt32();
				}
			} else if (reader.ValueTextEquals(_endAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					endAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_mediaStatus)) {
				reader.Read();

				mediaStatus = reader.GetString();
			} else if (reader.ValueTextEquals(_mediaType)) {
				reader.Read();

				mediaType = reader.GetString();
			} else if (reader.ValueTextEquals(_startAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					startAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_url)) {
				reader.Read();

				url = reader.GetString();
			} else {
				reader.Skip();
			}
		}

		return new EventMediaItem {
			CameraNumber = cameraNumber,
			EndAt = endAt,
			MediaStatus = mediaStatus,
			MediaType = mediaType,
			StartAt = startAt,
			Url = url
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		EventMediaItem value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
