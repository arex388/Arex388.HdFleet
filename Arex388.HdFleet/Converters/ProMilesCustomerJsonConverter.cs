using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arex388.HdFleet.Converters;

internal sealed class ProMilesCustomerJsonConverter :
	JsonConverter<ProMilesCustomer> {
	//	Property names (camelCase as returned by the API)
	private static ReadOnlySpan<byte> _companyId => "companyId"u8;
	private static ReadOnlySpan<byte> _customerId => "customerId"u8;
	private static ReadOnlySpan<byte> _name => "name"u8;

	public override ProMilesCustomer? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Null) {
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject) {
			throw new JsonException("Expected StartObject token");
		}

		int? companyId = null;
		var customerId = 0;
		string? name = null;

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (reader.ValueTextEquals(_companyId)) {
				reader.Read();

				if (reader.TokenType != JsonTokenType.Null) {
					companyId = reader.TokenType == JsonTokenType.String
						? int.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture)
						: reader.GetInt32();
				}
			} else if (reader.ValueTextEquals(_customerId)) {
				reader.Read();

				customerId = reader.TokenType == JsonTokenType.String
					? int.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture)
					: reader.GetInt32();
			} else if (reader.ValueTextEquals(_name)) {
				reader.Read();

				name = reader.GetString();
			} else {
				reader.Skip();
			}
		}

		return new ProMilesCustomer {
			CompanyId = companyId,
			CustomerId = customerId,
			Name = name
		};
	}

	public override void Write(
		Utf8JsonWriter writer,
		ProMilesCustomer value,
		JsonSerializerOptions options) => throw new NotImplementedException();
}
