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
  final String displayName;
  final String biography;
  final List<String> travelCountries;
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

  Future<Map<String, dynamic>> _json(
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
    if (response.body.isEmpty) return {};
    return jsonDecode(response.body) as Map<String, dynamic>;
  }
}
