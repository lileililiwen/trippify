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
              TextButton(
                onPressed: () => Navigator.pushNamed(context, '/discover'),
                child: const Text('Discover guides'),
              ),
              TextButton(
                onPressed: () => Navigator.pushNamed(context, '/library'),
                child: const Text('My library'),
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
          ],
        );
      },
    ),
  );
}

class LibraryScreen extends StatefulWidget {
  const LibraryScreen({super.key, required this.api});
  final AppApi api;
  @override
  State<LibraryScreen> createState() => _LibraryScreenState();
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

class AuthorScreen extends StatelessWidget {
  const AuthorScreen({super.key, required this.api, required this.slug});
  final AppApi api;
  final String slug;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Author')),
    body: FutureBuilder<AuthorPage>(
      future: api.getAuthor(slug),
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
