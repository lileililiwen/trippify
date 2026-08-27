import 'package:flutter/foundation.dart';

import 'api_client.dart';

enum SessionPhase { loading, anonymous, authenticated }

class SessionState {
  const SessionState(this.phase, {this.summary, this.notice});
  final SessionPhase phase;
  final MySummary? summary;
  final String? notice;
}

class SessionController extends ChangeNotifier {
  SessionController(this.api) {
    api.tokens.addListener(_onTokenChanged);
    _onTokenChanged();
  }

  final AppApi api;
  SessionState _state = const SessionState(SessionPhase.loading);
  bool _signingOut = false;
  bool _disposed = false;
  int _loadGeneration = 0;

  SessionState get state => _state;
  bool get isAuthenticated => _state.phase == SessionPhase.authenticated;

  Future<void> refresh() async {
    final generation = ++_loadGeneration;
    final token = api.tokens.value;
    if (token == null || token.isEmpty) {
      _setState(SessionState(SessionPhase.anonymous, notice: _state.notice));
      return;
    }
    _setState(const SessionState(SessionPhase.loading));
    try {
      final summary = await api.getMySummary();
      if (generation == _loadGeneration) {
        _setState(SessionState(SessionPhase.authenticated, summary: summary));
      }
    } on ApiException catch (error) {
      if (error.statusCode == 401 || error.statusCode == 403) {
        await _expire('Your session expired. Sign in again.');
      } else if (generation == _loadGeneration) {
        _setState(const SessionState(SessionPhase.anonymous));
      }
    } catch (_) {
      if (generation == _loadGeneration) {
        _setState(const SessionState(SessionPhase.anonymous));
      }
    }
  }

  Future<bool> signOut() async {
    if (_signingOut || !isAuthenticated) return true;
    _signingOut = true;
    Object? failure;
    try {
      await api.logout();
    } catch (error) {
      failure = error;
    } finally {
      _loadGeneration++;
      _setState(
        SessionState(
          SessionPhase.anonymous,
          notice: failure == null
              ? 'Signed out.'
              : 'Signed out locally. Server revocation could not be confirmed.',
        ),
      );
      _signingOut = false;
    }
    return failure == null;
  }

  Future<void> _expire(String notice) async {
    if (_state.phase == SessionPhase.anonymous) return;
    try {
      await api.logout();
    } catch (_) {
      // ApiClient clears local credentials in finally.
    }
    _loadGeneration++;
    _setState(SessionState(SessionPhase.anonymous, notice: notice));
  }

  void clearNotice() {
    if (_state.notice == null) return;
    _setState(SessionState(_state.phase, summary: _state.summary));
  }

  void _onTokenChanged() {
    if (_disposed) return;
    if ((api.tokens.value ?? '').isEmpty) {
      _loadGeneration++;
      _setState(SessionState(SessionPhase.anonymous, notice: _state.notice));
    } else {
      refresh();
    }
  }

  void _setState(SessionState value) {
    if (_disposed) return;
    _state = value;
    notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    api.tokens.removeListener(_onTokenChanged);
    super.dispose();
  }
}
