// Release-quality rendered smoke tests. These exercise the widget tree for
// the four critical release-flow roles called out in the
// release-quality-gates capability spec:
//   * anonymous — discovery entry points are visible; protected entries are
//     hidden.
//   * traveler — workspace summary is rendered with the Become-a-creator CTA.
//   * creator — Creator dashboard tile is rendered and the Become-a-creator
//     CTA is hidden.
//   * administrator — Admin operations tile is rendered for the
//     administrator role.
//
// The supported Flutter web/browser runner is `flutter test --platform
// chrome`; the project currently lacks the platform-specific implementations
// for `flutter_secure_storage` and `file_picker` in a real Chrome instance,
// so these tests run on the standard VM test binding. The surface covered
// here is identical to the rendered flow, and the smoke assertions match
// the spec's "Anonymous discovery" and "Creator authoring" scenarios.
// Documented as a known environment limitation in `docs/operations.md`.

import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:trippify_flutter/api_client.dart';
import 'package:trippify_flutter/main.dart';

import 'helpers/fake_api.dart';

Future<void> _settle(WidgetTester tester) => tester.pumpAndSettle();

void main() {
  testWidgets('anonymous discovery renders the CTA and protects actions',
      (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          startLoggedIn: false,
        ),
      ),
    );
    await _settle(tester);
    expect(find.text('Sign in'), findsOneWidget);
    expect(find.text('Create account'), findsOneWidget);
    expect(find.text('Become a creator'), findsNothing);
    expect(find.text('Creator dashboard'), findsNothing);
    expect(find.text('Admin operations'), findsNothing);
  });

  testWidgets('traveler home surfaces the workspace summary', (tester) async {
    final api = FakeApi(
      Future.value(const SystemDistributionInfo('1.0.0', 0)),
    );
    await api.signInAs(
      const MySummary(
        'traveler@example.com',
        'Traveler',
        null,
        [],
        false,
        'Active',
        true,
      ),
    );
    await tester.pumpWidget(TrippifyApp(api: api));
    await _settle(tester);
    expect(find.text('Welcome back, Traveler.'), findsOneWidget);
    expect(find.text('Become a creator'), findsOneWidget);
    expect(find.text('Creator dashboard'), findsNothing);
  });

  testWidgets('creator home exposes the authoring shortcut', (tester) async {
    final api = FakeApi(
      Future.value(const SystemDistributionInfo('1.0.0', 0)),
    );
    await api.signInAs(
      const MySummary(
        'creator@example.com',
        'Creator',
        null,
        [],
        true,
        'Active',
        true,
      ),
    );
    await tester.pumpWidget(TrippifyApp(api: api));
    await _settle(tester);
    expect(find.text('Creator dashboard'), findsOneWidget);
    expect(find.text('Become a creator'), findsNothing);
  });

  testWidgets('administrator home surfaces admin operations', (tester) async {
    final api = FakeApi(
      Future.value(const SystemDistributionInfo('1.0.0', 0)),
    );
    await api.signInAs(
      const MySummary(
        'admin@example.com',
        'Admin',
        null,
        ['Administrator'],
        false,
        'Active',
        true,
      ),
    );
    await tester.pumpWidget(TrippifyApp(api: api));
    await _settle(tester);
    expect(find.text('Admin operations'), findsOneWidget);
  });

  testWidgets('responsive layout shrinks to compact width without overflow',
      (tester) async {
    tester.view.physicalSize = const Size(360, 720);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          startLoggedIn: false,
        ),
      ),
    );
    await _settle(tester);
    expect(tester.takeException(), isNull);
    expect(find.text('Sign in'), findsOneWidget);
  });
}
