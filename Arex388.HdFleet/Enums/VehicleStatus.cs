using NetEscapades.EnumGenerators;

namespace Arex388.HdFleet;

/// <summary>
/// The <see cref="Vehicle"/>'s status.
/// </summary>
/// <remarks>
/// HD Fleet documents no value set for this field — members are seeded only from
/// observed live values. Until then <see cref="None"/> is the only member and every
/// wire value falls back to it.
/// </remarks>
[EnumExtensions(MetadataSource = MetadataSource.DisplayAttribute)]
public enum VehicleStatus :
	byte {
	/// <summary>
	/// The default status. If this is the value, then the response value wasn't parsed and this was used as a fallback.
	/// </summary>
	None
}
