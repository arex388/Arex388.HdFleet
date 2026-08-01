using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class BackfillSliceJsonConverter :
	JsonConverter<BackfillSlice> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _endpoint => "endpoint"u8;
	private static ReadOnlySpan<byte> _from => "from"u8;
	private static ReadOnlySpan<byte> _sequence => "sequence"u8;
	private static ReadOnlySpan<byte> _status => "status"u8;
	private static ReadOnlySpan<byte> _to => "to"u8;

	public override BackfillSlice? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		var endpoint = string.Empty;
		DateTimeOffset from = default;
		var sequence = 0;
		string? status = null;
		DateTimeOffset to = default;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_endpoint)) {
				reader.Read();

				endpoint = reader.GetString() ?? string.Empty;
			} else if (reader.ValueTextEquals(_from)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					from = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_sequence)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					sequence = reader.TokenType == JsonTokenType.String
						? int.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture)
						: reader.GetInt32();
				}
			} else if (reader.ValueTextEquals(_status)) {
				reader.Read();

				status = reader.GetString();
			} else if (reader.ValueTextEquals(_to)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					to = reader.GetDateTimeOffset();
				}
			} else {
				reader.Skip();
			}
		}

		return new BackfillSlice {
			Endpoint = endpoint,
			From = from,
			Sequence = sequence,
			Status = status,
			To = to
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		BackfillSlice value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
