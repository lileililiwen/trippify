import 'package:flutter/material.dart';

import '../l10n/generated/app_localizations.dart';

/// Centered spinner used by every screen while its future is pending.
class LoadingState extends StatelessWidget {
  const LoadingState({super.key, this.padding = const EdgeInsets.all(24)});

  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) =>
      Center(child: Padding(padding: padding, child: const CircularProgressIndicator()));
}

/// Inline error renderer with an optional retry button.
///
/// Use the [ErrorState.message] as the human-readable text (usually mapped
/// from [AppError] — see [api_client.dart]).
class ErrorState extends StatelessWidget {
  const ErrorState({super.key, required this.message, this.onRetry});

  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final l10n = AppLocalizations.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.error_outline, color: colorScheme.error, size: 48),
            const SizedBox(height: 12),
            Text(
              message,
              style: Theme.of(context).textTheme.bodyLarge,
              textAlign: TextAlign.center,
            ),
            if (onRetry != null) ...[
              const SizedBox(height: 16),
              FilledButton(onPressed: onRetry, child: Text(l10n?.retry ?? 'Retry')),
            ],
          ],
        ),
      ),
    );
  }
}

/// Empty-state renderer with an optional CTA action.
class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.message, this.action, this.icon});

  final String message;
  final Widget? action;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              icon ?? Icons.inbox_outlined,
              size: 48,
              color: theme.colorScheme.onSurfaceVariant,
            ),
            const SizedBox(height: 12),
            Text(
              message,
              style: theme.textTheme.bodyLarge,
              textAlign: TextAlign.center,
            ),
            if (action != null) ...[
              const SizedBox(height: 16),
              action!,
            ],
          ],
        ),
      ),
    );
  }
}

/// Provider taxonomy surfaced by the map and media surfaces. Each value
/// maps to a deliberate UI treatment; the renderer never fabricates data
/// when a provider is unavailable.
enum ProviderStatus { loading, success, unavailable, denied, offline }

class ProviderStateView extends StatelessWidget {
  const ProviderStateView({
    super.key,
    required this.status,
    required this.successMessage,
    this.onRetry,
    this.success,
    this.unavailableMessage = 'Provider is not configured for this environment.',
    this.deniedMessage = 'You do not have access to this provider.',
    this.offlineMessage = 'Provider is unreachable. Check your connection and retry.',
    this.icon,
  });

  final ProviderStatus status;
  final String successMessage;
  final Widget? success;
  final VoidCallback? onRetry;
  final String unavailableMessage;
  final String deniedMessage;
  final String offlineMessage;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final retryLabel = l10n?.retry ?? 'Retry';
    switch (status) {
      case ProviderStatus.loading:
        return const LoadingState();
      case ProviderStatus.success:
        return success ?? EmptyState(message: successMessage, icon: Icons.check_circle_outline);
      case ProviderStatus.unavailable:
        return EmptyState(
          message: unavailableMessage,
          icon: Icons.cloud_off_outlined,
          action: onRetry == null ? null : FilledButton(onPressed: onRetry, child: Text(retryLabel)),
        );
      case ProviderStatus.denied:
        return EmptyState(message: deniedMessage, icon: Icons.lock_outline);
      case ProviderStatus.offline:
        return EmptyState(
          message: offlineMessage,
          icon: Icons.wifi_off_outlined,
          action: onRetry == null ? null : FilledButton(onPressed: onRetry, child: Text(retryLabel)),
        );
    }
  }
}

/// Compact inline status badge used to label attribution, retries, and
/// resolution state next to map markers and media thumbnails.
class ProviderStatusBadge extends StatelessWidget {
  const ProviderStatusBadge({super.key, required this.label, this.tooltip});

  final String label;
  final String? tooltip;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final chip = Chip(
      visualDensity: VisualDensity.compact,
      materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
      avatar: const Icon(Icons.map_outlined, size: 16),
      label: Text(label, style: theme.textTheme.labelSmall),
    );
    return tooltip == null ? chip : Tooltip(message: tooltip!, child: chip);
  }
}

/// Visual placeholder for a map surface. Replaces the static "Map preview"
/// tile with loading, success with markers, unresolved geocode, denied
/// permission, or offline retries.
class MapSurfaceView extends StatelessWidget {
  const MapSurfaceView({
    super.key,
    required this.status,
    required this.markers,
    this.attribution,
    this.unresolvedCount = 0,
    this.onRetry,
  });

  final ProviderStatus status;
  final List<MapMarkerState> markers;
  final String? attribution;
  final int unresolvedCount;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final surface = theme.colorScheme.surfaceContainerHigh;
    final onSurface = theme.colorScheme.onSurfaceVariant;

    Widget body;
    switch (status) {
      case ProviderStatus.loading:
        body = const Center(child: CircularProgressIndicator());
        break;
      case ProviderStatus.success:
        if (markers.isEmpty) {
          body = Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.map_outlined, size: 48, color: onSurface),
                const SizedBox(height: 8),
                Text(
                  'No coordinates yet. Add places with an address.',
                  style: theme.textTheme.bodyMedium?.copyWith(color: onSurface),
                  textAlign: TextAlign.center,
                ),
                if (attribution != null) ...[
                  const SizedBox(height: 8),
                  ProviderStatusBadge(label: attribution!),
                ],
              ],
            ),
          );
        } else {
          body = Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Padding(
                padding: const EdgeInsets.all(12),
                child: Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  alignment: WrapAlignment.center,
                  children: [
                    for (final marker in markers)
                      ProviderStatusBadge(
                        label: marker.label,
                        tooltip: marker.subtitle,
                      ),
                  ],
                ),
              ),
              if (attribution != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: ProviderStatusBadge(label: attribution!),
                ),
              if (unresolvedCount > 0)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12, left: 16, right: 16),
                  child: Text(
                    '$unresolvedCount location${unresolvedCount == 1 ? '' : 's'} could not be resolved.',
                    style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
                    textAlign: TextAlign.center,
                  ),
                ),
            ],
          );
        }
        break;
      case ProviderStatus.unavailable:
        body = ProviderStateView(
          status: status,
          successMessage: '',
          onRetry: onRetry,
        );
        break;
      case ProviderStatus.denied:
        body = const ProviderStateView(
          status: ProviderStatus.denied,
          successMessage: '',
          unavailableMessage: 'Map access denied for this guide.',
          deniedMessage: 'You do not have permission to view this map.',
        );
        break;
      case ProviderStatus.offline:
        body = ProviderStateView(
          status: ProviderStatus.offline,
          successMessage: '',
          offlineMessage: 'Map provider is unreachable. Check your connection and retry.',
          onRetry: onRetry,
        );
        break;
    }

    return Container(
      height: 200,
      width: double.infinity,
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      decoration: BoxDecoration(
        color: surface,
        borderRadius: BorderRadius.circular(12),
      ),
      child: body,
    );
  }
}

/// Single marker rendered on the [MapSurfaceView] surface.
class MapMarkerState {
  const MapMarkerState({required this.label, this.subtitle});
  final String label;
  final String? subtitle;
}

