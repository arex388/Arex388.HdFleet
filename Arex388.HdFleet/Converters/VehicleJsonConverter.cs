using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class VehicleJsonConverter :
	JsonConverter<Vehicle> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _deviceId => "deviceId"u8;
	private static ReadOnlySpan<byte> _deviceType => "deviceType"u8;
	private static ReadOnlySpan<byte> _displayName => "displayName"u8;
	private static ReadOnlySpan<byte> _externalVehicleId => "externalVehicleId"u8;
	private static ReadOnlySpan<byte> _headingDegrees => "headingDegrees"u8;
	private static ReadOnlySpan<byte> _imei => "imei"u8;
	private static ReadOnlySpan<byte> _lastLatitude => "lastLatitude"u8;
	private static ReadOnlySpan<byte> _lastLongitude => "lastLongitude"u8;
	private static ReadOnlySpan<byte> _lastTrackingActivity => "lastTrackingActivity"u8;
	private static ReadOnlySpan<byte> _licensePlate => "licensePlate"u8;
	private static ReadOnlySpan<byte> _speedMph => "speedMph"u8;
	private static ReadOnlySpan<byte> _status => "status"u8;
	private static ReadOnlySpan<byte> _updatedAt => "updatedAt"u8;
	private static ReadOnlySpan<byte> _vin => "vin"u8;

	public override Vehicle? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		string? deviceId = null;
		string? deviceType = null;
		string? displayName = null;
		decimal? headingInDegrees = null;
		ExternalVehicleId id = default;
		string? imei = null;
		DateTimeOffset? lastActivityAt = null;
		decimal? lastLatitude = null;
		decimal? lastLongitude = null;
		string? licensePlateNumber = null;
		decimal? speedInMilesPerHour = null;
		var status = VehicleStatus.None;
		DateTimeOffset? updatedAt = null;
		string? vin = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_externalVehicleId)) {
				reader.Read();

				id = new ExternalVehicleId(reader.GetString()!);
			} else if (reader.ValueTextEquals(_deviceId)) {
				reader.Read();

				deviceId = reader.GetString();
			} else if (reader.ValueTextEquals(_deviceType)) {
				reader.Read();

				deviceType = reader.GetString();
			} else if (reader.ValueTextEquals(_displayName)) {
				reader.Read();

				displayName = reader.GetString();
			} else if (reader.ValueTextEquals(_headingDegrees)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					headingInDegrees = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_imei)) {
				reader.Read();

				imei = reader.GetString();
			} else if (reader.ValueTextEquals(_lastLatitude)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					lastLatitude = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_lastLongitude)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					lastLongitude = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_lastTrackingActivity)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					lastActivityAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_licensePlate)) {
				reader.Read();

				licensePlateNumber = reader.GetString();
			} else if (reader.ValueTextEquals(_speedMph)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					speedInMilesPerHour = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_status)) {
				reader.Read();

				//	HD Fleet documents no value set for status — every wire value
				//	falls back to VehicleStatus.None until live values seed the
				//	enum's members (matched here by ValueTextEquals, never thrown).
				if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) {
					reader.Skip();
				}
			} else if (reader.ValueTextEquals(_updatedAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					updatedAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_vin)) {
				reader.Read();

				vin = reader.GetString();
			} else {
				reader.Skip();
			}
		}

		return new Vehicle {
			DeviceId = deviceId,
			DeviceType = deviceType,
			DisplayName = displayName,
			HeadingInDegrees = headingInDegrees,
			Id = id,
			Imei = imei,
			LastActivityAt = lastActivityAt,
			LastLatitude = lastLatitude,
			LastLongitude = lastLongitude,
			LicensePlateNumber = licensePlateNumber,
			SpeedInMilesPerHour = speedInMilesPerHour,
			Status = status,
			UpdatedAt = updatedAt,
			Vin = vin
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		Vehicle value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
