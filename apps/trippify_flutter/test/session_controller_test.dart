import 'package:flutter_test/flutter_test.dart';
import 'package:trippify_flutter/api_client.dart';
import 'package:trippify_flutter/session_controller.dart';

import 'widget_test.dart' show FakeApi;

void main() {
  test('online sign out revokes once and clears the session', () async {
    final api = FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0)));
    await api.signInAs(
      const MySummary(
        'user@example.com',
        'User',
        null,
        [],
        false,
        'Active',
        true,
      ),
    );
    final controller = SessionController(api);
    await Future<void>.delayed(Duration.zero);
    expect(controller.isAuthenticated, isTrue);
    expect(await controller.signOut(), isTrue);
    expect(api.logoutCalls, 1);
    expect(api.tokens.value, isNull);
    expect(controller.state.notice, 'Signed out.');
    expect(await controller.signOut(), isTrue);
    expect(api.logoutCalls, 1);
    controller.dispose();
  });

  test(
    'offline sign out clears credentials and reports local-only revocation',
    () async {
      final api = FakeApi(
        Future.value(const SystemDistributionInfo('1.0.0', 0)),
        logoutError: const ApiException(503, 'offline'),
      );
      await api.signInAs(
        const MySummary(
          'user@example.com',
          'User',
          null,
          [],
          false,
          'Active',
          true,
        ),
      );
      final controller = SessionController(api);
      await Future<void>.delayed(Duration.zero);
      expect(await controller.signOut(), isFalse);
      expect(api.tokens.value, isNull);
      expect(controller.state.phase, SessionPhase.anonymous);
      expect(controller.state.notice, contains('could not be confirmed'));
      controller.dispose();
    },
  );

  test('expired summary session is cleared and does not retry', () async {
    final api = FakeApi(
      Future.value(const SystemDistributionInfo('1.0.0', 0)),
      summaryError: const ApiException(401, 'expired'),
    );
    final controller = SessionController(api);
    await Future<void>.delayed(Duration.zero);
    await Future<void>.delayed(Duration.zero);
    expect(api.tokens.value, isNull);
    expect(controller.state.phase, SessionPhase.anonymous);
    expect(api.logoutCalls, 1);
    controller.dispose();
  });
}
