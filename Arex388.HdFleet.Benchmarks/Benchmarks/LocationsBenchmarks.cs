using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Arex388.HdFleet.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, id: ".NET 10")]
public class LocationsBenchmarks {
	private IHdFleetClient _client = null!;
	private ListLatestLocations.Request _cursorRequest = null!;
	private byte[] _pageBytes = null!;
	private Type _pageOfLocationType = null!;
	private JsonSerializerOptions _serializerOptions = null!;

	[GlobalSetup]
	public void Setup() {
		var services = BenchmarkServiceProvider.Create();

		_client = BenchmarkServiceProvider.CreateClient(services);

		//	A cursor-bearing request bypasses the fresh-poll gate, so every
		//	invocation exercises the full pipeline against the 200-record page.
		_cursorRequest = new ListLatestLocations.Request {
			Cursor = "benchmark"
		};

		//	Prime the gate so the fresh benchmark measures the cache-hit path.
		_client.ListLatestLocationsAsync().GetAwaiter().GetResult();

		//	The raw-deserialization benchmark reads the library's own serializer
		//	options and internal Page<Location> type via reflection, resolved
		//	once here — the benchmark body pays only the deserialization cost.
		var assembly = typeof(IHdFleetClient).Assembly;
		var clientType = assembly.GetType("Arex388.HdFleet.HdFleetClient")!;

		_serializerOptions = (JsonSerializerOptions)clientType.GetField("_jsonSerializerOptions", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
		_pageOfLocationType = assembly.GetType("Arex388.HdFleet.Page`1")!.MakeGenericType(typeof(Location));
		_pageBytes = Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Responses", "locations-latest.json")));
	}

	/// <summary>Raw converter throughput over the 200-record page.</summary>
	[Benchmark]
	public object? DeserializeLocationsPage() => JsonSerializer.Deserialize(_pageBytes, _pageOfLocationType, _serializerOptions);

	/// <summary>Full pipeline including HTTP mock and deserialization — gate bypassed via cursor.</summary>
	[Benchmark]
	public Task<ListLatestLocations.Response> ListCursorPageAsync() => _client.ListLatestLocationsAsync(_cursorRequest);

	/// <summary>The fresh-poll gate cache-hit path — lookup plus response clone, no HTTP.</summary>
	[Benchmark]
	public Task<ListLatestLocations.Response> ListGateHitAsync() => _client.ListLatestLocationsAsync();
}
