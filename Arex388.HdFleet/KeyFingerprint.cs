using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Arex388.HdFleet;

//	The raw API key must never leave the process — anything that does (log
//	lines, error details, diagnostics) carries this fingerprint instead.
//	Compute once per options instance, never per request.
internal static class KeyFingerprint {
	public static string Compute(
		string key) {
		using var sha = SHA256.Create();

		var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
		var builder = new StringBuilder(16);

		for (var i = 0; i < 8; i++) {
			builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
		}

		return builder.ToString();
	}
}
