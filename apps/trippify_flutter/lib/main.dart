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
      '/discover': (_) => DiscoveryScreen(api: api),
      '/library': (_) => LibraryScreen(api: api),
      '/creator/dashboard': (_) => CreatorDashboardScreen(api: api),
      '/admin/operations': (_) => AdminOperationsScreen(api: api),
      '/notifications': (_) => NotificationsScreen(api: api),
      '/notification-preferences': (_) => NotificationPreferencesScreen(api: api),
      '/plugins': (_) => PluginCatalogScreen(api: api),
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
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
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
                TextButton(
                  onPressed: () => Navigator.pushNamed(context, '/discover'),
                  child: const Text('Discover guides'),
                ),
                TextButton(
                  onPressed: () => Navigator.pushNamed(context, '/library'),
                  child: const Text('My library'),
                ),
                TextButton(
                  onPressed: () => Navigator.push(
                    context,
                    MaterialPageRoute<void>(
                      builder: (_) => CreatorDashboardScreen(api: widget.api),
                    ),
                  ),
                  child: const Text('Creator dashboard'),
                ),
TextButton(
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute<void>(
                builder: (_) => AdminOperationsScreen(api: widget.api),
              ),
            ),
            child: const Text('Admin operations'),
          ),
          TextButton(
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute<void>(
                builder: (_) => NotificationsScreen(api: widget.api),
              ),
            ),
            child: const Text('Notifications'),
          ),
          TextButton(
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute<void>(
                builder: (_) => NotificationPreferencesScreen(api: widget.api),
              ),
            ),
            child: const Text('Notification preferences'),
          ),
          TextButton(
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute<void>(
                builder: (_) => PluginCatalogScreen(api: widget.api),
              ),
            ),
            child: const Text('Plugin catalog'),
          ),
              ],
            );
          },
        ),
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
              return const Padding(
                padding: EdgeInsets.all(24),
                child: Text('No published guides match your search yet.'),
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
    try {
      if (favorite) {
        await widget.api.removeFavorite(data.slug);
        setState(() => favorite = false);
      } else {
        await widget.api.addFavorite(data.slug);
        setState(() => favorite = true);
      }
    } catch (_) {
      setState(() => status = 'Favorites are unavailable.');
    }
  }

  Future<void> fork(PublicGuide data) async {
    try {
      final fork = await widget.api.forkGuide(data.slug);
      setState(
        () => status = 'Forked from "${fork.sourceTitle}" into a private draft.',
      );
    } catch (_) {
      setState(() => status = 'Cannot fork this guide right now.');
    }
  }

  Future<void> saveTrip(PublicGuide data) async {
    try {
      await widget.api.createTrip(data.slug, title: data.title);
      setState(() => status = 'Saved as a trip. Manage it from My library.');
    } catch (_) {
      setState(() => status = 'Cannot save this guide as a trip.');
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
                child: const Text('Buy and unlock'),
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
            Text('Reviews', style: Theme.of(context).textTheme.titleMedium),
            if (data.unlocked)
              _ReviewSection(
                api: widget.api,
                guideId: data.id,
                onSubmit: () => setState(() {
                  status = 'Review submitted.';
                }),
              ),
            const SizedBox(height: 16),
            Text(
              'Verified trips',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            _VerifiedTripsSection(api: widget.api, guideId: data.id, unlocked: data.unlocked, onSubmit: (message) => setState(() => status = message)),
            const SizedBox(height: 16),
            Text('Release history', style: Theme.of(context).textTheme.titleMedium),
            _ReleasesSection(api: widget.api, guideId: data.id),
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
            return const SizedBox.shrink();
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
            Text('Catalog', style: Theme.of(context).textTheme.titleMedium),
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
            Text('Overview', style: Theme.of(context).textTheme.titleMedium),
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
            Text('Reviews', style: Theme.of(context).textTheme.titleMedium),
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
            Text('Recent orders', style: Theme.of(context).textTheme.titleMedium),
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
            Text('Audit log', style: Theme.of(context).textTheme.titleMedium),
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
            Text('Users', style: Theme.of(context).textTheme.titleMedium),
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
            Text('Creators', style: Theme.of(context).textTheme.titleMedium),
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
      TextField(
        controller: body,
        decoration: const InputDecoration(labelText: 'Your review'),
      ),
      Row(
        children: [
          const Text('Rating: '),
          for (var i = 1; i <= 5; i++)
            IconButton(
              icon: Icon(i <= rating ? Icons.star : Icons.star_border),
              onPressed: () => setState(() => rating = i),
            ),
        ],
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
            return const SizedBox.shrink();
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
      await widget.api.submitEvidence(
        widget.guideId,
        kind: kind,
        body: body.text.trim(),
      );
      body.clear();
      setState(() => status = 'Evidence submitted for review.');
      widget.onSubmit('Evidence submitted for review.');
      _refresh();
    } catch (_) {
      setState(() => status = 'Cannot submit evidence.');
    }
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
            decoration: const InputDecoration(labelText: 'Evidence (50-4000 chars)'),
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
        Text('Trips', style: Theme.of(context).textTheme.titleMedium),
        _TripListSection(future: trips),
        const SizedBox(height: 16),
        Text('Favorites', style: Theme.of(context).textTheme.titleMedium),
        _FavoriteSection(future: favorites),
        const SizedBox(height: 16),
        Text('Owned guides', style: Theme.of(context).textTheme.titleMedium),
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
        return const Text('No purchased guides yet.');
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
              return const Center(
                child: Text('No notifications yet.'),
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
          IconButton(onPressed: _save, icon: const Icon(Icons.save)),
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
