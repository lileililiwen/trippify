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
      '/guides': (_) => GuideWorkspaceScreen(api: api),
      '/planning': (_) => PlanningScreen(api: api),
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
              TextButton(
                onPressed: () => Navigator.pushNamed(context, '/guides'),
                child: const Text('My guides'),
              ),
              TextButton(
                onPressed: () => Navigator.pushNamed(context, '/planning'),
                child: const Text('Plan routes & budget'),
              ),
            ],
          );
        },
      ),
    ),
  );
}

class GuideWorkspaceScreen extends StatefulWidget {
  const GuideWorkspaceScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<GuideWorkspaceScreen> createState() => _GuideWorkspaceScreenState();
}

class _GuideWorkspaceScreenState extends State<GuideWorkspaceScreen> {
  late Future<List<GuideSummary>> guides;
  final title = TextEditingController();
  final country = TextEditingController(text: 'JP');
  GuideDraft? draft;
  String? status;
  @override
  void initState() {
    super.initState();
    guides = widget.api.getMyGuides();
  }

  Future<void> create() async {
    try {
      final value = await widget.api.createGuide(
        title.text.trim(),
        country.text.trim(),
      );
      if (mounted) {
        setState(() {
          draft = value;
          status = 'Draft created.';
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() => status = 'Guide access denied or unavailable.');
      }
    }
  }

  Future<void> save() async {
    try {
      final value = await widget.api.saveGuideStructure(draft!);
      if (mounted) {
        setState(() {
          draft = value;
          status = 'Guide saved.';
        });
      }
    } catch (_) {
      if (mounted) {
        setState(
          () => status = 'Guide changed elsewhere. Reload before saving.',
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('My guides')),
    body: FutureBuilder<List<GuideSummary>>(
      future: guides,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return const Center(
            child: Text('Guide access denied or unavailable.'),
          );
        }
        return ListView(
          padding: const EdgeInsets.all(24),
          children: [
            if (snapshot.data!.isEmpty)
              const Text(
                'No guides yet. Create your first structured itinerary.',
              ),
            for (final guide in snapshot.data!)
              ListTile(
                title: Text(guide.title),
                subtitle: Text(
                  '${guide.countryCode} · ${guide.tripDays} days · ${guide.lifecycle}',
                ),
              ),
            TextField(
              controller: title,
              decoration: const InputDecoration(labelText: 'Guide title'),
            ),
            TextField(
              controller: country,
              decoration: const InputDecoration(labelText: 'Country code'),
            ),
            FilledButton(onPressed: create, child: const Text('Create draft')),
            if (draft != null) ...[
              Text(
                'Editing ${draft!.title}',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              for (var i = 0; i < draft!.days.length; i++)
                ListTile(
                  title: Text(draft!.days[i]),
                  trailing: i == 0
                      ? null
                      : IconButton(
                          tooltip: 'Move day up',
                          icon: const Icon(Icons.arrow_upward),
                          onPressed: () => setState(
                            () => draft = draft!.reordered(i, i - 1),
                          ),
                        ),
                ),
              FilledButton(
                onPressed: save,
                child: const Text('Save itinerary'),
              ),
            ],
            if (status != null)
              Semantics(liveRegion: true, child: Text(status!)),
          ],
        );
      },
    ),
  );
}

class PlanningScreen extends StatefulWidget {
  const PlanningScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<PlanningScreen> createState() => _PlanningScreenState();
}

class _PlanningScreenState extends State<PlanningScreen> {
  late Future<List<GuideSummary>> guides;
  GuideSummary? guide;
  int dayPosition = 0;
  int partySize = 1;

  @override
  void initState() {
    super.initState();
    guides = widget.api.getMyGuides();
  }

  void selectGuide(GuideSummary value) => setState(() {
    guide = value;
    dayPosition = 0;
  });

  void selectDay(int value) => setState(() => dayPosition = value);

  void changePartySize(int delta) =>
      setState(() => partySize = (partySize + delta).clamp(1, 20));

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Plan routes & budget')),
    body: FutureBuilder<List<GuideSummary>>(
      future: guides,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return const Center(
            child: Text('Planning access denied or unavailable.'),
          );
        }
        final options = snapshot.data!;
        if (options.isEmpty) {
          return const Center(
            child: Text(
              'No guides yet. Create a structured itinerary to plan routes.',
            ),
          );
        }
        final selected = guide ?? options.first;
        return ListView(
          padding: const EdgeInsets.all(24),
          children: [
            DropdownButtonFormField<GuideSummary>(
              initialValue: selected,
              decoration: const InputDecoration(labelText: 'Guide'),
              items: [
                for (final option in options)
                  DropdownMenuItem(value: option, child: Text(option.title)),
              ],
              onChanged: (value) {
                if (value != null) selectGuide(value);
              },
            ),
            DropdownButtonFormField<int>(
              initialValue: dayPosition,
              decoration: const InputDecoration(labelText: 'Day'),
              items: [
                for (var i = 0; i < selected.tripDays; i++)
                  DropdownMenuItem(value: i, child: Text('Day ${i + 1}')),
              ],
              onChanged: (value) {
                if (value != null) selectDay(value);
              },
            ),
            _DayRouteSection(
              api: widget.api,
              guideId: selected.id,
              dayPosition: dayPosition,
            ),
            _BudgetSection(
              api: widget.api,
              guideId: selected.id,
              partySize: partySize,
              onPartySizeChanged: changePartySize,
            ),
          ],
        );
      },
    ),
  );
}

class _DayRouteSection extends StatelessWidget {
  const _DayRouteSection({
    required this.api,
    required this.guideId,
    required this.dayPosition,
  });
  final AppApi api;
  final String guideId;
  final int dayPosition;
  @override
  Widget build(BuildContext context) => FutureBuilder<DayRoute>(
    future: api.getDayRoute(guideId, dayPosition),
    builder: (context, snapshot) {
      if (snapshot.connectionState != ConnectionState.done) {
        return const Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: CircularProgressIndicator()),
        );
      }
      if (snapshot.hasError) {
        return const Padding(
          padding: EdgeInsets.all(24),
          child: Text('Planning access denied or unavailable.'),
        );
      }
      final route = snapshot.data!;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (route.markers.isEmpty)
            const Padding(
              padding: EdgeInsets.all(24),
              child: Text('No markers yet. Add places to this day.'),
            ),
          for (final marker in route.markers)
            ListTile(
              leading: const Icon(Icons.place_outlined),
              title: Text(marker.name),
              subtitle: Text(
                marker.latitude == null || marker.longitude == null
                    ? 'No coordinates'
                    : '${marker.latitude!.toStringAsFixed(4)}, ${marker.longitude!.toStringAsFixed(4)}',
              ),
            ),
          for (final segment in route.segments)
            ListTile(
              leading: const Icon(Icons.route_outlined),
              title: Text('${segment.originName} → ${segment.destinationName}'),
              subtitle: Text(
                '${segment.mode} · ${segment.durationMinutes} min',
              ),
              trailing: Text(
                '${segment.costPerPersonMinorUnits} ${segment.currencyCode}',
              ),
            ),
        ],
      );
    },
  );
}

class _BudgetSection extends StatelessWidget {
  const _BudgetSection({
    required this.api,
    required this.guideId,
    required this.partySize,
    required this.onPartySizeChanged,
  });
  final AppApi api;
  final String guideId;
  final int partySize;
  final ValueChanged<int> onPartySizeChanged;
  @override
  Widget build(BuildContext context) => Column(
    children: [
      Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          IconButton(
            tooltip: 'Fewer travelers',
            onPressed: partySize > 1 ? () => onPartySizeChanged(-1) : null,
            icon: const Icon(Icons.remove),
          ),
          Semantics(label: 'Party size', child: Text('$partySize')),
          IconButton(
            tooltip: 'More travelers',
            onPressed: partySize < 20 ? () => onPartySizeChanged(1) : null,
            icon: const Icon(Icons.add),
          ),
        ],
      ),
      FutureBuilder<BudgetOverview>(
        future: api.getBudget(guideId, partySize),
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: CircularProgressIndicator()),
            );
          }
          if (snapshot.hasError) {
            return const Padding(
              padding: EdgeInsets.all(24),
              child: Text('Planning access denied or unavailable.'),
            );
          }
          final lines = snapshot.data!.lines;
          if (lines.isEmpty) {
            return const Padding(
              padding: EdgeInsets.all(24),
              child: Text('No budget entries yet.'),
            );
          }
          return Column(
            children: [
              for (final line in lines)
                ListTile(
                  title: Text(line.category),
                  subtitle: Text(
                    '${line.amountPerPersonMinorUnits} ${line.currencyCode} per person',
                  ),
                  trailing: Text(
                    '${line.partyTotalMinorUnits} ${line.currencyCode}',
                  ),
                ),
            ],
          );
        },
      ),
    ],
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
