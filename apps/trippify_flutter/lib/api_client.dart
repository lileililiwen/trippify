import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class SystemInfo {
  const SystemInfo(this.name, this.apiVersion);
  final String name;
  final String apiVersion;
}

abstract interface class AppApi {
  Future<SystemInfo> getSystemInfo();
  Future<void> register(String email, String password);
  Future<void> login(String email, String password);
  Future<PrivateProfile> getProfile();
  Future<void> updateProfile(
    String displayName,
    String? avatarUrl,
    String? locale,
  );
  Future<void> enrollCreator(
    String slug,
    String biography,
    List<String> countries,
  );
  Future<PublicCreator> getCreator(String slug);
  Future<List<GuideSummary>> getMyGuides();
  Future<GuideDraft> createGuide(String title, String countryCode);
  Future<GuideDraft> saveGuideStructure(GuideDraft guide);
  Future<DayRoute> getDayRoute(String guideId, int dayPosition);
  Future<DayRoute> saveDayRoute(
    String guideId,
    int dayPosition,
    DayRoute route,
  );
  Future<BudgetOverview> getBudget(String guideId, int partySize);
  Future<BudgetOverview> saveBudget(
    String guideId,
    String concurrencyToken,
    List<BudgetLine> lines,
  );
  Future<PublishResult> publishGuide(
    String guideId,
    String concurrencyToken, {
    int? priceMinorUnits,
    String? currencyCode,
  });
  Future<void> unpublishGuide(String guideId, String concurrencyToken);
  Future<SearchResult> searchGuides({
    String? country,
    String? tag,
    String? pricing,
    String? query,
  });
  Future<PublicGuide> getPublicGuide(String slug);
  Future<AuthorPage> getAuthor(String slug);
  Future<CheckoutSession> checkout(String guideId, {String? discountCode});
  Future<List<Entitlement>> getEntitlements();
  Future<void> addFavorite(String guideId);
  Future<void> removeFavorite(String guideId);
  Future<List<Favorite>> listFavorites();
  Future<List<Trip>> listTrips();
  Future<Trip> createTrip(String guideId, {String? title});
  Future<Trip> updateTrip(
    String tripId, {
    String? notes,
    String? status,
    String? title,
  });
  Future<ForkResult> forkGuide(String guideId);
  Future<List<Review>> listReviews(String guideId);
  Future<Review> submitReview(String guideId, int rating, String body);
  Future<Review> editReview(String reviewId, int rating, String body);
  Future<void> deleteReview(String reviewId);
  Future<Reply> replyToReview(String reviewId, String body);
  Future<void> reportReview(String reviewId, String reason);
  Future<void> submitFeedback(String guideId, String body);
  Future<VerifiedBadge> getVerifiedBadge(String guideId);
  Future<void> submitEvidence(
    String guideId, {
    required String kind,
    required String body,
    String? redactedReference,
  });
  Future<TripInsightSummary> getTripInsights(String guideId);
  Future<void> submitTripInsight(
    String guideId, {
    required int partySize,
    required int tripDays,
    required int totalCostMinorUnits,
    required String currencyCode,
  });
  Future<CreatorDashboardOverview> getCreatorDashboardOverview();
  Future<CreatorOrdersResponse> listCreatorOrders({int? limit});
  Future<CreatorReviewSummary> getCreatorDashboardReviews();
  Future<AdminAuditResponse> listAdminAudit({int? limit});
  Future<AdminUsersResponse> listAdminUsers({int? limit});
  Future<AdminCreatorsResponse> listAdminCreators({int? limit});
  Future<FollowStatus> getCreatorFollowStatus(String slug);
  Future<FollowStatus> followCreator(String slug);
  Future<void> unfollowCreator(String slug);
  Future<int> getCreatorFollowersCount(String slug);
  Future<NotificationList> listNotifications({int? limit});
  Future<void> markNotificationRead(String id);
  Future<NotificationPreferences> getNotificationPreferences();
  Future<NotificationPreferences> updateNotificationPreferences(NotificationPreferences preferences);
  Future<GuideReleaseList> listGuideReleases(String guideId, {int? limit});
  Future<GuideRelease> getGuideRelease(String releaseId);
  Future<GuideFreshness> getGuideFreshness(String guideId);
  Future<GuideRelease> publishGuideRelease(String guideId, String changelog);
  Future<PluginList> listPlugins({int? limit});
  Future<PluginSummary> getPlugin(String pluginId);
  Future<List<PluginInstallation>> listMyPluginInstallations();
  Future<void> installPlugin(String pluginId, List<String> scopes);
  Future<void> enablePlugin(String pluginId);
  Future<void> disablePlugin(String pluginId);
  Future<void> uninstallPlugin(String pluginId);
  Future<TenantDashboard> getMyTenant();
  Future<TenantDashboard> updateMySubscription(String plan);
  Future<QuotaList> listMyTenantQuotas();
  Future<ExportPayload> requestMyTenantExport();
}

class GuideSummary {
  const GuideSummary(
    this.id,
    this.title,
    this.countryCode,
    this.tripDays,
    this.lifecycle,
    this.concurrencyToken,
  );
  final String id, title, countryCode, lifecycle, concurrencyToken;
  final int tripDays;
}

class GuideDraft {
  const GuideDraft(
    this.id,
    this.title,
    this.countryCode,
    this.concurrencyToken,
    this.days,
  );
  final String id, title, countryCode, concurrencyToken;
  final List<String> days;
  GuideDraft reordered(int oldIndex, int newIndex) {
    final copy = [...days];
    final day = copy.removeAt(oldIndex);
    copy.insert(newIndex, day);
    return GuideDraft(id, title, countryCode, concurrencyToken, copy);
  }
}

class RouteMarker {
  const RouteMarker(
    this.nodeId,
    this.position,
    this.name,
    this.latitude,
    this.longitude,
  );
  final String nodeId, name;
  final int position;
  final double? latitude, longitude;
}

class RouteSegment {
  const RouteSegment(
    this.position,
    this.mode,
    this.label,
    this.originName,
    this.destinationName,
    this.durationMinutes,
    this.costPerPersonMinorUnits,
    this.currencyCode,
  );
  final int position, durationMinutes, costPerPersonMinorUnits;
  final String mode, label, originName, destinationName, currencyCode;
}

class DayRoute {
  const DayRoute(this.concurrencyToken, this.markers, this.segments);
  final String concurrencyToken;
  final List<RouteMarker> markers;
  final List<RouteSegment> segments;
}

class BudgetLine {
  const BudgetLine(
    this.category,
    this.amountPerPersonMinorUnits,
    this.partyTotalMinorUnits,
    this.currencyCode,
  );
  final String category, currencyCode;
  final int amountPerPersonMinorUnits, partyTotalMinorUnits;
}

class BudgetOverview {
  const BudgetOverview(this.partySize, this.lines);
  final int partySize;
  final List<BudgetLine> lines;
}

class PublishResult {
  const PublishResult(this.concurrencyToken, this.slug, this.lifecycle);
  final String concurrencyToken, slug, lifecycle;
}

class DiscoveryItem {
  const DiscoveryItem(
    this.slug,
    this.title,
    this.subtitle,
    this.summary,
    this.countryCode,
    this.tripDays,
    this.pricing,
    this.priceMinorUnits,
    this.currencyCode,
    this.authorSlug,
  );
  final String slug, title, subtitle, summary, countryCode, pricing, authorSlug;
  final int tripDays;
  final int? priceMinorUnits;
  final String? currencyCode;
}

class SearchResult {
  const SearchResult(this.total, this.tagFacets, this.items);
  final int total;
  final List<String> tagFacets;
  final List<DiscoveryItem> items;
}

class PublicGuideNode {
  const PublicGuideNode(this.name, this.hasDetails);
  final String name;
  final bool hasDetails;
}

class PublicGuideDay {
  const PublicGuideDay(this.title, this.nodes);
  final String title;
  final List<PublicGuideNode> nodes;
}

class PublicGuide {
  const PublicGuide(
    this.id,
    this.slug,
    this.title,
    this.subtitle,
    this.summary,
    this.countryCode,
    this.tripDays,
    this.cities,
    this.tags,
    this.pricing,
    this.authorSlug,
    this.shareUrl,
    this.priceMinorUnits,
    this.currencyCode,
    this.unlocked,
    this.days,
  );
  final String id, slug, title, subtitle, summary, countryCode, pricing, authorSlug,
    shareUrl;
  final List<String> cities, tags;
  final int tripDays;
  final int? priceMinorUnits;
  final String? currencyCode;
  final bool unlocked;
  final List<PublicGuideDay> days;
}

class CheckoutSession {
  const CheckoutSession(
    this.orderId,
    this.checkoutReference,
    this.amountMinorUnits,
    this.currencyCode,
  );
  final String orderId, checkoutReference, currencyCode;
  final int amountMinorUnits;
}

class Entitlement {
  const Entitlement(this.id, this.guideId, this.slug, this.title);
  final String id, guideId, slug, title;
}

class Favorite {
  const Favorite(
    this.guideId,
    this.slug,
    this.title,
    this.countryCode,
    this.pricing,
  );
  final String guideId, slug, title, countryCode, pricing;
}

class Trip {
  const Trip(
    this.id,
    this.title,
    this.sourceGuideId,
    this.forkedGuideId,
    this.status,
    this.notes,
  );
  final String id, title, status, notes;
  final String? sourceGuideId, forkedGuideId;
}

class ForkResult {
  const ForkResult(
    this.id,
    this.slug,
    this.title,
    this.sourceGuideId,
    this.sourceTitle,
  );
  final String id, slug, title, sourceGuideId, sourceTitle;
}

class Review {
  const Review(
    this.id,
    this.guideId,
    this.userId,
    this.rating,
    this.body,
    this.moderationStatus,
    this.createdAt,
    this.reply,
  );
  final String id, guideId, userId, body, moderationStatus;
  final int rating;
  final DateTime createdAt;
  final Reply? reply;
}

class Reply {
  const Reply(
    this.id,
    this.reviewId,
    this.authorUserId,
    this.body,
    this.createdAt,
  );
  final String id, reviewId, authorUserId, body;
  final DateTime createdAt;
}

class AuthorPage {
  const AuthorPage(
    this.slug,
    this.displayName,
    this.biography,
    this.travelCountries,
    this.guides,
  );
  final String slug, displayName, biography;
  final List<String> travelCountries;
  final List<DiscoveryItem> guides;
}

class PrivateProfile {
  const PrivateProfile(
    this.email,
    this.displayName,
    this.emailConfirmed,
    this.status,
  );
  final String email;
  final String displayName;
  final bool emailConfirmed;
  final String status;
}

class PublicCreator {
  const PublicCreator(
    this.slug,
    this.displayName,
    this.biography,
    this.travelCountries,
  );
  final String slug;
  final String displayName, biography;
  final List<String> travelCountries;
}

class VerifiedBadge {
  const VerifiedBadge(
    this.guideId,
    this.verified,
    this.approvedEvidenceCount,
    this.firstGrantedAt,
    this.lastGrantedAt,
  );
  final String guideId;
  final bool verified;
  final int approvedEvidenceCount;
  final DateTime? firstGrantedAt, lastGrantedAt;
}

class InsightAggregate {
  const InsightAggregate(
    this.currencyCode,
    this.count,
    this.partySize,
    this.tripDays,
    this.totalCostMinorUnits,
  );
  final String currencyCode;
  final int count;
  final double partySize, tripDays, totalCostMinorUnits;
}

class TripInsightSummary {
  const TripInsightSummary(
    this.guideId,
    this.submissionCount,
    this.meetsKAnonymity,
    this.median,
    this.average,
  );
  final String guideId;
  final int submissionCount;
  final bool meetsKAnonymity;
  final InsightAggregate? median, average;
}

class RevenueByCurrency {
  const RevenueByCurrency(
    this.currencyCode,
    this.grossMinorUnits,
    this.commissionMinorUnits,
    this.netMinorUnits,
    this.paidOrderCount,
    this.refundedOrderCount,
  );
  final String currencyCode;
  final int grossMinorUnits, commissionMinorUnits, netMinorUnits;
  final int paidOrderCount, refundedOrderCount;
}

class CreatorDashboardOverview {
  const CreatorDashboardOverview(
    this.guideCount,
    this.activeGuideCount,
    this.paidOrderCount,
    this.refundedOrderCount,
    this.revenue,
    this.generatedAt,
  );
  final int guideCount, activeGuideCount, paidOrderCount, refundedOrderCount;
  final List<RevenueByCurrency> revenue;
  final DateTime generatedAt;
}

class CreatorOrderRow {
  const CreatorOrderRow(
    this.orderId,
    this.guideId,
    this.guideTitle,
    this.amountMinorUnits,
    this.currencyCode,
    this.status,
    this.createdAt,
  );
  final String orderId, guideId, guideTitle, currencyCode, status;
  final int amountMinorUnits;
  final DateTime createdAt;
}

class CreatorOrdersResponse {
  const CreatorOrdersResponse(this.total, this.items);
  final int total;
  final List<CreatorOrderRow> items;
}

class CreatorReviewSummary {
  const CreatorReviewSummary(
    this.visibleCount,
    this.flaggedCount,
    this.hiddenCount,
    this.reportsOpen,
  );
  final int visibleCount, flaggedCount, hiddenCount, reportsOpen;
}

class AdminAuditEntryRow {
  const AdminAuditEntryRow(
    this.id,
    this.actorUserId,
    this.targetUserId,
    this.action,
    this.reason,
    this.occurredAt,
  );
  final String id, actorUserId, targetUserId, action, reason;
  final DateTime occurredAt;
}

class AdminAuditResponse {
  const AdminAuditResponse(this.total, this.items);
  final int total;
  final List<AdminAuditEntryRow> items;
}

class AdminUserRow {
  const AdminUserRow(
    this.userId,
    this.email,
    this.status,
    this.emailConfirmed,
    this.createdAt,
  );
  final String userId, email, status;
  final bool emailConfirmed;
  final DateTime createdAt;
}

class AdminUsersResponse {
  const AdminUsersResponse(this.total, this.items);
  final int total;
  final List<AdminUserRow> items;
}

class AdminCreatorRow {
  const AdminCreatorRow(
    this.userId,
    this.slug,
    this.status,
    this.createdAt,
  );
  final String userId, slug, status;
  final DateTime createdAt;
}

class AdminCreatorsResponse {
  const AdminCreatorsResponse(this.total, this.items);
  final int total;
  final List<AdminCreatorRow> items;
}

class FollowStatus {
  const FollowStatus(this.slug, this.following, this.followedAt);
  final String slug;
  final bool following;
  final DateTime? followedAt;
}

class NotificationEntry {
  const NotificationEntry(
    this.id,
    this.kind,
    this.title,
    this.body,
    this.targetSlug,
    this.targetGuideId,
    this.createdAt,
    this.readAt,
  );
  final String id, kind, title, body;
  final String? targetSlug, targetGuideId;
  final DateTime createdAt;
  final DateTime? readAt;
}

class NotificationList {
  const NotificationList(this.unreadCount, this.items);
  final int unreadCount;
  final List<NotificationEntry> items;
}

class NotificationPreferences {
  NotificationPreferences({
    required this.emailEnabled,
    required this.inAppEnabled,
    required this.newGuidePublishedEmail,
    required this.newGuidePublishedInApp,
    required this.newReviewOnMyGuideEmail,
    required this.newReviewOnMyGuideInApp,
    required this.newReplyToReviewEmail,
    required this.newReplyToReviewInApp,
    required this.followerGainedEmail,
    required this.followerGainedInApp,
    required this.evidenceReviewedEmail,
    required this.evidenceReviewedInApp,
  });
  bool emailEnabled,
      inAppEnabled,
      newGuidePublishedEmail,
      newGuidePublishedInApp,
      newReviewOnMyGuideEmail,
      newReviewOnMyGuideInApp,
      newReplyToReviewEmail,
      newReplyToReviewInApp,
      followerGainedEmail,
      followerGainedInApp,
      evidenceReviewedEmail,
      evidenceReviewedInApp;
}

class GuideRelease {
  const GuideRelease(
    this.id,
    this.guideId,
    this.versionNumber,
    this.changelog,
    this.title,
    this.publishedAt,
    this.nodeSummary,
  );
  final String id, guideId, changelog, title, nodeSummary;
  final int versionNumber;
  final DateTime publishedAt;
}

class GuideReleaseList {
  const GuideReleaseList(this.total, this.items);
  final int total;
  final List<GuideRelease> items;
}

class GuideFreshness {
  const GuideFreshness(
    this.guideId,
    this.latestVersion,
    this.latestPublishedAt,
    this.daysSinceLatest,
  );
  final String guideId;
  final int latestVersion;
  final DateTime? latestPublishedAt;
  final int? daysSinceLatest;
}

class PluginSummary {
  const PluginSummary(
    this.id,
    this.slug,
    this.displayName,
    this.version,
    this.publisher,
    this.status,
    this.createdAt,
  );
  final String id, slug, displayName, version, publisher, status;
  final DateTime createdAt;
}

class PluginList {
  const PluginList(this.total, this.items);
  final int total;
  final List<PluginSummary> items;
}

class PluginInstallation {
  const PluginInstallation(
    this.id,
    this.pluginId,
    this.pluginSlug,
    this.pluginDisplayName,
    this.lifecycle,
    this.installedAt,
    this.scopes,
  );
  final String id, pluginId, pluginSlug, pluginDisplayName, lifecycle;
  final DateTime installedAt;
  final List<String> scopes;
}

class TenantSummary {
  const TenantSummary(
    this.id,
    this.slug,
    this.displayName,
    this.primaryDomain,
    this.status,
    this.brandingJson,
    this.createdAt,
  );
  final String id, slug, displayName, primaryDomain, status, brandingJson;
  final DateTime createdAt;
}

class Subscription {
  const Subscription(
    this.id,
    this.plan,
    this.status,
    this.startsAt,
    this.endsAt,
  );
  final String id, plan, status;
  final DateTime startsAt;
  final DateTime? endsAt;
}

class TenantDashboard {
  const TenantDashboard(this.tenant, this.subscription);
  final TenantSummary tenant;
  final Subscription subscription;
}

class QuotaRow {
  const QuotaRow(
    this.metric,
    this.used,
    this.limit,
    this.periodStart,
    this.periodEnd,
  );
  final String metric;
  final int used, limit;
  final DateTime periodStart, periodEnd;
}

class QuotaList {
  const QuotaList(this.total, this.items);
  final int total;
  final List<QuotaRow> items;
}

class ExportPurchaseRow {
  const ExportPurchaseRow(this.guideId, this.orderId, this.grantedAt, this.revokedAt);
  final String guideId, orderId;
  final DateTime grantedAt;
  final DateTime? revokedAt;
}

class ExportPayload {
  const ExportPayload(this.userId, this.displayName, this.locale, this.purchases);
  final String userId, displayName;
  final String? locale;
  final List<ExportPurchaseRow> purchases;
}

abstract interface class TokenStore {
  Future<String?> read();
  Future<void> write(String? token);
}

class MemoryTokenStore implements TokenStore {
  String? _token;
  @override
  Future<String?> read() async => _token;
  @override
  Future<void> write(String? token) async {
    _token = token;
  }
}

class SecureTokenStore implements TokenStore {
  const SecureTokenStore([this._storage = const FlutterSecureStorage()]);
  static const _key = 'trippify_access_token';
  final FlutterSecureStorage _storage;
  @override
  Future<String?> read() => _storage.read(key: _key);
  @override
  Future<void> write(String? token) => token == null
      ? _storage.delete(key: _key)
      : _storage.write(key: _key, value: token);
}

class ApiClient implements AppApi {
  ApiClient(this.baseUri, {http.Client? client, TokenStore? tokenStore})
    : _client = client ?? http.Client(),
      _tokens = tokenStore ?? const SecureTokenStore();
  final Uri baseUri;
  final http.Client _client;
  final TokenStore _tokens;
  @override
  Future<SystemInfo> getSystemInfo() async {
    final token = await _tokens.read();
    final response = await _client.get(
      baseUri.resolve('/api/v1/system'),
      headers: {if (token != null) 'Authorization': 'Bearer $token'},
    );
    if (response.statusCode != 200) throw StateError('API unavailable');
    final value = jsonDecode(response.body) as Map<String, dynamic>;
    return SystemInfo(value['name'] as String, value['apiVersion'] as String);
  }

  @override
  Future<void> register(String email, String password) => _json(
    'POST',
    '/api/v1/auth/register',
    {'email': email, 'password': password},
  );
  @override
  Future<void> login(String email, String password) async {
    final response = await _json('POST', '/api/v1/auth/login', {
      'email': email,
      'password': password,
    });
    await _tokens.write(response['accessToken'] as String);
  }

  @override
  Future<PrivateProfile> getProfile() async {
    final v = await _json('GET', '/api/v1/me/profile', null);
    return PrivateProfile(
      v['email'] as String,
      v['displayName'] as String,
      v['emailConfirmed'] as bool,
      v['status'] as String,
    );
  }

  @override
  Future<void> updateProfile(
    String displayName,
    String? avatarUrl,
    String? locale,
  ) => _json('PUT', '/api/v1/me/profile', {
    'displayName': displayName,
    'avatarUrl': avatarUrl,
    'locale': locale,
  });
  @override
  Future<void> enrollCreator(
    String slug,
    String biography,
    List<String> countries,
  ) => _json('POST', '/api/v1/creators/enroll', {
    'slug': slug,
    'biography': biography,
    'travelCountries': countries,
  });
  @override
  Future<PublicCreator> getCreator(String slug) async {
    final v = await _json('GET', '/api/v1/creators/$slug', null);
    return PublicCreator(
      v['slug'] as String,
      v['displayName'] as String,
      v['biography'] as String,
      (v['travelCountries'] as List).cast<String>(),
    );
  }

  @override
  Future<List<GuideSummary>> getMyGuides() async {
    final values = await _request('GET', '/api/v1/guides', null) as List;
    return values.map((item) {
      final v = item as Map<String, dynamic>;
      return GuideSummary(
        v['id'] as String,
        v['title'] as String,
        v['countryCode'] as String,
        v['tripDays'] as int,
        v['lifecycle'] as String,
        v['concurrencyToken'] as String,
      );
    }).toList();
  }

  @override
  Future<GuideDraft> createGuide(String title, String countryCode) async {
    final v = await _json('POST', '/api/v1/guides', {
      'title': title,
      'subtitle': '',
      'summary': '',
      'coverUrl': null,
      'countryCode': countryCode,
      'cities': <String>[],
      'tags': <String>[],
      'tripDays': 0,
    });
    return GuideDraft(
      v['id'] as String,
      title,
      countryCode,
      v['concurrencyToken'] as String,
      const [],
    );
  }

  @override
  Future<GuideDraft> saveGuideStructure(GuideDraft guide) async {
    final v = await _json('PUT', '/api/v1/guides/${guide.id}/structure', {
      'concurrencyToken': guide.concurrencyToken,
      'days': guide.days
          .map((title) => {'title': title, 'notes': '', 'nodes': <Object>[]})
          .toList(),
      'sections': <Object>[],
    });
    return GuideDraft(
      guide.id,
      guide.title,
      guide.countryCode,
      v['concurrencyToken'] as String,
      guide.days,
    );
  }

  @override
  Future<DayRoute> getDayRoute(String guideId, int dayPosition) async {
    final v = await _json('GET', '/api/v1/guides/$guideId/days/$dayPosition/route', null);
    return DayRoute(
      v['concurrencyToken'] as String,
      (v['markers'] as List).map((item) {
        final m = item as Map<String, dynamic>;
        return RouteMarker(
          m['nodeId'] as String,
          m['position'] as int,
          m['name'] as String,
          (m['latitude'] as num?)?.toDouble(),
          (m['longitude'] as num?)?.toDouble(),
        );
      }).toList(),
      (v['segments'] as List).map((item) {
        final s = item as Map<String, dynamic>;
        return RouteSegment(
          s['position'] as int,
          s['mode'] as String,
          s['label'] as String,
          s['originName'] as String,
          s['destinationName'] as String,
          s['durationMinutes'] as int,
          s['costPerPersonMinorUnits'] as int,
          s['currencyCode'] as String,
        );
      }).toList(),
    );
  }

  @override
  Future<DayRoute> saveDayRoute(
    String guideId,
    int dayPosition,
    DayRoute route,
  ) async {
    final v = await _json(
      'PUT',
      '/api/v1/guides/$guideId/days/$dayPosition/route',
      {
        'concurrencyToken': route.concurrencyToken,
        'segments': route.segments
            .map(
              (s) => {
                'mode': s.mode,
                'label': s.label,
                'originName': s.originName,
                'destinationName': s.destinationName,
                'durationMinutes': s.durationMinutes,
                'costPerPersonMinorUnits': s.costPerPersonMinorUnits,
                'currencyCode': s.currencyCode,
              },
            )
            .toList(),
      },
    );
    return DayRoute(
      v['concurrencyToken'] as String,
      route.markers,
      (v['segments'] as List).map((item) {
        final s = item as Map<String, dynamic>;
        return RouteSegment(
          s['position'] as int,
          s['mode'] as String,
          s['label'] as String,
          s['originName'] as String,
          s['destinationName'] as String,
          s['durationMinutes'] as int,
          s['costPerPersonMinorUnits'] as int,
          s['currencyCode'] as String,
        );
      }).toList(),
    );
  }

  @override
  Future<BudgetOverview> getBudget(String guideId, int partySize) async {
    final v = await _json(
      'GET',
      '/api/v1/guides/$guideId/budget?partySize=$partySize',
      null,
    );
    return BudgetOverview(
      v['partySize'] as int,
      (v['lines'] as List).map((item) {
        final l = item as Map<String, dynamic>;
        return BudgetLine(
          l['category'] as String,
          l['amountPerPersonMinorUnits'] as int,
          l['partyTotalMinorUnits'] as int,
          l['currencyCode'] as String,
        );
      }).toList(),
    );
  }

  @override
  Future<BudgetOverview> saveBudget(
    String guideId,
    String concurrencyToken,
    List<BudgetLine> lines,
  ) async {
    final v = await _json('PUT', '/api/v1/guides/$guideId/budget', {
      'concurrencyToken': concurrencyToken,
      'entries': lines
          .map(
            (l) => {
              'category': l.category,
              'amountPerPersonMinorUnits': l.amountPerPersonMinorUnits,
              'currencyCode': l.currencyCode,
            },
          )
          .toList(),
    });
    return BudgetOverview(
      v['partySize'] as int,
      (v['lines'] as List).map((item) {
        final l = item as Map<String, dynamic>;
        return BudgetLine(
          l['category'] as String,
          l['amountPerPersonMinorUnits'] as int,
          l['partyTotalMinorUnits'] as int,
          l['currencyCode'] as String,
        );
      }).toList(),
    );
  }

  @override
  Future<PublishResult> publishGuide(
    String guideId,
    String concurrencyToken, {
    int? priceMinorUnits,
    String? currencyCode,
  }) async {
    final v = await _json('POST', '/api/v1/guides/$guideId/publish', {
      'concurrencyToken': concurrencyToken,
      if (priceMinorUnits != null)
        'pricing': {
          'priceMinorUnits': priceMinorUnits,
          'currencyCode': currencyCode,
        },
    });
    return PublishResult(
      v['concurrencyToken'] as String,
      v['slug'] as String,
      v['lifecycle'] as String,
    );
  }

  @override
  Future<void> unpublishGuide(String guideId, String concurrencyToken) =>
      _json('POST', '/api/v1/guides/$guideId/unpublish', {
        'concurrencyToken': concurrencyToken,
      });

  @override
  Future<SearchResult> searchGuides({
    String? country,
    String? tag,
    String? pricing,
    String? query,
  }) async {
    final params = Uri(
      queryParameters: {
        if (country != null && country.isNotEmpty) 'country': country,
        if (tag != null && tag.isNotEmpty) 'tag': tag,
        if (pricing != null && pricing.isNotEmpty) 'pricing': pricing,
        if (query != null && query.isNotEmpty) 'q': query,
      },
    ).query;
    final v = await _json(
      'GET',
      '/api/v1/discovery/guides${params.isEmpty ? '' : '?$params'}',
      null,
    );
    return SearchResult(
      v['total'] as int,
      (v['tagFacets'] as List)
          .map((f) => (f as Map<String, dynamic>)['value'] as String)
          .toList(),
      (v['items'] as List).map((item) {
        final i = item as Map<String, dynamic>;
        return DiscoveryItem(
          i['slug'] as String,
          i['title'] as String,
          i['subtitle'] as String,
          i['summary'] as String,
          i['countryCode'] as String,
          i['tripDays'] as int,
          i['pricing'] as String,
          (i['priceMinorUnits'] as num?)?.toInt(),
          i['currencyCode'] as String?,
          i['authorSlug'] as String,
        );
      }).toList(),
    );
  }

  @override
  Future<PublicGuide> getPublicGuide(String slug) async {
    final v = await _json('GET', '/api/v1/discovery/guides/$slug', null);
    final purchase = v['purchase'] as Map<String, dynamic>?;
    return PublicGuide(
      v['id'] as String,
      v['slug'] as String,
      v['title'] as String,
      (v['subtitle'] ?? '') as String,
      (v['summary'] ?? '') as String,
      v['countryCode'] as String,
      v['tripDays'] as int,
      ((v['cities'] ?? const []) as List).cast<String>(),
      ((v['tags'] ?? const []) as List).cast<String>(),
      v['pricing'] as String,
      v['authorSlug'] as String,
      v['shareUrl'] as String,
      (purchase?['priceMinorUnits'] as num?)?.toInt(),
      purchase?['currencyCode'] as String?,
      v['unlocked'] as bool? ?? false,
      (v['days'] as List? ?? const []).map((day) {
        final d = day as Map<String, dynamic>;
        return PublicGuideDay(
          d['title'] as String,
          (d['nodes'] as List? ?? const []).map((node) {
            final n = node as Map<String, dynamic>;
            return PublicGuideNode(n['name'] as String, n.containsKey('latitude'));
          }).toList(),
        );
      }).toList(),
    );
  }

  @override
  Future<AuthorPage> getAuthor(String slug) async {
    final v = await _json('GET', '/api/v1/discovery/authors/$slug', null);
    return AuthorPage(
      v['slug'] as String,
      v['displayName'] as String,
      v['biography'] as String,
      (v['travelCountries'] as List).cast<String>(),
      (v['guides'] as List).map((item) {
        final i = item as Map<String, dynamic>;
        return DiscoveryItem(
          i['slug'] as String,
          i['title'] as String,
          i['subtitle'] as String,
          i['summary'] as String,
          i['countryCode'] as String,
          i['tripDays'] as int,
          i['pricing'] as String,
          (i['priceMinorUnits'] as num?)?.toInt(),
          i['currencyCode'] as String?,
          i['authorSlug'] as String,
        );
      }).toList(),
    );
  }

  @override
  Future<CheckoutSession> checkout(String guideId, {String? discountCode}) async {
    final v = await _json('POST', '/api/v1/commerce/checkout', {
      'guideId': guideId,
      if (discountCode != null && discountCode.isNotEmpty)
        'discountCode': discountCode,
    });
    return CheckoutSession(
      v['orderId'] as String,
      v['checkoutReference'] as String,
      v['amountMinorUnits'] as int,
      v['currencyCode'] as String,
    );
  }

  @override
  Future<List<Entitlement>> getEntitlements() async {
    final values = await _request('GET', '/api/v1/commerce/entitlements', null) as List;
    return values.map((item) {
      final v = item as Map<String, dynamic>;
      return Entitlement(
        v['id'] as String,
        v['guideId'] as String,
        v['slug'] as String,
        v['title'] as String,
      );
    }).toList();
  }

  @override
  Future<void> addFavorite(String guideId) =>
      _request('POST', '/api/v1/library/favorites/$guideId', null);
  @override
  Future<void> removeFavorite(String guideId) =>
      _request('DELETE', '/api/v1/library/favorites/$guideId', null);

  @override
  Future<List<Favorite>> listFavorites() async {
    final values = await _request('GET', '/api/v1/library/favorites', null) as List;
    return values.map((item) {
      final v = item as Map<String, dynamic>;
      return Favorite(
        v['guideId'] as String,
        v['slug'] as String,
        v['title'] as String,
        v['countryCode'] as String,
        v['pricing'] as String,
      );
    }).toList();
  }

  @override
  Future<List<Trip>> listTrips() async {
    final values = await _request('GET', '/api/v1/library/trips', null) as List;
    return values.map((item) {
      final v = item as Map<String, dynamic>;
      return Trip(
        v['id'] as String,
        v['title'] as String,
        v['sourceGuideId'] as String?,
        v['forkedGuideId'] as String?,
        v['status'] as String,
        (v['notes'] ?? '') as String,
      );
    }).toList();
  }

  @override
  Future<Trip> createTrip(String guideId, {String? title}) async {
    final v = await _json('POST', '/api/v1/library/trips', {
      'guideId': guideId,
      if (title != null && title.isNotEmpty) 'title': title,
    });
    return Trip(
      v['id'] as String,
      v['title'] as String,
      v['sourceGuideId'] as String?,
      v['forkedGuideId'] as String?,
      v['status'] as String,
      (v['notes'] ?? '') as String,
    );
  }

  @override
  Future<Trip> updateTrip(
    String tripId, {
    String? notes,
    String? status,
    String? title,
  }) async {
    final v = await _json('PATCH', '/api/v1/library/trips/$tripId', {
      if (title != null) 'title': title,
      if (notes != null) 'notes': notes,
      if (status != null) 'status': status,
    });
    return Trip(
      v['id'] as String,
      v['title'] as String,
      v['sourceGuideId'] as String?,
      v['forkedGuideId'] as String?,
      v['status'] as String,
      (v['notes'] ?? '') as String,
    );
  }

  @override
  Future<ForkResult> forkGuide(String guideId) async {
    final v = await _json('POST', '/api/v1/library/forks', {'guideId': guideId});
    return ForkResult(
      v['id'] as String,
      v['slug'] as String,
      v['title'] as String,
      v['sourceGuideId'] as String,
      v['sourceTitle'] as String,
    );
  }

  @override
  Future<List<Review>> listReviews(String guideId) async {
    final values = await _request('GET', '/api/v1/guides/$guideId/reviews', null) as List;
    return values.map((item) {
      final v = item as Map<String, dynamic>;
      final replyData = v['reply'] as Map<String, dynamic>?;
      return Review(
        v['id'] as String,
        v['guideId'] as String,
        v['userId'] as String,
        v['rating'] as int,
        v['body'] as String,
        v['moderationStatus'] as String,
        DateTime.parse(v['createdAt'] as String),
        replyData != null
            ? Reply(
                replyData['id'] as String,
                replyData['reviewId'] as String,
                replyData['authorUserId'] as String,
                replyData['body'] as String,
                DateTime.parse(replyData['createdAt'] as String),
              )
            : null,
      );
    }).toList();
  }

  @override
  Future<Review> submitReview(String guideId, int rating, String body) async {
    final v = await _json('POST', '/api/v1/guides/$guideId/reviews', {
      'rating': rating,
      'body': body,
    });
    final replyData = v['reply'] as Map<String, dynamic>?;
    return Review(
      v['id'] as String,
      v['guideId'] as String,
      v['userId'] as String,
      v['rating'] as int,
      v['body'] as String,
      v['moderationStatus'] as String,
      DateTime.parse(v['createdAt'] as String),
      replyData != null
          ? Reply(
              replyData['id'] as String,
              replyData['reviewId'] as String,
              replyData['authorUserId'] as String,
              replyData['body'] as String,
              DateTime.parse(replyData['createdAt'] as String),
            )
          : null,
    );
  }

  @override
  Future<Review> editReview(String reviewId, int rating, String body) async {
    final v = await _json('PUT', '/api/v1/reviews/$reviewId', {
      'rating': rating,
      'body': body,
    });
    final replyData = v['reply'] as Map<String, dynamic>?;
    return Review(
      v['id'] as String,
      v['guideId'] as String,
      v['userId'] as String,
      v['rating'] as int,
      v['body'] as String,
      v['moderationStatus'] as String,
      DateTime.parse(v['createdAt'] as String),
      replyData != null
          ? Reply(
              replyData['id'] as String,
              replyData['reviewId'] as String,
              replyData['authorUserId'] as String,
              replyData['body'] as String,
              DateTime.parse(replyData['createdAt'] as String),
            )
          : null,
    );
  }

  @override
  Future<void> deleteReview(String reviewId) =>
      _request('DELETE', '/api/v1/reviews/$reviewId', null);

  @override
  Future<Reply> replyToReview(String reviewId, String body) async {
    final v = await _json('POST', '/api/v1/reviews/$reviewId/reply', {
      'body': body,
    });
    return Reply(
      v['id'] as String,
      v['reviewId'] as String,
      v['authorUserId'] as String,
      v['body'] as String,
      DateTime.parse(v['createdAt'] as String),
    );
  }

  @override
  Future<void> reportReview(String reviewId, String reason) =>
      _json('POST', '/api/v1/reviews/$reviewId/reports', {'reason': reason});

  @override
  Future<void> submitFeedback(String guideId, String body) =>
      _json('POST', '/api/v1/guides/$guideId/feedback', {'body': body});

  @override
  Future<VerifiedBadge> getVerifiedBadge(String guideId) async {
    final v = await _json('GET', '/api/v1/guides/$guideId/evidence/badge', null);
    return VerifiedBadge(
      v['guideId'] as String,
      v['verified'] as bool,
      v['approvedEvidenceCount'] as int,
      _parseDate(v['firstGrantedAt']),
      _parseDate(v['lastGrantedAt']),
    );
  }

  @override
  Future<void> submitEvidence(
    String guideId, {
    required String kind,
    required String body,
    String? redactedReference,
  }) =>
      _json('POST', '/api/v1/guides/$guideId/evidence', {
        'kind': kind,
        'body': body,
        if (redactedReference != null && redactedReference.isNotEmpty)
          'redactedReference': redactedReference,
      });

  @override
  Future<TripInsightSummary> getTripInsights(String guideId) async {
    final v = await _json('GET', '/api/v1/guides/$guideId/insights', null);
    return TripInsightSummary(
      v['guideId'] as String,
      v['submissionCount'] as int,
      v['meetsKAnonymity'] as bool,
      _aggregate(v['median'] as Map<String, dynamic>?),
      _aggregate(v['average'] as Map<String, dynamic>?),
    );
  }

  @override
  Future<void> submitTripInsight(
    String guideId, {
    required int partySize,
    required int tripDays,
    required int totalCostMinorUnits,
    required String currencyCode,
  }) =>
      _json('POST', '/api/v1/guides/$guideId/insights', {
        'partySize': partySize,
        'tripDays': tripDays,
        'totalCostMinorUnits': totalCostMinorUnits,
        'currencyCode': currencyCode,
      });

  static DateTime? _parseDate(Object? value) {
    if (value is String && value.isNotEmpty) return DateTime.parse(value);
    return null;
  }

  static InsightAggregate? _aggregate(Map<String, dynamic>? value) {
    if (value == null) return null;
    return InsightAggregate(
      value['currencyCode'] as String,
      value['count'] as int,
      (value['averagePartySize'] as num).toDouble(),
      (value['averageTripDays'] as num).toDouble(),
      (value['averageTotalCostMinorUnits'] as num).toDouble(),
    );
  }

  @override
  Future<CreatorDashboardOverview> getCreatorDashboardOverview() async {
    final v = await _json('GET', '/api/v1/creator/dashboard/overview', null);
    return CreatorDashboardOverview(
      v['guideCount'] as int,
      v['activeGuideCount'] as int,
      v['paidOrderCount'] as int,
      v['refundedOrderCount'] as int,
      ((v['revenue'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map((m) => RevenueByCurrency(
                m['currencyCode'] as String,
                m['grossMinorUnits'] as int,
                m['commissionMinorUnits'] as int,
                m['netMinorUnits'] as int,
                m['paidOrderCount'] as int,
                m['refundedOrderCount'] as int,
              ))
          .toList(),
      DateTime.parse(v['generatedAt'] as String),
    );
  }

  @override
  Future<CreatorOrdersResponse> listCreatorOrders({int? limit}) async {
    final params = Uri(queryParameters: {if (limit != null) 'limit': '$limit'}).query;
    final path = '/api/v1/creator/dashboard/orders${params.isEmpty ? '' : '?$params'}';
    final v = await _json('GET', path, null);
    return CreatorOrdersResponse(
      v['total'] as int,
      ((v['items'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map((m) => CreatorOrderRow(
                m['orderId'] as String,
                m['guideId'] as String,
                m['guideTitle'] as String,
                m['amountMinorUnits'] as int,
                m['currencyCode'] as String,
                m['status'] as String,
                DateTime.parse(m['createdAt'] as String),
              ))
          .toList(),
    );
  }

  @override
  Future<CreatorReviewSummary> getCreatorDashboardReviews() async {
    final v = await _json('GET', '/api/v1/creator/dashboard/reviews', null);
    return CreatorReviewSummary(
      v['visibleCount'] as int,
      v['flaggedCount'] as int,
      v['hiddenCount'] as int,
      v['reportsOpen'] as int,
    );
  }

  @override
  Future<AdminAuditResponse> listAdminAudit({int? limit}) async {
    final params = Uri(queryParameters: {if (limit != null) 'limit': '$limit'}).query;
    final path = '/api/v1/admin/operations/audit${params.isEmpty ? '' : '?$params'}';
    final v = await _json('GET', path, null);
    return AdminAuditResponse(
      v['total'] as int,
      ((v['items'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map((m) => AdminAuditEntryRow(
                m['id'] as String,
                m['actorUserId'] as String,
                m['targetUserId'] as String,
                m['action'] as String,
                m['reason'] as String,
                DateTime.parse(m['occurredAt'] as String),
              ))
          .toList(),
    );
  }

  @override
  Future<AdminUsersResponse> listAdminUsers({int? limit}) async {
    final params = Uri(queryParameters: {if (limit != null) 'limit': '$limit'}).query;
    final path = '/api/v1/admin/operations/users${params.isEmpty ? '' : '?$params'}';
    final v = await _json('GET', path, null);
    return AdminUsersResponse(
      v['total'] as int,
      ((v['items'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map((m) => AdminUserRow(
                m['userId'] as String,
                m['email'] as String,
                m['status'] as String,
                m['emailConfirmed'] as bool,
                DateTime.parse(m['createdAt'] as String),
              ))
          .toList(),
    );
  }

  @override
  Future<AdminCreatorsResponse> listAdminCreators({int? limit}) async {
    final params = Uri(queryParameters: {if (limit != null) 'limit': '$limit'}).query;
    final path = '/api/v1/admin/operations/creators${params.isEmpty ? '' : '?$params'}';
    final v = await _json('GET', path, null);
    return AdminCreatorsResponse(
      v['total'] as int,
      ((v['items'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map((m) => AdminCreatorRow(
                m['userId'] as String,
                m['slug'] as String,
                m['status'] as String,
                DateTime.parse(m['createdAt'] as String),
              ))
          .toList(),
    );
  }

  @override
  Future<FollowStatus> getCreatorFollowStatus(String slug) async {
    final v = await _json('GET', '/api/v1/creators/$slug/follow', null);
    return FollowStatus(
      v['slug'] as String,
      v['following'] as bool,
      _parseNullableDate(v['followedAt']),
    );
  }

  @override
  Future<FollowStatus> followCreator(String slug) async {
    final v = await _json('POST', '/api/v1/creators/$slug/follow', null);
    return FollowStatus(
      v['slug'] as String,
      v['following'] as bool,
      _parseNullableDate(v['followedAt']),
    );
  }

  @override
  Future<void> unfollowCreator(String slug) =>
      _request('DELETE', '/api/v1/creators/$slug/follow', null);

  @override
  Future<int> getCreatorFollowersCount(String slug) async {
    final v = await _json('GET', '/api/v1/creators/$slug/followers/count', null);
    return v['followers'] as int;
  }

  @override
  Future<NotificationList> listNotifications({int? limit}) async {
    final params = Uri(queryParameters: {if (limit != null) 'limit': '$limit'}).query;
    final path = '/api/v1/me/notifications${params.isEmpty ? '' : '?$params'}';
    final v = await _json('GET', path, null);
    return NotificationList(
      v['unreadCount'] as int,
      ((v['items'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map(_toNotification)
          .toList(),
    );
  }

  @override
  Future<void> markNotificationRead(String id) =>
      _request('POST', '/api/v1/me/notifications/$id/read', null);

  @override
  Future<NotificationPreferences> getNotificationPreferences() async {
    final v = await _json('GET', '/api/v1/me/notification-preferences', null);
    return _toPreferences(v);
  }

  @override
  Future<NotificationPreferences> updateNotificationPreferences(
    NotificationPreferences preferences,
  ) async {
    final v = await _json('PUT', '/api/v1/me/notification-preferences', {
      'emailEnabled': preferences.emailEnabled,
      'inAppEnabled': preferences.inAppEnabled,
      'newGuidePublishedEmail': preferences.newGuidePublishedEmail,
      'newGuidePublishedInApp': preferences.newGuidePublishedInApp,
      'newReviewOnMyGuideEmail': preferences.newReviewOnMyGuideEmail,
      'newReviewOnMyGuideInApp': preferences.newReviewOnMyGuideInApp,
      'newReplyToReviewEmail': preferences.newReplyToReviewEmail,
      'newReplyToReviewInApp': preferences.newReplyToReviewInApp,
      'followerGainedEmail': preferences.followerGainedEmail,
      'followerGainedInApp': preferences.followerGainedInApp,
      'evidenceReviewedEmail': preferences.evidenceReviewedEmail,
      'evidenceReviewedInApp': preferences.evidenceReviewedInApp,
    });
    return _toPreferences(v);
  }

  static NotificationEntry _toNotification(Map<String, dynamic> v) => NotificationEntry(
        v['id'] as String,
        v['kind'] as String,
        v['title'] as String,
        v['body'] as String,
        v['targetSlug'] as String?,
        v['targetGuideId'] == null ? null : v['targetGuideId'].toString(),
        DateTime.parse(v['createdAt'] as String),
        _parseNullableDate(v['readAt']),
      );

  static NotificationPreferences _toPreferences(Map<String, dynamic> v) =>
      NotificationPreferences(
        emailEnabled: v['emailEnabled'] as bool,
        inAppEnabled: v['inAppEnabled'] as bool,
        newGuidePublishedEmail: v['newGuidePublishedEmail'] as bool,
        newGuidePublishedInApp: v['newGuidePublishedInApp'] as bool,
        newReviewOnMyGuideEmail: v['newReviewOnMyGuideEmail'] as bool,
        newReviewOnMyGuideInApp: v['newReviewOnMyGuideInApp'] as bool,
        newReplyToReviewEmail: v['newReplyToReviewEmail'] as bool,
        newReplyToReviewInApp: v['newReplyToReviewInApp'] as bool,
        followerGainedEmail: v['followerGainedEmail'] as bool,
        followerGainedInApp: v['followerGainedInApp'] as bool,
        evidenceReviewedEmail: v['evidenceReviewedEmail'] as bool,
        evidenceReviewedInApp: v['evidenceReviewedInApp'] as bool,
      );

  static DateTime? _parseNullableDate(Object? value) {
    if (value is String && value.isNotEmpty) return DateTime.parse(value);
    return null;
  }

  @override
  Future<GuideReleaseList> listGuideReleases(String guideId, {int? limit}) async {
    final params = Uri(queryParameters: {if (limit != null) 'limit': '$limit'}).query;
    final path = '/api/v1/guides/$guideId/releases${params.isEmpty ? '' : '?$params'}';
    final v = await _json('GET', path, null);
    return GuideReleaseList(
      v['total'] as int,
      ((v['items'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map((m) => GuideRelease(
                m['id'] as String,
                m['guideId'] as String,
                m['versionNumber'] as int,
                m['changelog'] as String,
                m['title'] as String,
                DateTime.parse(m['publishedAt'] as String),
                m['nodeSummary'] as String? ?? '',
              ))
          .toList(),
    );
  }

  @override
  Future<GuideRelease> getGuideRelease(String releaseId) async {
    final v = await _json('GET', '/api/v1/releases/$releaseId', null);
    return GuideRelease(
      v['id'] as String,
      v['guideId'] as String,
      v['versionNumber'] as int,
      v['changelog'] as String,
      v['title'] as String,
      DateTime.parse(v['publishedAt'] as String),
      v['nodeSummary'] as String? ?? '',
    );
  }

  @override
  Future<GuideFreshness> getGuideFreshness(String guideId) async {
    final v = await _json('GET', '/api/v1/guides/$guideId/freshness', null);
    return GuideFreshness(
      v['guideId'] as String,
      v['latestVersion'] as int,
      _parseNullableDate(v['latestPublishedAt']),
      v['daysSinceLatest'] == null ? null : v['daysSinceLatest'] as int,
    );
  }

  @override
  Future<GuideRelease> publishGuideRelease(String guideId, String changelog) async {
    final v = await _json('POST', '/api/v1/guides/$guideId/releases', {
      'changelog': changelog,
    });
    return GuideRelease(
      v['id'] as String,
      v['guideId'] as String,
      v['versionNumber'] as int,
      v['changelog'] as String,
      v['title'] as String,
      DateTime.parse(v['publishedAt'] as String),
      v['nodeSummary'] as String? ?? '',
    );
  }

  @override
  Future<PluginList> listPlugins({int? limit}) async {
    final params = Uri(queryParameters: {if (limit != null) 'limit': '$limit'}).query;
    final path = '/api/v1/plugins${params.isEmpty ? '' : '?$params'}';
    final v = await _json('GET', path, null);
    return PluginList(
      v['total'] as int,
      ((v['items'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map(_toPlugin)
          .toList(),
    );
  }

  @override
  Future<PluginSummary> getPlugin(String pluginId) async {
    final v = await _json('GET', '/api/v1/plugins/$pluginId', null);
    return _toPlugin(v);
  }

  @override
  Future<List<PluginInstallation>> listMyPluginInstallations() async {
    final v = await _json('GET', '/api/v1/me/plugins/installations', null);
    return (v as List)
        .map((item) => item as Map<String, dynamic>)
        .map(_toInstallation)
        .toList();
  }

  @override
  Future<void> installPlugin(String pluginId, List<String> scopes) =>
      _json('POST', '/api/v1/me/plugins/$pluginId/install', {
        'scopes': scopes,
      });

  @override
  Future<void> enablePlugin(String pluginId) =>
      _request('POST', '/api/v1/me/plugins/$pluginId/enable', null);

  @override
  Future<void> disablePlugin(String pluginId) =>
      _request('POST', '/api/v1/me/plugins/$pluginId/disable', null);

  @override
  Future<void> uninstallPlugin(String pluginId) =>
      _request('DELETE', '/api/v1/me/plugins/$pluginId', null);

  static PluginSummary _toPlugin(Map<String, dynamic> v) => PluginSummary(
        v['id'] as String,
        v['slug'] as String,
        v['displayName'] as String,
        v['version'] as String,
        v['publisher'] as String,
        v['status'] as String,
        DateTime.parse(v['createdAt'] as String),
      );

  static PluginInstallation _toInstallation(Map<String, dynamic> v) =>
      PluginInstallation(
        v['id'] as String,
        v['pluginId'] as String,
        v['pluginSlug'] as String,
        v['pluginDisplayName'] as String,
        v['lifecycle'] as String,
        DateTime.parse(v['installedAt'] as String),
        ((v['scopes'] as List?) ?? const []).cast<String>(),
      );

  @override
  Future<TenantDashboard> getMyTenant() async {
    final v = await _json('GET', '/api/v1/me/tenant', null);
    return TenantDashboard(_toTenant(v['tenant'] as Map<String, dynamic>), _toSubscription(v['subscription'] as Map<String, dynamic>));
  }

  @override
  Future<TenantDashboard> updateMySubscription(String plan) async {
    final v = await _json('POST', '/api/v1/me/tenant/subscription', {'plan': plan});
    return TenantDashboard(_toTenant(v['tenant'] as Map<String, dynamic>), _toSubscription(v['subscription'] as Map<String, dynamic>));
  }

  @override
  Future<QuotaList> listMyTenantQuotas() async {
    final v = await _json('GET', '/api/v1/me/tenant/quotas', null);
    return QuotaList(
      v['total'] as int,
      ((v['items'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map((m) => QuotaRow(
                m['metric'] as String,
                m['used'] as int,
                m['limit'] as int,
                DateTime.parse(m['periodStart'] as String),
                DateTime.parse(m['periodEnd'] as String),
              ))
          .toList(),
    );
  }

  @override
  Future<ExportPayload> requestMyTenantExport() async {
    final v = await _json('POST', '/api/v1/me/tenant/export', null);
    return ExportPayload(
      v['userId'] as String,
      v['displayName'] as String? ?? '',
      v['locale'] as String?,
      ((v['purchases'] as List?) ?? const [])
          .map((item) => item as Map<String, dynamic>)
          .map((m) => ExportPurchaseRow(
                m['guideId'] as String,
                m['orderId'] as String,
                DateTime.parse(m['grantedAt'] as String),
                _parseNullableDate(m['revokedAt']),
              ))
          .toList(),
    );
  }

  static TenantSummary _toTenant(Map<String, dynamic> v) => TenantSummary(
        v['id'] as String,
        v['slug'] as String,
        v['displayName'] as String,
        v['primaryDomain'] as String,
        v['status'] as String,
        v['brandingJson'] as String? ?? '{}',
        DateTime.parse(v['createdAt'] as String),
      );

  static Subscription _toSubscription(Map<String, dynamic> v) => Subscription(
        v['id'] as String,
        v['plan'] as String,
        v['status'] as String,
        DateTime.parse(v['startsAt'] as String),
        _parseNullableDate(v['endsAt']),
      );

  Future<Map<String, dynamic>> _json(
    String method,
    String path,
    Map<String, dynamic>? body,
  ) async {
    final value = await _request(method, path, body);
    return value as Map<String, dynamic>;
  }

  Future<dynamic> _request(
    String method,
    String path,
    Map<String, dynamic>? body,
  ) async {
    final token = await _tokens.read();
    final headers = {
      'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };
    final uri = baseUri.resolve(path);
    final response = switch (method) {
      'GET' => await _client.get(uri, headers: headers),
      'POST' => await _client.post(
        uri,
        headers: headers,
        body: jsonEncode(body),
      ),
      'PUT' => await _client.put(uri, headers: headers, body: jsonEncode(body)),
      _ => throw ArgumentError.value(method),
    };
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw StateError('Request failed (${response.statusCode})');
    }
    if (response.body.isEmpty) return <String, dynamic>{};
    return jsonDecode(response.body);
  }
}
