using Arex388.HdFleet.Converters;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Arex388.HdFleet;

internal sealed class HdFleetClient(
	IServiceProvider services,
	HttpClient? httpClient = null,
	HdFleetClientOptions? options = null) :
	IHdFleetClient {
	//  Enough for any error envelope the API returns; bounded so a large HTML
	//  error page never ends up in an exception message or a log.
	private const int BodyExcerptLength = 512;
	private const string Redacted = "[redacted]";

	private static readonly JsonSerializerOptions _jsonSerializerOptions = new() {
		Converters = {
			new BackfillJobJsonConverter(),
			new BackfillSliceJsonConverter(),
			new DriverJsonConverter(),
			new ErrorEnvelopeJsonConverter(),
			new EventJsonConverter(),
			new EventMediaItemJsonConverter(),
			new EventMediaJsonConverter(),
			new IntegrationJsonConverter(),
			new LocationJsonConverter(),
			new PageJsonConverterFactory(),
			new ProMilesCustomerJsonConverter(),
			new ProMilesReportRowJsonConverter(),
			new ProMilesVehicleJsonConverter(),
			new VehicleJsonConverter()
		}
	};

	private readonly IValidator<CreateBackfill.Request> _createBackfillRequestValidator = services.GetRequiredService<IValidator<CreateBackfill.Request>>();
	private readonly IValidator<GetProMilesReport.Request> _getProMilesReportRequestValidator = services.GetRequiredService<IValidator<GetProMilesReport.Request>>();
	private readonly IValidator<ListProMilesCustomers.Request> _listProMilesCustomersRequestValidator = services.GetRequiredService<IValidator<ListProMilesCustomers.Request>>();
	private readonly IValidator<ListProMilesVehicles.Request> _listProMilesVehiclesRequestValidator = services.GetRequiredService<IValidator<ListProMilesVehicles.Request>>();
	private readonly IValidator<ListBackfillSlices.Request> _listBackfillSlicesRequestValidator = services.GetRequiredService<IValidator<ListBackfillSlices.Request>>();
	private readonly IValidator<ListDrivers.Request> _listDriversRequestValidator = services.GetRequiredService<IValidator<ListDrivers.Request>>();
	private readonly IValidator<ListEvents.Request> _listEventsRequestValidator = services.GetRequiredService<IValidator<ListEvents.Request>>();
	private readonly IValidator<ListIntegrations.Request> _listIntegrationsRequestValidator = services.GetRequiredService<IValidator<ListIntegrations.Request>>();
	private readonly IValidator<ListLatestLocations.Request> _listLatestLocationsRequestValidator = services.GetRequiredService<IValidator<ListLatestLocations.Request>>();
	private readonly IValidator<ListLocationHistory.Request> _listLocationHistoryRequestValidator = services.GetRequiredService<IValidator<ListLocationHistory.Request>>();
	private readonly IValidator<ListVehicles.Request> _listVehiclesRequestValidator = services.GetRequiredService<IValidator<ListVehicles.Request>>();
	private readonly FreshPollGate _freshPollGate = new(services.GetRequiredService<IMemoryCache>());
	private readonly HttpClient _httpClient = httpClient ?? services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IHdFleetClient));
	private readonly HdFleetClientOptions _options = options ?? services.GetRequiredService<HdFleetClientOptions>();

	public Task<CreateBackfill.Response> CreateBackfillAsync(
		CreateBackfill.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _createBackfillRequestValidator, static (BackfillJob? job) => job is null
			? null
			: new CreateBackfill.Response {
				Job = job
			}, null, cancellationToken);

	public Task<GetBackfill.Response> GetBackfillAsync(
		BackfillJobId id,
		CancellationToken cancellationToken = default) => GetBackfillAsync(new GetBackfill.Request {
			Id = id
		}, cancellationToken);

	public Task<GetBackfill.Response> GetBackfillAsync(
		GetBackfill.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, null, static (BackfillJob? job) => job is null
			? null
			: new GetBackfill.Response {
				Job = job
			}, null, cancellationToken);

	public Task<GetBackfillEventsPage.Response> GetBackfillEventsPageAsync(
		GetBackfillEventsPage.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, null, static (Page<Event>? page) => new GetBackfillEventsPage.Response {
			Events = page?.Data ?? [],
			NextCursor = page?.NextCursor
		}, null, cancellationToken);

	public Task<GetProMilesReport.Response> GetProMilesReportAsync(
		GetProMilesReport.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _getProMilesReportRequestValidator, static (Page<ProMilesReportRow>? page) => new GetProMilesReport.Response {
			NextCursor = page?.NextCursor,
			Rows = page?.Data ?? []
		}, null, cancellationToken);

	public Task<GetVehicle.Response> GetVehicleAsync(
		ExternalVehicleId id,
		CancellationToken cancellationToken = default) => GetVehicleAsync(new GetVehicle.Request {
			Id = id
		}, cancellationToken);

	public Task<GetVehicle.Response> GetVehicleAsync(
		GetVehicle.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, null, static (Vehicle? vehicle) => vehicle is null
			? null
			: new GetVehicle.Response {
				Vehicle = vehicle
			}, null, cancellationToken);

	public Task<ListBackfillSlices.Response> ListBackfillSlicesAsync(
		ListBackfillSlices.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _listBackfillSlicesRequestValidator, static (Page<BackfillSlice>? page) => new ListBackfillSlices.Response {
			NextCursor = page?.NextCursor,
			Slices = page?.Data ?? []
		}, null, cancellationToken);

	public Task<ListDrivers.Response> ListDriversAsync(
		CancellationToken cancellationToken = default) => ListDriversAsync(ListDrivers.Request.Default, cancellationToken);

	public Task<ListDrivers.Response> ListDriversAsync(
		ListDrivers.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _listDriversRequestValidator, static (Page<Driver>? page) => new ListDrivers.Response {
			Drivers = page?.Data ?? [],
			NextCursor = page?.NextCursor
		}, request.Cursor is null
			? FreshPollFloors.Drivers
			: (TimeSpan?)null, cancellationToken);

	public Task<ListEvents.Response> ListEventsAsync(
		ListEvents.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _listEventsRequestValidator, static (Page<Event>? page) => new ListEvents.Response {
			Events = page?.Data ?? [],
			NextCursor = page?.NextCursor
		}, request.Cursor is null
			? FreshPollFloors.Events
			: (TimeSpan?)null, cancellationToken);

	public Task<ListIntegrations.Response> ListIntegrationsAsync(
		CancellationToken cancellationToken = default) => ListIntegrationsAsync(ListIntegrations.Request.Default, cancellationToken);

	public Task<ListIntegrations.Response> ListIntegrationsAsync(
		ListIntegrations.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _listIntegrationsRequestValidator, static (Page<Integration>? page) => new ListIntegrations.Response {
			Integrations = page?.Data ?? [],
			NextCursor = page?.NextCursor
		}, null, cancellationToken);

	public Task<ListLatestLocations.Response> ListLatestLocationsAsync(
		CancellationToken cancellationToken = default) => ListLatestLocationsAsync(ListLatestLocations.Request.Default, cancellationToken);

	public Task<ListLatestLocations.Response> ListLatestLocationsAsync(
		ListLatestLocations.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _listLatestLocationsRequestValidator, static (Page<Location>? page) => new ListLatestLocations.Response {
			Locations = page?.Data ?? [],
			NextCursor = page?.NextCursor
		}, request.Cursor is null
			? FreshPollFloors.LatestLocations
			: (TimeSpan?)null, cancellationToken);

	public Task<ListLocationHistory.Response> ListLocationHistoryAsync(
		DateTimeOffset from,
		DateTimeOffset to,
		CancellationToken cancellationToken = default) => ListLocationHistoryAsync(new ListLocationHistory.Request {
			From = from,
			To = to
		}, cancellationToken);

	public Task<ListLocationHistory.Response> ListLocationHistoryAsync(
		ListLocationHistory.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _listLocationHistoryRequestValidator, static (Page<Location>? page) => new ListLocationHistory.Response {
			Locations = page?.Data ?? [],
			NextCursor = page?.NextCursor
		}, request.Cursor is null
			? FreshPollFloors.LocationHistory
			: (TimeSpan?)null, cancellationToken);

	public Task<ListProMilesCustomers.Response> ListProMilesCustomersAsync(
		CancellationToken cancellationToken = default) => ListProMilesCustomersAsync(ListProMilesCustomers.Request.Default, cancellationToken);

	public Task<ListProMilesCustomers.Response> ListProMilesCustomersAsync(
		ListProMilesCustomers.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _listProMilesCustomersRequestValidator, static (Page<ProMilesCustomer>? page) => new ListProMilesCustomers.Response {
			Customers = page?.Data ?? [],
			NextCursor = page?.NextCursor
		}, null, cancellationToken);

	public Task<ListProMilesVehicles.Response> ListProMilesVehiclesAsync(
		int customerId,
		CancellationToken cancellationToken = default) => ListProMilesVehiclesAsync(new ListProMilesVehicles.Request {
			CustomerId = customerId
		}, cancellationToken);

	public Task<ListProMilesVehicles.Response> ListProMilesVehiclesAsync(
		ListProMilesVehicles.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _listProMilesVehiclesRequestValidator, static (Page<ProMilesVehicle>? page) => new ListProMilesVehicles.Response {
			NextCursor = page?.NextCursor,
			Vehicles = page?.Data ?? []
		}, null, cancellationToken);

	public Task<ListVehicles.Response> ListVehiclesAsync(
		CancellationToken cancellationToken = default) => ListVehiclesAsync(ListVehicles.Request.Default, cancellationToken);

	public Task<ListVehicles.Response> ListVehiclesAsync(
		ListVehicles.Request request,
		CancellationToken cancellationToken = default) => SendAsync(request, _listVehiclesRequestValidator, static (Page<Vehicle>? page) => new ListVehicles.Response {
			NextCursor = page?.NextCursor,
			Vehicles = page?.Data ?? []
		}, request.Cursor is null
			? FreshPollFloors.Vehicles
			: (TimeSpan?)null, cancellationToken);

	//  ============================================================================
	//  Pipeline
	//  ============================================================================

	//  The single request pipeline behind every operation: cancellation
	//  pre-check → optional validation → SendCoreAsync. Every response leaves
	//  stamped with the request's relative endpoint, redacted: a cursor is
	//  wire text and can echo the key, which would then ride the endpoint
	//  (URL-encoded) into an HdFleetException message. The outgoing request
	//  URL itself is unchanged. The endpoint is evaluated only after the
	//  request passed validation; the never-sent outcomes stamp it through a
	//  guard, so a malformed request still returns rather than throws.
	private async Task<TResponse> SendAsync<TRequest, TModel, TResponse>(
		TRequest request,
		IValidator<TRequest>? validator,
		Func<TModel?, TResponse?> map,
		TimeSpan? freshPollFloor,
		CancellationToken cancellationToken)
		where TRequest : RequestBase
		where TModel : class
		where TResponse : ResponseBase<TResponse>, new() {
		if (cancellationToken.IsSupportedAndCancelled()) {
			return WithGuardedEndpoint(ResponseBase<TResponse>.Cancelled, request);
		}

		if (validator is not null) {
			// ReSharper disable once MethodHasAsyncOverloadWithCancellation
			var validationResult = validator.Validate(request);

			if (!validationResult.IsValid) {
				return WithGuardedEndpoint(ResponseBase<TResponse>.Invalid(validationResult), request);
			}
		}

		//  A malformed request's getter can throw; IHdFleetClient members never
		//  do, so that is a never-sent Failed with no Endpoint and no Metadata.
		string endpoint;

		try {
			endpoint = request.Endpoint;
		} catch (Exception e) {
			return ResponseBase<TResponse>.FailedWith(ResponseStatus.Failed, Redact(GetExceptionDetail(e)), null);
		}

		var response = await SendCoreAsync(request, endpoint, map, freshPollFloor, cancellationToken).ConfigureAwait(false);

		response.Endpoint = Redact(endpoint);

		return response;
	}

	//  The send behind every valid request: fresh-poll gate check → GET with a
	//  per-request Authorization header under the options' deadline → capture
	//  the status and headers → buffer the body → deserialize → map → gate
	//  store. A null map result means "the API returned nothing usable"
	//  (Failed); list mappers never return null, coalescing a null payload to
	//  empty. A null freshPollFloor skips the gate — cursor continuations and
	//  un-annotated endpoints are never gated. Every outcome after the send is
	//  attempted carries ResponseMetadata.
	private async Task<TResponse> SendCoreAsync<TRequest, TModel, TResponse>(
		TRequest request,
		string endpoint,
		Func<TModel?, TResponse?> map,
		TimeSpan? freshPollFloor,
		CancellationToken cancellationToken)
		where TRequest : RequestBase
		where TModel : class
		where TResponse : ResponseBase<TResponse>, new() {
		if (freshPollFloor.HasValue) {
			var cached = _freshPollGate.GetCached<TResponse>(_options.Key, endpoint);

			if (cached is not null) {
				return cached.WithFromCache();
			}
		}

		//  The library owns the deadline: the named HttpClient's Timeout is
		//  infinite, and this linked source covers the whole exchange — the
		//  send, the retry handler's delays and the body transfer. When it
		//  fires while the caller's token has not, the outcome is TimedOut
		//  rather than Cancelled.
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		deadline.CancelAfter(_options.EffectiveTimeout);

		HttpResponseMessage? httpResponse = null;
		ResponseMetadata? arrived = null;
		var stopwatch = new Stopwatch();

		try {
			//  The Authorization header is set per request, never on
			//  DefaultRequestHeaders — a pooled client with a default header is
			//  a cross-account data leak.
			using var httpRequest = new HttpRequestMessage(request.Method, endpoint);

			httpRequest.Content = request.GetContent();
			httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Key);

			stopwatch.Start();

			//  ResponseHeadersRead returns as soon as the status and headers
			//  arrive, so they are captured even when the body transfer then
			//  fails or misses the deadline. The retry handler only inspects
			//  the status code and Retry-After, so it is unaffected.
			httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
			arrived = CaptureResponse(httpResponse);

			//  The body is still fully buffered before anything reads it: async
			//  stream deserialization can invoke the converters on partially
			//  buffered JSON, where their unknown-property Skip() throws. A
			//  complete buffer makes isFinalBlock true, so Skip is always safe.
			var body = await ReadBodyAsync(httpResponse, deadline.Token).ConfigureAwait(false);

			stopwatch.Stop();

			if (!httpResponse.IsSuccessStatusCode) {
				return GetFailed<TResponse>(httpResponse, arrived, body, stopwatch.Elapsed);
			}

			var requestId = Redact(GetRequestId(httpResponse));

			TModel? model;

			try {
				model = JsonSerializer.Deserialize<TModel>(body, _jsonSerializerOptions);
			} catch (Exception e) {
				return ResponseBase<TResponse>.FailedWith(ResponseStatus.Failed, Redact(GetExceptionDetail(e)), requestId)
											  .WithMetadata(CompleteMetadata(arrived, stopwatch.Elapsed, null, GetBodyExcerpt(body)));
			}

			var response = map(model);

			if (response is null) {
				return ResponseBase<TResponse>.Failed
											  .WithMetadata(CompleteMetadata(arrived, stopwatch.Elapsed, null, GetBodyExcerpt(body)));
			}

			response.RequestId = requestId;
			response.Metadata = CompleteMetadata(arrived, stopwatch.Elapsed, (model as Page)?.RecordCount, null);

			if (freshPollFloor.HasValue) {
				_freshPollGate.Store(_options.Key, endpoint, response, freshPollFloor.Value);
			}

			return response;
		} catch (Exception) when (cancellationToken.IsCancellationRequested) {
			//  Any failure once the caller has cancelled is the caller's doing —
			//  some runtimes surface an aborted transfer as a transport
			//  exception rather than an OperationCanceledException.
			stopwatch.Stop();

			return ResponseBase<TResponse>.Cancelled
										  .WithMetadata(CompleteMetadata(arrived, stopwatch.Elapsed, null, null));
		} catch (Exception e) when (deadline.IsCancellationRequested) {
			//  The caller's token is not cancelled (the filter above), so the
			//  deadline fired — and the same runtime caveat applies.
			stopwatch.Stop();

			return ResponseBase<TResponse>.FailedWith(ResponseStatus.TimedOut, Redact(GetExceptionDetail(e)), null)
										  .WithMetadata(CompleteMetadata(arrived, stopwatch.Elapsed, null, null));
		} catch (Exception e) {
			stopwatch.Stop();

			return ResponseBase<TResponse>.FailedWith(ResponseStatus.Failed, Redact(GetExceptionDetail(e)), null)
										  .WithMetadata(CompleteMetadata(arrived, stopwatch.Elapsed, null, null));
		} finally {
			httpResponse?.Dispose();
		}
	}

	//  ============================================================================
	//  Utilities
	//  ============================================================================

	private void AddHeaders(
		Dictionary<string, string> headers,
		HttpHeaders source) {
		foreach (var header in source) {
			var value = Redact(string.Join(", ", header.Value));

			headers[header.Key] = headers.TryGetValue(header.Key, out var existing)
				? existing + ", " + value
				: value;
		}
	}

	//  The status and every header the response carries, response and content
	//  headers alike, values comma-joined and redacted — captured the moment
	//  the response arrives, before the body is read. The request's
	//  Authorization header is never in here; only the response is read.
	private ResponseMetadata CaptureResponse(
		HttpResponseMessage response) {
		var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		AddHeaders(headers, response.Headers);

		if (response.Content is not null) {
			AddHeaders(headers, response.Content.Headers);
		}

		return new ResponseMetadata {
			Age = response.Headers.Age,
			CacheControl = Redact(response.Headers.CacheControl?.ToString()),
			Date = response.Headers.Date,
			ETag = Redact(response.Headers.ETag?.ToString()),
			Headers = headers,
			StatusCode = response.StatusCode
		};
	}

	//  The captured status and headers (if a response arrived) plus what is
	//  only known at the end of the exchange.
	private static ResponseMetadata CompleteMetadata(
		ResponseMetadata? arrived,
		TimeSpan elapsed,
		int? recordCount,
		string? bodyExcerpt) => arrived is null
		? new ResponseMetadata {
			BodyExcerpt = bodyExcerpt,
			Elapsed = elapsed,
			RecordCount = recordCount
		}
		: new ResponseMetadata {
			Age = arrived.Age,
			BodyExcerpt = bodyExcerpt,
			CacheControl = arrived.CacheControl,
			Date = arrived.Date,
			Elapsed = elapsed,
			ETag = arrived.ETag,
			Headers = arrived.Headers,
			RecordCount = recordCount,
			StatusCode = arrived.StatusCode
		};

	//  The first 512 characters of the body with every echo of the key (raw
	//  or JSON-escaped) replaced as the excerpt is built. Redacting while
	//  scanning — rather than redacting a window and then cutting it — means
	//  a replacement shorter than the key can never pull a partial echo from
	//  past the cut into the excerpt.
	private string? GetBodyExcerpt(
		byte[] body) {
		if (body.Length == 0) {
			return null;
		}

		var text = Encoding.UTF8.GetString(body);
		var forms = _options.RedactionForms;
		var excerpt = new StringBuilder(BodyExcerptLength + Redacted.Length);
		var position = 0;

		while (excerpt.Length < BodyExcerptLength && position < text.Length) {
			var (index, length) = FindKey(text, position, forms);
			var end = index < 0
				? text.Length
				: index;
			var take = Math.Min(end - position, BodyExcerptLength - excerpt.Length);

			excerpt.Append(text, position, take);

			if (index < 0 || take < end - position) {
				break;
			}

			excerpt.Append(Redacted);

			position = index + length;
		}

		if (excerpt.Length > BodyExcerptLength) {
			excerpt.Length = BodyExcerptLength;
		}

		//  Never end on half a surrogate pair.
		if (excerpt.Length > 0 && char.IsHighSurrogate(excerpt[excerpt.Length - 1])) {
			excerpt.Length--;
		}

		return excerpt.ToString();
	}

	//  Detail strings are appended to Errors after "The request has failed." —
	//  never let the raw key into a detail string (it rides the Authorization
	//  header, not the URL, so the endpoint itself is safe to echo elsewhere).
	//  Callers still pass the detail through Redact.
	private static string GetExceptionDetail(
		Exception exception) => exception.GetType().Name + ": " + exception.Message;

	//  Parse the error envelope if the body is JSON; fall back to the HTTP
	//  status detail if not. Classify the status code and capture the request
	//  id from the header or the envelope. Everything taken off the wire is
	//  redacted before it is stored.
	private TResponse GetFailed<TResponse>(
		HttpResponseMessage response,
		ResponseMetadata arrived,
		byte[] body,
		TimeSpan elapsed)
		where TResponse : ResponseBase<TResponse>, new() {
		var detail = GetStatusDetail(response);
		var requestId = GetRequestId(response);

		try {
			if (body.Length > 0) {
				var envelope = JsonSerializer.Deserialize<ErrorEnvelope>(body, _jsonSerializerOptions);

				if (envelope is not null) {
					if (envelope.Message.HasValue()) {
						detail = envelope.Code.HasValue()
							? envelope.Code + ": " + envelope.Message
							: envelope.Message;

						if (envelope.Details.HasValue()) {
							detail = detail + " (" + envelope.Details + ")";
						}
					}

					if (requestId is null && envelope.RequestId.HasValue()) {
						requestId = envelope.RequestId;
					}
				}
			}
		} catch (JsonException) {
			//  Non-JSON error body — keep the HTTP status detail.
		}

		return ResponseBase<TResponse>.FailedWith(GetResponseStatus(response), Redact(detail), Redact(requestId))
									  .WithMetadata(CompleteMetadata(arrived, elapsed, null, GetBodyExcerpt(body)));
	}

	private static string? GetRequestId(
		HttpResponseMessage response) => response.Headers.TryGetValues("X-Request-ID", out var values)
		? values.FirstOrDefault()
		: null;

	private static ResponseStatus GetResponseStatus(
		HttpResponseMessage response) => (int)response.StatusCode switch {
			400 => ResponseStatus.InvalidRequest,
			401 => ResponseStatus.Unauthorized,
			403 => ResponseStatus.Forbidden,
			404 => ResponseStatus.NotFound,
			409 => ResponseStatus.Conflict,
			410 => ResponseStatus.Gone,
			429 => ResponseStatus.RateLimited,
			500 => ResponseStatus.ServerError,
			503 => ResponseStatus.Unavailable,
			_ => ResponseStatus.Failed
		};

	private static string GetStatusDetail(
		HttpResponseMessage response) {
		//  Explicit concatenation: interpolation lowers to string.Format on
		//  netstandard2.0 and boxes the status code; Enum.ToString is also
		//  reflection-based, so the name is resolved only when needed.
		var code = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
		var name = response.ReasonPhrase;

		if (string.IsNullOrEmpty(name)) {
			name = response.StatusCode.ToString();

			//  An unnamed status code stringifies to its numeric value — don't
			//  render it twice ("HTTP 599 599").
			if (name == code) {
				return "HTTP " + code;
			}
		}

		return "HTTP " + code + " " + name;
	}

	//  The earliest echo of any redaction form at or after start; on a tie the
	//  longest form wins. (-1, 0) when there is none.
	private static (int Index, int Length) FindKey(
		string text,
		int start,
		IReadOnlyList<string> forms) {
		var index = -1;
		var length = 0;

		foreach (var form in forms) {
			var found = text.IndexOf(form, start, StringComparison.Ordinal);

			if (found < 0) {
				continue;
			}

			if (index < 0 || found < index || (found == index && form.Length > length)) {
				index = found;
				length = form.Length;
			}
		}

		return (index, length);
	}

	//  netstandard2.0's ReadAsByteArrayAsync has no cancellation overload, so
	//  the read is raced against the deadline token; when the token wins, the
	//  response is disposed to abort the transfer and the cancellation
	//  surfaces to SendAsync's classification. A missing Content (possible on
	//  .NET Framework for a bodiless response) reads as an empty body.
	private static async Task<byte[]> ReadBodyAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken) {
		if (response.Content is null) {
			return [];
		}

		var readTask = response.Content.ReadAsByteArrayAsync();

		if (!readTask.IsCompleted) {
			var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled)) {
				var winner = await Task.WhenAny(readTask, cancelled.Task).ConfigureAwait(false);

				if (winner != readTask) {
					response.Dispose();

					//  The aborted read faults once the response is gone —
					//  observe it so it never surfaces as an unobserved
					//  task exception.
					_ = readTask.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

					cancellationToken.ThrowIfCancellationRequested();
				}
			}
		}

		return await readTask.ConfigureAwait(false);
	}

	//  Stamps the endpoint on an outcome that never reached the wire (Invalid,
	//  pre-send Cancelled). The getter runs on a request that may be
	//  malformed, and IHdFleetClient members never throw — a getter that
	//  throws leaves Endpoint null.
	private TResponse WithGuardedEndpoint<TResponse>(
		TResponse response,
		RequestBase request)
		where TResponse : ResponseBase<TResponse>, new() {
		try {
			response.Endpoint = Redact(request.Endpoint);
		} catch (Exception) {
			response.Endpoint = null;
		}

		return response;
	}

	//  An error page can echo the bearer token. Every string taken off the
	//  wire passes through here before it is stored, so the raw key (or its
	//  JSON-escaped form) never reaches Errors, ResponseMetadata or an
	//  HdFleetException message. Longest form first, so an escaped form is
	//  never partly consumed by a shorter one.
	[return: NotNullIfNotNull(nameof(text))]
	private string? Redact(
		string? text) {
		if (text is null) {
			return null;
		}

		foreach (var form in _options.RedactionForms) {
			if (text.IndexOf(form, StringComparison.Ordinal) >= 0) {
				text = text.Replace(form, Redacted);
			}
		}

		return text;
	}
}
