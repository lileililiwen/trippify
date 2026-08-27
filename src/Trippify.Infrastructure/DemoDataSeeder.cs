using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trippify.Application;

namespace Trippify.Infrastructure;

/// <summary>
/// Populates the database with a small but realistic set of demo data the
/// first time the API boots in the Development environment. The seeder
/// preserves unrelated users and uses its documented identity set as the
/// idempotency boundary.
/// </summary>
public interface IDemoDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

public sealed class DemoDataSeeder(
    AppDbContext db,
    UserManager<AppUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IClock clock,
    ILogger<DemoDataSeeder> logger) : IDemoDataSeeder
{
    private static readonly string[] NormalizedDemoEmails =
    [
        "DEMO@EXAMPLE.COM",
        "CREATOR@EXAMPLE.COM",
        "TRAVELER2@EXAMPLE.COM",
        "TRAVELER3@EXAMPLE.COM",
        "TRAVELER4@EXAMPLE.COM",
        "TRAVELER5@EXAMPLE.COM",
        "ADMIN@EXAMPLE.COM",
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existingDemoEmails = await db.Users
            .Where(user => user.NormalizedEmail != null && NormalizedDemoEmails.Contains(user.NormalizedEmail))
            .Select(user => user.NormalizedEmail!)
            .ToListAsync(cancellationToken);

        if (existingDemoEmails.Count == NormalizedDemoEmails.Length)
        {
            logger.LogInformation("Demo seeder skipped: the complete demo identity set already exists.");
            return;
        }

        if (existingDemoEmails.Count > 0)
        {
            throw new InvalidOperationException(
                "Demo seeding found a partial demo identity set. Recreate the local Development database or remove every documented demo account before trying again.");
        }

        logger.LogInformation("Demo seeder populating demo fixtures while preserving unrelated users.");

        var demo = await CreateUserAsync("demo@example.com", "Demo Traveler", isAdmin: false, cancellationToken);
        var creator = await CreateUserAsync("creator@example.com", "Demo Creator", isAdmin: false, cancellationToken);
        var traveler2 = await CreateUserAsync("traveler2@example.com", "Sam Rivera", isAdmin: false, cancellationToken);
        var traveler3 = await CreateUserAsync("traveler3@example.com", "Alex Chen", isAdmin: false, cancellationToken);
        var traveler4 = await CreateUserAsync("traveler4@example.com", "Morgan Lee", isAdmin: false, cancellationToken);
        var traveler5 = await CreateUserAsync("traveler5@example.com", "Jordan Kim", isAdmin: false, cancellationToken);
        var admin = await CreateUserAsync("admin@example.com", "Demo Admin", isAdmin: true, cancellationToken);

        db.CreatorProfiles.Add(new CreatorProfile
        {
            UserId = creator.Id,
            Slug = "demo-creator",
            Biography = "Local travel writer covering Japan, Italy, and Portugal. Loves slow trains, neighborhood coffee, and getting lost on purpose.",
            TravelCountries = new[] { "JP", "IT" },
            Status = CreatorStatus.Active,
        });

        foreach (var u in new[] { demo, creator, traveler2, traveler3, traveler4, traveler5, admin })
        {
            db.NotificationPreferences.Add(new NotificationPreference { UserId = u.Id });
        }
        await db.SaveChangesAsync(cancellationToken);

        var tokyo = AddTokyoGuide(creator.Id, cancellationToken);
        var kyoto = AddKyotoGuide(creator.Id, cancellationToken);
        var rome = AddRomeGuide(creator.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var guide in new[] { tokyo, kyoto, rome })
        {
            AddInitialRelease(guide, creator.Id, cancellationToken);
        }

        AddDiscounts(new[] { tokyo, kyoto, rome }, cancellationToken);

        var tokyoReview = AddReview(tokyo.Id, demo.Id, 5, "Easy to follow, the day-by-day saved us hours. The Senso-ji morning tip is gold.", cancellationToken);
        var tokyoReview2 = AddReview(tokyo.Id, traveler2.Id, 4, "Great for first-timers; missed a few food spots. Would love a ramen-focused day.", cancellationToken);
        var romeReview = AddReview(rome.Id, demo.Id, 5, "Walked the whole thing, all good. Trevi at 7am was a highlight.", cancellationToken);
        var kyotoReview = AddReview(kyoto.Id, demo.Id, 5, "Tea ceremony section was perfect. Booked the 10am slot like the guide said and it was empty.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        db.ReviewReplies.Add(new ReviewReply
        {
            Id = Guid.NewGuid(),
            ReviewId = tokyoReview.Id,
            AuthorUserId = creator.Id,
            Body = "Glad it helped! I'll add a Shinjuku food crawl in the next revision.",
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        var order = new GuideOrder
        {
            Id = Guid.NewGuid(),
            GuideId = kyoto.Id,
            BuyerUserId = demo.Id,
            Status = OrderStatus.Paid,
            AmountMinorUnits = 250_000,
            CurrencyCode = "JPY",
            DiscountCode = "WELCOME10",
            DiscountAmountMinorUnits = 25_000,
            CheckoutReference = "demo-checkout-" + Guid.NewGuid().ToString("N"),
            CreatedAt = clock.UtcNow,
            ConfirmedAt = clock.UtcNow,
        };
        db.GuideOrders.Add(order);
        db.PurchaseEntitlements.Add(new PurchaseEntitlement
        {
            Id = Guid.NewGuid(),
            GuideId = kyoto.Id,
            UserId = demo.Id,
            OrderId = order.Id,
            GrantedAt = clock.UtcNow,
        });
        db.CommerceLedgerEntries.AddRange(
            new CommerceLedgerEntry { Id = Guid.NewGuid(), OrderId = order.Id, Kind = LedgerKind.Gross, AmountMinorUnits = 250_000, CurrencyCode = "JPY", CreatedAt = clock.UtcNow },
            new CommerceLedgerEntry { Id = Guid.NewGuid(), OrderId = order.Id, Kind = LedgerKind.Commission, AmountMinorUnits = 25_000, CurrencyCode = "JPY", CommissionRateSnapshot = 0.10m, CreatedAt = clock.UtcNow },
            new CommerceLedgerEntry { Id = Guid.NewGuid(), OrderId = order.Id, Kind = LedgerKind.CreatorNet, AmountMinorUnits = 200_000, CurrencyCode = "JPY", CreatedAt = clock.UtcNow });
        db.RevenueShares.Add(new RevenueShare
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            UserId = creator.Id,
            Percent = 80,
            AmountMinorUnits = 200_000,
            CurrencyCode = "JPY",
            CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        db.UserTrips.Add(new UserTrip
        {
            Id = Guid.NewGuid(),
            UserId = demo.Id,
            Title = "Kyoto Spring Trip",
            SourceGuideId = kyoto.Id,
            Status = TripStatus.Planning,
            Notes = "April 2027. Want to add a day trip to Nara.",
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        });
        db.GuideFavorites.Add(new GuideFavorite
        {
            Id = Guid.NewGuid(),
            UserId = demo.Id,
            GuideId = tokyo.Id,
            CreatedAt = clock.UtcNow,
        });

        var evidence = new TripEvidence
        {
            Id = Guid.NewGuid(),
            GuideId = kyoto.Id,
            UserId = demo.Id,
            Kind = EvidenceKind.TripJournal,
            Body = "Day 1: arrived at Kansai, took the Haruka to Kyoto. Checked into the ryokan in Higashiyama. Walked to Sannenzaka for dinner.\n\nDay 2: started at Kiyomizu-dera at 7am before the tour buses. Then down through Sannenzaka to the Gion district. Tea ceremony booked for 10am — empty as promised.\n\nDay 3: Arashiyama bamboo grove at sunrise, then the monkey park. Train to Fushimi Inari in the afternoon.\n\nDay 4: Philosopher's Path, Ginkaku-ji, Nanzen-ji. Final dinner at a kaiseki place near the hotel.\n\nHeadline: 4 days was tight. Would have added a 5th for Nara if time allowed.",
            RedactedReference = "trip:kyoto-spring-2027",
            Status = EvidenceStatus.Approved,
            SubmittedAt = clock.UtcNow.AddDays(-7),
            RetentionDeadline = clock.UtcNow.AddDays(83),
            ReviewedAt = clock.UtcNow.AddDays(-5),
        };
        db.TripEvidence.Add(evidence);
        db.EvidenceReviews.Add(new EvidenceReviewEntry
        {
            Id = Guid.NewGuid(),
            EvidenceId = evidence.Id,
            ReviewerUserId = admin.Id,
            Decision = EvidenceStatus.Approved,
            Reason = "Detailed journal, specific places, matches the published itinerary.",
            ReviewedAt = clock.UtcNow.AddDays(-5),
        });
        db.VerifiedGuideBadges.Add(new VerifiedGuideBadge
        {
            Id = Guid.NewGuid(),
            GuideId = kyoto.Id,
            ApprovedEvidenceCount = 1,
            FirstGrantedAt = clock.UtcNow.AddDays(-5),
            LastGrantedAt = clock.UtcNow.AddDays(-5),
        });
        var metricUsers = new[] { demo, traveler2, traveler3, traveler4, traveler5 };
        for (var i = 0; i < metricUsers.Length; i++)
        {
            db.ActualTripMetrics.Add(new ActualTripMetric
            {
                Id = Guid.NewGuid(),
                GuideId = kyoto.Id,
                UserId = metricUsers[i].Id,
                PartySize = 2 + (i % 2),
                TripDays = 4,
                TotalCostMinorUnits = 180_000 + (i * 12_000),
                CurrencyCode = "JPY",
                SubmittedAt = clock.UtcNow.AddDays(-30 - i),
            });
        }
        await db.SaveChangesAsync(cancellationToken);

        db.CreatorFollows.AddRange(
            new CreatorFollow { Id = Guid.NewGuid(), FollowerUserId = demo.Id, CreatorUserId = creator.Id, CreatedAt = clock.UtcNow.AddDays(-20) },
            new CreatorFollow { Id = Guid.NewGuid(), FollowerUserId = traveler2.Id, CreatorUserId = creator.Id, CreatedAt = clock.UtcNow.AddDays(-15) });

        db.Notifications.AddRange(
            new Notification
            {
                Id = Guid.NewGuid(),
                UserId = demo.Id,
                Kind = NotificationKind.NewGuidePublished,
                Title = "New guide published",
                Body = "Demo Creator just published “Tokyo in 5 Days”.",
                TargetSlug = tokyo.Slug,
                TargetGuideId = tokyo.Id,
                CreatedAt = clock.UtcNow.AddDays(-10),
                ReadAt = clock.UtcNow.AddDays(-9),
            },
            new Notification
            {
                Id = Guid.NewGuid(),
                UserId = demo.Id,
                Kind = NotificationKind.NewReplyToReview,
                Title = "Your review got a reply",
                Body = "Demo Creator replied to your review of “Tokyo in 5 Days”.",
                TargetSlug = tokyo.Slug,
                TargetGuideId = tokyo.Id,
                CreatedAt = clock.UtcNow.AddDays(-3),
            },
            new Notification
            {
                Id = Guid.NewGuid(),
                UserId = demo.Id,
                Kind = NotificationKind.FollowerGained,
                Title = "You have a new follower",
                Body = "Sam Rivera started following you.",
                CreatedAt = clock.UtcNow.AddDays(-1),
            });

        var pluginManifest = "{\"slug\":\"demo-ai-translator\",\"version\":\"1.0.0\",\"permissions\":[\"ReadGuides\"]}";
        var plugin = new Plugin
        {
            Id = Guid.NewGuid(),
            Slug = "demo-ai-translator",
            DisplayName = "Demo AI Translator",
            Version = "1.0.0",
            Publisher = "Trippify Demo",
            Manifest = pluginManifest,
            Signature = SignManifest(pluginManifest),
            Status = PluginStatus.Approved,
            CreatedAt = clock.UtcNow.AddDays(-30),
        };
        db.Plugins.Add(plugin);
        var installation = new PluginInstallation
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            UserId = demo.Id,
            Lifecycle = PluginLifecycle.Enabled,
            InstalledAt = clock.UtcNow.AddDays(-10),
            EnabledAt = clock.UtcNow.AddDays(-10),
        };
        db.PluginInstallations.Add(installation);
        db.PluginPermissionGrants.Add(new PluginPermissionGrant
        {
            Id = Guid.NewGuid(),
            InstallationId = installation.Id,
            Scope = PluginPermissionScope.ReadGuides,
            GrantedAt = clock.UtcNow.AddDays(-10),
        });
        db.PluginAuditEntries.Add(new PluginAuditEntry
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            ActorUserId = admin.Id,
            Action = "Approved",
            Reason = "Demo seed.",
            OccurredAt = clock.UtcNow.AddDays(-30),
        });

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = "demo-tenant",
            DisplayName = "Demo Tenant",
            PrimaryDomain = "demo.localhost",
            BrandingJson = "{\"accent\":\"#6750A4\"}",
            Status = TenantStatus.Active,
            CreatedAt = clock.UtcNow.AddDays(-30),
        };
        db.Tenants.Add(tenant);
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Plan = SubscriptionPlan.Free,
            Status = SubscriptionStatus.Active,
            StartsAt = clock.UtcNow.AddDays(-30),
        };
        db.Subscriptions.Add(subscription);
        db.QuotaUsages.AddRange(
            new QuotaUsage { Id = Guid.NewGuid(), TenantId = tenant.Id, Metric = "guides_published", Used = 3, Limit = 10, PeriodStart = clock.UtcNow.AddDays(-30), PeriodEnd = clock.UtcNow },
            new QuotaUsage { Id = Guid.NewGuid(), TenantId = tenant.Id, Metric = "monthly_orders", Used = 1, Limit = 50, PeriodStart = clock.UtcNow.AddDays(-30), PeriodEnd = clock.UtcNow });
        db.TenantMembers.Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = creator.Id,
            Role = "Owner",
            CreatedAt = clock.UtcNow.AddDays(-30),
        });

        db.LicensePolicies.Add(new LicensePolicy
        {
            Id = Guid.NewGuid(),
            OwnerUserId = creator.Id,
            Slug = "cc-by-nc-4.0",
            DisplayName = "CC BY-NC 4.0",
            AllowCommercial = false,
            RequireApproval = true,
            RoyaltyPercent = 25,
            CreatedAt = clock.UtcNow.AddDays(-30),
            UpdatedAt = clock.UtcNow.AddDays(-30),
        });

        db.FeatureFlags.Add(new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = "verified-trips",
            Enabled = true,
            Value = "{\"rollout\":100}",
            UpdatedAt = clock.UtcNow.AddDays(-30),
            UpdatedByUserId = admin.Id,
        });

        db.IdentityAuditEntries.Add(new IdentityAuditEntry
        {
            Id = Guid.NewGuid(),
            ActorUserId = admin.Id,
            TargetUserId = creator.Id,
            Action = "creator.activated",
            Reason = "Demo seed.",
            OccurredAt = clock.UtcNow.AddDays(-30),
        });

        var importJob = new ImportJob
        {
            Id = Guid.NewGuid(),
            UserId = demo.Id,
            Kind = ImportKind.Text,
            SourceText = "Day 1: Arashiyama bamboo grove at sunrise. Day 2: Fushimi Inari hike.",
            Status = ImportStatus.Completed,
            SubmittedAt = clock.UtcNow.AddDays(-2),
            CompletedAt = clock.UtcNow.AddDays(-2),
        };
        db.ImportJobs.Add(importJob);
        var importDraft = new ImportDraft
        {
            Id = Guid.NewGuid(),
            ImportJobId = importJob.Id,
            UserId = demo.Id,
            SuggestedTitle = "Imported Kyoto draft",
            SuggestedNodesJson = "[{\"name\":\"Arashiyama bamboo grove\"},{\"name\":\"Fushimi Inari\"}]",
            ProvenanceJson = "{\"source\":\"pasted-text\"}",
            Status = ImportDraftStatus.Approved,
            CreatedAt = clock.UtcNow.AddDays(-2),
        };
        db.ImportDrafts.Add(importDraft);
        db.Translations.Add(new Translation
        {
            Id = Guid.NewGuid(),
            SourceDraftId = importDraft.Id,
            UserId = demo.Id,
            Locale = "en",
            Body = "Day 1: Arashiyama bamboo grove at sunrise. Day 2: Fushimi Inari hike.",
            Status = TranslationStatus.Approved,
            CreatedAt = clock.UtcNow.AddDays(-2),
            UpdatedAt = clock.UtcNow.AddDays(-2),
        });

        db.AiQuotaUsages.Add(new AiQuotaUsage
        {
            Id = Guid.NewGuid(),
            UserId = demo.Id,
            Metric = "import_assists",
            Used = 1,
            Limit = 25,
            PeriodStart = clock.UtcNow.AddDays(-30),
            PeriodEnd = clock.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Demo seeder populated the database with 7 users, 3 guides, 4 reviews, 1 paid order, 1 favorite, 1 trip, 1 plugin, 1 tenant, 3 notifications, and assorted supporting rows.");
    }

    private async Task<AppUser> CreateUserAsync(string email, string displayName, bool isAdmin, CancellationToken cancellationToken)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var result = await users.CreateAsync(user, DemoData.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Demo seeder could not create {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        db.UserProfiles.Add(new UserProfile { UserId = user.Id, DisplayName = displayName, Locale = "en" });
        if (isAdmin)
        {
            if (!await roles.RoleExistsAsync("Administrator"))
            {
                var roleResult = await roles.CreateAsync(new IdentityRole<Guid>("Administrator"));
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException($"Demo seeder could not create Administrator role: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                }
            }
            var roleAssignment = await users.AddToRoleAsync(user, "Administrator");
            if (!roleAssignment.Succeeded)
            {
                throw new InvalidOperationException($"Demo seeder could not assign Administrator role: {string.Join(", ", roleAssignment.Errors.Select(e => e.Description))}");
            }
        }
        return user;
    }

    private TravelGuide AddTokyoGuide(Guid ownerId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var guide = new TravelGuide
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId,
            Title = "Tokyo in 5 Days",
            Subtitle = "Neighborhoods, transit, and the spots locals actually go",
            Summary = "Five days of Tokyo broken into walkable days, with a metro-only itinerary and neighborhood food picks in each district.",
            CountryCode = "JP",
            Cities = new[] { "Tokyo" },
            Tags = new[] { "city", "japan", "transit" },
            TripDays = 5,
            Lifecycle = GuideLifecycle.FreePublic,
            Slug = "tokyo-in-5-days",
            PublishedAt = now.AddDays(-10),
            CreatedAt = now.AddDays(-15),
            UpdatedAt = now.AddDays(-10),
        };
        var day1 = new GuideDay { Id = Guid.NewGuid(), GuideId = guide.Id, Position = 0, Title = "Asakusa & the old city" };
        day1.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day1.Id, Position = 0, Type = GuideNodeType.Attraction, Name = "Sensō-ji", Address = "2 Chome-3-1 Asakusa, Taitō", Latitude = 35.7148, Longitude = 139.7967, StayMinutes = 90 });
        day1.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day1.Id, Position = 1, Type = GuideNodeType.Restaurant, Name = "Sushi no Midori (Tsukiji outer market)", Latitude = 35.6654, Longitude = 139.7707, StayMinutes = 45 });
        day1.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day1.Id, Position = 2, Type = GuideNodeType.Activity, Name = "teamLab Planets", Latitude = 35.6490, Longitude = 139.7900, StayMinutes = 120 });
        guide.Days.Add(day1);
        guide.Sections.Add(new GuideSection { Id = Guid.NewGuid(), GuideId = guide.Id, Position = 0, Type = GuideSectionType.Preparation, Title = "Before you go", Body = "Buy a Suica or Pasmo card at the airport. You will use it for every train and most vending machines. A 5-day Tokyo Metro Pass is worth it if you stick to the Yamanote and Marunouchi lines." });
        db.TravelGuides.Add(guide);
        return guide;
    }

    private TravelGuide AddKyotoGuide(Guid ownerId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var guide = new TravelGuide
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId,
            Title = "Kyoto Temples & Tea",
            Subtitle = "Slow days, ten side-streets, and the best tea ceremonies",
            Summary = "Four days of Kyoto with reserved-entry temple visits and a hands-on tea ceremony, with a Nara day-trip at the end.",
            CountryCode = "JP",
            Cities = new[] { "Kyoto" },
            Tags = new[] { "city", "japan", "tea", "temples" },
            TripDays = 4,
            Lifecycle = GuideLifecycle.Paid,
            Slug = "kyoto-temples-tea",
            PriceMinorUnits = 250_000,
            CurrencyCode = "JPY",
            PublishedAt = now.AddDays(-30),
            CreatedAt = now.AddDays(-40),
            UpdatedAt = now.AddDays(-15),
        };
        var day1 = new GuideDay { Id = Guid.NewGuid(), GuideId = guide.Id, Position = 0, Title = "Higashiyama walk" };
        day1.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day1.Id, Position = 0, Type = GuideNodeType.Attraction, Name = "Kiyomizu-dera (before 8am)", Latitude = 34.9949, Longitude = 135.7850, StayMinutes = 90 });
        day1.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day1.Id, Position = 1, Type = GuideNodeType.Attraction, Name = "Sannenzaka & Ninenzaka", Latitude = 34.9981, Longitude = 135.7807, StayMinutes = 60 });
        day1.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day1.Id, Position = 2, Type = GuideNodeType.Cafe, Name = "% Arabica Higashiyama", Latitude = 35.0036, Longitude = 135.7806, StayMinutes = 30 });
        var day2 = new GuideDay { Id = Guid.NewGuid(), GuideId = guide.Id, Position = 1, Title = "Tea & Arashiyama" };
        day2.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day2.Id, Position = 0, Type = GuideNodeType.Activity, Name = "Tea ceremony (10am slot)", Latitude = 35.0036, Longitude = 135.7807, StayMinutes = 60 });
        day2.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day2.Id, Position = 1, Type = GuideNodeType.Attraction, Name = "Arashiyama bamboo grove (sunrise)", Latitude = 35.0170, Longitude = 135.6716, StayMinutes = 45 });
        guide.Days.Add(day1);
        guide.Days.Add(day2);
        guide.Sections.Add(new GuideSection { Id = Guid.NewGuid(), GuideId = guide.Id, Position = 0, Type = GuideSectionType.Preparation, Title = "Reservations", Body = "Book the tea ceremony and any kaiseki dinners two weeks ahead. Kiyomizu-dera is free; the inner hall costs ¥400." });
        db.TravelGuides.Add(guide);
        return guide;
    }

    private TravelGuide AddRomeGuide(Guid ownerId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var guide = new TravelGuide
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId,
            Title = "Rome for First-Timers",
            Subtitle = "Three days, all walking, espresso in every chapter",
            Summary = "A compact first-timer itinerary built around three walkable loops, with the timings that actually work in summer heat.",
            CountryCode = "IT",
            Cities = new[] { "Rome" },
            Tags = new[] { "city", "italy", "walking" },
            TripDays = 3,
            Lifecycle = GuideLifecycle.FreePublic,
            Slug = "rome-for-first-timers",
            PublishedAt = now.AddDays(-25),
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now.AddDays(-20),
        };
        var day1 = new GuideDay { Id = Guid.NewGuid(), GuideId = guide.Id, Position = 0, Title = "Ancient Rome loop" };
        day1.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day1.Id, Position = 0, Type = GuideNodeType.Attraction, Name = "Colosseum (skip-the-line, 9am)", Latitude = 41.8902, Longitude = 12.4922, StayMinutes = 90 });
        day1.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day1.Id, Position = 1, Type = GuideNodeType.Attraction, Name = "Roman Forum & Palatine Hill", Latitude = 41.8925, Longitude = 12.4853, StayMinutes = 120 });
        day1.Nodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day1.Id, Position = 2, Type = GuideNodeType.Attraction, Name = "Pantheon", Latitude = 41.8986, Longitude = 12.4769, StayMinutes = 30 });
        guide.Days.Add(day1);
        db.TravelGuides.Add(guide);
        return guide;
    }

    private void AddDiscounts(IReadOnlyCollection<TravelGuide> guides, CancellationToken cancellationToken)
    {
        foreach (var guide in guides)
        {
            db.GuideDiscounts.Add(new GuideDiscount
            {
                Id = Guid.NewGuid(),
                GuideId = guide.Id,
                Code = "WELCOME10",
                PercentOff = 10,
                Active = true,
                CreatedAt = clock.UtcNow.AddDays(-20),
            });
        }
    }

    private GuideReview AddReview(Guid guideId, Guid userId, int rating, string body, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var review = new GuideReview
        {
            Id = Guid.NewGuid(),
            GuideId = guideId,
            UserId = userId,
            Rating = rating,
            Body = body,
            ModerationStatus = ReviewModerationStatus.Visible,
            CreatedAt = now.AddDays(-7),
            UpdatedAt = now.AddDays(-7),
        };
        db.GuideReviews.Add(review);
        return review;
    }

    private void AddInitialRelease(TravelGuide guide, Guid publisherUserId, CancellationToken cancellationToken)
    {
        db.GuideReleases.Add(new GuideRelease
        {
            Id = Guid.NewGuid(),
            GuideId = guide.Id,
            VersionNumber = 1,
            Changelog = "Initial release.",
            Title = guide.Title,
            PublishedAt = clock.UtcNow,
            PublisherUserId = publisherUserId,
            NodeSummary = string.Join(", ", guide.Days.SelectMany(d => d.Nodes).Select(n => n.Name)),
        });
    }

    private static string SignManifest(string manifest)
    {
        // Mirrors Trippify.Api.PluginSignature.Sign with the default
        // dev secret. The signature is stored verbatim so the admin
        // verification endpoint can round-trip it.
        const string secret = "trippify-dev-shared-hmac-secret";
        var key = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public static class DemoData
{
    public const string Password = "Seed!Pass123";
}
