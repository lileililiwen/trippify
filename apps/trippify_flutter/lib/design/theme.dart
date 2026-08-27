import 'package:flutter/material.dart';

import 'tokens.dart';

const Color _seed = Color(0xFF6750A4);

ThemeData _buildTheme(Brightness brightness) {
  final scheme = ColorScheme.fromSeed(seedColor: _seed, brightness: brightness);
  final tokens = brightness == Brightness.dark
      ? TrippifyTokens.dark
      : TrippifyTokens.light;
  return ThemeData(
    colorScheme: scheme,
    useMaterial3: true,
    brightness: brightness,
    appBarTheme: const AppBarTheme(centerTitle: true, elevation: 2),
    cardTheme: CardThemeData(
      elevation: 3,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
        textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
        textStyle: const TextStyle(fontSize: 16),
      ),
    ),
    extensions: <ThemeExtension<dynamic>>[tokens],
  );
}

final ThemeData lightTheme = _buildTheme(Brightness.light);
final ThemeData darkTheme = _buildTheme(Brightness.dark);
