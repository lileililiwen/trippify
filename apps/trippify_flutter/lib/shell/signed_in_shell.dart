import 'package:flutter/material.dart';

import '../api_client.dart';

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
    this.child,
    this.initialSummary,
  });

  final AppApi api;
  final SignedInDestination current;
  final ValueChanged<SignedInDestination> onNavigate;

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
  MySummary? _summary;

  @override
  void initState() {
    super.initState();
    // If a summary was passed in (e.g. from a parent that already
    // resolved the session), adopt it immediately so the first build
    // reflects the right destination count.
    final pending = widget.initialSummary;
    if (pending != null) {
      _summary = pending;
    }
    // Skip the async re-load when an initial summary was supplied —
    // the caller has already resolved the session, and re-fetching on
    // mount would race the first build.
    if (pending != null) return;
    _loadSummary();
  }

  Future<void> _loadSummary() async {
    final token = widget.api.tokens.value;
    if (token == null || token.isEmpty) return;
    try {
      final s = await widget.api.getMySummary();
      if (mounted) setState(() => _summary = s);
    } catch (_) {
      // Summary is best-effort; the home still renders the anonymous
      // shape if it can't load.
    }
  }

  @override
  Widget build(BuildContext context) {
    final isCreator = _summary?.isCreator ?? false;
    final destinations = <NavigationDestination>[
      const NavigationDestination(
        icon: Icon(Icons.home_outlined),
        selectedIcon: Icon(Icons.home),
        label: 'Home',
      ),
      const NavigationDestination(
        icon: Icon(Icons.explore_outlined),
        selectedIcon: Icon(Icons.explore),
        label: 'Discover',
      ),
      const NavigationDestination(
        icon: Icon(Icons.collections_bookmark_outlined),
        selectedIcon: Icon(Icons.collections_bookmark),
        label: 'Library',
      ),
    ];
    if (isCreator) {
      destinations.add(
        const NavigationDestination(
          icon: Icon(Icons.edit_outlined),
          selectedIcon: Icon(Icons.edit),
          label: 'Create',
        ),
      );
    } else {
      destinations.add(
        const NavigationDestination(
          icon: Icon(Icons.map_outlined),
          selectedIcon: Icon(Icons.map),
          label: 'Plan',
        ),
      );
    }

    return Scaffold(
      body: widget.child ?? const SizedBox.shrink(),
      bottomNavigationBar: NavigationBar(
        selectedIndex: widget.current.index,
        destinations: destinations,
        onDestinationSelected: (i) {
          final dest = SignedInDestination.values[i];
          widget.onNavigate(dest);
        },
      ),
    );
  }
}
