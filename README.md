# Arex388.HdFleet

Arex388.HdFleet is a highly opinionated .NET Standard 2.0 library for the [HD Fleet](https://www.hdfleet.com) Integrations External API v1. It's intended to be an easy, well structured, and highly performant client for retrieving GPS fleet tracking information — latest positions, location history, drivers, dashcam events with media, backfills, integrations, and ProMiles mileage reports. It can be used in applications interacting with a single account using `IHdFleetClient`, or with applications interacting with multiple accounts using `IHdFleetClientFactory`.

- [Releases](https://github.com/arex388/Arex388.HdFleet/releases)
- [Benchmarks](https://github.com/arex388/Arex388.HdFleet/blob/main/BENCHMARKS.md)



#### Dependency Injection

To configure dependency injection use the `AddHdFleet()` extensions on `IServiceCollection`. There are two signatures, with and without passing in an `HdFleetClientOptions` object. If the options object is passed to the extension, it will register `IHdFleetClient` for use with a single account, otherwise it will register `IHdFleetClientFactory` for use with multiple accounts.

```c#
//  Single account.
services.AddHdFleet(new HdFleetClientOptions {
    Key = "Your key from HD Fleet"
});

//  Multiple accounts.
services.AddHdFleet();
```

`HdFleetClientOptions.Timeout` sets the deadline for every request, transparent retries and their delays included. It defaults to 100 seconds, may be longer, and must be greater than zero (`AddHdFleet` and `CreateClient` throw `ArgumentOutOfRangeException` otherwise). The library owns this deadline: the named `HttpClient`'s own `Timeout` is set to infinite, so configure the value here rather than on the `HttpClient`.

```c#
services.AddHdFleet(new HdFleetClientOptions {
    Key = "Your key from HD Fleet",
    Timeout = TimeSpan.FromSeconds(30)
});
```



#### How to Use

For a single account, inject the `IHdFleetClient`.

```c#
private readonly IHdFleetClient _hdFleet;

var locations = await _hdFleet.ListLatestLocationsAsync();
var vehicles = await _hdFleet.ListVehiclesAsync();
var vehicle = await _hdFleet.GetVehicleAsync(new ExternalVehicleId("V-123"));
```

For multiple accounts, inject the `IHdFleetClientFactory` to create an instance per account. Clients are cached per key and timeout — repeated calls with the same key and `Timeout` return the same instance, and a different `Timeout` gets its own client.

```c#
private readonly IHdFleetClientFactory _hdFleetFactory;

var hdFleet = _hdFleetFactory.CreateClient(new HdFleetClientOptions {
    Key = "Your key from HD Fleet"
});

var locations = await hdFleet.ListLatestLocationsAsync();
```



#### The Response Contract

Responses never throw. Every response derives from `ResponseBase<TResponse>` — check `Success`, and branch on `Status` when the failure class matters:

```c#
var response = await _hdFleet.ListLatestLocationsAsync();

if (!response.Success) {
    //  response.Errors carries the details; response.Status classifies them:
    //  Unauthorized (bad key), Forbidden (missing scope), RateLimited,
    //  ServerError, Invalid (failed client-side validation), Cancelled,
    //  TimedOut (the request missed HdFleetClientOptions.Timeout), ...
    //  response.RequestId is HD Fleet's X-Request-ID — include it in support
    //  tickets.
    return;
}

foreach (var location in response.Locations) {
    //  location.VehicleId, location.Latitude, location.Longitude,
    //  location.RecordedAt (the GPS fix time) are always present.
}
```

API failures, validation failures, timeouts, and cancellation all come back as canned outcomes — never exceptions. `Cancelled` means your cancellation token fired; `TimedOut` means the client's own deadline fired while your token had not.

Every response produced after a request was sent also carries `Metadata`, what the client observed about the HTTP exchange:

```c#
var metadata = response.Metadata;

//  metadata.StatusCode, Date, Age, CacheControl, ETag — null when no response
//  arrived (timeout, cancellation, transport failure).
//  metadata.Headers — every response and content header, case-insensitive keys.
//  metadata.Elapsed — wall-clock time of the exchange, retries included.
//  metadata.RecordCount — records in the page before mapping; null when the
//  body is not a page.
//  metadata.BodyExcerpt — the first 512 characters of the body on a failure.
//  metadata.FromCache — true when the fresh-poll gate answered.
```

`Metadata` is `null` only when the request was never sent: a validation failure, or a token that was already cancelled.



#### The Fresh-Poll Gate

HD Fleet enforces minimum intervals between *fresh* calls per key: **60 seconds** on `/locations/latest` (and per new query on `/locations/history` and `/events`), **15 minutes** on `/vehicles` and `/drivers`. The client turns those floors into a response cache with matching TTLs, so you can poll at any interval safely:

- Within the floor, you get the cached response with `FromCache = true` — no request goes out, and a floor-driven `429` is structurally impossible. Its `Metadata` describes the original exchange, with `Metadata.FromCache = true`.
- Outside the floor, a real request goes out and refreshes the cache.
- Cursor continuation pages bypass the gate entirely (they are exempt from the floors, though they do count against quota).

Gate state is shared by all clients holding the same key over the same `IMemoryCache`, and isolated between keys.



#### Streaming

`IAsyncEnumerable<T>` helpers walk cursor pages to exhaustion:

```c#
await foreach (var location in _hdFleet.StreamLatestLocationsAsync()) { }
await foreach (var vehicle in _hdFleet.StreamVehiclesAsync()) { }
await foreach (var driver in _hdFleet.StreamDriversAsync()) { }
await foreach (var location in _hdFleet.StreamLocationHistoryAsync(from, to)) { }
```

An `IAsyncEnumerable<T>` has no response envelope, so the streams are the one place the library throws. A failed page throws `HdFleetException`, carrying the page's `Status`, `Errors`, `RequestId`, `Metadata` and relative `Endpoint`; items from earlier pages were already yielded, and the stream retries nothing beyond the transparent retries below. Cancelling your token throws `OperationCanceledException`, as usual.

```c#
try {
    await foreach (var location in _hdFleet.StreamLatestLocationsAsync(cancellationToken, onResponse: metadata => {
        //  Once per page that was sent, before its items are yielded:
        //  metadata.FromCache, metadata.RecordCount, metadata.Elapsed, ...
    })) { }
} catch (HdFleetException e) when (e.Status == ResponseStatus.TimedOut) {
    //  e.Message: "locations/latest?limit=200 returned TimedOut after 00:01:40.0: ..."
}
```

Every `Stream*` helper takes the optional `onResponse` callback as its last parameter; pass it by name. A page that was never sent (a validation failure) skips the callback and throws `HdFleetException` with `Status = Invalid`.

There are also convenience extensions: `GetLatestLocationAsync(vehicleId)` and `ListRecentLocationsAsync(within)` (the freshness filter is applied client-side). Both walk `StreamLatestLocationsAsync`, so they throw the same way.



#### Retries

Transient failures (`429`, `500`, `503`) retry transparently up to 3 times, honoring `Retry-After` when present and otherwise backing off exponentially with jitter. Non-retryable statuses (`400`, `401`, `403`, `404`, `409`, `410`) pass through immediately. **POSTs are never retried** — re-issue a failed `CreateBackfillAsync` yourself; HD Fleet reuses identical ready jobs, so accidental duplicates are harmless.



#### Backfills — Historical Catch-Up

Do **not** brute-force history through `/locations/history` — its required 15-minute windows and 60-second floors make a single day ~96 sequential queries. Use the backfill workflow: one job covers up to 7 days.

```c#
//  1. Create (or reuse) a job.
var created = await _hdFleet.CreateBackfillAsync(new CreateBackfill.Request {
    From = DateTimeOffset.UtcNow.AddDays(-7),
    To = DateTimeOffset.UtcNow.AddDays(-1)
});

//  2. Poll until ready.
var job = await _hdFleet.GetBackfillAsync(created.Job!.Id);

//  3. Walk the slices — strictly in sequence, inner cursors exhausted first.
await foreach (var @event in _hdFleet.StreamBackfillEventsAsync(job.Job!.Id)) { }
```

Jobs expire (`ExpiresAt`); an expired job's resources return `Status = Gone` — recreate it. A window overlapping an existing ready job returns `Status = Conflict`.



#### Multi-Account Operational Guidance

HD Fleet applies **global source-IP abuse protection** separately from per-key quotas. Many individually-compliant keys egressing from one IP can collectively trip it. If you poll on behalf of many accounts from one host:

- Stagger the poll schedules — offset each account's timer deterministically across the interval instead of firing them all at once.
- Bound process-wide concurrency across all accounts (a small `SemaphoreSlim` — start around 4).
- Treat a `429` with no `Retry-After` as a possible IP-level trip and back off globally, not just for that account.



#### Deployment Guidance for Long-Running Services

The client resolves its `HttpClient` once and holds it for the client's lifetime — zero per-request overhead, but it means `IHttpClientFactory`'s handler rotation never engages, so warm connections don't re-resolve DNS. If your deployment is a busy, long-running service, configure the connection pool on the named client instead:

```c#
services.AddHdFleet(options);
services.AddHttpClient(nameof(IHdFleetClient))
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15)
        });
```

Connections are then recycled at the pool level, picking up DNS changes without any per-request cost. `SocketsHttpHandler` exists on .NET Core 2.1+ / .NET 5+ only — on .NET Framework, use the `ServicePoint` machinery instead (`ServicePoint.ConnectionLeaseTimeout` for the HD Fleet endpoint).



#### Key Handling

Authentication is a static bearer key sent per request on the `Authorization` header — never on `HttpClient.DefaultRequestHeaders`, so a pooled client can never leak one account's key into another's request. The raw key never appears in logs, exception messages, or error details; anything diagnostic uses its SHA-256 fingerprint. If HD Fleet echoes the key back in an error body or a response header, the client replaces it with `[redacted]` before storing it in `Errors`, `Metadata.BodyExcerpt` or `Metadata.Headers`.
