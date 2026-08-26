import 'package:flutter_test/flutter_test.dart';
import 'package:trippify_flutter/api_client.dart';
import 'package:trippify_flutter/main.dart';

class FakeApi implements SystemApi {
  FakeApi(this.result, {this.error});
  final Future<SystemInfo> result;
  final Object? error;
  @override
  Future<SystemInfo> getSystemInfo() async {
    if (error != null) throw error!;
    return result;
  }
}

void main() {
  testWidgets('shows API result', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(Future.value(const SystemInfo('Trippify', 'v1'))),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Trippify v1'), findsOneWidget);
  });
  testWidgets('shows retry state', (tester) async {
    await tester.pumpWidget(
      TrippifyApp(
        api: FakeApi(
          Future.value(const SystemInfo('', '')),
          error: StateError('offline'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Unable to reach the service'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });
}
