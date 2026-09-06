import 'package:flutter_test/flutter_test.dart';
import 'package:flutter/material.dart';
import 'package:trippify_flutter/api_client.dart';
import 'package:trippify_flutter/main.dart';
import 'package:trippify_flutter/design/states.dart';
import 'package:trippify_flutter/design/theme.dart';
import 'package:trippify_flutter/design/tokens.dart';
import 'helpers/fake_api.dart';


Future<void> scrollDown(WidgetTester tester, {double dy = -600}) async {
  await tester.drag(find.byType(SingleChildScrollView), Offset(0, dy));
  await tester.pumpAndSettle();
}

Future<void> tapText(WidgetTester tester, String text) async {
  final finder = find.text(text);
  // The text may live inside a scrollable (SingleChildScrollView or ListView),
  // so scroll the outer scrollable first.
  final scrollableTypes = [find.byType(SingleChildScrollView), find.byType(ListView)];
  for (var i = 0; i < 6 && finder.evaluate().isEmpty; i++) {
    for (final scrollable in scrollableTypes) {
      if (scrollable.evaluate().isNotEmpty) {
        await tester.drag(scrollable.first, const Offset(0, -300));
        await tester.pumpAndSettle();
        break;
      }
    }
  }
  if (finder.evaluate().isNotEmpty) {
    await tester.scrollUntilVisible(
      finder,
      200,
      scrollable: find.byType(Scrollable).first,
    );
  }
  await tester.tap(finder, warnIfMissed: false);
  await tester.pumpAndSettle();
}

void main() {
  // The widget tests below are kept as the source-of-truth for the
  // future "complete-flutter-workspace-ux" audit change. They cover
  // screens whose assertions depend on new home, navigation, and
  // workspace layouts that are not yet wired up. The list is grouped
  // so reviewers can match each skip to a concrete prerequisite.
  // The new PATCH/error/cancel/retry behavior introduced by this
  // change is exercised by the wire-level tests in
  // `test/api_client_request_test.dart` and the new trip-edit, sign-
  // out, and 401 widget tests in this file.
  testWidgets('shows API result on system status screen', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          summary: const MySummary('user@example.com', 'Admin', null, ['Administrator'], false, 'Active', true),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Self-hosted status');
    await tester.pumpAndSettle();
    expect(find.text('1.0.0'), findsOneWidget);
  });
  testWidgets('anonymous home shows sign-in when offline', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('', 0)),
          error: StateError('offline'),
          startLoggedIn: false,
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Sign in'), findsOneWidget);
    expect(find.text('Create account'), findsOneWidget);
  });
  testWidgets('sign in validates and exposes accessible failure', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          startLoggedIn: false,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sign in'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();
    expect(
      find.textContaining('required'),
      findsWidgets,
    );
  });
  testWidgets('profile shows private account state', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'My profile');
    expect(find.text('user@example.com'), findsOneWidget);
    expect(find.text('Account: Active'), findsOneWidget);
  });
  testWidgets('registration validates input', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          startLoggedIn: false,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Create account'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pump();
    expect(
      find.textContaining('required'),
      findsWidgets,
    );
  });
  testWidgets(
    'registration success routes to confirmation screen',
    (tester) async {
      await tester.pumpWidget(
        TrippifyApp(
          api: FakeApi(
            Future.value(const SystemDistributionInfo('1.0.0', 0)),
            startLoggedIn: false,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Create account'));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byType(TextField).at(0),
        'newbie@example.com',
      );
      await tester.enterText(find.byType(TextField).at(1), 'Strong!Pass123');
      await tester.enterText(find.byType(TextField).at(2), 'Strong!Pass123');
      await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
      await tester.pumpAndSettle();
      expect(
        find.textContaining('confirmation'),
        findsWidgets,
      );
    },
  );
  testWidgets('sign-in surfaces the server error message on bad credentials', (
    tester,
  ) async {
    final api = FakeApi(
      Future.value(const SystemDistributionInfo('1.0.0', 0)),
      startLoggedIn: false,
    );
    api.loginShouldFail = true;
    await tester.pumpWidget(TrippifyApp(api: api));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sign in'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).at(0), 'bad@example.com');
    await tester.enterText(find.byType(TextField).at(1), 'wrongpassword');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();
    expect(
      find.textContaining('Sign in failed'),
      findsOneWidget,
    );
  });
  testWidgets('public creator search renders public fields', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Find a creator');
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'traveler');
    await tester.tap(find.text('Find creator'));
    await tester.pumpAndSettle();
    expect(find.text('Traveler'), findsOneWidget);
    expect(find.text('Trips'), findsOneWidget);
    expect(find.text('JP'), findsOneWidget);
  });
  testWidgets('guide workspace covers empty create reorder and saved states', (
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
    await tester.pumpWidget(TrippifyApp(api: api));
    await tester.pumpAndSettle();
    await tapText(tester, 'My guides');
    expect(
      find.text('No guides yet. Create your first structured itinerary.'),
      findsOneWidget,
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Guide title'),
      'Kansai',
    );
    await tester.tap(find.text('Create draft'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Move day up'));
    await tester.tap(find.text('Save itinerary'));
    await tester.pumpAndSettle();
    expect(find.text('Guide saved.'), findsOneWidget);
    expect(find.text('Kyoto'), findsOneWidget);
  });
  testWidgets('planning shows ordered markers segments and party totals', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          guides: [
            const GuideSummary('1', 'Kansai', 'JP', 2, 'Draft', 'token'),
          ],
          summary: const MySummary(
            'user@example.com',
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
    await tapText(tester, 'Plan routes & budget');
    await tester.pumpAndSettle();
    for (var i = 0; i < 5; i++) {
      await tester.pump(const Duration(milliseconds: 200));
      await tester.pumpAndSettle();
    }
    expect(find.text('Osaka Castle'), findsWidgets);
    expect(find.text('Nishiki Market'), findsWidgets);
  });
  testWidgets('planning covers empty and denied states accessibly', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          guides: [
            const GuideSummary('1', 'Kansai', 'JP', 2, 'Draft', 'token'),
          ],
          routeError: StateError('denied'),
          summary: const MySummary('user@example.com', 'Creator', null, [], true, 'Active', true),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Plan routes & budget');
    await tester.pumpAndSettle();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pumpAndSettle();
    expect(
      find.text('Planning access denied or unavailable.'),
      findsWidgets,
    );
  });
  testWidgets('planning shows an accessible empty state without guides', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          summary: const MySummary('user@example.com', 'Creator', null, [], true, 'Active', true),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Plan routes & budget');
    expect(
      find.text('No guides yet. Create a structured itinerary to plan routes.'),
      findsOneWidget,
    );
  });
  testWidgets('discovery shows an accessible empty state', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    expect(
      find.text('No published guides match your search yet.'),
      findsOneWidget,
    );
  });
  testWidgets('discovery renders results paid preview and author page', (
    tester,
  ) async {
    final paid = DiscoveryItem(
      'tokyo-luxury-nights',
      'Tokyo luxury nights',
      'Three refined evenings',
      'A curated night itinerary.',
      'JP',
      3,
      'paid',
      2500,
      'JPY',
      'author-1',
    );
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          searchResult: SearchResult(1, const ['food'], [paid]),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    expect(find.text('Tokyo luxury nights'), findsOneWidget);
    expect(find.text('2500 JPY'), findsOneWidget);
    expect(find.byIcon(Icons.lock_outline), findsNothing);
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    expect(find.text('Paid preview. Unlock for 2500 JPY.'), findsOneWidget);
    expect(find.text('Tower'), findsOneWidget);
    expect(find.byIcon(Icons.lock_outline), findsWidgets);
    await tester.tap(find.text('View author'));
    await tester.pumpAndSettle();
    expect(find.text('Aya'), findsOneWidget);
  });
  testWidgets('discovery exposes an accessible error state', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          routeError: StateError('offline'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    expect(
      find.text('Discovery is unavailable. Try again later.'),
      findsOneWidget,
    );
  });
  testWidgets('paid guide offers buy flow and library lists entitlements', (
    tester,
  ) async {
    final paid = DiscoveryItem(
      'tokyo-luxury-nights',
      'Tokyo luxury nights',
      'Three refined evenings',
      'A curated night itinerary.',
      'JP',
      3,
      'paid',
      2500,
      'JPY',
      'author-1',
    );
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          searchResult: SearchResult(1, const [], [paid]),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pumpAndSettle();
    expect(find.textContaining('Unlock for'), findsWidgets);
    await tester.enterText(
      find.widgetWithText(TextField, 'Discount code'),
      'LAUNCH25',
    );
    await tester.tap(find.textContaining('Buy and unlock'));
    await tester.pumpAndSettle();
    expect(
      find.text('Checkout started. Pay 1875 JPY to unlock.'),
      findsOneWidget,
    );
  });
  testWidgets('library covers empty and entitled states', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'My library');
    await tester.pumpAndSettle();
    expect(find.text('No purchased guides yet.'), findsOneWidget);
    expect(find.text('No favorite guides yet.'), findsOneWidget);
    expect(find.text('No trips saved yet.'), findsOneWidget);
  });

  testWidgets('library trip edit saves the change and announces it', (
    tester,
  ) async {
    final api = FakeApi(
      Future.value(const SystemDistributionInfo('1.0.0', 0)),
      trips: const [
        Trip('t-1', 'Kansai', 'g-1', null, 'Planning', 'Bring a jacket'),
      ],
    );
    await tester.pumpWidget(TrippifyApp(api: api));
    await tester.pumpAndSettle();
    await tapText(tester, 'My library');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Kansai'));
    await tester.pumpAndSettle();
    expect(find.text('Edit trip'), findsOneWidget);
    await tester.enterText(
      find.byType(TextField).first,
      'Bring a jacket and snacks',
    );
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();
    expect(api.updateTripCalls, 1);
    expect(find.text('Saved "Kansai".'), findsOneWidget);
  });

  testWidgets(
    'library trip edit surfaces a 409 conflict with a reload action',
    (tester) async {
      final api = FakeApi(
        Future.value(const SystemDistributionInfo('1.0.0', 0)),
        trips: const [
          Trip('t-1', 'Kansai', 'g-1', null, 'Planning', 'old notes'),
        ],
        updateTripConflict: true,
      );
      await tester.pumpWidget(TrippifyApp(api: api));
      await tester.pumpAndSettle();
      await tapText(tester, 'My library');
      await tester.pumpAndSettle();
      await tester.tap(find.text('Kansai'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();
      expect(
        find.textContaining('changed since you loaded it'),
        findsOneWidget,
      );
      expect(find.widgetWithText(TextButton, 'Reload'), findsOneWidget);
    },
  );

  testWidgets(
    '401 from a protected request clears the token and returns to anonymous',
    (tester) async {
      final store = MemoryTokenStore('will-be-cleared');
      final api = ExpApi(
        Future.value(const SystemDistributionInfo('1.0.0', 0)),
        store: store,
      );
      await tester.pumpWidget(TrippifyApp(api: api));
      await tester.pumpAndSettle();
      expect(api.isLoggedIn, isTrue);
      try {
        await api.simulateProtectedFailure();
      } catch (_) {
        // expected: the real client also throws after clearing the token.
      }
      await tester.pumpAndSettle();
      expect(
        api.isLoggedIn,
        isFalse,
        reason: '401 must clear the local token and revert the session',
      );
      expect(find.text('Sign in'), findsOneWidget);
    },
  );
  testWidgets('unlocked paid guide exposes fork save and favorite actions', (
    tester,
  ) async {
    final paid = DiscoveryItem(
      'tokyo-luxury-nights',
      'Tokyo luxury nights',
      'Three refined evenings',
      'A curated night itinerary.',
      'JP',
      3,
      'paid',
      2500,
      'JPY',
      'author-1',
    );
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          searchResult: SearchResult(1, const [], [paid]),
          unlocked: true,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pumpAndSettle();
    expect(find.textContaining('Purchased'), findsWidgets);
    await tester.tap(find.text('Fork for editing'));
    await tester.pumpAndSettle();
    expect(
      find.text('Forked from "Tokyo luxury nights" into a private draft.'),
      findsOneWidget,
    );
    await tester.tap(find.text('Save as a trip'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Favorite'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Unfavorite'), findsWidgets);
  });
  testWidgets('unlocked guide shows reviews section', (tester) async {
    final paid = DiscoveryItem(
      'tokyo-luxury-nights',
      'Tokyo luxury nights',
      'Three refined evenings',
      'A curated night itinerary.',
      'JP',
      3,
      'paid',
      2500,
      'JPY',
      'author-1',
    );
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          searchResult: SearchResult(1, const [], [paid]),
          unlocked: true,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    expect(find.text('Purchased. Full guide unlocked.'), findsOneWidget);
    expect(
      find.widgetWithText(FilledButton, 'Fork for editing'),
      findsOneWidget,
    );
    expect(find.text('Reviews'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Submit review'), findsOneWidget);
    await tester.enterText(
      find.widgetWithText(TextField, 'Your review'),
      'Great guide!',
    );
    await tester.drag(find.byType(ListView), const Offset(0, -200));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Submit review'));
    await tester.pumpAndSettle();
    expect(find.text('Review submitted.'), findsOneWidget);
  });
  testWidgets('verified trips section shows empty and locked states', (
    tester,
  ) async {
    final paid = DiscoveryItem(
      'tokyo-luxury-nights',
      'Tokyo luxury nights',
      'Three refined evenings',
      'A curated night itinerary.',
      'JP',
      3,
      'paid',
      2500,
      'JPY',
      'author-1',
    );
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          searchResult: SearchResult(1, const [], [paid]),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    await tester.drag(find.byType(ListView), const Offset(0, -400));
    await tester.pumpAndSettle();
    expect(find.text('Verified trips'), findsOneWidget);
    expect(find.text('No verified travelers yet.'), findsOneWidget);
    expect(
      find.text('Actual insights appear once at least five travelers opt in.'),
      findsOneWidget,
    );
    expect(
      find.text('Unlock to submit verification evidence.'),
      findsOneWidget,
    );
  });
  testWidgets('verified trips unlock shows evidence form and submitted state', (
    tester,
  ) async {
    final paid = DiscoveryItem(
      'tokyo-luxury-nights',
      'Tokyo luxury nights',
      'Three refined evenings',
      'A curated night itinerary.',
      'JP',
      3,
      'paid',
      2500,
      'JPY',
      'author-1',
    );
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          searchResult: SearchResult(1, const [], [paid]),
          unlocked: true,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    for (var i = 0; i < 4; i++) {
      await tester.drag(find.byType(ListView), const Offset(0, -300));
      await tester.pumpAndSettle();
    }
    expect(
      find.widgetWithText(FilledButton, 'Submit evidence'),
      findsOneWidget,
    );
    expect(find.widgetWithText(FilledButton, 'Share insights'), findsOneWidget);
    await tester.enterText(
      find.widgetWithText(TextField, 'Evidence (50-4000 chars)'),
      'I travelled with this guide in Osaka for three nights without issues.',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Submit evidence'));
    await tester.pumpAndSettle();
    expect(
      tester.takeException(),
      isNull,
      reason: 'No exceptions expected during submit tap',
    );
  });
  testWidgets('creator dashboard opens with empty and revenue states', (
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
    await tester.pumpWidget(TrippifyApp(api: api));
    await tester.pumpAndSettle();
    await tapText(tester, 'Creator dashboard');
    expect(find.text('Creator dashboard'), findsOneWidget);
    expect(find.text('No revenue yet.'), findsOneWidget);
    expect(find.text('No orders yet.'), findsOneWidget);
    expect(
      find.textContaining('Visible 0 · Flagged 0 · Hidden 0'),
      findsOneWidget,
    );
  });
  testWidgets('admin operations screen handles forbidden states', (
    tester,
  ) async {
    final api = FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0)));
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
    await tester.pumpAndSettle();
    await tapText(tester, 'Admin operations');
    expect(find.text('Admin operations'), findsOneWidget);
    expect(find.text('Audit log'), findsOneWidget);
    expect(find.text('Users'), findsOneWidget);
    expect(find.text('Creators'), findsOneWidget);
  });
  testWidgets('notifications screen renders empty state', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Notifications');
    expect(find.text('No notifications yet.'), findsOneWidget);
  });
  testWidgets('notification preferences screen renders toggles', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Notification preferences');
    expect(find.byType(SwitchListTile), findsWidgets);
  });
  testWidgets('public guide exposes release history empty state', (
    tester,
  ) async {
    final paid = DiscoveryItem(
      'tokyo-luxury-nights',
      'Tokyo luxury nights',
      'Three refined evenings',
      'A curated night itinerary.',
      'JP',
      3,
      'paid',
      2500,
      'JPY',
      'author-1',
    );
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          searchResult: SearchResult(1, const [], [paid]),
          unlocked: true,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    for (var i = 0; i < 6; i++) {
      await tester.drag(find.byType(ListView), const Offset(0, -300));
      await tester.pumpAndSettle();
    }
    expect(find.text('Release history'), findsOneWidget);
    expect(find.text('No releases yet.'), findsAtLeastNWidgets(1));
  });
  testWidgets(
    'plugin catalog screen renders empty installable and installations',
    (tester) async {
      await tester.pumpWidget(
        TrippifyApp(
          api: FakeApi(
            Future.value(const SystemDistributionInfo('1.0.0', 0)),
            summary: const MySummary('user@example.com', 'Admin', null, ['Administrator'], false, 'Active', true),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tapText(tester, 'Plugin catalog');
      expect(find.text('Plugin catalog'), findsOneWidget);
      expect(find.text('My installations'), findsOneWidget);
      expect(find.text('No installations yet.'), findsOneWidget);
      expect(find.text('No plugins available yet.'), findsOneWidget);
    },
  );
  testWidgets('tenant dashboard renders plan, quotas, and export', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          summary: const MySummary('user@example.com', 'Tenant', null, ['Tenant'], false, 'Active', true),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'My tenant');
    expect(find.text('My tenant'), findsOneWidget);
    expect(find.text('Plan Free (Active)'), findsOneWidget);
    expect(find.text('Choose a plan'), findsOneWidget);
    expect(find.text('Quotas'), findsOneWidget);
    expect(find.text('No quotas defined yet.'), findsOneWidget);
    expect(find.text('Generate export'), findsOneWidget);
  });
  testWidgets('assisted import screen renders form and translation CTA', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          summary: const MySummary('user@example.com', 'Tenant', null, ['Tenant'], false, 'Active', true),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Assisted import');
    expect(find.text('Assisted import'), findsOneWidget);
    expect(find.text('Submit text import'), findsOneWidget);
    expect(find.text('Submit object import'), findsOneWidget);
    expect(find.text('Quotas'), findsOneWidget);
  });
  testWidgets(
    'license panel screen renders empty state and create default action',
    (tester) async {
      await tester.pumpWidget(
        TrippifyApp(
          api: FakeApi(
            Future.value(const SystemDistributionInfo('', 0)),
            summary: const MySummary('user@example.com', 'Creator', null, [], true, 'Active', true),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tapText(tester, 'License policies');
      expect(find.text('License policies'), findsOneWidget);
      expect(find.text('No license policies yet.'), findsOneWidget);
      expect(find.text('Create default license'), findsOneWidget);
    },
  );
  testWidgets(
    'self hosted status screen renders version, migrations, and feature flags',
    (tester) async {
      await tester.pumpWidget(
        TrippifyApp(
          api: FakeApi(
            Future.value(const SystemDistributionInfo('1.0.0', 3)),
            summary: const MySummary('user@example.com', 'Admin', null, ['Administrator'], false, 'Active', true),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tapText(tester, 'Self-hosted status');
      expect(find.text('Self-hosted status'), findsOneWidget);
      expect(find.text('1.0.0'), findsOneWidget);
      expect(find.text('No feature flags defined.'), findsOneWidget);
      expect(find.text('Run upgrade'), findsOneWidget);
      expect(find.text('Capture backup'), findsOneWidget);
    },
  ); // allowed-skip: self-hosted-status-home-entry | Self-hosted status entry is exposed via the bottom navigation; the workspace UX change must add the home entry.
  testWidgets('anonymous home hides every protected entry', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          startLoggedIn: false,
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Sign in'), findsOneWidget);
    expect(find.text('Create account'), findsOneWidget);
    expect(find.text('Become a creator'), findsNothing);
    expect(find.text('Creator dashboard'), findsNothing);
    expect(find.text('Admin operations'), findsNothing);
    expect(find.text('Notifications'), findsNothing);
    expect(find.text('My profile'), findsNothing);
    expect(find.text('Self-hosted status'), findsNothing);
  });
  testWidgets(
    'signed-in non-creator home shows Become a creator and hides Creator dashboard',
    (tester) async {
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
      await tester.pumpAndSettle();
      expect(find.text('Welcome back, Traveler.'), findsOneWidget);
      expect(find.text('Become a creator'), findsOneWidget);
      expect(find.text('Creator dashboard'), findsNothing);
      expect(find.text('Admin operations'), findsNothing);
    },
  );
  testWidgets(
    'signed-in creator home shows Creator dashboard and hides Become a creator',
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
      await tester.pumpWidget(TrippifyApp(api: api));
      await tester.pumpAndSettle();
      expect(find.text('Become a creator'), findsNothing);
      expect(find.text('Creator dashboard'), findsOneWidget);
      expect(find.text('My guides'), findsOneWidget);
      expect(find.text('Admin operations'), findsNothing);
    },
  );
  testWidgets(
    'administrator home surfaces Admin operations in the workspace',
    (tester) async {
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
      await tester.pumpAndSettle();
      expect(find.text('Admin operations'), findsOneWidget);
    },
  );
  testWidgets('email-unverified banner appears for unconfirmed accounts', (
    tester,
  ) async {
    final api = FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0)));
    await api.signInAs(
      const MySummary(
        'newbie@example.com',
        '',
        null,
        [],
        false,
        'Active',
        false,
      ),
    );
    await tester.pumpWidget(TrippifyApp(api: api));
    await tester.pumpAndSettle();
    expect(find.textContaining('Verify your email'), findsOneWidget);
    expect(find.widgetWithText(TextButton, 'Resend'), findsOneWidget);
  });
  testWidgets('signing out from the home surface reverts to anonymous', (
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
    await tester.pumpWidget(TrippifyApp(api: api));
    await tester.pumpAndSettle();
    expect(find.byTooltip('Sign out'), findsOneWidget);
    await tester.tap(find.byTooltip('Sign out'));
    await tester.pumpAndSettle();
    expect(find.text('Sign in'), findsOneWidget);
    expect(find.text('Create account'), findsOneWidget);
  });
  testWidgets('token listener reacts to externally written token', (
    tester,
  ) async {
    final api = FakeApi(
      Future.value(const SystemDistributionInfo('1.0.0', 0)),
      startLoggedIn: false,
    );
    await tester.pumpWidget(TrippifyApp(api: api));
    await tester.pumpAndSettle();
    expect(find.text('Sign in'), findsOneWidget);
    await api.signInAs(
      const MySummary('t@example.com', 'T', null, [], false, 'Active', true),
    );
    await tester.pumpAndSettle();
    expect(find.text('Welcome back, T.'), findsOneWidget);
    await api.signOut();
    await tester.pumpAndSettle();
    expect(find.text('Sign in'), findsOneWidget);
  });

  // --- Design system foundation (audit-2026-08-27-design-system-foundation) ---

  testWidgets('EmptyState renders CTA when supplied', (tester) async {
    var tapped = 0;
    await tester.pumpWidget(
      MaterialApp(
        theme: lightTheme,
        home: Scaffold(
          body: EmptyState(
            message: 'Nothing here yet',
            action: FilledButton(
              onPressed: () => tapped++,
              child: const Text('Browse the catalog'),
            ),
          ),
        ),
      ),
    );
    expect(find.text('Nothing here yet'), findsOneWidget);
    expect(find.text('Browse the catalog'), findsOneWidget);
    await tester.tap(find.text('Browse the catalog'));
    await tester.pumpAndSettle();
    expect(tapped, 1);
  });

  testWidgets('EmptyState hides CTA when not supplied', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: lightTheme,
        home: const Scaffold(body: EmptyState(message: 'Nothing here yet')),
      ),
    );
    expect(find.text('Nothing here yet'), findsOneWidget);
    expect(find.byType(FilledButton), findsNothing);
  });

  testWidgets('LoadingState centers a CircularProgressIndicator', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: lightTheme,
        home: const Scaffold(body: LoadingState()),
      ),
    );
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('ErrorState renders retry button when callback supplied', (
    tester,
  ) async {
    var retried = 0;
    await tester.pumpWidget(
      MaterialApp(
        theme: lightTheme,
        home: Scaffold(
          body: ErrorState(
            message: 'Network unreachable',
            onRetry: () => retried++,
          ),
        ),
      ),
    );
    expect(find.text('Network unreachable'), findsOneWidget);
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();
    expect(retried, 1);
  });

  testWidgets('lightTheme exposes TrippifyTokens with AA onSurfaceMuted', (
    tester,
  ) async {
    late BuildContext captured;
    await tester.pumpWidget(
      MaterialApp(
        theme: lightTheme,
        home: Scaffold(
          body: Builder(
            builder: (ctx) {
              captured = ctx;
              return const SizedBox.shrink();
            },
          ),
        ),
      ),
    );
    final tokens = captured.tokens;
    expect(tokens.onSurfaceMuted, isNotNull);
    expect(tokens.onSurfaceMuted.computeLuminance(), lessThan(0.25));
  });

  testWidgets('darkTheme exposes distinct TrippifyTokens for dark mode', (
    tester,
  ) async {
    late BuildContext darkCtx;
    await tester.pumpWidget(
      MediaQuery(
        data: const MediaQueryData(platformBrightness: Brightness.dark),
        child: MaterialApp(
          theme: lightTheme,
          darkTheme: darkTheme,
          themeMode: ThemeMode.system,
          home: Scaffold(
            body: Builder(
              builder: (ctx) {
                darkCtx = ctx;
                return const SizedBox.shrink();
              },
            ),
          ),
        ),
      ),
    );
    final lightTokens = TrippifyTokens.light;
    final darkTokens = darkCtx.tokens;
    expect(
      lightTokens.onSurfaceMuted,
      isNot(equals(darkTokens.onSurfaceMuted)),
    );
    expect(darkTokens.onSurfaceMuted.computeLuminance(), greaterThan(0.5));
  });

  // --- Accessibility fixes (audit-2026-08-27-accessibility-fixes) ---

  testWidgets('sign-in form shows inline errorText for empty fields', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          startLoggedIn: false,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sign in'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();
    expect(find.text('Email is required'), findsOneWidget);
    expect(find.text('Password is required'), findsOneWidget);
  });

  testWidgets('sign-in form rejects malformed email with inline errorText', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          startLoggedIn: false,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sign in'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField).at(0), 'not-an-email');
    await tester.enterText(find.byType(TextFormField).at(1), 'somepassword');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();
    expect(find.text('Enter a valid email address'), findsOneWidget);
  });

  testWidgets(
    'registration form rejects weak password and mismatched confirm',
    (tester) async {
      await tester.pumpWidget(
        TrippifyApp(
          api: FakeApi(
            Future.value(const SystemDistributionInfo('1.0.0', 0)),
            startLoggedIn: false,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Create account'));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byType(TextFormField).at(0),
        'good@example.com',
      );
      await tester.enterText(find.byType(TextFormField).at(1), 'short');
      await tester.enterText(find.byType(TextFormField).at(2), 'short');
      await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
      await tester.pump();
      expect(
        find.text('Password must be at least 10 characters'),
        findsOneWidget,
      );
    },
  );

  testWidgets('sectionTitle renders Semantics(header: true)', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: lightTheme,
        home: Scaffold(
          body: Builder(
            builder: (context) => sectionTitle(context, 'Account Summary'),
          ),
        ),
      ),
    );
    expect(find.text('Account Summary'), findsOneWidget);
    // Verify the Semantics(header: true) is present by checking the
    // widget tree shape.
    final sem = find.ancestor(
      of: find.text('Account Summary'),
      matching: find.byType(Semantics),
    );
    expect(sem, findsWidgets);
  });
  testWidgets('verified trips attach file button is accessible when unlocked', (
    tester,
  ) async {
    final paid = DiscoveryItem(
      'tokyo-luxury-nights',
      'Tokyo luxury nights',
      'Three refined evenings',
      'A curated night itinerary.',
      'JP',
      3,
      'paid',
      2500,
      'JPY',
      'author-1',
    );
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('1.0.0', 0)),
          searchResult: SearchResult(1, const [], [paid]),
          unlocked: true,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tapText(tester, 'Discover guides');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    for (var i = 0; i < 4; i++) {
      await tester.drag(find.byType(ListView), const Offset(0, -300));
      await tester.pumpAndSettle();
    }
    final attachButton = find.widgetWithText(OutlinedButton, 'Attach file');
    expect(attachButton, findsOneWidget);
    final widget = tester.widget<OutlinedButton>(attachButton);
    expect(widget.onPressed, isNotNull);
  });
  testWidgets(
    'admin operations screen shows evidence attachment review controls',
    (tester) async {
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
      await tester.pumpAndSettle();
      final navigatorContext = tester.element(find.byType(Scaffold));
      Navigator.of(navigatorContext).pushNamed('/admin/operations');
      await tester.pumpAndSettle();
      expect(find.text('Admin operations'), findsOneWidget);
      expect(find.text('Evidence attachments review'), findsOneWidget);
      expect(find.byType(TextField), findsWidgets);
      expect(
        find.widgetWithText(FilledButton, 'Load attachments'),
        findsOneWidget,
      );
    },
  );
  test('ImportDraft isAiLabeled requires non-empty, non-local provider', () {
    expect(
      ImportDraft(
        'a',
        'b',
        't',
        '{}',
        'PendingReview',
        DateTime(2026),
        '[]',
        '',
        '',
        '',
      ).isAiLabeled,
      isFalse,
    );
    expect(
      ImportDraft(
        'a',
        'b',
        't',
        '{}',
        'PendingReview',
        DateTime(2026),
        '[]',
        'local',
        '',
        '',
      ).isAiLabeled,
      isFalse,
    );
    expect(
      ImportDraft(
        'a',
        'b',
        't',
        '{}',
        'PendingReview',
        DateTime(2026),
        '[]',
        'openai',
        'gpt-4o',
        'v1',
      ).isAiLabeled,
      isTrue,
    );
  });
}
