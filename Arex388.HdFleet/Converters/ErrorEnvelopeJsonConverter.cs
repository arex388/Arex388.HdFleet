using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class ErrorEnvelopeJsonConverter :
	JsonConverter<ErrorEnvelope> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _code => "code"u8;
	private static ReadOnlySpan<byte> _details => "details"u8;
	private static ReadOnlySpan<byte> _error => "error"u8;
	private static ReadOnlySpan<byte> _message => "message"u8;
	private static ReadOnlySpan<byte> _requestId => "requestId"u8;

	public override ErrorEnvelope? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		string? code = null;
		string? details = null;
		string? message = null;
		string? requestId = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (!reader.ValueTextEquals(_error)) {
				reader.Skip();

				continue;
			}

			reader.Read();

			if (reader.TokenType != JsonTokenType.StartObject) {
				reader.Skip();

				continue;
			}

			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndObject) {
					break;
				}

				if (reader.TokenType != JsonTokenType.PropertyName) {
					continue;
				}

				if (reader.ValueTextEquals(_code)) {
					reader.Read();

					code = reader.TokenType == JsonTokenType.String
						? reader.GetString()
						: null;
				} else if (reader.ValueTextEquals(_details)) {
					reader.Read();

					details = reader.TokenType == JsonTokenType.String
						? reader.GetString()
						: null;
				} else if (reader.ValueTextEquals(_message)) {
					reader.Read();

					message = reader.TokenType == JsonTokenType.String
						? reader.GetString()
						: null;
				} else if (reader.ValueTextEquals(_requestId)) {
					reader.Read();

					requestId = reader.TokenType == JsonTokenType.String
						? reader.GetString()
						: null;
				} else {
					reader.Skip();
				}
			}
		}

		return new ErrorEnvelope {
			Code = code,
			Details = details,
			Message = message,
			RequestId = requestId
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		ErrorEnvelope value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
