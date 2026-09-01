import 'package:flutter/material.dart';

import '../api_client.dart';

/// Maximum content width for wide screens (web layout). Keeps long forms and
/// tile rows readable on a desktop browser while letting mobile layouts
/// fill the available width.
const double kWorkspaceMaxContentWidth = 720;

/// Single workspace entry rendered as a tap card. The label, icon, and
/// destination route are required. A short description is rendered below
/// the label and wraps inside the card so it never overflows.
class WorkspaceEntry {
  const WorkspaceEntry({
    required this.label,
    required this.icon,
    required this.route,
    this.description,
  });

  final String label;
  final IconData icon;
  final String route;
  final String? description;
}

/// A labelled grouping of [WorkspaceEntry]s. The shell renders these as
/// a vertical list of sections so the user can scan their authorized
/// workflows without re-reading the role badges.
class WorkspaceSection {
  const WorkspaceSection({required this.title, required this.entries});

  final String title;
  final List<WorkspaceEntry> entries;
}

/// Returns the workspace navigation groups for a given session summary.
/// The result is a list of sections that the home surface renders in
/// order. Roles that the user does not have are omitted entirely; the
/// server remains authoritative for the actual route handlers.
List<WorkspaceSection> workspaceSectionsFor(MySummary summary) {
  final sections = <WorkspaceSection>[];

  if (summary.isCreator) {
    sections.add(
      const WorkspaceSection(
        title: 'Creator workspace',
        entries: [
          WorkspaceEntry(
            label: 'Creator dashboard',
            icon: Icons.dashboard_outlined,
            route: '/creator/dashboard',
            description: 'Sales, reviews, and revenue summary.',
          ),
          WorkspaceEntry(
            label: 'My guides',
            icon: Icons.menu_book_outlined,
            route: '/guides',
            description: 'Authoring, planning, and releases.',
          ),
          WorkspaceEntry(
            label: 'Plan routes & budget',
            icon: Icons.map_outlined,
            route: '/planning',
            description: 'Day-by-day routes, budget lines, and party totals.',
          ),
          WorkspaceEntry(
            label: 'License policies',
            icon: Icons.gavel_outlined,
            route: '/license-panel',
            description: 'Commercial and remix defaults.',
          ),
        ],
      ),
    );
  } else {
    sections.add(
      const WorkspaceSection(
        title: 'Get started',
        entries: [
          WorkspaceEntry(
            label: 'Become a creator',
            icon: Icons.edit_outlined,
            route: '/creator/enroll',
            description: 'Submit a slug and bio to publish your own guides.',
          ),
        ],
      ),
    );
  }

  sections.add(
    const WorkspaceSection(
      title: 'Discover & plan',
      entries: [
        WorkspaceEntry(
          label: 'Discover guides',
          icon: Icons.explore_outlined,
          route: '/discover',
          description: 'Browse the public catalog and curated trips.',
        ),
        WorkspaceEntry(
          label: 'My library',
          icon: Icons.collections_bookmark_outlined,
          route: '/library',
          description: 'Purchased guides, forks, and saved trips.',
        ),
      ],
    ),
  );

  if (summary.isAdministrator) {
    sections.add(
      const WorkspaceSection(
        title: 'Administration',
        entries: [
          WorkspaceEntry(
            label: 'Admin operations',
            icon: Icons.admin_panel_settings_outlined,
            route: '/admin/operations',
            description: 'Audit log, users, creators, and evidence review.',
          ),
        ],
      ),
    );
  }

  if (summary.isTenant) {
    sections.add(
      const WorkspaceSection(
        title: 'Tenant',
        entries: [
          WorkspaceEntry(
            label: 'My tenant',
            icon: Icons.business_outlined,
            route: '/tenant',
            description: 'Plan, quotas, and exports for your deployment.',
          ),
          WorkspaceEntry(
            label: 'Assisted import',
            icon: Icons.cloud_download_outlined,
            route: '/assisted-import',
            description: 'Convert sources into AI-assisted drafts.',
          ),
        ],
      ),
    );
  }

  sections.add(
    const WorkspaceSection(
      title: 'Account',
      entries: [
        WorkspaceEntry(
          label: 'My profile',
          icon: Icons.person_outline,
          route: '/profile',
          description: 'Display name, avatar, and locale preferences.',
        ),
        WorkspaceEntry(
          label: 'Notifications',
          icon: Icons.notifications_outlined,
          route: '/notifications',
          description: 'Replies, remix decisions, and reviewer updates.',
        ),
        WorkspaceEntry(
          label: 'Notification preferences',
          icon: Icons.tune_outlined,
          route: '/notification-preferences',
          description: 'Choose how the service reaches you.',
        ),
      ],
    ),
  );

  return sections;
}

/// Authorized deep-link routes. The shell and `AccessDeniedRoute` use
/// this map to either render the underlying screen or show a
/// permission-denied surface when the active session lacks the role.
const Map<String, Set<String>> authorizedRouteRoles = {
  '/admin/operations': {'Administrator'},
  '/tenant': {'Tenant'},
  '/assisted-import': {'Tenant', 'Creator', 'Administrator'},
  '/license-panel': {'Creator', 'Administrator'},
  '/creator/dashboard': {'Administrator'},
  '/plugins': {'Administrator', 'Tenant'},
};

/// Creator-only deep links. These routes accept any user whose summary
/// has `isCreator` set, regardless of explicit role labels, because the
/// creator grant is the source of truth.
const Set<String> _creatorOnlyRoutes = {
  '/guides',
  '/creator/dashboard',
  '/license-panel',
};

/// Returns true when the given route is allowed for the supplied
/// session summary. Public and globally accessible routes return true
/// unconditionally; the server is still the authority for resource
/// access.
bool isRouteAllowedFor(String route, MySummary? summary) {
  if (summary == null) return false;
  if (_creatorOnlyRoutes.contains(route)) {
    if (summary.isCreator) return true;
  }
  final required = authorizedRouteRoles[route];
  if (required == null) return true;
  for (final role in required) {
    if (summary.hasRole(role)) return true;
  }
  return false;
}

extension on MySummary {
  bool get isAdministrator => hasRole('Administrator');
  bool get isTenant => hasRole('Tenant');
  bool hasRole(String role) {
    for (final existing in roles) {
      if (existing == role) return true;
    }
    return false;
  }
}

/// Role-aware workspace home. Renders a greeting, optional
/// email-unverified banner, and a list of [WorkspaceSection]s for the
/// active session. Tapping a tile invokes [onNavigate] with the route
/// name so the host can decide between push, pushReplacement, and
/// replacement-from-root navigation.
class WorkspaceScreen extends StatelessWidget {
  const WorkspaceScreen({
    super.key,
    required this.summary,
    required this.onNavigate,
    required this.onResendVerification,
    this.resending = false,
  });

  final MySummary summary;
  final ValueChanged<String> onNavigate;
  final Future<void> Function() onResendVerification;
  final bool resending;

  @override
  Widget build(BuildContext context) {
    final sections = workspaceSectionsFor(summary);
    final greeting = _greetingName();
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Welcome back, $greeting.',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          _AccountSummaryCard(summary: summary),
          if (!summary.emailConfirmed) ...[
            const SizedBox(height: 16),
            _UnverifiedBanner(
              email: summary.email,
              onResend: onResendVerification,
              busy: resending,
            ),
          ],
          const SizedBox(height: 16),
          for (final section in sections) ...[
            _WorkspaceSectionView(section: section, onNavigate: onNavigate),
            const SizedBox(height: 16),
          ],
        ],
      ),
    );
  }

  String _greetingName() {
    final name = summary.displayName.trim();
    if (name.isEmpty) return summary.email;
    return name;
  }
}

class _AccountSummaryCard extends StatelessWidget {
  const _AccountSummaryCard({required this.summary});
  final MySummary summary;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Account summary', style: theme.textTheme.titleMedium),
            const SizedBox(height: 8),
            _SummaryRow(label: 'Email', value: summary.email),
            const SizedBox(height: 6),
            _SummaryRow(
              label: 'Roles',
              value: summary.roles.isEmpty ? 'None' : summary.roles.join(', '),
            ),
            const SizedBox(height: 6),
            _SummaryRow(label: 'Status', value: summary.accountStatus),
            const SizedBox(height: 6),
            _SummaryRow(
              label: 'Creator',
              value: summary.isCreator ? 'Yes' : 'No',
              accent: summary.isCreator ? colorScheme.primary : null,
            ),
          ],
        ),
      ),
    );
  }
}

class _SummaryRow extends StatelessWidget {
  const _SummaryRow({required this.label, required this.value, this.accent});

  final String label;
  final String value;
  final Color? accent;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(width: 88, child: Text(label)),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            value,
            style: TextStyle(color: accent ?? colorScheme.onSurfaceVariant),
            overflow: TextOverflow.ellipsis,
            maxLines: 2,
          ),
        ),
      ],
    );
  }
}

class _UnverifiedBanner extends StatelessWidget {
  const _UnverifiedBanner({
    required this.email,
    required this.onResend,
    required this.busy,
  });

  final String email;
  final Future<void> Function() onResend;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Container(
      decoration: BoxDecoration(
        color: colorScheme.tertiaryContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      padding: const EdgeInsets.all(12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.mark_email_unread_outlined, color: colorScheme.onTertiaryContainer),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Verify your email',
                  style: Theme.of(context).textTheme.titleSmall,
                ),
                const SizedBox(height: 4),
                Text(
                  'Confirm $email to unlock purchases, forks, and creator tools.',
                ),
                const SizedBox(height: 8),
                TextButton.icon(
                  onPressed: busy ? null : () => onResend(),
                  icon: busy
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.refresh),
                  label: Text(busy ? 'Sending…' : 'Resend'),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _WorkspaceSectionView extends StatelessWidget {
  const _WorkspaceSectionView({
    required this.section,
    required this.onNavigate,
  });

  final WorkspaceSection section;
  final ValueChanged<String> onNavigate;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
          child: Semantics(
            header: true,
            child: Text(section.title, style: theme.textTheme.titleMedium),
          ),
        ),
        Card(
          child: Column(
            children: [
              for (var i = 0; i < section.entries.length; i++) ...[
                _WorkspaceTile(
                  entry: section.entries[i],
                  onTap: () => onNavigate(section.entries[i].route),
                ),
                if (i < section.entries.length - 1)
                  const Divider(height: 1, indent: 56),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class _WorkspaceTile extends StatelessWidget {
  const _WorkspaceTile({required this.entry, required this.onTap});

  final WorkspaceEntry entry;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return ListTile(
      onTap: onTap,
      leading: Icon(entry.icon, color: colorScheme.primary),
      title: Text(
        entry.label,
        overflow: TextOverflow.ellipsis,
        maxLines: 1,
      ),
      subtitle: entry.description == null
          ? null
          : Text(
              entry.description!,
              overflow: TextOverflow.ellipsis,
              maxLines: 2,
            ),
      trailing: Icon(
        Icons.chevron_right,
        color: colorScheme.onSurfaceVariant,
      ),
    );
  }
}

/// Surface shown when an authenticated user follows a deep link to a
/// route they are not authorized for. Server authorization remains the
/// authority; this is a presentation fallback so the user is not
/// silently routed away from their intended destination.
class AccessDeniedScreen extends StatelessWidget {
  const AccessDeniedScreen({
    super.key,
    required this.route,
    this.onReturnHome,
  });

  final String route;
  final VoidCallback? onReturnHome;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Icon(
                Icons.lock_outline,
                size: 56,
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
              const SizedBox(height: 16),
              Text(
                'Access denied',
                style: Theme.of(context).textTheme.headlineSmall,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 8),
              Text(
                'Your account does not have permission to open $route. '
                'Return to your workspace or sign in with a different account.',
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 24),
              FilledButton.icon(
                onPressed: onReturnHome,
                icon: const Icon(Icons.home_outlined),
                label: const Text('Back to workspace'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Wraps [child] with a centered, width-constrained container so long
/// forms and tile lists remain readable on a desktop browser while
/// filling the available width on a phone.
class ConstrainedWorkspace extends StatelessWidget {
  const ConstrainedWorkspace({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: kWorkspaceMaxContentWidth),
        child: child,
      ),
    );
  }
}
