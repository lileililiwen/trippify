import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'api_client.dart';

void main() => runApp(
  TrippifyApp(
    api: ApiClient(
      Uri.parse(
        const String.fromEnvironment(
          'API_BASE_URL',
          defaultValue: 'http://localhost:5000',
        ),
      ),
    ),
  ),
);

class TrippifyApp extends StatelessWidget {
  const TrippifyApp({super.key, required this.api});
  final SystemApi api;
  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'Trippify',
    localizationsDelegates: GlobalMaterialLocalizations.delegates,
    supportedLocales: const [Locale('en'), Locale('zh')],
    home: SystemScreen(api: api),
  );
}

class SystemScreen extends StatefulWidget {
  const SystemScreen({super.key, required this.api});
  final SystemApi api;
  @override
  State<SystemScreen> createState() => _SystemScreenState();
}

class _SystemScreenState extends State<SystemScreen> {
  late Future<SystemInfo> _result;
  @override
  void initState() {
    super.initState();
    _result = widget.api.getSystemInfo();
  }

  void _retry() => setState(() => _result = widget.api.getSystemInfo());
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Trippify')),
    body: Center(
      child: FutureBuilder<SystemInfo>(
        future: _result,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const CircularProgressIndicator();
          }
          if (snapshot.hasError) {
            return Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('Unable to reach the service'),
                FilledButton(onPressed: _retry, child: const Text('Retry')),
              ],
            );
          }
          return Semantics(
            label: 'API version',
            child: Text('${snapshot.data!.name} ${snapshot.data!.apiVersion}'),
          );
        },
      ),
    ),
  );
}
