# Design: Align Flutter API methods and error states

## Client contract

`ApiClient._request` supports `PATCH` with the same JSON, auth, response,
and 401 token-clearing rules as the other mutating methods. Unsupported
methods remain programmer errors. The dispatcher accepts an optional
`CancelToken` that screens can pass to abort an in-flight call without
mutating disposed state. The dispatcher also exposes a `_withCancel`
helper that races the in-flight `http.Response` against the cancel
completer and surfaces a `CancelledApiCall` exception so widgets can
short-circuit the future.

`listTrips` and `updateTrip` accept an optional `CancelToken`. Screens
that own the trip list (e.g. `_TripListSection`) keep the token as state
and cancel it in `dispose`. The trip edit dialog also passes the
section's cancel token so a user who dismisses the dialog mid-save
cannot trigger a state mutation after disposal.

## Error taxonomy

`AppError` now covers every declared HTTP status and the new
transport-level cancel path. The mapping in `toAppError`:

| HTTP / signal | AppError |
| --- | --- |
| 400, 422 | `validation` |
| 401, 403 | `unauthorized` |
| 404 | `notFound` |
| 409 | `conflict` |
| 402 | `paymentDeclined` |
| 429 | `rateLimit` |
| 5xx | `server` |
| `SocketException`, `ClientException`, `TimeoutException`, `Failed host lookup`, `Connection refused` | `network` |
| `CancelledApiCall` | `cancelled` |
| any other status | `unknown` |

`appErrorMessage` returns actionable copy for every member of the enum
and never embeds bearer tokens, raw server bodies, or provider names.
The trip edit surface renders a `_TripStatusBanner` with a Reload button
when the response is `conflict`; the banner is wrapped in
`Semantics(liveRegion: true)` so screen readers announce the new
state.

## Screens

`LibraryScreen` now exposes a tap-to-edit interaction for the trip list.
A user taps a trip, the `_TripEditDialog` collects new notes and status,
and the section calls `api.updateTrip`. A 409 response is mapped to
`AppError.conflict` and rendered as a reload banner; the list is
re-fetched on demand. The 401 path falls through to the existing
`SessionController._onTokenChanged` listener, which clears the local
credential and reverts the shell to the anonymous phase. A widget
test in `test/widget_test.dart` proves both branches.

## Verification

Wire-level tests in `test/api_client_request_test.dart` exercise
`MockClient` against `ApiClient` to prove:
- `updateTrip` sends one authenticated PATCH with the JSON body and
  parses the trip;
- a 401 response clears the local token and rethrows `ApiException`;
- a 409 response surfaces as `ApiException(409, ...)` so the screen
  can map it to `AppError.conflict`;
- a 400 response is returned as `ApiException(400, ...)` and is not
  retried automatically;
- a 429 response is returned as `ApiException(429, ...)`;
- `listTrips` honours a `CancelToken` and throws `CancelledApiCall`
  when the token is cancelled mid-flight;
- `searchGuides` and `checkout` do not silently retry; each caller
  issues a fresh request.

A widget test in `test/widget_test.dart` covers the trip edit flow,
the 409 conflict banner with its Reload button, and the 401 → anonymous
side effect. The 32 widget tests that were previously skipped are now
either unskipped (the sign-out flow) or carry a concrete skip reason
that names the prerequisite change. The wire-level
`api_client_request_test.dart` covers the contract for retry and
cancellation that the skipped widget tests asserted at the screen
layer.
