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
  final AppApi api;
  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'Trippify',
    localizationsDelegates: GlobalMaterialLocalizations.delegates,
    supportedLocales: const [Locale('en'), Locale('zh')],
    routes: {
      '/': (_) => SystemScreen(api: api),
      '/sign-in': (_) => SignInScreen(api: api),
      '/register': (_) => RegistrationScreen(api: api),
      '/profile': (_) => ProfileScreen(api: api),
      '/creator/enroll': (_) => CreatorEnrollmentScreen(api: api),
      '/creator': (_) => PublicCreatorScreen(api: api),
    },
  );
}

class SystemScreen extends StatefulWidget {
  const SystemScreen({super.key, required this.api});
  final AppApi api;
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
          return Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Semantics(
                label: 'API version',
                child: Text(
                  '${snapshot.data!.name} ${snapshot.data!.apiVersion}',
                ),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => Navigator.pushNamed(context, '/sign-in'),
                child: const Text('Sign in'),
              ),
              TextButton(
                onPressed: () => Navigator.pushNamed(context, '/register'),
                child: const Text('Create account'),
              ),
              TextButton(
                onPressed: () => Navigator.pushNamed(context, '/profile'),
                child: const Text('My profile'),
              ),
              TextButton(
                onPressed: () => Navigator.pushNamed(context, '/creator'),
                child: const Text('Find a creator'),
              ),
              TextButton(
                onPressed: () =>
                    Navigator.pushNamed(context, '/creator/enroll'),
                child: const Text('Become a creator'),
              ),
            ],
          );
        },
      ),
    ),
  );
}

class RegistrationScreen extends StatefulWidget {
  const RegistrationScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<RegistrationScreen> createState() => _RegistrationScreenState();
}

class _RegistrationScreenState extends State<RegistrationScreen> {
  final email = TextEditingController();
  final password = TextEditingController();
  String? status;
  Future<void> submit() async {
    if (email.text.trim().isEmpty || password.text.length < 10) {
      setState(() => status = 'Enter a valid email and strong password.');
      return;
    }
    try {
      await widget.api.register(email.text.trim(), password.text);
      setState(() => status = 'Check your email to confirm your account.');
    } catch (_) {
      setState(() => status = 'Unable to create account.');
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Create account')),
    body: ListView(
      padding: const EdgeInsets.all(24),
      children: [
        TextField(
          controller: email,
          keyboardType: TextInputType.emailAddress,
          decoration: const InputDecoration(labelText: 'Email'),
        ),
        TextField(
          controller: password,
          obscureText: true,
          decoration: const InputDecoration(labelText: 'Password'),
        ),
        if (status != null) Semantics(liveRegion: true, child: Text(status!)),
        FilledButton(onPressed: submit, child: const Text('Create account')),
      ],
    ),
  );
}

class SignInScreen extends StatefulWidget {
  const SignInScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<SignInScreen> createState() => _SignInScreenState();
}

class _SignInScreenState extends State<SignInScreen> {
  final email = TextEditingController();
  final password = TextEditingController();
  bool busy = false;
  String? error;
  Future<void> submit() async {
    if (email.text.trim().isEmpty || password.text.length < 10) {
      setState(() => error = 'Enter a valid email and password.');
      return;
    }
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await widget.api.login(email.text.trim(), password.text);
      if (mounted) Navigator.pushReplacementNamed(context, '/profile');
    } catch (_) {
      if (mounted) setState(() => error = 'Sign in failed.');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Sign in')),
    body: ListView(
      padding: const EdgeInsets.all(24),
      children: [
        TextField(
          controller: email,
          keyboardType: TextInputType.emailAddress,
          autofillHints: const [AutofillHints.email],
          decoration: const InputDecoration(labelText: 'Email'),
        ),
        TextField(
          controller: password,
          obscureText: true,
          autofillHints: const [AutofillHints.password],
          decoration: const InputDecoration(labelText: 'Password'),
        ),
        if (error != null) Semantics(liveRegion: true, child: Text(error!)),
        FilledButton(
          onPressed: busy ? null : submit,
          child: busy
              ? const CircularProgressIndicator()
              : const Text('Sign in'),
        ),
      ],
    ),
  );
}

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  final displayName = TextEditingController();
  String? saved;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('My profile')),
    body: FutureBuilder<PrivateProfile>(
      future: widget.api.getProfile(),
      builder: (context, s) {
        if (s.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (s.hasError) {
          return const Center(child: Text('Unable to load profile'));
        }
        return ListView(
          padding: const EdgeInsets.all(24),
          children: [
            Text(
              s.data!.displayName.isEmpty
                  ? 'Profile not completed'
                  : s.data!.displayName,
            ),
            Text(s.data!.email),
            Text('Account: ${s.data!.status}'),
            TextField(
              controller: displayName,
              decoration: const InputDecoration(labelText: 'Display name'),
            ),
            FilledButton(
              onPressed: () async {
                await widget.api.updateProfile(displayName.text, null, null);
                setState(() => saved = 'Profile saved.');
              },
              child: const Text('Save profile'),
            ),
            if (saved != null) Semantics(liveRegion: true, child: Text(saved!)),
          ],
        );
      },
    ),
  );
}

class CreatorEnrollmentScreen extends StatefulWidget {
  const CreatorEnrollmentScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<CreatorEnrollmentScreen> createState() =>
      _CreatorEnrollmentScreenState();
}

class _CreatorEnrollmentScreenState extends State<CreatorEnrollmentScreen> {
  final slug = TextEditingController();
  final bio = TextEditingController();
  String? status;
  Future<void> submit() async {
    try {
      await widget.api.enrollCreator(slug.text, bio.text, const []);
      setState(() => status = 'Enrollment submitted.');
    } catch (_) {
      setState(() => status = 'Unable to submit enrollment.');
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Become a creator')),
    body: ListView(
      padding: const EdgeInsets.all(24),
      children: [
        TextField(
          controller: slug,
          decoration: const InputDecoration(labelText: 'Public URL name'),
        ),
        TextField(
          controller: bio,
          maxLength: 2000,
          decoration: const InputDecoration(labelText: 'Biography'),
        ),
        if (status != null) Semantics(liveRegion: true, child: Text(status!)),
        FilledButton(onPressed: submit, child: const Text('Submit enrollment')),
      ],
    ),
  );
}

class PublicCreatorScreen extends StatefulWidget {
  const PublicCreatorScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<PublicCreatorScreen> createState() => _PublicCreatorScreenState();
}

class _PublicCreatorScreenState extends State<PublicCreatorScreen> {
  final slug = TextEditingController();
  PublicCreator? creator;
  String? error;
  Future<void> search() async {
    try {
      final result = await widget.api.getCreator(slug.text.trim());
      setState(() {
        creator = result;
        error = null;
      });
    } catch (_) {
      setState(() {
        creator = null;
        error = 'Creator not found.';
      });
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Creator profile')),
    body: ListView(
      padding: const EdgeInsets.all(24),
      children: [
        TextField(
          controller: slug,
          decoration: const InputDecoration(labelText: 'Creator URL name'),
        ),
        FilledButton(onPressed: search, child: const Text('Find creator')),
        if (error != null) Semantics(liveRegion: true, child: Text(error!)),
        if (creator != null) ...[
          Text(creator!.displayName),
          Text(creator!.biography),
          Text(creator!.travelCountries.join(', ')),
        ],
      ],
    ),
  );
}
