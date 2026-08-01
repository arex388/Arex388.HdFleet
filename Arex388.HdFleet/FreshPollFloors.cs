namespace Arex388.HdFleet;

//	HD Fleet's documented per-key minimum intervals between fresh calls, per
//	route. Un-annotated endpoints (/vehicles/{id}, backfills, integrations,
//	ProMiles) have no documented floor and are not gated.
internal static class FreshPollFloors {
	public static readonly TimeSpan Drivers = TimeSpan.FromMinutes(15);
	public static readonly TimeSpan Events = TimeSpan.FromSeconds(60);
	public static readonly TimeSpan LatestLocations = TimeSpan.FromSeconds(60);
	public static readonly TimeSpan LocationHistory = TimeSpan.FromSeconds(60);
	public static readonly TimeSpan Vehicles = TimeSpan.FromMinutes(15);
}
