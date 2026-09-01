// ignore_for_file: avoid_print
// Standalone Dart script: validates Flutter test skip markers against the
// repository's allowlist. Run via `dart run tool/check_flutter_skips.dart`
// from `apps/trippify_flutter/`. Exits non-zero when a skip is unapproved,
// when the allowlist entry has drifted, or when the comment annotation is
// missing.

library;

import 'dart:convert';
import 'dart:io';

import 'src/skip_policy.dart';

void main(List<String> args) {
  final testRoot = Directory('test');
  final allowlistFile = File('tool/skip_allowlist.json');
  if (!testRoot.existsSync()) {
    stderr.writeln('error: test/ directory not found; run from the Flutter app root.');
    exit(2);
  }
  if (!allowlistFile.existsSync()) {
    stderr.writeln('error: tool/skip_allowlist.json is missing.');
    exit(2);
  }
  final allowlist = jsonDecode(allowlistFile.readAsStringSync()) as Map<String, dynamic>;
  final result = checkSkips(
    testRoot: testRoot,
    allowlist: allowlist,
    excludePaths: const ['test/skip_policy_test.dart'],
  );
  if (result.errors.isNotEmpty) {
    for (final message in result.errors) {
      stderr.writeln(message);
    }
    stderr.writeln('check_flutter_skips: ${result.errors.length} error(s).');
    exit(1);
  }
  stdout.writeln(
    'check_flutter_skips: ${result.findings.length} skip(s) accounted for; budget ${result.budget}.',
  );
}
