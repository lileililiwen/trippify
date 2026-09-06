import 'package:flutter/foundation.dart';
import 'package:trippify_flutter/api_client.dart';

/// In-memory [AppApi] stub used by widget and integration tests. The stub is
/// intentionally permissive (every call returns a placeholder) so tests can
/// focus on UI behavior. Override fields to script specific responses.
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
    this.summary,
    this.initialToken,
    this.startLoggedIn = true,
    this.loginShouldFail = false,
    this.logoutError,
    this.summaryError,
    this.updateTripConflict = false,
  }) {
    if (startLoggedIn) {
      _tokens.writeSync(initialToken ?? 'fake-token');
    }
  }
  final Future<SystemDistributionInfo> result;
  final Object? error;
  final Object? routeError;
  final List<GuideSummary> guides;
  final SearchResult? searchResult;
  final bool unlocked;
  final List<Entitlement> entitlements;
  final List<Favorite> favorites;
  final List<Trip> trips;
  MySummary? summary;
  String? initialToken;
  final bool startLoggedIn;
  bool loginShouldFail;
  final Object? logoutError;
  final Object? summaryError;
  final bool updateTripConflict;
  int updateTripCalls = 0;
  int logoutCalls = 0;
  final MemoryTokenStore _tokens = MemoryTokenStore();

  @override
  ValueListenable<String?> get tokens => _tokens.listenable;
  @override
  bool get isLoggedIn => (tokens.value ?? '').isNotEmpty;

  Future<void> signInAs(MySummary s, {String token = 'fake-token'}) async {
    summary = s;
    await _tokens.write(token);
  }

  Future<void> signOut() async => _tokens.write(null);

  @override
  Future<void> register(String email, String password) async {}
  @override
  Future<void> login(String email, String password) async {
    if (loginShouldFail) {
      throw const ApiException(
        401,
        'Email or password is incorrect, or the account has not been confirmed.',
      );
    }
    if (error != null) throw error!;
    await _tokens.write(initialToken ?? 'fake-token');
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
        RouteMarker('n1', 0, 'Osaka Castle', 34.687, 135.526, 'Manual', null),
        RouteMarker(
          'n2',
          1,
          'Nishiki Market',
          35.005,
          135.765,
          'Resolved',
          'Local geocoder',
        ),
      ],
      const [
        RouteSegment(
          0,
          'Train',
          'JR line',
          'Osaka Castle',
          'Nishiki Market',
          55,
          820,
          'JPY',
        ),
      ],
      'Local geocoder',
    );
  }

  @override
  Future<DayRoute> saveDayRoute(
    String guideId,
    int dayPosition,
    DayRoute route,
  ) async =>
      route;
  @override
  Future<BudgetOverview> getBudget(String guideId, int partySize) async {
    if (routeError != null) throw routeError!;
    return BudgetOverview(partySize, [
      BudgetLine('Transport', 5000, 5000 * partySize, 'JPY'),
      const BudgetLine('Food', 3000, 3000, 'JPY'),
    ]);
  }

  @override
  Future<BudgetOverview> saveBudget(
    String guideId,
    String concurrencyToken,
    List<BudgetLine> lines,
  ) async =>
      BudgetOverview(1, lines);

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
    String? idempotencyKey,
  }) async {
    if (routeError != null) throw routeError!;
    final amount = discountCode != null && discountCode.toUpperCase() == 'LAUNCH25' ? 1875 : 2500;
    return CheckoutSession(
      'order-1',
      'cs_test_1',
      '',
      amount,
      'JPY',
      'test',
    );
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
  Future<List<Trip>> listTrips({CancelToken? cancelToken}) async => trips;
  @override
  Future<Trip> createTrip(String guideId, {String? title}) async =>
      Trip('trip-1', title ?? 'Trip', guideId, null, 'Planning', '');
  @override
  Future<Trip> updateTrip(
    String tripId, {
    String? notes,
    String? status,
    String? title,
    CancelToken? cancelToken,
  }) async {
    updateTripCalls++;
    if (updateTripConflict) {
      throw const ApiException(409, 'The trip changed since you loaded it.');
    }
    return Trip(
      tripId,
      title ?? 'Trip',
      null,
      null,
      status ?? 'Planning',
      notes ?? '',
    );
  }

  @override
  Future<ForkResult> forkGuide(String guideId) async => ForkResult(
    'fork-1',
    'fork-slug',
    'Tokyo luxury nights',
    guideId,
    'Tokyo luxury nights',
  );

  @override
  Future<List<Review>> listReviews(String guideId) async => const [];
  @override
  Future<Review> submitReview(String guideId, int rating, String body) async =>
      Review(
        'r1',
        guideId,
        'user-1',
        rating,
        body,
        'Visible',
        DateTime(2026, 1, 1),
        null,
      );
  @override
  Future<Review> editReview(String reviewId, int rating, String body) async =>
      Review(
        reviewId,
        'g1',
        'user-1',
        rating,
        body,
        'Visible',
        DateTime(2026, 1, 1),
        null,
      );
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
    List<String> attachmentIds = const [],
  }) async {}
  @override
  Future<EvidenceAttachmentStageResult> stageEvidenceAttachment({
    required String fileName,
    required String contentType,
    required int sizeBytes,
    required String sha256,
  }) async =>
      EvidenceAttachmentStageResult(
        attachmentId: 'att-1',
        storageKey: 'evidence/key',
        expiresAt: DateTime.now().add(const Duration(hours: 1)),
      );
  @override
  Future<EvidenceAttachmentSummary> uploadEvidenceAttachmentContent({
    required String attachmentId,
    required List<int> bytes,
  }) async =>
      EvidenceAttachmentSummary(
        id: attachmentId,
        fileName: 'fake.bin',
        contentType: 'application/octet-stream',
        sizeBytes: bytes.length,
        state: 'Scanning',
        createdAt: DateTime.now(),
      );
  @override
  Future<List<EvidenceAttachmentSummary>>
  listEvidenceStagedAttachments() async => const [];
  @override
  Future<List<EvidenceAttachmentSummary>> listEvidenceAttachments(
    String evidenceId,
  ) async => const [];
  @override
  Future<void> removeEvidenceAttachment(String attachmentId) async {}
  @override
  Future<EvidenceAttachmentDownload> getEvidenceAttachmentDownload(
    String attachmentId,
  ) async =>
      EvidenceAttachmentDownload(
        id: attachmentId,
        url: Uri.parse('https://example.com/download'),
        contentType: 'application/octet-stream',
        fileName: 'fake.bin',
        expiresAt: DateTime.now().add(const Duration(minutes: 5)),
      );
  @override
  Future<List<EvidenceAttachmentReviewerView>> listReviewerEvidenceAttachments(
    String evidenceId,
  ) async => const [];
  @override
  Future<EvidenceAttachmentDownload> getReviewerEvidenceAttachmentDownload(
    String attachmentId,
  ) async =>
      EvidenceAttachmentDownload(
        id: attachmentId,
        url: Uri.parse('https://example.com/download'),
        contentType: 'application/octet-stream',
        fileName: 'fake.bin',
        expiresAt: DateTime.now().add(const Duration(minutes: 5)),
      );
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
  Future<GuideReleaseList> listGuideReleases(
    String guideId, {
    int? limit,
  }) async =>
      const GuideReleaseList(0, []);
  @override
  Future<GuideRelease> getGuideRelease(String releaseId) async => GuideRelease(
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
  Future<GuideRelease> publishGuideRelease(
    String guideId,
    String changelog,
  ) async =>
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
  Future<PluginSummary> getPlugin(String pluginId) async => PluginSummary(
    pluginId,
    'demo',
    'Demo',
    '1.0.0',
    'Trippify',
    'Approved',
    DateTime(2026, 1, 1),
  );
  @override
  Future<List<PluginInstallation>> listMyPluginInstallations() async =>
      const [];
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
    TenantSummary(
      't1',
      'default',
      'Default Tenant',
      '',
      'Active',
      '{}',
      DateTime(2026, 1, 1),
    ),
    Subscription('s1', 'Free', 'Active', DateTime(2026, 1, 1), null),
  );
  @override
  Future<TenantDashboard> updateMySubscription(String plan) async =>
      TenantDashboard(
        TenantSummary(
          't1',
          'default',
          'Default Tenant',
          '',
          'Active',
          '{}',
          DateTime(2026, 1, 1),
        ),
        Subscription('s1', plan, 'Active', DateTime(2026, 1, 1), null),
      );
  @override
  Future<QuotaList> listMyTenantQuotas() async => const QuotaList(0, []);
  @override
  Future<ExportPayload> requestMyTenantExport() async =>
      const ExportPayload('u1', 'Display', 'en', []);
  @override
  Future<ImportJobDetail> submitTextImport(String sourceText) async =>
      ImportJobDetail(
        ImportJob(
          'j1',
          'u1',
          'Text',
          'Completed',
          DateTime(2026, 1, 1),
          null,
          '',
          '',
          '',
          '',
          '',
        ),
        null,
      );
  @override
  Future<ImportJobDetail> submitObjectImport(
    String objectKey,
    String kind,
  ) async =>
      ImportJobDetail(
        ImportJob(
          'j1',
          'u1',
          'Photo',
          'Completed',
          DateTime(2026, 1, 1),
          null,
          '',
          '',
          '',
          '',
          '',
        ),
        null,
      );
  @override
  Future<ImportJobDetail> processImportJob(String jobId) async =>
      ImportJobDetail(
        ImportJob(
          'j1',
          'u1',
          'Text',
          'Completed',
          DateTime(2026, 1, 1),
          null,
          '',
          '',
          '',
          '',
          '',
        ),
        null,
      );
  @override
  Future<ImportDraft> approveImportDraft(
    String draftId,
    String? guideId,
  ) async =>
      ImportDraft(
        draftId,
        'j1',
        'Imported',
        '{}',
        'Approved',
        DateTime(2026, 1, 1),
        '[]',
        '',
        '',
        '',
      );
  @override
  Future<ImportDraft> rejectImportDraft(String draftId) async => ImportDraft(
    draftId,
    'j1',
    'Imported',
    '{}',
    'Rejected',
    DateTime(2026, 1, 1),
    '[]',
    '',
    '',
    '',
  );
  @override
  Future<Translation> createTranslation(
    String sourceDraftId,
    String locale,
    String body,
  ) async =>
      Translation(
        't1',
        sourceDraftId,
        locale,
        body,
        'Linked',
        DateTime(2026, 1, 1),
        null,
        '',
        '',
        '',
      );
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
      LicensePolicy(
        'p1',
        'u1',
        slug,
        displayName ?? slug,
        allowCommercial,
        requireApproval,
        royaltyPercent,
        DateTime(2026, 1, 1),
        DateTime(2026, 1, 1),
      );
  @override
  Future<List<LicensePolicy>> listCreatorLicensePolicies(String slug) async =>
      const [];
  @override
  Future<RemixAncestry> declareRemixAncestry({
    required String childGuideId,
    required String parentGuideId,
    required String licensePolicyId,
    String? attributionJson,
  }) async =>
      RemixAncestry(
        'a1',
        childGuideId,
        parentGuideId,
        licensePolicyId,
        '{}',
        'Pending',
        DateTime(2026, 1, 1),
        null,
      );
  @override
  Future<RemixAncestry> getGuideAncestry(String guideId) async => RemixAncestry(
    'a1',
    guideId,
    'parent',
    'policy',
    '{}',
    'Pending',
    DateTime(2026, 1, 1),
    null,
  );
  @override
  Future<List<RemixAncestry>> listApprovalQueue() async => const [];
  @override
  Future<RemixAncestry> decideRemixApproval({
    required String ancestryId,
    required String decision,
    String? reason,
  }) async =>
      RemixAncestry(
        ancestryId,
        'child',
        'parent',
        'policy',
        '{}',
        decision,
        DateTime(2026, 1, 1),
        DateTime(2026, 1, 1),
      );
  @override
  Future<SystemDistributionInfo> getSystemInfo() async {
    if (error != null) throw error!;
    return result;
  }

  @override
  Future<MySummary> getMySummary() async {
    if (summaryError != null) throw summaryError!;
    return summary ??
        const MySummary(
          'user@example.com',
          'Traveler',
          null,
          [],
          false,
          'Active',
          true,
        );
  }

  @override
  Future<void> logout() async {
    logoutCalls++;
    try {
      if (logoutError != null) throw logoutError!;
    } finally {
      await _tokens.write(null);
    }
  }

  @override
  Future<void> resendVerification() async {}
  @override
  Future<SystemStatus> getSystemStatus() async =>
      const SystemStatus('1.0.0', 0, 0, [], []);
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
  Future<FeatureFlag> upsertFeatureFlag({
    required String key,
    required bool enabled,
    required String value,
  }) async =>
      FeatureFlag(
        key: key,
        enabled: enabled,
        value: value,
        updatedAt: DateTime(2026, 1, 1),
      );

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
  Future<AuthorPage> getAuthor(String slug) async =>
      AuthorPage(slug, 'Aya', 'Guides for night owls.', const ['JP'], const []);
}

/// Subclass of [FakeApi] whose [updateTrip] always fails with 401 so the
/// widget test can prove the session controller reverts to anonymous.
class ExpApi extends FakeApi {
  ExpApi(super.result, {required this.store})
    : super(initialToken: 'will-be-cleared');
  final MemoryTokenStore store;
  Future<void> simulateProtectedFailure() async {
    // Simulate the wire-level 401 by clearing the local token and
    // throwing the same exception the real ApiClient raises.
    await _tokens.write(null);
    throw const ApiException(401, 'expired');
  }
}
