// Shared policy logic for the Flutter skip allowlist. Re-used by
// `tool/check_flutter_skips.dart` and by `test/skip_policy_test.dart` so the
// unapproved-skip path is exercised both in CI and at the unit-test level.

import 'dart:io';

class AllowlistEntry {
  AllowlistEntry({
    required this.id,
    required this.path,
    required this.line,
    required this.reason,
    required this.scope,
  });

  final String id;
  final String path;
  final int line;
  final String reason;
  final String scope;

  static AllowlistEntry fromJson(Map<String, dynamic> json) => AllowlistEntry(
        id: json['id'] as String,
        path: json['path'] as String,
        line: json['line'] as int,
        reason: json['reason'] as String,
        scope: json['scope'] as String? ?? 'release-quality-gates',
      );
}

class SkipFinding {
  SkipFinding({required this.path, required this.line, required this.slug});

  final String path;
  final int line;
  final String? slug;
}

class SkipCheckResult {
  SkipCheckResult({
    required this.budget,
    required this.findings,
    required this.errors,
  });

  final int budget;
  final List<SkipFinding> findings;
  final List<String> errors;
}

SkipCheckResult checkSkips({
  required Directory testRoot,
  required Map<String, dynamic> allowlist,
  List<String> excludePaths = const [],
}) {
  final budget = allowlist['budget'] as int? ?? 0;
  final entries = (allowlist['skips'] as List)
      .cast<Map<String, dynamic>>()
      .map(AllowlistEntry.fromJson)
      .toList();

  final dartFiles = testRoot
      .listSync(recursive: true)
      .whereType<File>()
      .where((f) => f.path.endsWith('.dart'))
      .where((f) => !excludePaths.contains(f.path))
      .toList()
    ..sort((a, b) => a.path.compareTo(b.path));

  final findings = <SkipFinding>[];
  for (final file in dartFiles) {
    final lines = file.readAsLinesSync();
    for (var i = 0; i < lines.length; i++) {
      if (!_isSkipMarker(lines[i])) continue;
      findings.add(
        SkipFinding(
          path: file.path,
          line: i + 1,
          slug: _extractAllowedSkip(lines, i),
        ),
      );
    }
  }

  final errors = <String>[];
  final seenSlugs = <String>{};
  for (final finding in findings) {
    final slug = finding.slug;
    if (slug == null || slug.isEmpty) {
      errors.add(
        '${finding.path}:${finding.line}: skip is missing an `// allowed-skip: <id>` annotation on or after this line.',
      );
      continue;
    }
    final entry = entries.where((e) => e.id == slug).cast<AllowlistEntry?>().firstWhere(
          (_) => true,
          orElse: () => null,
        );
    if (entry == null) {
      errors.add(
        '${finding.path}:${finding.line}: skip id "$slug" is not declared in the allowlist.',
      );
      continue;
    }
    seenSlugs.add(slug);
    if (entry.path != finding.path) {
      errors.add(
        '${finding.path}:${finding.line}: skip id "$slug" is anchored to ${entry.path}:${entry.line}; update the allowlist when relocating a skip.',
      );
    } else if (entry.line != finding.line) {
      errors.add(
        '${finding.path}:${finding.line}: skip id "$slug" is anchored to line ${entry.line}; update the allowlist when moving a skip.',
      );
    }
  }

  for (final entry in entries) {
    if (!seenSlugs.contains(entry.id)) {
      errors.add(
        'allowlist entry "${entry.id}" (${entry.path}:${entry.line}) no longer matches a current skip.',
      );
    }
  }

  if (findings.length > budget) {
    errors.add(
      '${findings.length} skip(s) exceed the release budget of $budget.',
    );
  }

  return SkipCheckResult(
    budget: budget,
    findings: findings,
    errors: errors,
  );
}

bool _isSkipMarker(String line) {
  final trimmed = line.trim();
  if (!trimmed.contains('skip:')) return false;
  if (trimmed.contains('skip: false')) return false;
  return trimmed.contains('true') || trimmed.endsWith(',');
}

String? _extractAllowedSkip(List<String> lines, int index) {
  const marker = 'allowed-skip:';
  for (var i = index; i < lines.length && i < index + 5; i++) {
    final line = lines[i];
    final commentStart = line.indexOf('//');
    if (commentStart < 0) continue;
    final comment = line.substring(commentStart);
    final at = comment.indexOf(marker);
    if (at < 0) continue;
    final tail = comment.substring(at + marker.length).trim();
    final id = tail.split(RegExp(r'\s')).first;
    if (id.isEmpty) return null;
    return id;
  }
  return null;
}
