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
