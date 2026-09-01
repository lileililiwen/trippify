import 'package:flutter/material.dart';

import '../api_client.dart';
import '../l10n/generated/app_localizations.dart';
import '../session_controller.dart';

/// Signed-in app shell. Renders a [Scaffold] with a [NavigationBar] that
/// adapts to the user's role: 4 destinations for non-creators (home,
/// discover, library, plan), 5 for creators (replaces "plan" with
/// "create").
///
/// The shell owns the bottom navigation and the surface mapping; the
/// individual screens render their own [AppBar]s and bodies.
class SignedInShell extends StatefulWidget {
  const SignedInShell({
    super.key,
    required this.api,
    required this.current,
    required this.onNavigate,
    this.session,
    this.onSignedOut,
    this.child,
    this.initialSummary,
  });

  final AppApi api;
  final SignedInDestination current;
  final ValueChanged<SignedInDestination> onNavigate;
  final SessionController? session;
  final VoidCallback? onSignedOut;

  /// Optional override for the body. When null, the shell renders a
  /// placeholder for the selected destination; screens wired via
  /// `/shell/<dest>` ignore this.
  final Widget? child;

  /// Optional pre-resolved session summary. When supplied, the shell
  /// skips its async load and renders the correct destination count
  /// on the first frame.
  final MySummary? initialSummary;

  @override
  State<SignedInShell> createState() => _SignedInShellState();
}

enum SignedInDestination { home, discover, library, plan, create }

class _SignedInShellState extends State<SignedInShell> {
  late final SessionController _session =
      widget.session ?? SessionController(widget.api);
  MySummary? _summary;
  bool _summaryResolved = false;

  @override
  void initState() {
    super.initState();
    _session.addListener(_sessionChanged);
    // If a summary was passed in (e.g. from a parent that already
    // resolved the session), adopt it immediately so the first build
    // reflects the right destination count.
    final pending = widget.initialSummary;
    if (pending != null) {
      _summary = pending;
      _summaryResolved = true;
    }
    // Skip the async re-load when an initial summary was supplied —
    // the caller has already resolved the session, and re-fetching on
    // mount would race the first build.
    if (pending != null) return;
    _loadSummary();
  }

  void _sessionChanged() {
    if (!mounted) return;
    final state = _session.state;
    if (state.phase == SessionPhase.anonymous) {
      widget.onSignedOut?.call();
      return;
    }
    if (state.summary != null) {
      setState(() {
        _summary = state.summary;
        _summaryResolved = true;
      });
    }
  }

  @override
  void dispose() {
    _session.removeListener(_sessionChanged);
    if (widget.session == null) _session.dispose();
    super.dispose();
  }

  Future<void> _loadSummary() async {
    final token = widget.api.tokens.value;
    if (token == null || token.isEmpty) {
      _summaryResolved = true;
      return;
    }
    try {
      final s = await widget.api.getMySummary();
      if (mounted) {
        setState(() {
          _summary = s;
          _summaryResolved = true;
        });
      }
    } catch (_) {
      // Summary is best-effort; the home still renders the anonymous
      // shape if it can't load.
      if (mounted) setState(() => _summaryResolved = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!_summaryResolved) {
      return Scaffold(body: widget.child ?? const SizedBox.shrink());
    }

    final l10n = AppLocalizations.of(context);
    final isCreator = _summary?.isCreator ?? false;
    final destinations =
        <({SignedInDestination destination, NavigationDestination navigation})>[
          (
            destination: SignedInDestination.home,
            navigation: NavigationDestination(
              icon: const Icon(Icons.home_outlined),
              selectedIcon: const Icon(Icons.home),
              label: l10n?.destinationHome ?? 'Home',
            ),
          ),
          (
            destination: SignedInDestination.discover,
            navigation: NavigationDestination(
              icon: const Icon(Icons.explore_outlined),
              selectedIcon: const Icon(Icons.explore),
              label: l10n?.destinationDiscover ?? 'Discover',
            ),
          ),
          (
            destination: SignedInDestination.library,
            navigation: NavigationDestination(
              icon: const Icon(Icons.collections_bookmark_outlined),
              selectedIcon: const Icon(Icons.collections_bookmark),
              label: l10n?.destinationLibrary ?? 'Library',
            ),
          ),
        ];
    if (isCreator) {
      destinations.add((
        destination: SignedInDestination.create,
        navigation: NavigationDestination(
          icon: const Icon(Icons.edit_outlined),
          selectedIcon: const Icon(Icons.edit),
          label: l10n?.destinationCreate ?? 'Create',
        ),
      ));
    } else {
      destinations.add((
        destination: SignedInDestination.plan,
        navigation: NavigationDestination(
          icon: const Icon(Icons.map_outlined),
          selectedIcon: const Icon(Icons.map),
          label: l10n?.destinationPlan ?? 'Plan',
        ),
      ));
    }

    final currentIndex = destinations.indexWhere(
      (entry) => entry.destination == widget.current,
    );

    return Scaffold(
      body: widget.child ?? const SizedBox.shrink(),
      bottomNavigationBar: NavigationBar(
        selectedIndex: currentIndex < 0 ? 0 : currentIndex,
        destinations: destinations.map((entry) => entry.navigation).toList(),
        onDestinationSelected: (i) {
          widget.onNavigate(destinations[i].destination);
        },
      ),
    );
  }
}
