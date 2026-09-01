import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:trippify_flutter/api_client.dart';
import 'package:trippify_flutter/l10n/generated/app_localizations.dart';
import 'package:trippify_flutter/main.dart';
import 'package:trippify_flutter/shell/signed_in_shell.dart';
import 'package:trippify_flutter/shell/workspace_screen.dart' as workspace;

import 'widget_test.dart' show FakeApi, tapText;

const _systemInfo = SystemDistributionInfo('1.0.0', 0);

Future<AppLocalizations> _l10n(WidgetTester tester) async {
  late AppLocalizations l10n;
  await tester.pumpWidget(MaterialApp(
    localizationsDelegates: [
      ...GlobalMaterialLocalizations.delegates,
      AppLocalizations.delegate,
    ],
    supportedLocales: AppLocalizations.supportedLocales,
    home: Builder(
      builder: (context) {
        l10n = AppLocalizations.of(context)!;
        return const SizedBox();
      },
    ),
  ));
  await tester.pump();
  return l10n;
}

void main() {
  testWidgets('workspace sections for a non-creator show the enrollment CTA', (
    tester,
  ) async {
    final l10n = await _l10n(tester);
    final summary = const MySummary(
      'traveler@example.com',
      'Traveler',
      null,
      [],
      false,
      'Active',
      true,
    );
    final sections = workspace.workspaceSectionsFor(summary, l10n: l10n);
    final titles = sections.map((s) => s.title).toList();
    expect(titles, contains(l10n.sectionGetStarted));
    expect(titles, isNot(contains(l10n.sectionCreatorWorkspace)));
    expect(titles, contains(l10n.sectionDiscoverPlan));
    expect(titles, isNot(contains(l10n.sectionAdministration)));
    final labels = [
      for (final s in sections) for (final e in s.entries) e.label,
    ];
    expect(labels, contains(l10n.entryBecomeCreator));
  });

  testWidgets('workspace sections for a creator include authoring, sales, and reviews', (
    tester,
  ) async {
    final l10n = await _l10n(tester);
    final summary = const MySummary(
      'creator@example.com',
      'Creator',
      null,
      [],
      true,
      'Active',
      true,
    );
    final sections = workspace.workspaceSectionsFor(summary, l10n: l10n);
    final labels = [
      for (final s in sections) for (final e in s.entries) e.label,
    ];
    expect(labels, contains(l10n.entryCreatorDashboard));
    expect(labels, contains(l10n.entryMyGuides));
    expect(labels, contains(l10n.entryPlanRoutes));
    expect(labels, contains(l10n.entryLicensePolicies));
    expect(labels, contains(l10n.entryMyLibrary));
    expect(labels, contains(l10n.entryNotifications));
  });

  testWidgets('workspace sections for an administrator surface the admin section', (
    tester,
  ) async {
    final l10n = await _l10n(tester);
    final summary = const MySummary(
      'admin@example.com',
      'Admin',
      null,
      ['Administrator'],
      false,
      'Active',
      true,
    );
    final sections = workspace.workspaceSectionsFor(summary, l10n: l10n);
    final labels = [
      for (final s in sections) for (final e in s.entries) e.label,
    ];
    expect(labels, contains(l10n.entryAdminOperations));
  });

  testWidgets('workspace sections for a tenant surface the tenant section', (
    tester,
  ) async {
    final l10n = await _l10n(tester);
    final summary = const MySummary(
      'tenant@example.com',
      'Tenant',
      null,
      ['Tenant'],
      false,
      'Active',
      true,
    );
    final sections = workspace.workspaceSectionsFor(summary, l10n: l10n);
    final labels = [
      for (final s in sections) for (final e in s.entries) e.label,
    ];
    expect(labels, contains(l10n.entryMyTenant));
    expect(labels, contains(l10n.entryAssistedImport));
  });

  testWidgets('workspace greeting falls back to the email when name is empty', (
    tester,
  ) async {
    final l10n = await _l10n(tester);
    final summary = const MySummary(
      'anon@example.com',
      '',
      null,
      [],
      false,
      'Active',
      true,
    );
    await tester.pumpWidget(MaterialApp(
      localizationsDelegates: [
        ...GlobalMaterialLocalizations.delegates,
        AppLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      home: Scaffold(
        body: workspace.WorkspaceScreen(
          summary: summary,
          onNavigate: (_) {},
          onResendVerification: () async {},
        ),
      ),
    ));
    expect(find.text(l10n.homeGreeting('anon@example.com')), findsOneWidget);
  });

  testWidgets('workspace shows an unverified email banner and resends', (
    tester,
  ) async {
    var resendCalls = 0;
    final l10n = await _l10n(tester);
    final summary = const MySummary(
      'newbie@example.com',
      'Newbie',
      null,
      [],
      false,
      'Active',
      false,
    );
    await tester.pumpWidget(MaterialApp(
      localizationsDelegates: [
        ...GlobalMaterialLocalizations.delegates,
        AppLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      home: Scaffold(
        body: workspace.WorkspaceScreen(
          summary: summary,
          onNavigate: (_) {},
          onResendVerification: () async {
            resendCalls++;
          },
        ),
      ),
    ));
    expect(find.textContaining(l10n.verifyEmailTitle), findsOneWidget);
    await tester.tap(find.widgetWithText(TextButton, l10n.verifyEmailResend));
    await tester.pumpAndSettle();
    expect(resendCalls, 1);
  });

  testWidgets(
    'access denied screen offers a safe return to the workspace',
    (tester) async {
      final l10n = await _l10n(tester);
      var returned = 0;
      await tester.pumpWidget(MaterialApp(
        localizationsDelegates: [
          ...GlobalMaterialLocalizations.delegates,
          AppLocalizations.delegate,
        ],
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(
          body: workspace.AccessDeniedScreen(
            route: '/admin/operations',
            onReturnHome: () => returned++,
          ),
        ),
      ));
      expect(find.text(l10n.accessDeniedTitle), findsOneWidget);
      expect(find.textContaining('/admin/operations'), findsOneWidget);
      await tester.tap(find.text(l10n.accessDeniedReturn));
      await tester.pumpAndSettle();
      expect(returned, 1);
    },
  );

  testWidgets('route guard denies a non-administrator admin deep link', (
    tester,
  ) async {
    final api = FakeApi(Future.value(_systemInfo));
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
    await tester.tap(find.text('My library'));
    await tester.pumpAndSettle();
    Navigator.of(
      tester.element(find.byType(Scaffold).first),
    ).pushNamed('/admin/operations');
    await tester.pumpAndSettle();
    expect(find.text('Access denied'), findsOneWidget);
    expect(find.text('Back to workspace'), findsOneWidget);
  });

  testWidgets('home renders the role-aware workspace for a creator', (
    tester,
  ) async {
    final api = FakeApi(Future.value(_systemInfo));
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
    expect(find.text('Welcome back, Creator.'), findsOneWidget);
    expect(find.text('Creator dashboard'), findsOneWidget);
    expect(find.text('Become a creator'), findsNothing);
    expect(find.text('My guides'), findsOneWidget);
  });

  testWidgets('home renders the role-aware workspace for a traveler', (
    tester,
  ) async {
    final api = FakeApi(Future.value(_systemInfo));
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
  });

  testWidgets('home renders the administrator workspace section', (
    tester,
  ) async {
    final api = FakeApi(Future.value(_systemInfo));
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
    expect(find.text('Welcome back, Admin.'), findsOneWidget);
    expect(find.text('Admin operations'), findsOneWidget);
  });

  testWidgets('long email and role list wrap inside the home summary card', (
    tester,
  ) async {
    final longEmail =
        'a.very.long.email.address.that.should.not.overflow.the.summary.card@trippify.example.com';
    final longRole =
        'Administrator · TravelAgent · Moderator · VerifiedReviewer · EarlyAccess';
    final api = FakeApi(Future.value(_systemInfo));
    await api.signInAs(
      MySummary(
        longEmail,
        'A very long display name for the home greeting',
        null,
        longRole.split(' · '),
        true,
        'Active',
        true,
      ),
    );
    await tester.pumpWidget(TrippifyApp(api: api));
    await tester.pumpAndSettle();
    final view = tester.view.physicalSize;
    tester.view.physicalSize = Size(320 * tester.view.devicePixelRatio, view.height);
    addTearDown(() => tester.view.resetPhysicalSize());
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.textContaining(longEmail), findsOneWidget);
  });

  testWidgets('workspace tiles navigate to the destination route', (
    tester,
  ) async {
    final api = FakeApi(Future.value(_systemInfo));
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
    await tapText(tester, 'My library');
    await tester.pumpAndSettle();
    expect(find.text('No purchased guides yet.'), findsOneWidget);
  });

  testWidgets(
    'signed-in shell exposes one sign-out action and no floating action button',
    (tester) async {
      final api = FakeApi(Future.value(_systemInfo));
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
      await tester.pumpWidget(MaterialApp(
        home: SignedInShell(
          api: api,
          current: SignedInDestination.create,
          onNavigate: (_) {},
        ),
      ));
      await tester.pumpAndSettle();
      expect(find.byType(FloatingActionButton), findsNothing);
      expect(find.text('Sign out'), findsNothing);
    },
  );

  testWidgets('route guard allows a creator into /guides', (tester) async {
    final api = FakeApi(Future.value(_systemInfo));
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
    Navigator.of(
      tester.element(find.byType(Scaffold).first),
    ).pushNamed('/guides');
    await tester.pumpAndSettle();
    expect(find.text('Access denied'), findsNothing);
    expect(find.text('My guides'), findsOneWidget);
  });

  testWidgets('route guard denies a non-creator into /guides', (tester) async {
    final api = FakeApi(Future.value(_systemInfo));
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
    Navigator.of(
      tester.element(find.byType(Scaffold).first),
    ).pushNamed('/guides');
    await tester.pumpAndSettle();
    expect(find.text('Access denied'), findsOneWidget);
  });
}
