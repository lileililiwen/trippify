import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import '../tool/src/skip_policy.dart';

void main() {
  group('checkSkips', () {
    late Directory sandbox;

    setUp(() {
      sandbox = Directory.systemTemp.createTempSync('trippify_skip_policy_');
    });

    tearDown(() {
      if (sandbox.existsSync()) sandbox.deleteSync(recursive: true);
    });

    test('flags unapproved skip markers as errors', () {
      final testDir = Directory('${sandbox.path}/test')..createSync();
      File('${testDir.path}/a_test.dart').writeAsStringSync(
        "testWidgets('missing approval', (tester) async {}, skip: true);\n",
      );
      final result = checkSkips(
        testRoot: Directory(testDir.path),
        allowlist: {
          'budget': 0,
          'skips': <Map<String, dynamic>>[],
        },
      );
      expect(result.errors, isNotEmpty);
      expect(
        result.errors.first,
        contains(r'missing an `// allowed-skip: <id>`'),
      );
    });

    test('rejects an unknown skip id even when an allowlist entry exists', () {
      final testDir = Directory('${sandbox.path}/test')..createSync();
      File('${testDir.path}/b_test.dart').writeAsStringSync(
        "testWidgets('wrong id', (tester) async {}, skip: true);"
        " // allowed-skip: not-in-allowlist | the reason\n",
      );
      final result = checkSkips(
        testRoot: Directory(testDir.path),
        allowlist: {
          'budget': 1,
          'skips': [
            {
              'id': 'approved',
              'path': '${testDir.path}/b_test.dart',
              'line': 1,
              'reason': 'reason',
              'scope': 'release-quality-gates',
            }
          ],
        },
      );
      expect(
        result.errors.any(
          (e) => e.contains('not-in-allowlist') && e.contains('not declared'),
        ),
        isTrue,
        reason: 'mismatched id should produce a clear error',
      );
    });

    test('accepts the production skip file against the production allowlist',
        () {
      final result = checkSkips(
        testRoot: Directory('test'),
        allowlist: _productionAllowlist(),
        excludePaths: const ['test/skip_policy_test.dart'],
      );
      expect(
        result.errors,
        isEmpty,
        reason: 'production allowlist and skip markers must remain in sync',
      );
      expect(result.findings.length, 0);
      expect(result.budget, 0);
    });

    test('detects when the budget is exceeded', () {
      final testDir = Directory('${sandbox.path}/test')..createSync();
      File('${testDir.path}/c_test.dart').writeAsStringSync(
        "testWidgets('over budget', (tester) async {}, skip: true);\n",
      );
      final result = checkSkips(
        testRoot: Directory(testDir.path),
        allowlist: {
          'budget': 0,
          'skips': <Map<String, dynamic>>[],
        },
      );
      expect(
        result.errors.any((e) => e.contains('exceed the release budget of 0')),
        isTrue,
      );
    });
  });
}

Map<String, dynamic> _productionAllowlist() {
  final file = File('tool/skip_allowlist.json');
  if (!file.existsSync()) {
    throw StateError(
      'tool/skip_allowlist.json is missing; the unit test cannot compare against production data.',
    );
  }
  return jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
}
