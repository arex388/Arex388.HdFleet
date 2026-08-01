using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class ProMilesVehicleJsonConverter :
	JsonConverter<ProMilesVehicle> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _displayName => "displayName"u8;
	private static ReadOnlySpan<byte> _externalVehicleId => "externalVehicleId"u8;
	private static ReadOnlySpan<byte> _vin => "vin"u8;

	public override ProMilesVehicle? Read(
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
		ExternalVehicleId id = default;
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
			} else if (reader.ValueTextEquals(_displayName)) {
				reader.Read();

				displayName = reader.GetString();
			} else if (reader.ValueTextEquals(_vin)) {
				reader.Read();

				vin = reader.GetString();
			} else {
				reader.Skip();
			}
		}

		return new ProMilesVehicle {
			DisplayName = displayName,
			Id = id,
			Vin = vin
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		ProMilesVehicle value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
