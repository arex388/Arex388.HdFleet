using FluentAssertions;
using System.Reflection;
using System.Text.Json;

namespace Arex388.HdFleet.Tests.Unit;

//	The models carry no serialization metadata — every model type deserializes
//	exclusively through a hand-written converter, and a converter file that is
//	added but never registered in HdFleetClient's serializer options fails
//	silently. This guard makes that trap loud.
public sealed class ConverterRegistrationTests {
	private static readonly Assembly _libraryAssembly = typeof(IHdFleetClient).Assembly;

	private static JsonSerializerOptions GetClientOptions() {
		var clientType = _libraryAssembly.GetType("Arex388.HdFleet.HdFleetClient")!;

		return (JsonSerializerOptions)clientType.GetField("_jsonSerializerOptions", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
	}

	[Theory]
	[InlineData(typeof(Driver))]
	[InlineData(typeof(Location))]
	[InlineData(typeof(Vehicle))]
	public void EveryPublicModelType_HasARegisteredConverter(
		Type modelType) => AssertRegistered(modelType);

	[Fact]
	public void ErrorEnvelope_HasARegisteredConverter() => AssertRegistered(_libraryAssembly.GetType("Arex388.HdFleet.ErrorEnvelope")!);

	[Fact]
	public void PageOfLocation_HasARegisteredConverter() => AssertRegistered(_libraryAssembly.GetType("Arex388.HdFleet.Page`1")!.MakeGenericType(typeof(Location)));

	private static void AssertRegistered(
		Type modelType) {
		var converter = GetClientOptions().GetConverter(modelType);

		//	An unregistered type resolves to System.Text.Json's reflection-based
		//	fallback converter; a registered one resolves to the library's own.
		converter.GetType().Assembly.Should().BeSameAs(_libraryAssembly, $"{modelType.Name} must have its hand-written converter registered in HdFleetClient's serializer options");
	}
}
