import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:trippify_flutter/api_client.dart';

class _Recorder {
  final List<http.Request> requests = [];
}

http.Client _client(
  _Recorder recorder,
  Future<http.Response> Function(http.Request) respond,
) {
  return MockClient((http.Request request) async {
    recorder.requests.add(request);
    return respond(request);
  });
}

ApiClient _clientFor(
  _Recorder recorder,
  Future<http.Response> Function(http.Request) respond, {
  String? token,
}) {
  return ApiClient(
    Uri.parse('https://api.test'),
    client: _client(recorder, respond),
    tokenStore: MemoryTokenStore(token),
  );
}

void main() {
  group('PATCH dispatch', () {
    test(
      'updateTrip sends authenticated PATCH with JSON body and parses the trip',
      () async {
        final recorder = _Recorder();
        final api = _clientFor(recorder, (req) async {
          if (req.method != 'PATCH') {
            return http.Response('method was ${req.method}', 500);
          }
          expect(req.url.path, '/api/v1/library/trips/trip-1');
          expect(req.headers['Authorization'], 'Bearer test-token');
          expect(req.headers['Content-Type'], contains('application/json'));
          final body = jsonDecode(req.body) as Map<String, dynamic>;
          expect(body, {'title': 'Renamed', 'notes': 'Updated'});
          return http.Response(
            jsonEncode({
              'id': 'trip-1',
              'title': 'Renamed',
              'sourceGuideId': 'g1',
              'forkedGuideId': null,
              'status': 'Planning',
              'notes': 'Updated',
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        }, token: 'test-token');

        final trip = await api.updateTrip(
          'trip-1',
          title: 'Renamed',
          notes: 'Updated',
        );

        expect(trip.id, 'trip-1');
        expect(trip.title, 'Renamed');
        expect(trip.notes, 'Updated');
        expect(recorder.requests, hasLength(1));
        expect(recorder.requests.single.method, 'PATCH');
      },
    );

    test('updateTrip on 401 clears the local token and rethrows as ApiException', () async {
      final recorder = _Recorder();
      final store = MemoryTokenStore('will-be-cleared');
      final api = ApiClient(
        Uri.parse('https://api.test'),
        client: _client(recorder, (req) async {
          return http.Response(
            jsonEncode({'message': 'expired'}),
            401,
            headers: {'content-type': 'application/json'},
          );
        }),
        tokenStore: store,
      );

      await expectLater(
        api.updateTrip('trip-1', notes: 'x'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 401)
              .having((e) => e.message, 'message', contains('expired')),
        ),
      );
      expect(
        await store.read(),
        isNull,
        reason:
            '401 must clear the local credential so the next call is anonymous',
      );
      expect(api.isLoggedIn, isFalse);
    });

    test('updateTrip on 409 surfaces a conflict error to the caller', () async {
      final recorder = _Recorder();
      final api = _clientFor(recorder, (req) async {
        return http.Response(
          jsonEncode({'detail': 'The trip changed since you loaded it.'}),
          409,
          headers: {'content-type': 'application/json'},
        );
      }, token: 'test-token');

      await expectLater(
        api.updateTrip('trip-1', status: 'Done'),
        throwsA(
          isA<ApiException>().having((e) => e.statusCode, 'statusCode', 409),
        ),
      );
    });

    test(
      'updateTrip on 400 returns validation error without retrying',
      () async {
        final recorder = _Recorder();
        final api = _clientFor(recorder, (req) async {
          return http.Response(
            jsonEncode({
              'errors': {
                'title': ['Title is required'],
              },
            }),
            400,
            headers: {'content-type': 'application/json'},
          );
        }, token: 'test-token');

        await expectLater(
          api.updateTrip('trip-1', title: ''),
          throwsA(
            isA<ApiException>().having((e) => e.statusCode, 'statusCode', 400),
          ),
        );
        expect(recorder.requests, hasLength(1));
      },
    );

    test('updateTrip on 429 returns a rate-limit error', () async {
      final recorder = _Recorder();
      final api = _clientFor(recorder, (req) async {
        return http.Response('slow down', 429);
      }, token: 'test-token');

      await expectLater(
        api.updateTrip('trip-1', notes: 'x'),
        throwsA(
          isA<ApiException>().having((e) => e.statusCode, 'statusCode', 429),
        ),
      );
    });
  });

  group('AppError taxonomy', () {
    test('maps every declared HTTP status to the right AppError', () {
      expect(toAppError(const ApiException(400, '')), AppError.validation);
      expect(toAppError(const ApiException(422, '')), AppError.validation);
      expect(toAppError(const ApiException(401, '')), AppError.unauthorized);
      expect(toAppError(const ApiException(403, '')), AppError.unauthorized);
      expect(toAppError(const ApiException(404, '')), AppError.notFound);
      expect(toAppError(const ApiException(409, '')), AppError.conflict);
      expect(toAppError(const ApiException(402, '')), AppError.paymentDeclined);
      expect(toAppError(const ApiException(429, '')), AppError.rateLimit);
      expect(toAppError(const ApiException(500, '')), AppError.server);
      expect(toAppError(const ApiException(503, '')), AppError.server);
      expect(toAppError(const ApiException(418, '')), AppError.unknown);
    });

    test(
      'maps socket, client, timeout, and host-lookup failures to network',
      () {
        expect(
          toAppError(Exception('SocketException: refused')),
          AppError.network,
        );
        expect(
          toAppError(Exception('ClientException: connect timeout')),
          AppError.network,
        );
        expect(
          toAppError(Exception('TimeoutException after 0:00:30')),
          AppError.network,
        );
        expect(
          toAppError(Exception('Failed host lookup: api.test')),
          AppError.network,
        );
        expect(toAppError(Exception('Connection refused')), AppError.network);
      },
    );

    test('exposes a safe actionable message for every AppError', () {
      for (final err in AppError.values) {
        final message = appErrorMessage(err);
        expect(message, isNotEmpty, reason: 'AppError.$err has no copy');
        expect(
          message,
          isNot(
            contains(
              RegExp(r'\b(bearer|token|secret)\b', caseSensitive: false),
            ),
          ),
          reason: 'AppError.$err copy leaks raw credentials',
        );
      }
    });
  });

  group('Cancellation and disposal', () {
    test('listTrips surfaces CancelledApiCall when the token is cancelled mid-flight', () async {
      final recorder = _Recorder();
      final cancel = CancelToken();
      final api = _clientFor(recorder, (req) async {
        await Future<void>.delayed(const Duration(milliseconds: 50));
        return http.Response('[]', 200);
      }, token: 't');

      final future = api.listTrips(cancelToken: cancel);
      cancel.cancel();
      await expectLater(future, throwsA(isA<CancelledApiCall>()));
    });

    test(
      'searchGuides is retryable and the retry issues a fresh GET',
      () async {
        final recorder = _Recorder();
        int calls = 0;
        final api = _clientFor(recorder, (req) async {
          calls++;
          if (calls == 1) {
            return http.Response('boom', 503);
          }
          return http.Response(
            jsonEncode({
              'total': 0,
              'tagFacets': const <Map<String, dynamic>>[],
              'items': const <Map<String, dynamic>>[],
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        }, token: 't');

        await expectLater(api.searchGuides(), throwsA(isA<ApiException>()));
        final result = await api.searchGuides();
        expect(result.total, 0);
        expect(calls, 2);
        expect(recorder.requests, hasLength(2));
        expect(recorder.requests.every((r) => r.method == 'GET'), isTrue);
      },
    );

    test(
      'checkout endpoint does not duplicate the request when retried manually',
      () async {
        final recorder = _Recorder();
        int calls = 0;
        final api = _clientFor(recorder, (req) async {
          calls++;
          if (calls == 1) {
            return http.Response('boom', 502);
          }
          return http.Response(
            jsonEncode({
              'orderId': 'o1',
              'checkoutReference': 'cs1',
              'checkoutUrl': 'https://pay.test/cs1',
              'amountMinorUnits': 1000,
              'currencyCode': 'JPY',
              'providerName': 'stripe',
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        }, token: 't');

        await expectLater(
          api.checkout('g1', idempotencyKey: 'k1'),
          throwsA(isA<ApiException>()),
        );
        final session = await api.checkout('g1', idempotencyKey: 'k2');
        expect(session.amountMinorUnits, 1000);
        // Retrying must not be implicit; each call is a deliberate new request.
        expect(calls, 2);
      },
    );
  });
}
