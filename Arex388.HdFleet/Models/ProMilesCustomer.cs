namespace Arex388.HdFleet;

/// <summary>
/// ProMiles customer object.
/// </summary>
public sealed class ProMilesCustomer {
	/// <summary>
	/// The customer's company id.
	/// </summary>
	public int? CompanyId { get; init; }

	/// <summary>
	/// The customer's id.
	/// </summary>
	public int CustomerId { get; init; }

	/// <summary>
	/// The customer's name.
	/// </summary>
	public string? Name { get; init; }
}
