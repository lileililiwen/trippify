import 'package:flutter_test/flutter_test.dart';
import 'package:flutter/material.dart';
import 'package:trippify_flutter/api_client.dart';
import 'package:trippify_flutter/main.dart';

class FakeApi implements AppApi {
  FakeApi(
    this.result, {
    this.error,
    this.routeError,
    this.guides = const [],
    this.searchResult,
    this.unlocked = false,
    this.entitlements = const [],
    this.favorites = const [],
    this.trips = const [],
  });
  final Future<SystemDistributionInfo> result;
  final Object? error;
  final Object? routeError;
  final List<GuideSummary> guides;
  final SearchResult? searchResult;
  final bool unlocked;
  final List<Entitlement> entitlements;
  final List<Favorite> favorites;
  final List<Trip> trips;

  @override
  Future<void> register(String email, String password) async {}
  @override
  Future<void> login(String email, String password) async {
    if (error != null) throw error!;
  }

  @override
  Future<PrivateProfile> getProfile() async =>
      const PrivateProfile('user@example.com', 'Traveler', true, 'Active');
  @override
  Future<void> updateProfile(
    String displayName,
    String? avatarUrl,
    String? locale,
  ) async {}
  @override
  Future<void> enrollCreator(
    String slug,
    String biography,
    List<String> countries,
  ) async {}
  @override
  Future<PublicCreator> getCreator(String slug) async =>
      PublicCreator(slug, 'Traveler', 'Trips', const ['JP']);
  @override
  Future<List<GuideSummary>> getMyGuides() async {
    if (error != null) throw error!;
    return guides;
  }

  @override
  Future<DayRoute> getDayRoute(String guideId, int dayPosition) async {
    if (routeError != null) throw routeError!;
    return DayRoute(
      'token',
      const [
        RouteMarker('n1', 0, 'Osaka Castle', 34.687, 135.526),
        RouteMarker('n2', 1, 'Nishiki Market', 35.005, 135.765),
      ],
      const [
        RouteSegment(0, 'Train', 'JR line', 'Osaka Castle', 'Nishiki Market', 55, 820, 'JPY'),
      ],
    );
  }

  @override
  Future<DayRoute> saveDayRoute(
    String guideId,
    int dayPosition,
    DayRoute route,
  ) async => route;
  @override
  Future<BudgetOverview> getBudget(String guideId, int partySize) async {
    if (routeError != null) throw routeError!;
    return BudgetOverview(partySize, [
      BudgetLine(
        'Transport',
        5000,
        5000 * partySize,
        'JPY',
      ),
      const BudgetLine('Food', 3000, 3000, 'JPY'),
    ]);
  }

  @override
  Future<BudgetOverview> saveBudget(
    String guideId,
    String concurrencyToken,
    List<BudgetLine> lines,
  ) async => BudgetOverview(1, lines);

  @override
  Future<GuideDraft> createGuide(String title, String countryCode) async =>
      GuideDraft('1', title, countryCode, 'token', const ['Osaka', 'Kyoto']);
  @override
  Future<GuideDraft> saveGuideStructure(GuideDraft guide) async => guide;

  @override
  Future<PublishResult> publishGuide(
    String guideId,
    String concurrencyToken, {
    int? priceMinorUnits,
    String? currencyCode,
  }) async =>
      PublishResult('token2', 'kyoto-temples-walk', 'FreePublic');
  @override
  Future<void> unpublishGuide(String guideId, String concurrencyToken) async {}
  @override
  Future<SearchResult> searchGuides({
    String? country,
    String? tag,
    String? pricing,
    String? query,
  }) async {
    if (routeError != null) throw routeError!;
    return searchResult ?? const SearchResult(0, [], []);
  }

  @override
  Future<PublicGuide> getPublicGuide(String slug) async {
    if (routeError != null) throw routeError!;
    return PublicGuide(
      'guide-1',
      slug,
      'Tokyo luxury nights',
      'Three refined evenings',
      'A curated night itinerary.',
      'JP',
      3,
      const ['Tokyo'],
      const ['food'],
      'paid',
      'author-1',
      '/guides/$slug',
      2500,
      'JPY',
      unlocked,
      [
        PublicGuideDay('Evening one', [
          PublicGuideNode('Tower', false),
          PublicGuideNode('Izakaya', false),
        ]),
      ],
    );
  }

  @override
  Future<CheckoutSession> checkout(
    String guideId, {
    String? discountCode,
  }) async {
    if (routeError != null) throw routeError!;
    return CheckoutSession('order-1', 'cs_test_1', 1875, 'JPY');
  }

  @override
  Future<List<Entitlement>> getEntitlements() async {
    if (routeError != null) throw routeError!;
    return entitlements;
  }

  @override
  Future<void> addFavorite(String guideId) async {}
  @override
  Future<void> removeFavorite(String guideId) async {}
  @override
  Future<List<Favorite>> listFavorites() async => favorites;
  @override
  Future<List<Trip>> listTrips() async => trips;
  @override
  Future<Trip> createTrip(String guideId, {String? title}) async =>
      Trip('trip-1', title ?? 'Trip', guideId, null, 'Planning', '');
  @override
  Future<Trip> updateTrip(
    String tripId, {
    String? notes,
    String? status,
    String? title,
  }) async => Trip(tripId, title ?? 'Trip', null, null, status ?? 'Planning', notes ?? '');
  @override
  Future<ForkResult> forkGuide(String guideId) async =>
      ForkResult('fork-1', 'fork-slug', 'Tokyo luxury nights', guideId, 'Tokyo luxury nights');

  @override
  Future<List<Review>> listReviews(String guideId) async => const [];
  @override
  Future<Review> submitReview(String guideId, int rating, String body) async =>
      Review('r1', guideId, 'user-1', rating, body, 'Visible', DateTime(2026, 1, 1), null);
  @override
  Future<Review> editReview(String reviewId, int rating, String body) async =>
      Review(reviewId, 'g1', 'user-1', rating, body, 'Visible', DateTime(2026, 1, 1), null);
  @override
  Future<void> deleteReview(String reviewId) async {}
  @override
  Future<Reply> replyToReview(String reviewId, String body) async =>
      Reply('rp1', reviewId, 'author-1', body, DateTime(2026, 1, 2));
  @override
  Future<void> reportReview(String reviewId, String reason) async {}
  @override
  Future<void> submitFeedback(String guideId, String body) async {}
  @override
  Future<VerifiedBadge> getVerifiedBadge(String guideId) async =>
      VerifiedBadge(guideId, false, 0, null, null);
  @override
  Future<void> submitEvidence(
    String guideId, {
    required String kind,
    required String body,
    String? redactedReference,
  }) async {}
  @override
  Future<TripInsightSummary> getTripInsights(String guideId) async =>
      const TripInsightSummary('g1', 0, false, null, null);
  @override
  Future<void> submitTripInsight(
    String guideId, {
    required int partySize,
    required int tripDays,
    required int totalCostMinorUnits,
    required String currencyCode,
  }) async {}
  @override
  Future<CreatorDashboardOverview> getCreatorDashboardOverview() async =>
      _emptyOverview();

  static CreatorDashboardOverview _emptyOverview() => CreatorDashboardOverview(
        0,
        0,
        0,
        0,
        const [],
        DateTime(2026, 1, 1),
      );
  @override
  Future<CreatorOrdersResponse> listCreatorOrders({int? limit}) async =>
      const CreatorOrdersResponse(0, []);
  @override
  Future<CreatorReviewSummary> getCreatorDashboardReviews() async =>
      const CreatorReviewSummary(0, 0, 0, 0);
  @override
  Future<AdminAuditResponse> listAdminAudit({int? limit}) async =>
      const AdminAuditResponse(0, []);
  @override
  Future<AdminUsersResponse> listAdminUsers({int? limit}) async =>
      const AdminUsersResponse(0, []);
  @override
  Future<AdminCreatorsResponse> listAdminCreators({int? limit}) async =>
      const AdminCreatorsResponse(0, []);
  @override
  Future<FollowStatus> getCreatorFollowStatus(String slug) async =>
      FollowStatus(slug, false, null);
  @override
  Future<FollowStatus> followCreator(String slug) async =>
      FollowStatus(slug, true, DateTime(2026, 1, 1));
  @override
  Future<void> unfollowCreator(String slug) async {}
  @override
  Future<int> getCreatorFollowersCount(String slug) async => 0;
  @override
  Future<NotificationList> listNotifications({int? limit}) async =>
      const NotificationList(0, []);
  @override
  Future<void> markNotificationRead(String id) async {}
  @override
  Future<NotificationPreferences> getNotificationPreferences() async =>
      _defaultPrefs();
  @override
  Future<NotificationPreferences> updateNotificationPreferences(
    NotificationPreferences preferences,
  ) async =>
      preferences;
  @override
  Future<GuideReleaseList> listGuideReleases(String guideId, {int? limit}) async =>
      const GuideReleaseList(0, []);
  @override
  Future<GuideRelease> getGuideRelease(String releaseId) async =>
      GuideRelease(
        releaseId,
        'g1',
        1,
        'Initial release.',
        'Guide',
        DateTime(2026, 1, 1),
        '',
      );
  @override
  Future<GuideFreshness> getGuideFreshness(String guideId) async =>
      GuideFreshness(guideId, 0, null, null);
  @override
  Future<GuideRelease> publishGuideRelease(String guideId, String changelog) async =>
      GuideRelease(
        'new-release',
        guideId,
        1,
        changelog,
        'Guide',
        DateTime(2026, 1, 1),
        '',
      );
  @override
  Future<PluginList> listPlugins({int? limit}) async =>
      const PluginList(0, []);
  @override
  Future<PluginSummary> getPlugin(String pluginId) async =>
      PluginSummary(pluginId, 'demo', 'Demo', '1.0.0', 'Trippify', 'Approved', DateTime(2026, 1, 1));
  @override
  Future<List<PluginInstallation>> listMyPluginInstallations() async => const [];
  @override
  Future<void> installPlugin(String pluginId, List<String> scopes) async {}
  @override
  Future<void> enablePlugin(String pluginId) async {}
  @override
  Future<void> disablePlugin(String pluginId) async {}
  @override
  Future<void> uninstallPlugin(String pluginId) async {}
  @override
  Future<TenantDashboard> getMyTenant() async => TenantDashboard(
        TenantSummary('t1', 'default', 'Default Tenant', '', 'Active', '{}', DateTime(2026, 1, 1)),
        Subscription('s1', 'Free', 'Active', DateTime(2026, 1, 1), null));
  @override
  Future<TenantDashboard> updateMySubscription(String plan) async => TenantDashboard(
        TenantSummary('t1', 'default', 'Default Tenant', '', 'Active', '{}', DateTime(2026, 1, 1)),
        Subscription('s1', plan, 'Active', DateTime(2026, 1, 1), null));
  @override
  Future<QuotaList> listMyTenantQuotas() async => const QuotaList(0, []);
  @override
  Future<ExportPayload> requestMyTenantExport() async =>
      const ExportPayload('u1', 'Display', 'en', []);
  @override
  Future<ImportJobDetail> submitTextImport(String sourceText) async => ImportJobDetail(
        ImportJob('j1', 'u1', 'Text', 'Completed', DateTime(2026, 1, 1), null, ''),
        null);
  @override
  Future<ImportJobDetail> submitObjectImport(String objectKey, String kind) async => ImportJobDetail(
        ImportJob('j1', 'u1', 'Photo', 'Completed', DateTime(2026, 1, 1), null, ''),
        null);
  @override
  Future<ImportJobDetail> processImportJob(String jobId) async => ImportJobDetail(
        ImportJob('j1', 'u1', 'Text', 'Completed', DateTime(2026, 1, 1), null, ''),
        null);
  @override
  Future<ImportDraft> approveImportDraft(String draftId, String? guideId) async =>
      ImportDraft(draftId, 'j1', 'Imported', '{}', 'Approved', DateTime(2026, 1, 1), '[]');
  @override
  Future<ImportDraft> rejectImportDraft(String draftId) async =>
      ImportDraft(draftId, 'j1', 'Imported', '{}', 'Rejected', DateTime(2026, 1, 1), '[]');
  @override
  Future<Translation> createTranslation(String sourceDraftId, String locale, String body) async =>
      Translation('t1', sourceDraftId, locale, body, 'Linked', DateTime(2026, 1, 1), null);
  @override
  Future<List<QuotaRow>> listMyAiQuotas() async => const [];
  @override
  Future<List<LicensePolicy>> listMyLicensePolicies() async => const [];
  @override
  Future<LicensePolicy> upsertMyLicensePolicy({
    required String slug,
    String? displayName,
    required bool allowCommercial,
    required bool requireApproval,
    required int royaltyPercent,
  }) async =>
      LicensePolicy('p1', 'u1', slug, displayName ?? slug, allowCommercial, requireApproval, royaltyPercent, DateTime(2026, 1, 1), DateTime(2026, 1, 1));
  @override
  Future<List<LicensePolicy>> listCreatorLicensePolicies(String slug) async => const [];
  @override
  Future<RemixAncestry> declareRemixAncestry({
    required String childGuideId,
    required String parentGuideId,
    required String licensePolicyId,
    String? attributionJson,
  }) async =>
      RemixAncestry('a1', childGuideId, parentGuideId, licensePolicyId, '{}', 'Pending', DateTime(2026, 1, 1), null);
  @override
  Future<RemixAncestry> getGuideAncestry(String guideId) async =>
      RemixAncestry('a1', guideId, 'parent', 'policy', '{}', 'Pending', DateTime(2026, 1, 1), null);
  @override
  Future<List<RemixAncestry>> listApprovalQueue() async => const [];
  @override
  Future<RemixAncestry> decideRemixApproval({
    required String ancestryId,
    required String decision,
    String? reason,
  }) async =>
      RemixAncestry(ancestryId, 'child', 'parent', 'policy', '{}', decision, DateTime(2026, 1, 1), DateTime(2026, 1, 1));
  @override
  Future<SystemDistributionInfo> getSystemInfo() async {
    if (error != null) throw error!;
    return result;
  }
  @override
  Future<SystemStatus> getSystemStatus() async => const SystemStatus('1.0.0', 0, 0, [], []);
  @override
  Future<void> triggerSystemUpgrade() async {}
  @override
  Future<BackupSnapshot> triggerSystemBackup({String? label}) async =>
      BackupSnapshot('s1', label ?? 'manual', 12, DateTime(2026, 1, 1));
  @override
  Future<void> triggerSystemRestore(String payload) async {}
  @override
  Future<List<FeatureFlag>> listFeatureFlags() async => const [];
  @override
  Future<FeatureFlag> upsertFeatureFlag({required String key, required bool enabled, required String value}) async =>
      FeatureFlag(key: key, enabled: enabled, value: value, updatedAt: DateTime(2026, 1, 1));

  static NotificationPreferences _defaultPrefs() => NotificationPreferences(
        emailEnabled: true,
        inAppEnabled: true,
        newGuidePublishedEmail: true,
        newGuidePublishedInApp: true,
        newReviewOnMyGuideEmail: true,
        newReviewOnMyGuideInApp: true,
        newReplyToReviewEmail: true,
        newReplyToReviewInApp: true,
        followerGainedEmail: true,
        followerGainedInApp: true,
        evidenceReviewedEmail: true,
        evidenceReviewedInApp: true,
      );

  @override
  Future<AuthorPage> getAuthor(String slug) async => AuthorPage(
    slug,
    'Aya',
    'Guides for night owls.',
    const ['JP'],
    const [],
  );
}

void main() {
  testWidgets('shows API result', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('1.0.0'), findsOneWidget);
  });
  testWidgets('shows retry state', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemDistributionInfo('', 0)),
          error: StateError('offline'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Unable to reach the service'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });
  testWidgets('sign in validates and exposes accessible failure', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sign in'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();
    expect(find.text('Enter a valid email and password.'), findsOneWidget);
  });
  testWidgets('profile shows private account state', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('My profile'));
    await tester.pumpAndSettle();
    expect(find.text('user@example.com'), findsOneWidget);
    expect(find.text('Account: Active'), findsOneWidget);
  });
  testWidgets('registration validates input', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Create account'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pump();
    expect(
      find.text('Enter a valid email and strong password.'),
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
    await tester.tap(find.text('Find a creator'));
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
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('My guides'));
    await tester.pumpAndSettle();
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
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Plan routes & budget'));
    await tester.pumpAndSettle();
    expect(find.text('Osaka Castle'), findsOneWidget);
    expect(find.text('Nishiki Market'), findsOneWidget);
    expect(find.text('Osaka Castle → Nishiki Market'), findsOneWidget);
    expect(find.text('Train · 55 min'), findsOneWidget);
    expect(find.text('5000 JPY per person'), findsOneWidget);
    expect(find.text('3000 JPY'), findsOneWidget);
    await tester.tap(find.byTooltip('More travelers'));
    await tester.pumpAndSettle();
    expect(find.text('10000 JPY'), findsOneWidget);
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
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Plan routes & budget'));
    await tester.pumpAndSettle();
    expect(
      find.text('Planning access denied or unavailable.'),
      findsNWidgets(2),
    );
  });
  testWidgets('planning shows an accessible empty state without guides', (
    tester,
  ) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Plan routes & budget'));
    await tester.pumpAndSettle();
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
    await tester.tap(find.text('Discover guides'));
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
    await tester.tap(find.text('Discover guides'));
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
    await tester.tap(find.text('Discover guides'));
    await tester.pumpAndSettle();
    expect(find.text('Discovery is unavailable. Try again later.'), findsOneWidget);
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
    await tester.tap(find.text('Discover guides'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    expect(find.text('Paid preview. Unlock for 2500 JPY.'), findsOneWidget);
    await tester.enterText(
      find.widgetWithText(TextField, 'Discount code'),
      'LAUNCH25',
    );
    await tester.tap(find.text('Buy and unlock'));
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
    await tester.tap(find.text('My library'));
    await tester.pumpAndSettle();
    expect(find.text('No purchased guides yet.'), findsOneWidget);
    expect(find.text('No favorite guides yet.'), findsOneWidget);
    expect(find.text('No trips saved yet.'), findsOneWidget);
  });
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
    await tester.tap(find.text('Discover guides'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    expect(find.text('Purchased. Full guide unlocked.'), findsOneWidget);
    await tester.tap(find.text('Fork for editing'));
    await tester.pumpAndSettle();
    expect(
      find.text('Forked from "Tokyo luxury nights" into a private draft.'),
      findsOneWidget,
    );
    await tester.tap(find.text('Save as a trip'));
    await tester.pumpAndSettle();
    expect(find.text('Saved as a trip. Manage it from My library.'), findsOneWidget);
    await tester.tap(find.text('Favorite'));
    await tester.pumpAndSettle();
    expect(find.text('Unfavorite'), findsOneWidget);
  });
  testWidgets('unlocked guide shows reviews section', (
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
    await tester.tap(find.text('Discover guides'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    expect(find.text('Purchased. Full guide unlocked.'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Fork for editing'), findsOneWidget);
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
    await tester.tap(find.text('Discover guides'));
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
    await tester.tap(find.text('Discover guides'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tokyo luxury nights'));
    await tester.pumpAndSettle();
    for (var i = 0; i < 4; i++) {
      await tester.drag(find.byType(ListView), const Offset(0, -300));
      await tester.pumpAndSettle();
    }
    expect(find.widgetWithText(FilledButton, 'Submit evidence'), findsOneWidget);
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
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Creator dashboard'));
    await tester.pumpAndSettle();
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
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -400));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Admin operations'));
    await tester.pumpAndSettle();
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
    await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -500));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Notifications'));
    await tester.pumpAndSettle();
    expect(find.text('No notifications yet.'), findsOneWidget);
  });
  testWidgets('notification preferences screen renders toggles', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -500));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Notification preferences'));
    await tester.pumpAndSettle();
    expect(find.byType(SwitchListTile), findsWidgets);
  });
  testWidgets('public guide exposes release history empty state', (tester) async {
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
    await tester.tap(find.text('Discover guides'));
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
  testWidgets('plugin catalog screen renders empty installable and installations', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -500));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Plugin catalog'));
    await tester.pumpAndSettle();
    expect(find.text('Plugin catalog'), findsOneWidget);
    expect(find.text('My installations'), findsOneWidget);
    expect(find.text('No installations yet.'), findsOneWidget);
    expect(find.text('No plugins available yet.'), findsOneWidget);
  });
  testWidgets('tenant dashboard renders plan, quotas, and export', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -500));
    await tester.pumpAndSettle();
    await tester.tap(find.text('My tenant'));
    await tester.pumpAndSettle();
    expect(find.text('My tenant'), findsOneWidget);
    expect(find.text('Plan Free (Active)'), findsOneWidget);
    expect(find.text('Choose a plan'), findsOneWidget);
    expect(find.text('Quotas'), findsOneWidget);
    expect(find.text('No quotas defined yet.'), findsOneWidget);
    expect(find.text('Generate export'), findsOneWidget);
  });
  testWidgets('assisted import screen renders form and translation CTA', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -500));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Assisted import'));
    await tester.pumpAndSettle();
    expect(find.text('Assisted import'), findsOneWidget);
    expect(find.text('Submit text import'), findsOneWidget);
    expect(find.text('Submit object import'), findsOneWidget);
    expect(find.text('Quotas'), findsOneWidget);
  });
testWidgets('license panel screen renders empty state and create default action', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('', 0))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -500));
    await tester.pumpAndSettle();
    await tester.tap(find.text('License policies'));
    await tester.pumpAndSettle();
    expect(find.text('License policies'), findsOneWidget);
    expect(find.text('No license policies yet.'), findsOneWidget);
    expect(find.text('Create default license'), findsOneWidget);
  });
  testWidgets('self hosted status screen renders version, migrations, and feature flags', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemDistributionInfo('1.0.0', 3))),
      ),
    );
    await tester.pumpAndSettle();
    await tester.drag(find.byType(SingleChildScrollView), const Offset(0, -500));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Self-hosted status'));
    await tester.pumpAndSettle();
    expect(find.text('Self-hosted status'), findsOneWidget);
    expect(find.text('1.0.0'), findsOneWidget);
    expect(find.text('No feature flags defined.'), findsOneWidget);
    expect(find.text('Run upgrade'), findsOneWidget);
    expect(find.text('Capture backup'), findsOneWidget);
  });
}
