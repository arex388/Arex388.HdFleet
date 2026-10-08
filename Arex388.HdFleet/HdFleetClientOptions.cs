using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Arex388.HdFleet;

/// <summary>
/// The <see cref="IHdFleetClient"/>'s configuration options.
/// </summary>
public sealed class HdFleetClientOptions {
	//	CancellationTokenSource.CancelAfter rejects anything above
	//	Int32.MaxValue milliseconds, so that is the ceiling as well.
	private static readonly TimeSpan _maximumTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

	internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);

	//	The SHA-256 fingerprint used by anything that leaves the process —
	//	computed once per options instance, never per request.
	internal string Fingerprint => field ??= KeyFingerprint.Compute(Key);

	//	Every form an echo of the key can take in wire text: the raw key, its
	//	JSON-escaped forms (default and relaxed encoders, with upper- and
	//	lower-case hex escapes) and its URL-encoded forms (a cursor echoing
	//	the key rides the endpoint URL-encoded), distinct and longest first.
	//	Computed once per options instance, never per request.
	internal IReadOnlyList<string> RedactionForms => field ??= GetRedactionForms(Key);

	//	null and an explicit 100 seconds are the same identity — the factory
	//	keys its client cache on this, not on Timeout.
	internal TimeSpan EffectiveTimeout => Timeout ?? DefaultTimeout;

	/// <summary>
	/// The account's API key.
	/// </summary>
	public required string Key { get; init; }

	/// <summary>
	/// The deadline for every request the client sends, covering the whole exchange including
	/// transparent retries, their delays and the body transfer. <c>null</c> means 100 seconds.
	/// A request that misses it returns <see cref="ResponseStatus.TimedOut"/>.
	/// </summary>
	/// <remarks>
	/// Must be greater than zero and no longer than <see cref="int.MaxValue"/> milliseconds;
	/// <c>AddHdFleet</c> and
	/// <see cref="IHdFleetClientFactory.CreateClient(HdFleetClientOptions)"/> throw
	/// <see cref="ArgumentOutOfRangeException"/> otherwise. The library owns the deadline: the
	/// named <see cref="HttpClient"/>'s own <see cref="HttpClient.Timeout"/> is disabled.
	/// </remarks>
	public TimeSpan? Timeout { get; init; }

	internal static void ThrowIfInvalid(
		HdFleetClientOptions options) {
		if (options.Timeout is not { } timeout
			|| (timeout > TimeSpan.Zero && timeout <= _maximumTimeout)) {
			return;
		}

		throw new ArgumentOutOfRangeException(nameof(options), timeout, "HdFleetClientOptions.Timeout must be greater than zero and no more than " + int.MaxValue.ToString(CultureInfo.InvariantCulture) + " milliseconds.");
	}

	//	============================================================================
	//	Utilities
	//	============================================================================

	private static string[] GetRedactionForms(
		string key) {
		if (!key.HasValue()) {
			return [];
		}

		var escaped = JsonEncodedText.Encode(key, JavaScriptEncoder.Default).ToString();
		var forms = new HashSet<string>(StringComparer.Ordinal) {
			key,
			escaped,
			GetLowerCaseHexEscapes(escaped),
			JsonEncodedText.Encode(key, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString(),
			Uri.EscapeDataString(key),
			WebUtility.UrlEncode(key)
		};

		return forms.OrderByDescending(static form => form.Length)
					.ToArray();
	}

	//	JSON permits \uXXXX escapes in either case; the encoders emit upper case.
	private static string GetLowerCaseHexEscapes(
		string value) {
		var chars = value.ToCharArray();

		for (var i = 0; i + 5 < chars.Length; i++) {
			if (chars[i] != '\\') {
				continue;
			}

			if (chars[i + 1] == 'u') {
				for (var j = i + 2; j <= i + 5; j++) {
					chars[j] = char.ToLowerInvariant(chars[j]);
				}

				i += 5;
			} else {
				//	Skip the escaped character so "\\u" is never misread.
				i++;
			}
		}

		return new string(chars);
	}
}
