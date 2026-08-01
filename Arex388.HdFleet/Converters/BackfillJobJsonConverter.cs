using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class BackfillJobJsonConverter :
	JsonConverter<BackfillJob> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _createdAt => "createdAt"u8;
	private static ReadOnlySpan<byte> _expiresAt => "expiresAt"u8;
	private static ReadOnlySpan<byte> _externalVehicleId => "externalVehicleId"u8;
	private static ReadOnlySpan<byte> _from => "from"u8;
	private static ReadOnlySpan<byte> _id => "id"u8;
	private static ReadOnlySpan<byte> _reason => "reason"u8;
	private static ReadOnlySpan<byte> _reused => "reused"u8;
	private static ReadOnlySpan<byte> _sliceCount => "sliceCount"u8;
	private static ReadOnlySpan<byte> _sliceMinutes => "sliceMinutes"u8;
	private static ReadOnlySpan<byte> _slicesUrl => "slicesUrl"u8;
	private static ReadOnlySpan<byte> _status => "status"u8;
	private static ReadOnlySpan<byte> _stream => "stream"u8;
	private static ReadOnlySpan<byte> _to => "to"u8;

	public override BackfillJob? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		DateTimeOffset createdAt = default;
		DateTimeOffset expiresAt = default;
		DateTimeOffset from = default;
		BackfillJobId id = default;
		string? reason = null;
		var reused = false;
		var sliceCount = 0;
		var sliceMinutes = 0;
		string? slicesUrl = null;
		string? status = null;
		string? stream = null;
		DateTimeOffset to = default;
		ExternalVehicleId? vehicleId = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_id)) {
				reader.Read();

				id = new BackfillJobId(reader.GetGuid());
			} else if (reader.ValueTextEquals(_createdAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					createdAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_expiresAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					expiresAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_externalVehicleId)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					vehicleId = new ExternalVehicleId(reader.GetString()!);
				}
			} else if (reader.ValueTextEquals(_from)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					from = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_reason)) {
				reader.Read();

				reason = reader.GetString();
			} else if (reader.ValueTextEquals(_reused)) {
				reader.Read();

				reused = reader.TokenType == JsonTokenType.True;
			} else if (reader.ValueTextEquals(_sliceCount)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					sliceCount = reader.TokenType == JsonTokenType.String
						? int.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture)
						: reader.GetInt32();
				}
			} else if (reader.ValueTextEquals(_sliceMinutes)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					sliceMinutes = reader.TokenType == JsonTokenType.String
						? int.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture)
						: reader.GetInt32();
				}
			} else if (reader.ValueTextEquals(_slicesUrl)) {
				reader.Read();

				slicesUrl = reader.GetString();
			} else if (reader.ValueTextEquals(_status)) {
				reader.Read();

				status = reader.GetString();
			} else if (reader.ValueTextEquals(_stream)) {
				reader.Read();

				stream = reader.GetString();
			} else if (reader.ValueTextEquals(_to)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					to = reader.GetDateTimeOffset();
				}
			} else {
				reader.Skip();
			}
		}

		return new BackfillJob {
			CreatedAt = createdAt,
			ExpiresAt = expiresAt,
			From = from,
			Id = id,
			Reason = reason,
			Reused = reused,
			SliceCount = sliceCount,
			SliceMinutes = sliceMinutes,
			SlicesUrl = slicesUrl,
			Status = status,
			Stream = stream,
			To = to,
			VehicleId = vehicleId
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		BackfillJob value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
