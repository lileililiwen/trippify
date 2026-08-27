import 'package:flutter/material.dart';

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
              FilledButton(onPressed: onRetry, child: const Text('Retry')),
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
