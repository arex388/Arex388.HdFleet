using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Arex388.HdFleet.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, id: ".NET 10")]
public class VehiclesBenchmarks {
	private IHdFleetClient _client = null!;
	private ExternalVehicleId _vehicleId;

	[GlobalSetup]
	public void Setup() {
		var services = BenchmarkServiceProvider.Create();

		_client = BenchmarkServiceProvider.CreateClient(services);
		_vehicleId = new ExternalVehicleId("V-1");
	}

	/// <summary>Full pipeline on the ungated bare-object endpoint.</summary>
	[Benchmark]
	public Task<GetVehicle.Response> GetAsync() => _client.GetVehicleAsync(_vehicleId);
}
