using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class IntegrationJsonConverter :
	JsonConverter<Integration> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _displayName => "displayName"u8;
	private static ReadOnlySpan<byte> _id => "id"u8;
	private static ReadOnlySpan<byte> _lastErrorAt => "lastErrorAt"u8;
	private static ReadOnlySpan<byte> _lastSuccessAt => "lastSuccessAt"u8;
	private static ReadOnlySpan<byte> _mode => "mode"u8;
	private static ReadOnlySpan<byte> _partner => "partner"u8;
	private static ReadOnlySpan<byte> _status => "status"u8;
	private static ReadOnlySpan<byte> _updatedAt => "updatedAt"u8;

	public override Integration? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		string? displayName = null;
		var id = string.Empty;
		DateTimeOffset? lastErrorAt = null;
		DateTimeOffset? lastSuccessAt = null;
		var mode = string.Empty;
		var partner = string.Empty;
		var status = string.Empty;
		DateTimeOffset? updatedAt = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_id)) {
				reader.Read();

				id = reader.GetString() ?? string.Empty;
			} else if (reader.ValueTextEquals(_displayName)) {
				reader.Read();

				displayName = reader.GetString();
			} else if (reader.ValueTextEquals(_lastErrorAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					lastErrorAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_lastSuccessAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					lastSuccessAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_mode)) {
				reader.Read();

				mode = reader.GetString() ?? string.Empty;
			} else if (reader.ValueTextEquals(_partner)) {
				reader.Read();

				partner = reader.GetString() ?? string.Empty;
			} else if (reader.ValueTextEquals(_status)) {
				reader.Read();

				status = reader.GetString() ?? string.Empty;
			} else if (reader.ValueTextEquals(_updatedAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					updatedAt = reader.GetDateTimeOffset();
				}
			} else {
				reader.Skip();
			}
		}

		return new Integration {
			DisplayName = displayName,
			Id = id,
			LastErrorAt = lastErrorAt,
			LastSuccessAt = lastSuccessAt,
			Mode = mode,
			Partner = partner,
			Status = status,
			UpdatedAt = updatedAt
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		Integration value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
