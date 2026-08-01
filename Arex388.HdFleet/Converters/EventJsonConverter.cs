using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class EventJsonConverter :
	JsonConverter<Event> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _altitude => "altitude"u8;
	private static ReadOnlySpan<byte> _associatedDriverId => "associatedDriverId"u8;
	private static ReadOnlySpan<byte> _associatedDriverName => "associatedDriverName"u8;
	private static ReadOnlySpan<byte> _deviceId => "deviceId"u8;
	private static ReadOnlySpan<byte> _eventType => "eventType"u8;
	private static ReadOnlySpan<byte> _externalEventId => "externalEventId"u8;
	private static ReadOnlySpan<byte> _externalVehicleId => "externalVehicleId"u8;
	private static ReadOnlySpan<byte> _headingDegrees => "headingDegrees"u8;
	private static ReadOnlySpan<byte> _imei => "imei"u8;
	private static ReadOnlySpan<byte> _latitude => "latitude"u8;
	private static ReadOnlySpan<byte> _longitude => "longitude"u8;
	private static ReadOnlySpan<byte> _media => "media"u8;
	private static ReadOnlySpan<byte> _occurredAt => "occurredAt"u8;
	private static ReadOnlySpan<byte> _speedMph => "speedMph"u8;
	private static ReadOnlySpan<byte> _vin => "vin"u8;

	public override Event? Read(
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
		ExternalEventId id = default;
		string? imei = null;
		decimal? latitude = null;
		decimal? longitude = null;
		EventMedia? media = null;
		DateTimeOffset occurredAt = default;
		decimal? speedInMilesPerHour = null;
		var type = string.Empty;
		ExternalVehicleId? vehicleId = null;
		string? vin = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_externalEventId)) {
				reader.Read();

				id = new ExternalEventId(reader.GetString()!);
			} else if (reader.ValueTextEquals(_altitude)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					altitude = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_associatedDriverId)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					driverId = new ExternalDriverId(reader.GetString()!);
				}
			} else if (reader.ValueTextEquals(_associatedDriverName)) {
				reader.Read();

				driverName = reader.GetString();
			} else if (reader.ValueTextEquals(_deviceId)) {
				reader.Read();

				deviceId = reader.GetString();
			} else if (reader.ValueTextEquals(_eventType)) {
				reader.Read();

				type = reader.GetString() ?? string.Empty;
			} else if (reader.ValueTextEquals(_externalVehicleId)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					vehicleId = new ExternalVehicleId(reader.GetString()!);
				}
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

				if (reader.TokenType != JsonTokenType.Null) {
					latitude = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_longitude)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					longitude = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_media)) {
				reader.Read();

				if (reader.TokenType == JsonTokenType.StartObject) {
					media = ((JsonConverter<EventMedia>)options.GetConverter(typeof(EventMedia))).Read(ref reader, typeof(EventMedia), options);
				} else if (reader.TokenType is JsonTokenType.StartArray) {
					reader.Skip();
				}
			} else if (reader.ValueTextEquals(_occurredAt)) {
				reader.Read();

				occurredAt = reader.GetDateTimeOffset();
			} else if (reader.ValueTextEquals(_speedMph)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					speedInMilesPerHour = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_vin)) {
				reader.Read();

				vin = reader.GetString();
			} else {
				reader.Skip();
			}
		}

		return new Event {
			Altitude = altitude,
			DeviceId = deviceId,
			DriverId = driverId,
			DriverName = driverName,
			HeadingInDegrees = headingInDegrees,
			Id = id,
			Imei = imei,
			Latitude = latitude,
			Longitude = longitude,
			Media = media,
			OccurredAt = occurredAt,
			SpeedInMilesPerHour = speedInMilesPerHour,
			Type = type,
			VehicleId = vehicleId,
			Vin = vin
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		Event value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
