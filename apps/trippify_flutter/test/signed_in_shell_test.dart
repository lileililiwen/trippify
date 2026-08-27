import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:trippify_flutter/api_client.dart';
import 'package:trippify_flutter/shell/signed_in_shell.dart';

import 'widget_test.dart' show FakeApi;

void main() {
  testWidgets('SignedInShell shows 4 destinations for non-creator', (
    tester,
  ) async {
    final api = FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0)));
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
    await tester.pumpWidget(
      MaterialApp(
        home: SignedInShell(
          api: api,
          current: SignedInDestination.home,
          onNavigate: (_) {},
          initialSummary: const MySummary(
            'traveler@example.com',
            'Traveler',
            null,
            [],
            false,
            'Active',
            true,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byType(NavigationDestination), findsNWidgets(4));
    expect(find.text('Home'), findsOneWidget);
    expect(find.text('Discover'), findsOneWidget);
    expect(find.text('Library'), findsOneWidget);
    expect(find.text('Plan'), findsOneWidget);
  });

  testWidgets(
    'SignedInShell shows 4 destinations for creator with Create tab',
    (tester) async {
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
      await tester.pumpWidget(
        MaterialApp(
          home: SignedInShell(
            api: api,
            current: SignedInDestination.home,
            onNavigate: (_) {},
            initialSummary: const MySummary(
              'creator@example.com',
              'Creator',
              null,
              [],
              true,
              'Active',
              true,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byType(NavigationDestination), findsNWidgets(4));
      expect(find.text('Create'), findsOneWidget);
      expect(find.text('Plan'), findsNothing);
    },
  );

  testWidgets(
    'creator guide route does not use an out-of-range loading index',
    (tester) async {
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

      await tester.pumpWidget(
        MaterialApp(
          home: SignedInShell(
            api: api,
            current: SignedInDestination.create,
            onNavigate: (_) {},
          ),
        ),
      );

      expect(tester.takeException(), isNull);
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.text('Create'), findsOneWidget);
      expect(
        tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex,
        3,
      );
    },
  );

  testWidgets('SignedInShell invokes onNavigate when a destination is tapped', (
    tester,
  ) async {
    final api = FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0)));
    await api.signInAs(
      const MySummary('t@example.com', 'T', null, [], false, 'Active', true),
    );
    SignedInDestination? navigated;
    await tester.pumpWidget(
      MaterialApp(
        home: SignedInShell(
          api: api,
          current: SignedInDestination.home,
          onNavigate: (d) => navigated = d,
          initialSummary: const MySummary(
            't@example.com',
            'T',
            null,
            [],
            false,
            'Active',
            true,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Discover'));
    await tester.pumpAndSettle();
    expect(navigated, SignedInDestination.discover);
  });

  testWidgets('role-specific final destination emits the mapped enum', (
    tester,
  ) async {
    final api = FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0)));
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
    SignedInDestination? navigated;
    await tester.pumpWidget(
      MaterialApp(
        home: SignedInShell(
          api: api,
          current: SignedInDestination.home,
          onNavigate: (destination) => navigated = destination,
          initialSummary: const MySummary(
            'creator@example.com',
            'Creator',
            null,
            [],
            true,
            'Active',
            true,
          ),
        ),
      ),
    );

    await tester.tap(find.text('Create'));
    await tester.pumpAndSettle();

    expect(navigated, SignedInDestination.create);
  });
}
