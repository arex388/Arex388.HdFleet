using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class LocationJsonConverter :
	JsonConverter<Location> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _altitude => "altitude"u8;
	private static ReadOnlySpan<byte> _deviceId => "deviceId"u8;
	private static ReadOnlySpan<byte> _driverId => "driverId"u8;
	private static ReadOnlySpan<byte> _driverName => "driverName"u8;
	private static ReadOnlySpan<byte> _externalVehicleId => "externalVehicleId"u8;
	private static ReadOnlySpan<byte> _headingDegrees => "headingDegrees"u8;
	private static ReadOnlySpan<byte> _imei => "imei"u8;
	private static ReadOnlySpan<byte> _latitude => "latitude"u8;
	private static ReadOnlySpan<byte> _longitude => "longitude"u8;
	private static ReadOnlySpan<byte> _odometer => "odometer"u8;
	private static ReadOnlySpan<byte> _odometerUnit => "odometerUnit"u8;
	private static ReadOnlySpan<byte> _recordedAt => "recordedAt"u8;
	private static ReadOnlySpan<byte> _speedMph => "speedMph"u8;
	private static ReadOnlySpan<byte> _status => "status"u8;
	private static ReadOnlySpan<byte> _vin => "vin"u8;

	public override Location? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		decimal? altitude = null;
		string? deviceId = null;
		ExternalDriverId? driverId = null;
		string? driverName = null;
		decimal? headingInDegrees = null;
		string? imei = null;
		decimal latitude = 0;
		decimal longitude = 0;
		decimal? odometer = null;
		string? odometerUnit = null;
		DateTimeOffset recordedAt = default;
		decimal? speedInMilesPerHour = null;
		string? status = null;
		ExternalVehicleId vehicleId = default;
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

				vehicleId = new ExternalVehicleId(reader.GetString()!);
			} else if (reader.ValueTextEquals(_altitude)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					altitude = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_deviceId)) {
				reader.Read();

				deviceId = reader.GetString();
			} else if (reader.ValueTextEquals(_driverId)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					driverId = new ExternalDriverId(reader.GetString()!);
				}
			} else if (reader.ValueTextEquals(_driverName)) {
				reader.Read();

				driverName = reader.GetString();
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
			} else if (reader.ValueTextEquals(_latitude)) {
				reader.Read();

				latitude = reader.TokenType == JsonTokenType.String
					? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
					: reader.GetDecimal();
			} else if (reader.ValueTextEquals(_longitude)) {
				reader.Read();

				longitude = reader.TokenType == JsonTokenType.String
					? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
					: reader.GetDecimal();
			} else if (reader.ValueTextEquals(_odometer)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					odometer = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_odometerUnit)) {
				reader.Read();

				odometerUnit = reader.GetString();
			} else if (reader.ValueTextEquals(_recordedAt)) {
				reader.Read();

				recordedAt = reader.GetDateTimeOffset();
			} else if (reader.ValueTextEquals(_speedMph)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					speedInMilesPerHour = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_status)) {
				reader.Read();

				status = reader.TokenType == JsonTokenType.String
					? reader.GetString()
					: null;
			} else if (reader.ValueTextEquals(_vin)) {
				reader.Read();

				vin = reader.GetString();
			} else {
				reader.Skip();
			}
		}

		return new Location {
			Altitude = altitude,
			DeviceId = deviceId,
			DriverId = driverId,
			DriverName = driverName,
			HeadingInDegrees = headingInDegrees,
			Imei = imei,
			Latitude = latitude,
			Longitude = longitude,
			Odometer = odometer,
			OdometerUnit = odometerUnit,
			RecordedAt = recordedAt,
			SpeedInMilesPerHour = speedInMilesPerHour,
			Status = status,
			VehicleId = vehicleId,
			Vin = vin
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		Location value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
