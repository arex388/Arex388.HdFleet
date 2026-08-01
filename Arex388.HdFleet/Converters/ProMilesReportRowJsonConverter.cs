using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class ProMilesReportRowJsonConverter :
	JsonConverter<ProMilesReportRow> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _distanceMiles => "distanceMiles"u8;
	private static ReadOnlySpan<byte> _firstLocationAt => "firstLocationAt"u8;
	private static ReadOnlySpan<byte> _lastLocationAt => "lastLocationAt"u8;
	private static ReadOnlySpan<byte> _pointCount => "pointCount"u8;
	private static ReadOnlySpan<byte> _reportDate => "reportDate"u8;
	private static ReadOnlySpan<byte> _vehicleName => "vehicleName"u8;

	public override ProMilesReportRow? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		decimal? distanceMiles = null;
		DateTimeOffset? firstLocationAt = null;
		DateTimeOffset? lastLocationAt = null;
		var pointCount = 0;
		DateTimeOffset? reportDate = null;
		string? vehicleName = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_distanceMiles)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					distanceMiles = reader.TokenType == JsonTokenType.String
						? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
						: reader.GetDecimal();
				}
			} else if (reader.ValueTextEquals(_firstLocationAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					firstLocationAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_lastLocationAt)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					lastLocationAt = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_pointCount)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					pointCount = reader.TokenType == JsonTokenType.String
						? int.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture)
						: reader.GetInt32();
				}
			} else if (reader.ValueTextEquals(_reportDate)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					reportDate = reader.GetDateTimeOffset();
				}
			} else if (reader.ValueTextEquals(_vehicleName)) {
				reader.Read();

				vehicleName = reader.GetString();
			} else {
				reader.Skip();
			}
		}

		return new ProMilesReportRow {
			DistanceMiles = distanceMiles,
			FirstLocationAt = firstLocationAt,
			LastLocationAt = lastLocationAt,
			PointCount = pointCount,
			ReportDate = reportDate,
			VehicleName = vehicleName
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		ProMilesReportRow value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
