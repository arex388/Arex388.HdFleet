using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class DriverJsonConverter :
	JsonConverter<Driver> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _displayName => "displayName"u8;
	private static ReadOnlySpan<byte> _driverEmail => "driverEmail"u8;
	private static ReadOnlySpan<byte> _driverPhone => "driverPhone"u8;
	private static ReadOnlySpan<byte> _externalDriverId => "externalDriverId"u8;
	private static ReadOnlySpan<byte> _licenseNumber => "licenseNumber"u8;
	private static ReadOnlySpan<byte> _licenseState => "licenseState"u8;
	private static ReadOnlySpan<byte> _updatedAt => "updatedAt"u8;

	public override Driver? Read(
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
		string? email = null;
		ExternalDriverId id = default;
		string? licenseNumber = null;
		string? licenseState = null;
		string? phone = null;
		DateTimeOffset? updatedAt = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_externalDriverId)) {
				reader.Read();

				id = new ExternalDriverId(reader.GetString()!);
			} else if (reader.ValueTextEquals(_displayName)) {
				reader.Read();

				displayName = reader.GetString();
			} else if (reader.ValueTextEquals(_driverEmail)) {
				reader.Read();

				email = reader.GetString();
			} else if (reader.ValueTextEquals(_driverPhone)) {
				reader.Read();

				phone = reader.GetString();
			} else if (reader.ValueTextEquals(_licenseNumber)) {
				reader.Read();

				licenseNumber = reader.GetString();
			} else if (reader.ValueTextEquals(_licenseState)) {
				reader.Read();

				licenseState = reader.GetString();
			} else if (reader.ValueTextEquals(_updatedAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					updatedAt = reader.GetDateTimeOffset();
				}
			} else {
				reader.Skip();
			}
		}

		return new Driver {
			DisplayName = displayName,
			Email = email,
			Id = id,
			LicenseNumber = licenseNumber,
			LicenseState = licenseState,
			Phone = phone,
			UpdatedAt = updatedAt
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		Driver value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
