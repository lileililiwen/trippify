import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'api_client.dart';
import 'design/states.dart';
import 'design/theme.dart';
import 'onboarding/registration_confirmation_screen.dart';
import 'shell/signed_in_shell.dart';

/// A section title that announces itself as a header to assistive tech.
Widget sectionTitle(BuildContext context, String text, {TextStyle? style}) {
  return Semantics(
    header: true,
    child: Text(text, style: style ?? Theme.of(context).textTheme.titleMedium),
  );
}

/// Bridge for the email captured by the registration screen. The
/// `/register/done` route builder only has a [BuildContext], so we
/// stash the value here and the confirmation screen reads it.
String? _lastRegisteredEmail;

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
    theme: lightTheme,
    darkTheme: darkTheme,
    themeMode: ThemeMode.system,
    localizationsDelegates: GlobalMaterialLocalizations.delegates,
    supportedLocales: const [Locale('en'), Locale('zh')],
    routes: {
      '/': (_) => SystemScreen(api: api),
      '/sign-in': (_) => SignInScreen(api: api),
      '/register': (_) => RegistrationScreen(api: api),
      '/register/done': (_) => RegistrationConfirmationScreen(
        api: api,
        email: _lastRegisteredEmail,
      ),
      '/profile': (_) => _ShellRoute(
        api: api,
        destination: SignedInDestination.home,
        child: ProfileScreen(api: api),
      ),
      '/creator/enroll': (_) => _ShellRoute(
        api: api,
        destination: SignedInDestination.library,
        child: CreatorEnrollmentScreen(api: api),
      ),
      '/creator': (_) => PublicCreatorScreen(api: api),
      '/guides': (_) => _ShellRoute(
        api: api,
        destination: SignedInDestination.create,
        child: GuideWorkspaceScreen(api: api),
      ),
      '/planning': (_) => _ShellRoute(
        api: api,
        destination: SignedInDestination.plan,
        child: PlanningScreen(api: api),
      ),
      '/discover': (_) => _ShellRoute(
        api: api,
        destination: SignedInDestination.discover,
        child: DiscoveryScreen(api: api),
      ),
      '/library': (_) => _ShellRoute(
        api: api,
        destination: SignedInDestination.library,
        child: LibraryScreen(api: api),
      ),
      '/creator/dashboard': (_) => CreatorDashboardScreen(api: api),
      '/admin/operations': (_) => AdminOperationsScreen(api: api),
      '/notifications': (_) => NotificationsScreen(api: api),
      '/notification-preferences': (_) => NotificationPreferencesScreen(api: api),
      '/plugins': (_) => PluginCatalogScreen(api: api),
      '/tenant': (_) => TenantDashboardScreen(api: api),
      '/assisted-import': (_) => AssistedImportScreen(api: api),
      '/license-panel': (_) => LicensePanelScreen(api: api),
      '/system': (_) => SystemStatusScreen(api: api),
    },
  );
}

/// Wraps [child] in the [SignedInShell] and navigates to the right
/// destination when the user taps a bottom-nav item.
class _ShellRoute extends StatelessWidget {
  const _ShellRoute({
    required this.api,
    required this.destination,
    required this.child,
  });
  final AppApi api;
  final SignedInDestination destination;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return SignedInShell(
      api: api,
      current: destination,
      onNavigate: (d) {
        switch (d) {
          case SignedInDestination.home:
            Navigator.pushReplacementNamed(context, '/profile');
          case SignedInDestination.discover:
            Navigator.pushReplacementNamed(context, '/discover');
          case SignedInDestination.library:
            Navigator.pushReplacementNamed(context, '/library');
          case SignedInDestination.plan:
            Navigator.pushReplacementNamed(context, '/planning');
          case SignedInDestination.create:
            Navigator.pushReplacementNamed(context, '/guides');
        }
      },
      child: child,
    );
  }
}

class SystemScreen extends StatefulWidget {
  const SystemScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<SystemScreen> createState() => _SystemScreenState();
}

class _SystemScreenState extends State<SystemScreen> {
  late ValueListenable<String?> _tokens;
  MySummary? _userSummary;
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _tokens = widget.api.tokens;
    _loadUserSummary();
  }

  Future<void> _loadUserSummary() async {
    final token = widget.api.tokens.value;
    if (token != null && token.isNotEmpty) {
      setState(() => _isLoading = true);
      try {
        final summary = await widget.api.getMySummary();
        if (mounted) {
          setState(() {
            _userSummary = summary;
            _isLoading = false;
          });
        }
      } catch (e) {
        if (mounted) {
          setState(() {
            _isLoading = false;
            _userSummary = null;
          });
        }
      }
    } else {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _userSummary = null;
        });
      }
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _tokens.addListener(_loadUserSummary);
  }

  @override
  void dispose() {
    _tokens.removeListener(_loadUserSummary);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Trippify'),
        elevation: 2,
        actions: [
          if (_userSummary != null)
            IconButton(
              icon: const Icon(Icons.logout),
              tooltip: 'Sign out',
              onPressed: () async {
                await widget.api.login('dummy', 'dummy');
                if (mounted) setState(() {});
              },
            ),
        ],
      ),
      body: _isLoading
          ? const LoadingState()
          : _userSummary == null
              ? _buildAnonymousHome(context)
              : _buildAuthenticatedHome(context),
    );
  }

  Widget _buildAnonymousHome(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.person_outline, size: 64, color: colorScheme.onSurfaceVariant),
          const SizedBox(height: 16),
          Text(
            'Welcome to Trippify',
            style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                ),
          ),
          const SizedBox(height: 16),
          Text(
            'Please sign in to access your home screen',
            style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                ),
          ),
          const SizedBox(height: 24),
          FilledButton(
            onPressed: () => Navigator.pushNamed(context, '/sign-in'),
            child: const Text('Sign in'),
          ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: () => Navigator.pushNamed(context, '/register'),
            child: const Text('Create account'),
          ),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: () => Navigator.pushNamed(context, '/discover'),
            icon: const Icon(Icons.explore),
            label: const Text('Browse the catalog'),
          ),
        ],
      ),
    );
  }

  Widget _buildAuthenticatedHome(BuildContext context) => SingleChildScrollView(
    padding: const EdgeInsets.all(24),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: 32),
        _buildHeader(context),
        const SizedBox(height: 32),
        _buildSummaryCard(context),
        const SizedBox(height: 32),
        _buildQuickActions(context),
      ],
    ),
  );

  Widget _buildHeader(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Row(
      children: [
        CircleAvatar(
          backgroundColor: colorScheme.primary,
          radius: 32,
          child: Text(
            _userSummary!.displayName.isNotEmpty
                ? _userSummary!.displayName[0].toUpperCase()
                : 'U',
            style: TextStyle(color: colorScheme.onPrimary, fontSize: 20),
          ),
        ),
        const SizedBox(width: 16),
        Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              _userSummary!.displayName,
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.bold,
                  ),
            ),
            const SizedBox(height: 4),
            Text(
              _userSummary!.accountStatus,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildSummaryCard(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.only(bottom: 24),
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Account Summary',
              style: theme.textTheme.titleLarge?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w600,
                  ),
            ),
            const SizedBox(height: 16),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Email:'),
                Text(
                  _userSummary!.email,
                  style: TextStyle(color: colorScheme.onSurfaceVariant),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Creator:'),
                Text(
                  _userSummary!.isCreator ? 'Yes' : 'No',
                  style: TextStyle(
                    color: _userSummary!.isCreator
                        ? colorScheme.primary
                        : colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Roles:'),
                Text(
                  _userSummary!.roles.isEmpty
                      ? 'None'
                      : _userSummary!.roles.join(', '),
                  style: TextStyle(color: colorScheme.onSurfaceVariant),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildQuickActions(BuildContext context) => Wrap(
    spacing: 12,
    runSpacing: 12,
    children: [
      _actionButton(context, '/guides', Icons.menu_book, 'My guides'),
      _actionButton(context, '/discover', Icons.explore, 'Discover guides'),
      _actionButton(context, '/library', Icons.collections_bookmark, 'My library'),
    ],
  );

  Widget _actionButton(BuildContext context, String route, IconData icon, String label) => FilledButton.icon(
    onPressed: () => Navigator.pushNamed(context, route),
    icon: Icon(icon, size: 20),
    label: Text(label),
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
          Container(
            height: 200,
            width: double.infinity,
            margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.surfaceContainerHigh,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Center(
              child: Text(
                'Map preview',
                style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
              ),
            ),
          ),
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

class DiscoveryScreen extends StatefulWidget {
  const DiscoveryScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<DiscoveryScreen> createState() => _DiscoveryScreenState();
}

class _DiscoveryScreenState extends State<DiscoveryScreen> {
  final search = TextEditingController();
  String pricing = '';
  late Future<SearchResult> results;
  @override
  void initState() {
    super.initState();
    results = widget.api.searchGuides();
  }

  void runSearch() => setState(() {
    results = widget.api.searchGuides(
      query: search.text.trim(),
      pricing: pricing,
    );
  });

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Discover guides')),
    body: ListView(
      padding: const EdgeInsets.all(24),
      children: [
        TextField(
          controller: search,
          decoration: InputDecoration(
            labelText: 'Search guides',
            suffixIcon: IconButton(
              tooltip: 'Search',
              icon: const Icon(Icons.search),
              onPressed: runSearch,
            ),
          ),
          onSubmitted: (_) => runSearch(),
        ),
        DropdownButtonFormField<String>(
          initialValue: pricing.isEmpty ? '' : pricing,
          decoration: const InputDecoration(labelText: 'Pricing'),
          items: const [
            DropdownMenuItem(value: '', child: Text('All')),
            DropdownMenuItem(value: 'free', child: Text('Free')),
            DropdownMenuItem(value: 'paid', child: Text('Paid')),
          ],
          onChanged: (value) {
            pricing = value ?? '';
            runSearch();
          },
        ),
        FutureBuilder<SearchResult>(
          future: results,
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
                child: Text('Discovery is unavailable. Try again later.'),
              );
            }
            final data = snapshot.data!;
            if (data.items.isEmpty) {
              return Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Padding(
                      padding: EdgeInsets.all(24),
                      child: Text('No published guides match your search yet.'),
                    ),
                    FilledButton.icon(
                      onPressed: () =>
                          Navigator.pushReplacementNamed(context, '/discover'),
                      icon: const Icon(Icons.explore),
                      label: const Text('Browse the catalog'),
                    ),
                  ],
                ),
              );
            }
            return Column(
              children: [
                for (final item in data.items)
                  ListTile(
                    title: Text(item.title),
                    subtitle: Text(
                      '${item.countryCode} · ${item.tripDays} days',
                    ),
                    trailing: item.pricing == 'paid'
                        ? Text(
                            '${item.priceMinorUnits} ${item.currencyCode}',
                          )
                        : const Text('Free'),
                    onTap: () => Navigator.push(
                      context,
                      MaterialPageRoute<void>(
                        builder: (_) =>
                            PublicGuideScreen(api: widget.api, slug: item.slug),
                      ),
                    ),
                  ),
              ],
            );
          },
        ),
      ],
    ),
  );
}

class PublicGuideScreen extends StatefulWidget {
  const PublicGuideScreen({super.key, required this.api, required this.slug});
  final AppApi api;
  final String slug;
  @override
  State<PublicGuideScreen> createState() => _PublicGuideScreenState();
}

class _PublicGuideScreenState extends State<PublicGuideScreen> {
  final discount = TextEditingController();
  String? status;
  bool favorite = false;
  late Future<PublicGuide> guide;
  @override
  void initState() {
    super.initState();
    guide = widget.api.getPublicGuide(widget.slug);
  }

  Future<void> buy(PublicGuide data) async {
    try {
      final session = await widget.api.checkout(
        data.slug,
        discountCode: discount.text.trim(),
      );
      setState(
        () => status =
            'Checkout started. Pay ${session.amountMinorUnits} '
            '${session.currencyCode} to unlock.',
      );
    } catch (_) {
      setState(() => status = 'Payments are unavailable right now.');
    }
  }

  Future<void> toggleFavorite(PublicGuide data) async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      if (favorite) {
        await widget.api.removeFavorite(data.slug);
        if (!mounted) return;
        setState(() => favorite = false);
        messenger.showSnackBar(
          const SnackBar(content: Text('Removed from favorites.')),
        );
      } else {
        await widget.api.addFavorite(data.slug);
        if (!mounted) return;
        setState(() => favorite = true);
        messenger.showSnackBar(
          const SnackBar(content: Text('Added to favorites.')),
        );
      }
    } catch (_) {
      if (!mounted) return;
      messenger.showSnackBar(
        const SnackBar(content: Text('Favorites are unavailable.')),
      );
    }
  }

  Future<void> fork(PublicGuide data) async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      final fork = await widget.api.forkGuide(data.slug);
      if (!mounted) return;
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            'Forked from "${fork.sourceTitle}" into a private draft.',
          ),
        ),
      );
    } catch (_) {
      if (!mounted) return;
      messenger.showSnackBar(
        const SnackBar(content: Text('Cannot fork this guide right now.')),
      );
    }
  }

  Future<void> saveTrip(PublicGuide data) async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      await widget.api.createTrip(data.slug, title: data.title);
      if (!mounted) return;
      messenger.showSnackBar(
        const SnackBar(
          content: Text('Saved as a trip. Manage it from My library.'),
        ),
      );
    } catch (_) {
      if (!mounted) return;
      messenger.showSnackBar(
        const SnackBar(content: Text('Cannot save this guide as a trip.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Guide')),
    body: FutureBuilder<PublicGuide>(
      future: guide,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return const Center(child: Text('Guide not found.'));
        }
        final data = snapshot.data!;
        return ListView(
          padding: const EdgeInsets.all(24),
          children: [
            Text(data.title, style: Theme.of(context).textTheme.titleLarge),
            Text(data.subtitle),
            Text(data.summary),
            Text('${data.countryCode} · ${data.cities.join(', ')}'),
            TextButton(
              onPressed: () => Navigator.push(
                context,
                MaterialPageRoute<void>(
                  builder: (_) =>
                      AuthorScreen(api: widget.api, slug: data.authorSlug),
                ),
              ),
              child: const Text('View author'),
            ),
            if (data.pricing == 'paid' && !data.unlocked) ...[
              Semantics(
                liveRegion: true,
                child: Text(
                  'Paid preview. Unlock for '
                  '${data.priceMinorUnits} ${data.currencyCode}.',
                ),
              ),
              TextField(
                controller: discount,
                decoration: const InputDecoration(labelText: 'Discount code'),
              ),
              FilledButton(
                onPressed: () => buy(data),
                child: Text(
                  'Buy and unlock · ${data.priceMinorUnits} ${data.currencyCode}',
                ),
              ),
            ] else if (data.pricing == 'paid')
              Semantics(
                liveRegion: true,
                child: const Text('Purchased. Full guide unlocked.'),
              ),
            if (data.unlocked) ...[
              FilledButton(
                onPressed: () => fork(data),
                child: const Text('Fork for editing'),
              ),
              OutlinedButton(
                onPressed: () => saveTrip(data),
                child: const Text('Save as a trip'),
              ),
            ],
            OutlinedButton(
              onPressed: () => toggleFavorite(data),
              child: Text(favorite ? 'Unfavorite' : 'Favorite'),
            ),
            if (status != null) Semantics(liveRegion: true, child: Text(status!)),
            for (final day in data.days) ...[
              Text(day.title, style: Theme.of(context).textTheme.titleMedium),
              for (final node in day.nodes)
                ListTile(
                  leading: Icon(
                    node.hasDetails ? Icons.place : Icons.lock_outline,
                  ),
                  title: Text(node.name),
                ),
            ],
            const SizedBox(height: 16),
            sectionTitle(context, 'Reviews'),
            if (data.unlocked)
              _ReviewSection(
                api: widget.api,
                guideId: data.id,
                onSubmit: () => setState(() {
                  status = 'Review submitted.';
                }),
              ),
            const SizedBox(height: 16),
            sectionTitle(context, 'Verified trips'),
            _VerifiedTripsSection(api: widget.api, guideId: data.id, unlocked: data.unlocked, onSubmit: (message) => setState(() => status = message)),
            const SizedBox(height: 16),
            sectionTitle(context, 'Release history'),
            _ReleasesSection(api: widget.api, guideId: data.id),
            const SizedBox(height: 16),
            sectionTitle(context, 'Send feedback'),
            _PublicFeedbackForm(
              api: widget.api,
              guideId: data.id,
              onSubmitted: (message) => setState(() => status = message),
            ),
          ],
        );
      },
    ),
  );
}

class _ReleasesSection extends StatefulWidget {
  const _ReleasesSection({required this.api, required this.guideId});
  final AppApi api;
  final String guideId;
  @override
  State<_ReleasesSection> createState() => _ReleasesSectionState();
}

class _ReleasesSectionState extends State<_ReleasesSection> {
  late Future<GuideReleaseList> releases;
  late Future<GuideFreshness> freshness;
  String? status;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  void _refresh() {
    setState(() {
      releases = widget.api.listGuideReleases(widget.guideId);
      freshness = widget.api.getGuideFreshness(widget.guideId);
    });
  }

  Widget _freshnessView() => FutureBuilder<GuideFreshness>(
        future: freshness,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Padding(
              padding: EdgeInsets.all(8),
              child: SizedBox(height: 16, width: 16, child: CircularProgressIndicator()),
            );
          }
          if (snapshot.hasError || !snapshot.hasData) {
            return Padding(
              padding: const EdgeInsets.all(16),
              child: ErrorState(
                message: 'Freshness data is unavailable.',
                onRetry: _refresh,
              ),
            );
          }
          final value = snapshot.data!;
          final label = value.latestVersion == 0
              ? 'No releases yet.'
              : 'Last updated ${value.daysSinceLatest ?? 0} day(s) ago (v${value.latestVersion}).';
          return Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Semantics(
              label: 'Freshness',
              child: Text(label),
            ),
          );
        },
      );

  Widget _releasesView() => FutureBuilder<GuideReleaseList>(
        future: releases,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            );
          }
          if (snapshot.hasError) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Text('Release history unavailable.'),
            );
          }
          final items = snapshot.data!.items;
          if (items.isEmpty) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Text('No releases yet.'),
            );
          }
          return Column(
            children: [
              for (final release in items)
                ListTile(
                  leading: CircleAvatar(child: Text('v${release.versionNumber}')),
                  title: Text(release.title),
                  subtitle: Text(release.changelog),
                ),
            ],
          );
        },
      );

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _freshnessView(),
        _releasesView(),
        if (status != null) Semantics(liveRegion: true, child: Text(status!)),
      ],
    );
  }
}

class LibraryScreen extends StatefulWidget {
  const LibraryScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<LibraryScreen> createState() => _LibraryScreenState();
}

class SystemStatusScreen extends StatefulWidget {
  const SystemStatusScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<SystemStatusScreen> createState() => _SystemStatusScreenState();
}

class _SystemStatusScreenState extends State<SystemStatusScreen> {
  SystemDistributionInfo? info;
  SystemStatus? status;
  List<FeatureFlag> flags = const [];
  String? statusMessage;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final publicInfo = await widget.api.getSystemInfo();
      SystemStatus adminStatus = SystemStatus(publicInfo.version, 0, 0, const [], const []);
      List<FeatureFlag> adminFlags = const <FeatureFlag>[];
      try {
        adminStatus = await widget.api.getSystemStatus();
        adminFlags = await widget.api.listFeatureFlags();
      } catch (_) {
        // anonymous caller or limited role; keep fallback values
      }
      if (mounted) {
        setState(() {
          info = publicInfo;
          status = adminStatus;
          flags = adminFlags;
        });
      }
    } catch (_) {
      if (mounted) setState(() => statusMessage = 'System status unavailable.');
    }
  }

  Future<void> _upgrade() async {
    try {
      await widget.api.triggerSystemUpgrade();
      _load();
    } catch (_) {
      if (mounted) setState(() => statusMessage = 'Cannot trigger upgrade.');
    }
  }

  Future<void> _backup() async {
    try {
      final snap = await widget.api.triggerSystemBackup(label: 'manual');
      if (mounted) setState(() => statusMessage = 'Backup ${snap.label} captured.');
    } catch (_) {
      if (mounted) setState(() => statusMessage = 'Cannot create backup.');
    }
  }

  Future<void> _toggle(FeatureFlag flag) async {
    try {
      await widget.api.upsertFeatureFlag(key: flag.key, enabled: !flag.enabled, value: flag.value);
      _load();
    } catch (_) {
      if (mounted) setState(() => statusMessage = 'Cannot update flag.');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Self-hosted status')),
      body: RefreshIndicator(
        onRefresh: () async => _load(),
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            sectionTitle(context, 'Version'),
            Text(info?.version ?? 'Unknown'),
            const SizedBox(height: 16),
            sectionTitle(context, 'Migrations'),
            Text('Applied ${status?.appliedCount ?? 0} · Pending ${status?.pendingCount ?? 0}'),
            const SizedBox(height: 16),
            FilledButton(onPressed: _upgrade, child: const Text('Run upgrade')),
            const SizedBox(height: 8),
            OutlinedButton(onPressed: _backup, child: const Text('Capture backup')),
            const SizedBox(height: 16),
            sectionTitle(context, 'Feature flags'),
            if (flags.isEmpty)
              const Padding(
                padding: EdgeInsets.all(8),
                child: Text('No feature flags defined.'),
              )
            else
              for (final f in flags)
                ListTile(
                  title: Text(f.key),
                  subtitle: Text('Enabled ${f.enabled}'),
                  trailing: Switch(
                    value: f.enabled,
                    onChanged: (_) => _toggle(f),
                  ),
                ),
            if (statusMessage != null)
              Semantics(liveRegion: true, child: Text(statusMessage!)),
          ],
        ),
      ),
    );
  }
}

class LicensePanelScreen extends StatefulWidget {
  const LicensePanelScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<LicensePanelScreen> createState() => _LicensePanelScreenState();
}

class _LicensePanelScreenState extends State<LicensePanelScreen> {
  List<LicensePolicy> policies = const [];
  String? status;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final value = await widget.api.listMyLicensePolicies();
      if (mounted) setState(() => policies = value);
    } catch (_) {
      if (mounted) setState(() => status = 'License policies unavailable.');
    }
  }

  Future<void> _seed(String slug) async {
    try {
      await widget.api.upsertMyLicensePolicy(
        slug: slug,
        displayName: slug,
        allowCommercial: true,
        requireApproval: false,
        royaltyPercent: 25,
      );
      _load();
    } catch (_) {
      if (mounted) setState(() => status = 'Cannot save policy.');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('License policies')),
      body: RefreshIndicator(
        onRefresh: () async => _load(),
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            sectionTitle(context, 'My policies'),
            if (policies.isEmpty)
              const Padding(
                padding: EdgeInsets.all(16),
                child: Text('No license policies yet.'),
              )
            else
              for (final p in policies)
                ListTile(
                  title: Text(p.displayName),
                  subtitle: Text(
                      'Royalty ${p.royaltyPercent}% · ${p.allowCommercial ? "commercial OK" : "no commercial"}'),
                ),
            const SizedBox(height: 16),
            OutlinedButton(
              onPressed: () => _seed('default'),
              child: const Text('Create default license'),
            ),
            if (status != null) Semantics(liveRegion: true, child: Text(status!)),
          ],
        ),
      ),
    );
  }
}

class TenantDashboardScreen extends StatefulWidget {
  const TenantDashboardScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<TenantDashboardScreen> createState() => _TenantDashboardScreenState();
}

class _TenantDashboardScreenState extends State<TenantDashboardScreen> {
  TenantDashboard? dashboard;
  List<QuotaRow> quotas = const [];
  ExportPayload? export;
  String? status;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  Future<void> _refresh() async {
    try {
      final value = await widget.api.getMyTenant();
      final quotaList = await widget.api.listMyTenantQuotas();
      if (mounted) {
        setState(() {
          dashboard = value;
          quotas = quotaList.items;
        });
      }
    } catch (_) {
      if (mounted) setState(() => status = 'Tenant dashboard unavailable.');
    }
  }

  Future<void> _upgrade(String plan) async {
    try {
      final updated = await widget.api.updateMySubscription(plan);
      setState(() {
        dashboard = updated;
        status = 'Plan changed to $plan.';
      });
    } catch (_) {
      setState(() => status = 'Cannot update plan.');
    }
  }

  Future<void> _export() async {
    try {
      final payload = await widget.api.requestMyTenantExport();
      setState(() {
        export = payload;
        status = 'Export ready with ${payload.purchases.length} purchase(s).';
      });
    } catch (_) {
      setState(() => status = 'Export failed.');
    }
  }

  @override
  Widget build(BuildContext context) {
    if (dashboard == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('My tenant')),
        body: const Center(child: CircularProgressIndicator()),
      );
    }
    final t = dashboard!.tenant;
    final s = dashboard!.subscription;
    return Scaffold(
      appBar: AppBar(title: const Text('My tenant')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Semantics(label: 'Tenant summary', child: Text(t.displayName, style: Theme.of(context).textTheme.titleLarge)),
          Text(t.primaryDomain.isEmpty ? 'No custom domain' : t.primaryDomain),
          Text('Plan ${s.plan} (${s.status})'),
          const SizedBox(height: 16),
          sectionTitle(context, 'Choose a plan'),
          Wrap(
            spacing: 8,
            children: [
              for (final plan in const ['Free', 'Pro', 'Enterprise'])
                OutlinedButton(
                  onPressed: s.plan == plan ? null : () => _upgrade(plan),
                  child: Text(plan),
                ),
            ],
          ),
          const SizedBox(height: 16),
          sectionTitle(context, 'Quotas'),
          if (quotas.isEmpty)
            const Padding(
              padding: EdgeInsets.all(8),
              child: Text('No quotas defined yet.'),
            )
          else
            for (final q in quotas)
              ListTile(
                title: Text(q.metric),
                subtitle: Text('Used ${q.used} / Limit ${q.limit}'),
              ),
          const SizedBox(height: 16),
          OutlinedButton(
            onPressed: _export,
            child: const Text('Generate export'),
          ),
        ],
      ),
    );
  }
}

class AssistedImportScreen extends StatefulWidget {
  const AssistedImportScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<AssistedImportScreen> createState() => _AssistedImportScreenState();
}

class _AssistedImportScreenState extends State<AssistedImportScreen> {
  final source = TextEditingController();
  final objectKey = TextEditingController();
  String? status;
  ImportDraft? draft;
  List<QuotaRow> quotas = const [];

  @override
  void initState() {
    super.initState();
    _loadQuotas();
  }

  Future<void> _loadQuotas() async {
    try {
      final value = await widget.api.listMyAiQuotas();
      if (mounted) setState(() => quotas = value);
    } catch (_) {}
  }

  Future<void> _submitText() async {
    try {
      final detail = await widget.api.submitTextImport(source.text.trim());
      setState(() {
        draft = detail.draft;
        status = detail.job.status == 'Completed'
            ? 'Import ready for review.'
            : 'Import ${detail.job.status}.';
      });
      _loadQuotas();
    } catch (error) {
      setState(() => status = _quotaAwareMessage(error, 'Cannot submit text import.'));
    }
  }

  Future<void> _submitObject() async {
    try {
      final detail = await widget.api.submitObjectImport(objectKey.text.trim(), 'Photo');
      setState(() {
        draft = detail.draft;
        status = detail.job.status == 'Completed'
            ? 'Object import ready for review.'
            : 'Object import ${detail.job.status}.';
      });
      _loadQuotas();
    } catch (error) {
      setState(() => status = _quotaAwareMessage(error, 'Cannot submit object import.'));
    }
  }

  Future<void> _approve() async {
    if (draft == null) return;
    try {
      final updated = await widget.api.approveImportDraft(draft!.id, null);
      setState(() {
        draft = updated;
        status = 'Draft approved. Translate or share when ready.';
      });
    } catch (_) {
      setState(() => status = 'Cannot approve draft.');
    }
  }

  Future<void> _reject() async {
    if (draft == null) return;
    try {
      final updated = await widget.api.rejectImportDraft(draft!.id);
      setState(() {
        draft = updated;
        status = 'Draft rejected.';
      });
    } catch (_) {
      setState(() => status = 'Cannot reject draft.');
    }
  }

  Future<void> _translate(String locale) async {
    if (draft == null) return;
    try {
      final translation = await widget.api.createTranslation(draft!.id, locale, 'Translated version');
      setState(() => status = 'Translation saved (${translation.locale}).');
      _loadQuotas();
    } catch (error) {
      setState(() => status = _quotaAwareMessage(error, 'Cannot save translation.'));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Assisted import')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          sectionTitle(context, 'Paste text'),
          TextField(
            controller: source,
            minLines: 3,
            maxLines: 6,
            decoration: const InputDecoration(labelText: 'Source text (30+ chars)'),
          ),
          FilledButton(onPressed: _submitText, child: const Text('Submit text import')),
          const SizedBox(height: 16),
          sectionTitle(context, 'Object-backed'),
          TextField(
            controller: objectKey,
            decoration: const InputDecoration(labelText: 'Object key'),
          ),
          FilledButton(onPressed: _submitObject, child: const Text('Submit object import')),
          const SizedBox(height: 16),
          sectionTitle(context, 'Quotas'),
          for (final q in quotas)
            ListTile(
              title: Text(q.metric),
              subtitle: Text('Used ${q.used} / Limit ${q.limit}'),
            ),
          const SizedBox(height: 16),
          if (draft != null) ...[
            sectionTitle(context, 'Latest draft'),
            Semantics(
              label: 'Draft title',
              child: Text(draft!.suggestedTitle),
            ),
            Text(draft!.status),
            Wrap(
              spacing: 8,
              children: [
                OutlinedButton(onPressed: _approve, child: const Text('Approve')),
                TextButton(onPressed: _reject, child: const Text('Reject')),
                OutlinedButton(
                  onPressed: () => _translate('es'),
                  child: const Text('Translate (es)'),
                ),
              ],
            ),
          ],
          if (status != null) Semantics(liveRegion: true, child: Text(status!)),
        ],
      ),
    );
  }

  String _quotaAwareMessage(Object error, String fallback) {
    if (error is ApiException &&
        error.statusCode == 403 &&
        error.message.toLowerCase().contains('quota')) {
      return 'Plan limit reached. Check Quotas for the reset date or upgrade your plan.';
    }
    return fallback;
  }
}

class PluginCatalogScreen extends StatefulWidget {
  const PluginCatalogScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<PluginCatalogScreen> createState() => _PluginCatalogScreenState();
}

class _PluginCatalogScreenState extends State<PluginCatalogScreen> {
  late Future<PluginList> catalog;
  late Future<List<PluginInstallation>> installations;
  String? status;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  void _refresh() {
    final pendingCatalog = widget.api.listPlugins();
    final pendingInstallations = widget.api.listMyPluginInstallations();
    setState(() {
      catalog = pendingCatalog;
      installations = pendingInstallations;
    });
  }

  Future<void> install(PluginSummary plugin) async {
    try {
      await widget.api.installPlugin(plugin.id, ['ReadGuides']);
      setState(() => status = 'Plugin installed.');
      _refresh();
    } catch (_) {
      setState(() => status = 'Cannot install plugin.');
    }
  }

  Future<void> toggle(PluginInstallation installation) async {
    try {
      if (installation.lifecycle == 'Disabled' || installation.lifecycle == 'Installed') {
        await widget.api.enablePlugin(installation.pluginId);
      } else {
        await widget.api.disablePlugin(installation.pluginId);
      }
      setState(() => status = 'Lifecycle updated.');
      _refresh();
    } catch (_) {
      setState(() => status = 'Cannot update plugin.');
    }
  }

  Future<void> uninstall(PluginInstallation installation) async {
    try {
      await widget.api.uninstallPlugin(installation.pluginId);
      setState(() => status = 'Plugin uninstalled.');
      _refresh();
    } catch (_) {
      setState(() => status = 'Cannot uninstall plugin.');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Plugin catalog')),
      body: RefreshIndicator(
        onRefresh: () async => _refresh(),
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            Text('My installations',
                style: Theme.of(context).textTheme.titleMedium),
            FutureBuilder<List<PluginInstallation>>(
              future: installations,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasError) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Installations unavailable.'),
                  );
                }
                final list = snapshot.data ?? const [];
                if (list.isEmpty) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No installations yet.'),
                  );
                }
                return Column(
                  children: [
                    for (final i in list)
                      ListTile(
                        title: Text(i.pluginDisplayName),
                        subtitle: Text('${i.lifecycle} · ${i.scopes.join(',')}'),
                        trailing: Wrap(
                          spacing: 4,
                          children: [
                            OutlinedButton(
                              onPressed: () => toggle(i),
                              child: Text(i.lifecycle == 'Enabled'
                                  ? 'Disable'
                                  : 'Enable'),
                            ),
                            TextButton(
                              onPressed: () => uninstall(i),
                              child: const Text('Remove'),
                            ),
                          ],
                        ),
                      ),
                  ],
                );
              },
            ),
            const SizedBox(height: 16),
            sectionTitle(context, 'Catalog'),
            FutureBuilder<PluginList>(
              future: catalog,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasError) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Catalog unavailable.'),
                  );
                }
                final items = snapshot.data!.items;
                if (items.isEmpty) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No plugins available yet.'),
                  );
                }
                return Column(
                  children: [
                    for (final p in items)
                      ListTile(
                        title: Text(p.displayName),
                        subtitle: Text('${p.publisher} · v${p.version}'),
                        trailing: FilledButton(
                          onPressed: () => install(p),
                          child: const Text('Install'),
                        ),
                      ),
                  ],
                );
              },
            ),
            if (status != null)
              Semantics(liveRegion: true, child: Text(status!)),
          ],
        ),
      ),
    );
  }
}

class CreatorDashboardScreen extends StatefulWidget {
  const CreatorDashboardScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<CreatorDashboardScreen> createState() => _CreatorDashboardScreenState();
}

class _CreatorDashboardScreenState extends State<CreatorDashboardScreen> {
  late Future<CreatorDashboardOverview> overview;
  late Future<CreatorOrdersResponse> orders;
  late Future<CreatorReviewSummary> reviews;
  String? status;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  void _refresh() {
    setState(() {
      overview = widget.api.getCreatorDashboardOverview();
      orders = widget.api.listCreatorOrders();
      reviews = widget.api.getCreatorDashboardReviews();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Creator dashboard')),
      body: RefreshIndicator(
        onRefresh: () async => _refresh(),
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            sectionTitle(context, 'Overview'),
            FutureBuilder<CreatorDashboardOverview>(
              future: overview,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasError) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Creator dashboard is unavailable.'),
                  );
                }
                final value = snapshot.data!;
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Semantics(
                      label: 'Guides summary',
                      child: Text(
                        'Guides ${value.guideCount} (active ${value.activeGuideCount})',
                      ),
                    ),
                    Semantics(
                      label: 'Orders summary',
                      child: Text(
                        'Paid orders ${value.paidOrderCount} · Refunded ${value.refundedOrderCount}',
                      ),
                    ),
                    if (value.revenue.isEmpty)
                      const Padding(
                        padding: EdgeInsets.all(16),
                        child: Text('No revenue yet.'),
                      )
                    else
                      for (final entry in value.revenue)
                        ListTile(
                          title: Text(entry.currencyCode),
                          subtitle: Text(
                            'Gross ${entry.grossMinorUnits} · '
                            'Net ${entry.netMinorUnits}',
                          ),
                          trailing: Text(
                            'Paid ${entry.paidOrderCount} · '
                            'Refunded ${entry.refundedOrderCount}',
                          ),
                        ),
                  ],
                );
              },
            ),
            const SizedBox(height: 16),
            sectionTitle(context, 'Reviews'),
            FutureBuilder<CreatorReviewSummary>(
              future: reviews,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasError) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Review summary unavailable.'),
                  );
                }
                final summary = snapshot.data!;
                return Padding(
                  padding: const EdgeInsets.all(8),
                  child: Semantics(
                    label: 'Review moderation summary',
                    child: Text(
                      'Visible ${summary.visibleCount} · '
                      'Flagged ${summary.flaggedCount} · '
                      'Hidden ${summary.hiddenCount} · '
                      'Open reports ${summary.reportsOpen}',
                    ),
                  ),
                );
              },
            ),
            const SizedBox(height: 16),
            sectionTitle(context, 'Recent orders'),
            FutureBuilder<CreatorOrdersResponse>(
              future: orders,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasError) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Order list is unavailable.'),
                  );
                }
                final data = snapshot.data!;
                if (data.items.isEmpty) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No orders yet.'),
                  );
                }
                return Column(
                  children: [
                    for (final order in data.items)
                      ListTile(
                        title: Text(order.guideTitle),
                        subtitle: Text('${order.status} · ${order.currencyCode}'),
                        trailing: Text('${order.amountMinorUnits}'),
                      ),
                  ],
                );
              },
            ),
            if (status != null) Semantics(liveRegion: true, child: Text(status!)),
          ],
        ),
      ),
    );
  }
}

class AdminOperationsScreen extends StatefulWidget {
  const AdminOperationsScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<AdminOperationsScreen> createState() => _AdminOperationsScreenState();
}

class _AdminOperationsScreenState extends State<AdminOperationsScreen> {
  late Future<AdminAuditResponse> audit;
  late Future<AdminUsersResponse> users;
  late Future<AdminCreatorsResponse> creators;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  void _refresh() {
    setState(() {
      audit = widget.api.listAdminAudit(limit: 50);
      users = widget.api.listAdminUsers(limit: 50);
      creators = widget.api.listAdminCreators(limit: 50);
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Admin operations')),
      body: RefreshIndicator(
        onRefresh: () async => _refresh(),
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            sectionTitle(context, 'Audit log'),
            FutureBuilder<AdminAuditResponse>(
              future: audit,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasError) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Audit log unavailable.'),
                  );
                }
                final entries = snapshot.data!.items;
                if (entries.isEmpty) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No audit entries yet.'),
                  );
                }
                return Column(
                  children: [
                    for (final entry in entries)
                      ListTile(
                        title: Text(entry.action),
                        subtitle: Text(entry.reason),
                        trailing: Text(entry.targetUserId.substring(0, 8)),
                      ),
                  ],
                );
              },
            ),
            const SizedBox(height: 16),
            sectionTitle(context, 'Users'),
            FutureBuilder<AdminUsersResponse>(
              future: users,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasError) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Users list unavailable.'),
                  );
                }
                final entries = snapshot.data!.items;
                if (entries.isEmpty) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No users found.'),
                  );
                }
                return Column(
                  children: [
                    for (final u in entries)
                      ListTile(
                        title: Text(u.email),
                        subtitle: Text('${u.status} · confirmed ${u.emailConfirmed}'),
                      ),
                  ],
                );
              },
            ),
            const SizedBox(height: 16),
            sectionTitle(context, 'Creators'),
            FutureBuilder<AdminCreatorsResponse>(
              future: creators,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasError) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Creators list unavailable.'),
                  );
                }
                final entries = snapshot.data!.items;
                if (entries.isEmpty) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No creators yet.'),
                  );
                }
                return Column(
                  children: [
                    for (final c in entries)
                      ListTile(
                        title: Text(c.slug),
                        subtitle: Text(c.status),
                      ),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _ReviewSection extends StatefulWidget {
  const _ReviewSection({
    required this.api,
    required this.guideId,
    required this.onSubmit,
  });
  final AppApi api;
  final String guideId;
  final VoidCallback onSubmit;
  @override
  State<_ReviewSection> createState() => _ReviewSectionState();
}

class _ReviewSectionState extends State<_ReviewSection> {
  final body = TextEditingController();
  int rating = 5;
  String? status;
  late Future<List<Review>> reviews;
  @override
  void initState() {
    super.initState();
    reviews = widget.api.listReviews(widget.guideId);
  }
  Future<void> submit() async {
    try {
      await widget.api.submitReview(
        widget.guideId,
        rating,
        body.text.trim(),
      );
      body.clear();
      setState(() {
        reviews = widget.api.listReviews(widget.guideId);
        status = 'Review submitted.';
      });
      widget.onSubmit();
    } catch (_) {
      setState(() => status = 'Cannot submit review.');
    }
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      TextFormField(
        controller: body,
        minLines: 4,
        maxLines: 8,
        maxLength: 4000,
        decoration: const InputDecoration(
          labelText: 'Your review',
          helperText: '30–4000 characters',
        ),
        validator: (v) {
          final value = (v ?? '').trim();
          if (value.length < 30) {
            return 'A review must be at least 30 characters.';
          }
          return null;
        },
      ),
      Semantics(
        container: true,
        label: 'Rating: $rating of 5',
        child: Row(
          children: [
            const Text('Rating: '),
            for (var i = 1; i <= 5; i++)
              IconButton(
                tooltip: 'Set rating to $i',
                icon: Icon(i <= rating ? Icons.star : Icons.star_border),
                onPressed: () => setState(() => rating = i),
              ),
          ],
        ),
      ),
      FilledButton(
        onPressed: submit,
        child: const Text('Submit review'),
      ),
      if (status != null) Semantics(liveRegion: true, child: Text(status!)),
      FutureBuilder<List<Review>>(
        future: reviews,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            );
          }
          if (snapshot.hasError) {
            return Padding(
              padding: const EdgeInsets.all(16),
              child: ErrorState(
                message: 'Reviews are unavailable right now.',
                onRetry: () => setState(
                  () => reviews = widget.api.listReviews(widget.guideId),
                ),
              ),
            );
          }
          final items = snapshot.data!;
          if (items.isEmpty) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Text('No reviews yet.'),
            );
          }
          return Column(
            children: [
              for (final review in items)
                ListTile(
                  leading: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      for (var i = 0; i < review.rating; i++)
                        const Icon(Icons.star, size: 16),
                    ],
                  ),
                  title: Text(review.body),
                  subtitle: review.reply != null
                      ? Text('Author reply: ${review.reply!.body}')
                      : null,
                ),
            ],
          );
        },
      ),
    ],
  );
}

class _PublicFeedbackForm extends StatefulWidget {
  const _PublicFeedbackForm({
    required this.api,
    required this.guideId,
    required this.onSubmitted,
  });
  final AppApi api;
  final String guideId;
  final ValueChanged<String> onSubmitted;
  @override
  State<_PublicFeedbackForm> createState() => _PublicFeedbackFormState();
}

class _PublicFeedbackFormState extends State<_PublicFeedbackForm> {
  final _formKey = GlobalKey<FormState>();
  final body = TextEditingController();
  String? status;
  bool busy = false;

  Future<void> submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      busy = true;
      status = null;
    });
    try {
      await widget.api.submitFeedback(widget.guideId, body.text.trim());
      if (!mounted) return;
      body.clear();
      setState(() {
        status = 'Feedback sent. Thank you.';
        busy = false;
      });
      widget.onSubmitted('Feedback sent. Thank you.');
    } catch (_) {
      if (!mounted) return;
      setState(() {
        status = 'Feedback is unavailable right now.';
        busy = false;
      });
    }
  }

  @override
  void dispose() {
    body.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Form(
    key: _formKey,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextFormField(
          controller: body,
          minLines: 4,
          maxLines: 8,
          maxLength: 4000,
          decoration: const InputDecoration(
            labelText: 'Your feedback',
            helperText: '30–4000 characters',
          ),
          validator: (v) {
            final value = (v ?? '').trim();
            if (value.length < 30) {
              return 'Feedback must be at least 30 characters.';
            }
            return null;
          },
        ),
        if (status != null) Semantics(liveRegion: true, child: Text(status!)),
        FilledButton(
          onPressed: busy ? null : submit,
          child: busy
              ? const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Text('Send feedback'),
        ),
      ],
    ),
  );
}

class _VerifiedTripsSection extends StatefulWidget {
  const _VerifiedTripsSection({
    required this.api,
    required this.guideId,
    required this.unlocked,
    required this.onSubmit,
  });
  final AppApi api;
  final String guideId;
  final bool unlocked;
  final ValueChanged<String> onSubmit;
  @override
  State<_VerifiedTripsSection> createState() => _VerifiedTripsSectionState();
}

class _VerifiedTripsSectionState extends State<_VerifiedTripsSection> {
  late Future<VerifiedBadge> badge;
  late Future<TripInsightSummary> insights;
  final body = TextEditingController();
  String kind = 'TripJournal';
  DateTime? evidenceDate;
  String? evidenceAttachment;
  String? status;
  final party = TextEditingController(text: '2');
  final tripDays = TextEditingController(text: '5');
  final cost = TextEditingController(text: '150000');
  final currency = TextEditingController(text: 'JPY');

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  void _refresh() {
    setState(() {
      badge = widget.api.getVerifiedBadge(widget.guideId);
      insights = widget.api.getTripInsights(widget.guideId);
    });
  }

  Future<void> submitEvidence() async {
    try {
      final prefix = evidenceDate == null
          ? ''
          : '${evidenceDate!.toIso8601String().substring(0, 10)}: ';
      await widget.api.submitEvidence(
        widget.guideId,
        kind: kind,
        body: '$prefix${body.text.trim()}',
        redactedReference: evidenceAttachment,
      );
      body.clear();
      setState(() {
        evidenceDate = null;
        evidenceAttachment = null;
        status = 'Evidence submitted for review.';
      });
      widget.onSubmit('Evidence submitted for review.');
      _refresh();
    } catch (_) {
      setState(() => status = 'Cannot submit evidence.');
    }
  }

  Future<void> _pickEvidenceDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: evidenceDate ?? DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );
    if (picked != null) {
      setState(() => evidenceDate = picked);
    }
  }

  Future<void> _pickEvidenceAttachment() async {
    // The backend does not yet accept attachments; surface a placeholder
    // bottom sheet so the surface is discoverable. The future
    // implementation will read a file picker result here.
    await showModalBottomSheet<void>(
      context: context,
      builder: (ctx) => const Padding(
        padding: EdgeInsets.all(24),
        child: Text(
          'Attachment uploads are coming soon. The picker will read a '
          'local file or image and upload it with your evidence.',
        ),
      ),
    );
  }

  Future<void> submitInsight() async {
    try {
      final partyValue = int.tryParse(party.text.trim()) ?? 0;
      final daysValue = int.tryParse(tripDays.text.trim()) ?? 0;
      final costValue = int.tryParse(cost.text.trim()) ?? 0;
      await widget.api.submitTripInsight(
        widget.guideId,
        partySize: partyValue,
        tripDays: daysValue,
        totalCostMinorUnits: costValue,
        currencyCode: currency.text.trim(),
      );
      setState(() => status = 'Insight submitted.');
      widget.onSubmit('Insight submitted.');
      _refresh();
    } catch (_) {
      setState(() => status = 'Cannot submit insight.');
    }
  }

  Widget _badgeView() => FutureBuilder<VerifiedBadge>(
        future: badge,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            );
          }
          if (snapshot.hasError) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Text('Verification badge unavailable.'),
            );
          }
          final value = snapshot.data!;
          return Padding(
            padding: const EdgeInsets.all(16),
            child: Semantics(
              label: value.verified ? 'Verified by travelers' : 'Not yet verified',
              child: Text(
                value.verified
                    ? 'Verified by ${value.approvedEvidenceCount} traveler(s).'
                    : 'No verified travelers yet.',
              ),
            ),
          );
        },
      );

  Widget _insightsView() => FutureBuilder<TripInsightSummary>(
        future: insights,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            );
          }
          if (snapshot.hasError) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Text('Actual insights unavailable.'),
            );
          }
          final value = snapshot.data!;
          if (!value.meetsKAnonymity) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Text(
                'Actual insights appear once at least five travelers opt in.',
              ),
            );
          }
          final summary = value.average ?? value.median;
          if (summary == null) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Text('Actual insights are not yet available.'),
            );
          }
          return Padding(
            padding: const EdgeInsets.all(16),
            child: Semantics(
              label: 'Average trip insights',
              child: Text(
                'Avg party ${summary.partySize.toStringAsFixed(1)} · '
                '${summary.tripDays.toStringAsFixed(1)} days · '
                '${summary.totalCostMinorUnits.toStringAsFixed(0)} ${summary.currencyCode}',
              ),
            ),
          );
        },
      );

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _badgeView(),
        _insightsView(),
        if (widget.unlocked) ...[
          DropdownButtonFormField<String>(
            initialValue: kind,
            decoration: const InputDecoration(labelText: 'Evidence kind'),
            items: const [
              DropdownMenuItem(value: 'TripJournal', child: Text('Trip journal')),
              DropdownMenuItem(value: 'Receipt', child: Text('Receipt')),
              DropdownMenuItem(value: 'BookingConfirmation', child: Text('Booking')),
              DropdownMenuItem(value: 'PhotoNote', child: Text('Photo note')),
              DropdownMenuItem(value: 'Other', child: Text('Other')),
            ],
            onChanged: (value) => setState(() => kind = value ?? kind),
          ),
          TextField(
            controller: body,
            minLines: 3,
            maxLines: 6,
            decoration: InputDecoration(
              labelText: evidenceDate == null
                  ? 'Evidence (50-4000 chars)'
                  : 'Evidence (50-4000 chars, dated ${evidenceDate!.toIso8601String().substring(0, 10)})',
            ),
          ),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _pickEvidenceDate,
                  icon: const Icon(Icons.calendar_today_outlined),
                  label: Text(
                    evidenceDate == null
                        ? 'Pick trip date'
                        : 'Trip date: ${evidenceDate!.toIso8601String().substring(0, 10)}',
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _pickEvidenceAttachment,
                  icon: const Icon(Icons.attach_file),
                  label: Text(
                    evidenceAttachment == null
                        ? 'Attach file'
                        : 'Attached: $evidenceAttachment',
                  ),
                ),
              ),
            ],
          ),
          FilledButton(
            onPressed: submitEvidence,
            child: const Text('Submit evidence'),
          ),
          const SizedBox(height: 8),
          Text(
            'Optional coarse insights',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: party,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'Party size'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: TextField(
                  controller: tripDays,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'Trip days'),
                ),
              ),
            ],
          ),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: cost,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'Total minor units'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: TextField(
                  controller: currency,
                  decoration: const InputDecoration(labelText: 'Currency code'),
                ),
              ),
            ],
          ),
          FilledButton(
            onPressed: submitInsight,
            child: const Text('Share insights'),
          ),
        ] else
          const Padding(
            padding: EdgeInsets.all(16),
            child: Text('Unlock to submit verification evidence.'),
          ),
        if (status != null)
          Semantics(liveRegion: true, child: Text(status!)),
      ],
    );
  }
}

class _LibraryScreenState extends State<LibraryScreen> {
  late Future<List<Entitlement>> entitlements;
  late Future<List<Favorite>> favorites;
  late Future<List<Trip>> trips;
  @override
  void initState() {
    super.initState();
    entitlements = widget.api.getEntitlements();
    favorites = widget.api.listFavorites();
    trips = widget.api.listTrips();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('My library')),
    body: ListView(
      padding: const EdgeInsets.all(24),
      children: [
        sectionTitle(context, 'Trips'),
        _TripListSection(future: trips),
        const SizedBox(height: 16),
        sectionTitle(context, 'Favorites'),
        _FavoriteSection(future: favorites),
        const SizedBox(height: 16),
        sectionTitle(context, 'Owned guides'),
        _EntitlementList(future: entitlements, api: widget.api),
      ],
    ),
  );
}

class _TripListSection extends StatelessWidget {
  const _TripListSection({required this.future});
  final Future<List<Trip>> future;
  @override
  Widget build(BuildContext context) => FutureBuilder<List<Trip>>(
    future: future,
    builder: (context, snapshot) {
      if (snapshot.connectionState != ConnectionState.done) {
        return const Center(child: CircularProgressIndicator());
      }
      if (snapshot.hasError) {
        return const Text('Trips are unavailable.');
      }
      final items = snapshot.data!;
      if (items.isEmpty) {
        return const Text('No trips saved yet.');
      }
      return Column(
        children: [
          for (final trip in items)
            ListTile(
              title: Text(trip.title),
              subtitle: Text('${trip.status} · ${trip.notes}'),
            ),
        ],
      );
    },
  );
}

class _FavoriteSection extends StatelessWidget {
  const _FavoriteSection({required this.future});
  final Future<List<Favorite>> future;
  @override
  Widget build(BuildContext context) => FutureBuilder<List<Favorite>>(
    future: future,
    builder: (context, snapshot) {
      if (snapshot.connectionState != ConnectionState.done) {
        return const Center(child: CircularProgressIndicator());
      }
      if (snapshot.hasError) {
        return const Text('Favorites are unavailable.');
      }
      final items = snapshot.data!;
      if (items.isEmpty) {
        return const Text('No favorite guides yet.');
      }
      return Column(
        children: [
          for (final favorite in items)
            ListTile(
              title: Text(favorite.title),
              subtitle: Text('${favorite.countryCode} · ${favorite.pricing}'),
            ),
        ],
      );
    },
  );
}

class _EntitlementList extends StatelessWidget {
  const _EntitlementList({required this.future, required this.api});
  final Future<List<Entitlement>> future;
  final AppApi api;
  @override
  Widget build(BuildContext context) => FutureBuilder<List<Entitlement>>(
    future: future,
    builder: (context, snapshot) {
      if (snapshot.connectionState != ConnectionState.done) {
        return const Center(child: CircularProgressIndicator());
      }
      if (snapshot.hasError) {
        return const Text('Library is unavailable.');
      }
      final items = snapshot.data!;
      if (items.isEmpty) {
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('No purchased guides yet.'),
              const SizedBox(height: 8),
              FilledButton.icon(
                onPressed: () => Navigator.pushNamed(context, '/discover'),
                icon: const Icon(Icons.explore),
                label: const Text('Browse the catalog'),
              ),
            ],
          ),
        );
      }
      return Column(
        children: [
          for (final item in items)
            ListTile(
              title: Text(item.title),
              subtitle: Text(item.slug),
              onTap: () => Navigator.push(
                context,
                MaterialPageRoute<void>(
                  builder: (_) =>
                      PublicGuideScreen(api: api, slug: item.slug),
                ),
              ),
            ),
        ],
      );
    },
  );
}

class AuthorScreen extends StatefulWidget {
  const AuthorScreen({super.key, required this.api, required this.slug});
  final AppApi api;
  final String slug;
  @override
  State<AuthorScreen> createState() => _AuthorScreenState();
}

class _AuthorScreenState extends State<AuthorScreen> {
  Future<AuthorPage>? authorFuture;
  Future<int>? followersFuture;
  Future<FollowStatus>? followFuture;
  bool busy = false;

  @override
  void initState() {
    super.initState();
    authorFuture = widget.api.getAuthor(widget.slug);
    followersFuture = widget.api.getCreatorFollowersCount(widget.slug);
    followFuture = widget.api.getCreatorFollowStatus(widget.slug);
  }

  Future<void> toggleFollow() async {
    setState(() => busy = true);
    try {
      final status = await followFuture;
      if (status == null) return;
      if (status.following) {
        await widget.api.unfollowCreator(widget.slug);
      } else {
        await widget.api.followCreator(widget.slug);
      }
      setState(() {
        followFuture = widget.api.getCreatorFollowStatus(widget.slug);
        followersFuture = widget.api.getCreatorFollowersCount(widget.slug);
      });
    } catch (_) {
      setState(() {});
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final api = widget.api;
    return Scaffold(
      appBar: AppBar(title: const Text('Author')),
      body: FutureBuilder<AuthorPage>(
        future: authorFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return const Center(child: Text('Author not found.'));
          }
          final author = snapshot.data!;
          return ListView(
            padding: const EdgeInsets.all(24),
            children: [
              Text(author.displayName,
                  style: Theme.of(context).textTheme.titleLarge),
              Text(author.biography),
              Text(author.travelCountries.join(', ')),
              const SizedBox(height: 16),
              FutureBuilder<FollowStatus>(
                future: followFuture,
                builder: (context, snapshot) {
                  final status = snapshot.data;
                  return Row(
                    children: [
                      OutlinedButton(
                        onPressed: busy ? null : toggleFollow,
                        child: Text(status?.following == true
                            ? 'Unfollow'
                            : 'Follow'),
                      ),
                      const SizedBox(width: 16),
                      FutureBuilder<int>(
                        future: followersFuture,
                        builder: (context, snapshot) {
                          final value = snapshot.data ?? 0;
                          return Semantics(
                            label: 'Followers count',
                            child: Text('Followers $value'),
                          );
                        },
                      ),
                    ],
                  );
                },
              ),
              const SizedBox(height: 16),
              if (author.guides.isEmpty)
                const Text('No published guides yet.'),
              for (final guide in author.guides)
                ListTile(
                  title: Text(guide.title),
                  subtitle: Text('${guide.countryCode} · ${guide.pricing}'),
                  onTap: () => Navigator.push(
                    context,
                    MaterialPageRoute<void>(
                      builder: (_) =>
                          PublicGuideScreen(api: api, slug: guide.slug),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}

class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  late Future<NotificationList> future;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  void _refresh() {
    final pending = widget.api.listNotifications();
    setState(() {
      future = pending;
    });
  }

  Future<void> markRead(String id) async {
    try {
      await widget.api.markNotificationRead(id);
      _refresh();
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Notifications')),
      body: RefreshIndicator(
        onRefresh: () async => _refresh(),
        child: FutureBuilder<NotificationList>(
          future: future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return const Center(
                child: Text('Notifications are unavailable.'),
              );
            }
            final items = snapshot.data!.items;
            if (items.isEmpty) {
              return Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text('No notifications yet.'),
                    const SizedBox(height: 12),
                    TextButton(
                      onPressed: () => Navigator.pushNamed(
                        context,
                        '/notification-preferences',
                      ),
                      child: const Text('Adjust notification preferences'),
                    ),
                  ],
                ),
              );
            }
            return ListView(
              children: [
                for (final entry in items)
                  ListTile(
                    leading: Icon(
                      entry.readAt == null
                          ? Icons.mark_email_unread
                          : Icons.mark_email_read,
                    ),
                    title: Text(entry.title),
                    subtitle: Text(entry.body),
                    onTap: entry.readAt == null
                        ? () {
                            markRead(entry.id);
                          }
                        : null,
                  ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class NotificationPreferencesScreen extends StatefulWidget {
  const NotificationPreferencesScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<NotificationPreferencesScreen> createState() =>
      _NotificationPreferencesScreenState();
}

class _NotificationPreferencesScreenState
    extends State<NotificationPreferencesScreen> {
  NotificationPreferences? prefs;
  String? status;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final value = await widget.api.getNotificationPreferences();
      if (mounted) setState(() => prefs = value);
    } catch (_) {
      if (mounted) setState(() => status = 'Cannot load preferences.');
    }
  }

  Future<void> _save() async {
    if (prefs == null) return;
    try {
      final updated = await widget.api.updateNotificationPreferences(prefs!);
      if (mounted) setState(() {
        prefs = updated;
        status = 'Preferences saved.';
      });
    } catch (_) {
      if (mounted) setState(() => status = 'Cannot save preferences.');
    }
  }

  @override
  Widget build(BuildContext context) {
    if (prefs == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Notification preferences')),
        body: const Center(child: CircularProgressIndicator()),
      );
    }
    final p = prefs!;
    Widget toggle(String label, bool value, void Function(bool) onChanged) {
      return SwitchListTile(
        title: Text(label),
        value: value,
        onChanged: onChanged,
      );
    }
    return Scaffold(
      appBar: AppBar(
        title: const Text('Notification preferences'),
        actions: [
          IconButton(
            tooltip: 'Save preferences',
            onPressed: _save,
            icon: const Icon(Icons.save),
          ),
        ],
      ),
      body: ListView(
        children: [
          toggle('Email enabled', p.emailEnabled,
              (v) => setState(() => prefs!.emailEnabled = v)),
          toggle('In-app enabled', p.inAppEnabled,
              (v) => setState(() => prefs!.inAppEnabled = v)),
          const Divider(),
          toggle('New guide email', p.newGuidePublishedEmail,
              (v) => setState(() => prefs!.newGuidePublishedEmail = v)),
          toggle('New guide in-app', p.newGuidePublishedInApp,
              (v) => setState(() => prefs!.newGuidePublishedInApp = v)),
          toggle('New review on my guide email', p.newReviewOnMyGuideEmail,
              (v) => setState(() => prefs!.newReviewOnMyGuideEmail = v)),
          toggle(
              'New review on my guide in-app',
              p.newReviewOnMyGuideInApp,
              (v) => setState(() => prefs!.newReviewOnMyGuideInApp = v)),
          toggle('New reply email', p.newReplyToReviewEmail,
              (v) => setState(() => prefs!.newReplyToReviewEmail = v)),
          toggle('New reply in-app', p.newReplyToReviewInApp,
              (v) => setState(() => prefs!.newReplyToReviewInApp = v)),
          toggle('Follower gained email', p.followerGainedEmail,
              (v) => setState(() => prefs!.followerGainedEmail = v)),
          toggle('Follower gained in-app', p.followerGainedInApp,
              (v) => setState(() => prefs!.followerGainedInApp = v)),
          toggle('Evidence reviewed email', p.evidenceReviewedEmail,
              (v) => setState(() => prefs!.evidenceReviewedEmail = v)),
          toggle('Evidence reviewed in-app', p.evidenceReviewedInApp,
              (v) => setState(() => prefs!.evidenceReviewedInApp = v)),
          if (status != null)
            Padding(
              padding: const EdgeInsets.all(16),
              child: Semantics(liveRegion: true, child: Text(status!)),
            ),
        ],
      ),
    );
  }
}

class RegistrationScreen extends StatefulWidget {
  const RegistrationScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<RegistrationScreen> createState() => _RegistrationScreenState();
}

class _RegistrationScreenState extends State<RegistrationScreen> {
  final _formKey = GlobalKey<FormState>();
  final email = TextEditingController();
  final password = TextEditingController();
  final passwordConfirm = TextEditingController();
  String? status;
  Future<void> submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    try {
      await widget.api.register(email.text.trim(), password.text);
      if (!mounted) return;
      _lastRegisteredEmail = email.text.trim();
      Navigator.pushReplacementNamed(context, '/register/done');
    } catch (e) {
      if (!mounted) return;
      setState(() {
        status = 'Unable to create account. ${appErrorMessage(toAppError(e))}';
      });
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Create account')),
    body: Form(
      key: _formKey,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          TextFormField(
            controller: email,
            keyboardType: TextInputType.emailAddress,
            autofillHints: const [AutofillHints.email],
            decoration: const InputDecoration(labelText: 'Email'),
            validator: (v) {
              final value = (v ?? '').trim();
              if (value.isEmpty) return 'Email is required.';
              if (!_emailRe.hasMatch(value)) return 'Email is invalid.';
              return null;
            },
          ),
          TextFormField(
            controller: password,
            obscureText: true,
            autofillHints: const [AutofillHints.newPassword],
            decoration: const InputDecoration(labelText: 'Password'),
            validator: (v) {
              if ((v ?? '').length < 10) {
                return 'Password must be at least 10 characters.';
              }
              return null;
            },
          ),
          TextFormField(
            controller: passwordConfirm,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Confirm Password'),
            validator: (v) {
              if (v != password.text) return 'Passwords do not match.';
              return null;
            },
          ),
          if (status != null) Semantics(liveRegion: true, child: Text(status!)),
          FilledButton(onPressed: submit, child: const Text('Create account')),
        ],
      ),
    ),
  );
}

final RegExp _emailRe = RegExp(
  r"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@"
  r'[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?'
  r'(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+$',
);

class SignInScreen extends StatefulWidget {
  const SignInScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<SignInScreen> createState() => _SignInScreenState();
}

class _SignInScreenState extends State<SignInScreen> {
  final _formKey = GlobalKey<FormState>();
  final email = TextEditingController();
  final password = TextEditingController();
  bool busy = false;
  String? error;
  Future<void> submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await widget.api.login(email.text.trim(), password.text);
      if (!mounted) return;
      // Return to wherever the user came from (or the home) so a
      // sign-in launched from /discover does not lose their place.
      if (Navigator.canPop(context)) {
        Navigator.popUntil(context, (r) => r.isFirst);
      }
      Navigator.pushReplacementNamed(context, '/');
    } catch (e) {
      if (!mounted) return;
      setState(() => error = 'Sign in failed. ${appErrorMessage(toAppError(e))}');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Sign in')),
    body: Form(
      key: _formKey,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          TextFormField(
            controller: email,
            keyboardType: TextInputType.emailAddress,
            autofillHints: const [AutofillHints.email],
            decoration: const InputDecoration(labelText: 'Email'),
            validator: (v) {
              final value = (v ?? '').trim();
              if (value.isEmpty) return 'Email is required.';
              if (!_emailRe.hasMatch(value)) return 'Email is invalid.';
              return null;
            },
          ),
          TextFormField(
            controller: password,
            obscureText: true,
            autofillHints: const [AutofillHints.password],
            decoration: const InputDecoration(labelText: 'Password'),
            validator: (v) {
              if ((v ?? '').isEmpty) return 'Password is required.';
              return null;
            },
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
  void initState() {
    super.initState();
    displayName.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    displayName.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !_isDirty(),
    onPopInvokedWithResult: (didPop, _) async {
      if (didPop) return;
      await _confirmDiscard();
    },
    child: Scaffold(
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
              TextFormField(
                controller: displayName,
                maxLength: 80,
                decoration: const InputDecoration(
                  labelText: 'Display name',
                  helperText: 'Up to 80 characters',
                ),
              ),
              FilledButton(
                onPressed: () async {
                  final messenger = ScaffoldMessenger.of(context);
                  await widget.api.updateProfile(displayName.text, null, null);
                  if (!mounted) return;
                  setState(() => saved = 'Profile saved.');
                  messenger.showSnackBar(
                    const SnackBar(content: Text('Profile saved.')),
                  );
                },
                child: const Text('Save profile'),
              ),
              if (saved != null)
                Semantics(liveRegion: true, child: Text(saved!)),
            ],
          );
        },
      ),
    ),
  );

  bool _isDirty() => displayName.text.trim().isNotEmpty;

  Future<void> _confirmDiscard() async {
    final discard = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Discard changes?'),
        content: const Text('You have unsaved changes. Leave anyway?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Keep editing'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Discard changes'),
          ),
        ],
      ),
    );
    if (discard == true && mounted) {
      Navigator.of(context).pop();
    }
  }
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
