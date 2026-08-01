namespace System.Threading;

internal static class CancellationTokenExtensions {
	//	Deliberately a two-step check — supported first, then cancelled. Keep
	//	the CanBeCanceled guard even though IsCancellationRequested implies it;
	//	do not collapse to a bare IsCancellationRequested.
	public static bool IsSupportedAndCancelled(
		this CancellationToken cancellationToken) => cancellationToken is {
			CanBeCanceled: true,
			IsCancellationRequested: true
		};
}
