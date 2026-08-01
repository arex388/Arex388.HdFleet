namespace Arex388.HdFleet;

/// <summary>
/// GetVehicle request and response containers.
/// </summary>
public static class GetVehicle {
	/// <summary>
	/// GetVehicle request container.
	/// </summary>
	/// <remarks>
	/// Repeated automated lookups are subject to client quota and global abuse
	/// protection — prefer <see cref="ListVehicles"/> for sync jobs; never call
	/// this in a loop.
	/// </remarks>
	public sealed class Request :
		RequestBase {
		//  Explicit concatenation: on netstandard2.0 interpolation with a struct
		//  hole lowers to string.Format and boxes the id.
		internal override string Endpoint => "vehicles/" + Id.ToString();

		/// <summary>
		/// The vehicle's external id.
		/// </summary>
		public ExternalVehicleId Id { get; init; }
	}

	/// <summary>
	/// GetVehicle response container.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The matched vehicle.
		/// </summary>
		public Vehicle? Vehicle { get; init; }
	}
}
