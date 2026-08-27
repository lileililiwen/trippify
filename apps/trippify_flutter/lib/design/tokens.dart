import 'package:flutter/material.dart';

/// Semantic color, spacing, and radius tokens for the Trippify client.
///
/// The M3 [ColorScheme] covers primary/secondary/tertiary/error and their
/// containers. It does not cover domain concepts like "creator status",
/// "purchased", "free", or "warning" — that is what this extension is for.
@immutable
class TrippifyTokens extends ThemeExtension<TrippifyTokens> {
  const TrippifyTokens({
    required this.creator,
    required this.purchased,
    required this.free,
    required this.warning,
    required this.danger,
    required this.onSurfaceMuted,
    required this.spaceXs,
    required this.spaceSm,
    required this.spaceMd,
    required this.spaceLg,
    required this.spaceXl,
    required this.spaceXxl,
    required this.radiusSm,
    required this.radiusMd,
    required this.radiusLg,
    required this.radiusXl,
  });

  /// Color used to indicate a creator-owned asset (e.g. isCreator: Yes).
  final Color creator;

  /// Color used to indicate a purchased guide or paid entitlement.
  final Color purchased;

  /// Color used to indicate a free guide.
  final Color free;

  /// Color used for non-fatal warnings (e.g. "verify your email").
  final Color warning;

  /// Color used for fatal errors and destructive actions.
  final Color danger;

  /// AA-compliant muted body text color (≥ 4.5:1 on `colorScheme.surface`).
  final Color onSurfaceMuted;

  // Spacing scale — 4pt grid.
  final double spaceXs; // 4
  final double spaceSm; // 8
  final double spaceMd; // 12
  final double spaceLg; // 16
  final double spaceXl; // 24
  final double spaceXxl; // 32

  // Radius scale.
  final double radiusSm; // 6
  final double radiusMd; // 10
  final double radiusLg; // 16
  final double radiusXl; // 24

  static const TrippifyTokens light = TrippifyTokens(
    creator: Color(0xFF6750A4),
    purchased: Color(0xFF2E7D32),
    free: Color(0xFF1565C0),
    warning: Color(0xFFB26A00),
    danger: Color(0xFFB3261E),
    onSurfaceMuted: Color(0xFF49454F), // passes 4.5:1 on Color(0xFFFEF7FF)
    spaceXs: 4,
    spaceSm: 8,
    spaceMd: 12,
    spaceLg: 16,
    spaceXl: 24,
    spaceXxl: 32,
    radiusSm: 6,
    radiusMd: 10,
    radiusLg: 16,
    radiusXl: 24,
  );

  static const TrippifyTokens dark = TrippifyTokens(
    creator: Color(0xFFD0BCFF),
    purchased: Color(0xFF81C784),
    free: Color(0xFF64B5F6),
    warning: Color(0xFFFFB74D),
    danger: Color(0xFFF2B8B5),
    onSurfaceMuted: Color(0xFFCAC4D0), // passes 4.5:1 on Color(0xFF141218)
    spaceXs: 4,
    spaceSm: 8,
    spaceMd: 12,
    spaceLg: 16,
    spaceXl: 24,
    spaceXxl: 32,
    radiusSm: 6,
    radiusMd: 10,
    radiusLg: 16,
    radiusXl: 24,
  );

  @override
  TrippifyTokens copyWith({
    Color? creator,
    Color? purchased,
    Color? free,
    Color? warning,
    Color? danger,
    Color? onSurfaceMuted,
    double? spaceXs,
    double? spaceSm,
    double? spaceMd,
    double? spaceLg,
    double? spaceXl,
    double? spaceXxl,
    double? radiusSm,
    double? radiusMd,
    double? radiusLg,
    double? radiusXl,
  }) {
    return TrippifyTokens(
      creator: creator ?? this.creator,
      purchased: purchased ?? this.purchased,
      free: free ?? this.free,
      warning: warning ?? this.warning,
      danger: danger ?? this.danger,
      onSurfaceMuted: onSurfaceMuted ?? this.onSurfaceMuted,
      spaceXs: spaceXs ?? this.spaceXs,
      spaceSm: spaceSm ?? this.spaceSm,
      spaceMd: spaceMd ?? this.spaceMd,
      spaceLg: spaceLg ?? this.spaceLg,
      spaceXl: spaceXl ?? this.spaceXl,
      spaceXxl: spaceXxl ?? this.spaceXxl,
      radiusSm: radiusSm ?? this.radiusSm,
      radiusMd: radiusMd ?? this.radiusMd,
      radiusLg: radiusLg ?? this.radiusLg,
      radiusXl: radiusXl ?? this.radiusXl,
    );
  }

  @override
  TrippifyTokens lerp(ThemeExtension<TrippifyTokens>? other, double t) {
    if (other is! TrippifyTokens) return this;
    return TrippifyTokens(
      creator: Color.lerp(creator, other.creator, t)!,
      purchased: Color.lerp(purchased, other.purchased, t)!,
      free: Color.lerp(free, other.free, t)!,
      warning: Color.lerp(warning, other.warning, t)!,
      danger: Color.lerp(danger, other.danger, t)!,
      onSurfaceMuted: Color.lerp(onSurfaceMuted, other.onSurfaceMuted, t)!,
      spaceXs: spaceXs,
      spaceSm: spaceSm,
      spaceMd: spaceMd,
      spaceLg: spaceLg,
      spaceXl: spaceXl,
      spaceXxl: spaceXxl,
      radiusSm: radiusSm,
      radiusMd: radiusMd,
      radiusLg: radiusLg,
      radiusXl: radiusXl,
    );
  }
}

/// Helper to read tokens from a [BuildContext].
extension TrippifyTokensContext on BuildContext {
  TrippifyTokens get tokens =>
      Theme.of(this).extension<TrippifyTokens>() ?? TrippifyTokens.light;
}
