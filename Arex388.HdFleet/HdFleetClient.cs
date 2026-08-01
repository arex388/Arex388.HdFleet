using Arex388.HdFleet.Converters;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Arex388.HdFleet;

internal sealed class HdFleetClient(
	IServiceProvider services,
	HttpClient? httpClient = null,
	HdFleetClientOptions? options = null) :
	IHdFleetClient {
	private static readonly JsonSerializerOptions _jsonSerializerOptions = new() {
		Converters = {
			new DriverJsonConverter(),
			new ErrorEnvelopeJsonConverter(),
			new LocationJsonConverter(),
			new PageJsonConverterFactory(),
			new VehicleJsonConverter()
		}
	};

	private readonly IValidator<ListDrivers.Request> _listDriversRequestValidator = services.GetRequiredService<IValidator<ListDrivers.Request>>();
	private readonly IValidator<ListLatestLocations.Request> _listLatestLocationsRequestValidator = services.GetRequiredService<IValidator<ListLatestLocations.Request>>();
	private readonly IValidator<ListLocationHistory.Request> _listLocationHistoryRequestValidator = services.GetRequiredService<IValidator<ListLocationHistory.Request>>();
	private readonly IValidator<ListVehicles.Request> _listVehiclesRequestValidator = services.GetRequiredService<IValidator<ListVehicles.Request>>();
	private readonly FreshPollGate _freshPollGate = new(services.GetRequiredService<IMemoryCache>());
	private readonly HttpClient _httpClient = httpClient ?? services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IHdFleetClient));
	private readonly HdFleetClientOptions _options = options ?? services.GetRequiredService<HdFleetClientOptions>();

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
	//  pre-check → optional validation → fresh-poll gate check → GET with a
	//  per-request Authorization header (fully buffered) → deserialize → map →
	//  gate store. A null map result means "the API returned nothing usable"
	//  (Failed); list mappers never return null, coalescing a null payload to
	//  empty. A null freshPollFloor skips the gate — cursor continuations and
	//  un-annotated endpoints are never gated.
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
			return ResponseBase<TResponse>.Cancelled;
		}

		if (validator is not null) {
			// ReSharper disable once MethodHasAsyncOverloadWithCancellation
			var validationResult = validator.Validate(request);

			if (!validationResult.IsValid) {
				return ResponseBase<TResponse>.Invalid(validationResult);
			}
		}

		var endpoint = request.Endpoint;

		if (freshPollFloor.HasValue) {
			var cached = _freshPollGate.GetCached<TResponse>(_options.Key, endpoint);

			if (cached is not null) {
				return cached.WithFromCache();
			}
		}

		try {
			//  The Authorization header is set per request, never on
			//  DefaultRequestHeaders — a pooled client with a default header is
			//  a cross-account data leak.
			using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);

			httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Key);

			//  The default ResponseContentRead completion buffers the full body
			//  inside SendAsync, governed by the caller's token and
			//  HttpClient.Timeout on every runtime — netstandard2.0's
			//  ReadAsByteArrayAsync has no cancellation overload, so buffering
			//  after a headers-read completion could not be cancelled.
			using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

			if (!httpResponse.IsSuccessStatusCode) {
				return await GetFailedAsync<TResponse>(httpResponse).ConfigureAwait(false);
			}

			var requestId = GetRequestId(httpResponse);

			//  Deserialize from the completed buffer, never a stream: async
			//  stream deserialization can invoke the converters on partially
			//  buffered JSON, where their unknown-property Skip() throws. A
			//  complete buffer makes isFinalBlock true, so Skip is always safe.
			var body = await httpResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
			var model = JsonSerializer.Deserialize<TModel>(body, _jsonSerializerOptions);
			var response = map(model);

			if (response is null) {
				return ResponseBase<TResponse>.Failed;
			}

			response.RequestId = requestId;

			if (freshPollFloor.HasValue) {
				_freshPollGate.Store(_options.Key, endpoint, response, freshPollFloor.Value);
			}

			return response;
		} catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
			return ResponseBase<TResponse>.Cancelled;
		} catch (Exception e) {
			return ResponseBase<TResponse>.FailedWith(ResponseStatus.Failed, GetExceptionDetail(e), null);
		}
	}

	//  ============================================================================
	//  Utilities
	//  ============================================================================

	//  Detail strings are appended to Errors after "The request has failed." —
	//  never let the raw key into a detail string (it rides the Authorization
	//  header, not the URL, so the endpoint itself is safe to echo elsewhere).
	private static string GetExceptionDetail(
		Exception exception) => exception.GetType().Name + ": " + exception.Message;

	//  Parse the error envelope if the body is JSON; fall back to the HTTP
	//  status detail if not. Classify the status code and capture the request
	//  id from the header or the envelope.
	private static async Task<TResponse> GetFailedAsync<TResponse>(
		HttpResponseMessage response)
		where TResponse : ResponseBase<TResponse>, new() {
		var detail = GetStatusDetail(response);
		var requestId = GetRequestId(response);

		try {
			var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

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

		return ResponseBase<TResponse>.FailedWith(GetResponseStatus(response), detail, requestId);
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
}
