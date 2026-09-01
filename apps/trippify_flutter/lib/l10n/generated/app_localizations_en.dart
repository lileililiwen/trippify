// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get appTitle => 'Trippify';

  @override
  String get loading => 'Loading';

  @override
  String get restricted => 'Restricted';

  @override
  String get homeAnonymousTitle => 'Welcome to Trippify';

  @override
  String get homeAnonymousSubtitle =>
      'Please sign in to access your home screen';

  @override
  String get homeSignIn => 'Sign in';

  @override
  String get homeCreateAccount => 'Create account';

  @override
  String get homeBrowseCatalog => 'Browse the catalog';

  @override
  String homeGreeting(String name) {
    return 'Welcome back, $name.';
  }

  @override
  String get accountSummaryTitle => 'Account summary';

  @override
  String get accountEmail => 'Email';

  @override
  String get accountRoles => 'Roles';

  @override
  String get accountStatus => 'Status';

  @override
  String get accountCreator => 'Creator';

  @override
  String get accountRolesNone => 'None';

  @override
  String get accountCreatorYes => 'Yes';

  @override
  String get accountCreatorNo => 'No';

  @override
  String get verifyEmailTitle => 'Verify your email';

  @override
  String verifyEmailBody(String email) {
    return 'Confirm $email to unlock purchases, forks, and creator tools.';
  }

  @override
  String get verifyEmailSending => 'Sending…';

  @override
  String get verifyEmailResend => 'Resend';

  @override
  String get signOutTooltip => 'Sign out';

  @override
  String get accessDeniedTitle => 'Access denied';

  @override
  String accessDeniedBody(String route) {
    return 'Your account does not have permission to open $route. Return to your workspace or sign in with a different account.';
  }

  @override
  String get accessDeniedReturn => 'Back to workspace';

  @override
  String get sectionCreatorWorkspace => 'Creator workspace';

  @override
  String get sectionGetStarted => 'Get started';

  @override
  String get sectionDiscoverPlan => 'Discover & plan';

  @override
  String get sectionAdministration => 'Administration';

  @override
  String get sectionTenant => 'Tenant';

  @override
  String get sectionAccount => 'Account';

  @override
  String get entryCreatorDashboard => 'Creator dashboard';

  @override
  String get entryCreatorDashboardDesc =>
      'Sales, reviews, and revenue summary.';

  @override
  String get entryMyGuides => 'My guides';

  @override
  String get entryMyGuidesDesc => 'Authoring, planning, and releases.';

  @override
  String get entryPlanRoutes => 'Plan routes & budget';

  @override
  String get entryPlanRoutesDesc =>
      'Day-by-day routes, budget lines, and party totals.';

  @override
  String get entryLicensePolicies => 'License policies';

  @override
  String get entryLicensePoliciesDesc => 'Commercial and remix defaults.';

  @override
  String get entryBecomeCreator => 'Become a creator';

  @override
  String get entryBecomeCreatorDesc =>
      'Submit a slug and bio to publish your own guides.';

  @override
  String get entryDiscoverGuides => 'Discover guides';

  @override
  String get entryDiscoverGuidesDesc =>
      'Browse the public catalog and curated trips.';

  @override
  String get entryMyLibrary => 'My library';

  @override
  String get entryMyLibraryDesc => 'Purchased guides, forks, and saved trips.';

  @override
  String get entryAdminOperations => 'Admin operations';

  @override
  String get entryAdminOperationsDesc =>
      'Audit log, users, creators, and evidence review.';

  @override
  String get entryMyTenant => 'My tenant';

  @override
  String get entryMyTenantDesc =>
      'Plan, quotas, and exports for your deployment.';

  @override
  String get entryAssistedImport => 'Assisted import';

  @override
  String get entryAssistedImportDesc =>
      'Convert sources into AI-assisted drafts.';

  @override
  String get entryMyProfile => 'My profile';

  @override
  String get entryMyProfileDesc =>
      'Display name, avatar, and locale preferences.';

  @override
  String get entryNotifications => 'Notifications';

  @override
  String get entryNotificationsDesc =>
      'Replies, remix decisions, and reviewer updates.';

  @override
  String get entryNotificationPreferences => 'Notification preferences';

  @override
  String get entryNotificationPreferencesDesc =>
      'Choose how the service reaches you.';

  @override
  String get destinationHome => 'Home';

  @override
  String get destinationDiscover => 'Discover';

  @override
  String get destinationLibrary => 'Library';

  @override
  String get destinationPlan => 'Plan';

  @override
  String get destinationCreate => 'Create';

  @override
  String get signInEmail => 'Email';

  @override
  String get signInPassword => 'Password';

  @override
  String get signInSubmit => 'Sign in';

  @override
  String get signInCreateAccount => 'Create account';

  @override
  String get signInInvalidEmail => 'Enter a valid email address';

  @override
  String get signInMissingEmail => 'Email is required';

  @override
  String get signInMissingPassword => 'Password is required';

  @override
  String get signInTitle => 'Sign in';

  @override
  String get signInFailure =>
      'Email or password is incorrect, or the account has not been confirmed.';

  @override
  String get registerTitle => 'Create account';

  @override
  String get registerEmail => 'Email';

  @override
  String get registerPassword => 'Password';

  @override
  String get registerConfirmPassword => 'Confirm password';

  @override
  String get registerSubmit => 'Create account';

  @override
  String get registerInvalidEmail => 'Enter a valid email address';

  @override
  String get registerPasswordsDoNotMatch => 'Passwords do not match';

  @override
  String get registerPasswordTooShort =>
      'Password must be at least 10 characters';

  @override
  String get registerPasswordRequiresMixed =>
      'Use letters, numbers, or symbols';

  @override
  String get registerPasswordRequiresLetterAndNumber =>
      'Include at least one letter and one number';

  @override
  String get registerPasswordRequiresSymbol => 'Include at least one symbol';

  @override
  String get registerSuccessTitle => 'Confirm your email';

  @override
  String get registerSuccessBodyNoEmail =>
      'We sent a confirmation link to your email.';

  @override
  String registerSuccessBodyWithEmail(String email) {
    return 'We sent a confirmation link to $email.';
  }

  @override
  String get registerChecklistInbox =>
      'Open your inbox and look for the Trippify email.';

  @override
  String get registerChecklistSpam =>
      'If it is not there, check your spam or junk folder.';

  @override
  String get registerChecklistClickLink =>
      'Click the link in the email to activate your account.';

  @override
  String get registerResend => 'Resend verification';

  @override
  String get registerResentSuccess =>
      'Verification email re-sent. Check your inbox in a few minutes.';

  @override
  String get registerResentFailure =>
      'Resend is unavailable right now. Please try later.';

  @override
  String get registerBackToSignIn => 'Back to sign in';

  @override
  String get discoverTitle => 'Discover guides';

  @override
  String get discoverSearchLabel => 'Search guides';

  @override
  String get discoverSearchTooltip => 'Search';

  @override
  String get discoverPricingLabel => 'Pricing';

  @override
  String get discoverPricingAll => 'All';

  @override
  String get discoverPricingFree => 'Free';

  @override
  String get discoverPricingPaid => 'Paid';

  @override
  String get discoverUnavailable =>
      'Discovery is unavailable. Try again later.';

  @override
  String get discoverEmpty => 'No published guides match your search yet.';

  @override
  String get libraryTitle => 'My library';

  @override
  String get libraryUnavailable => 'Guide access denied or unavailable.';

  @override
  String get guidesTitle => 'My guides';

  @override
  String get guidesUnavailable => 'Guide access denied or unavailable.';

  @override
  String get guidesEmpty =>
      'No guides yet. Create your first structured itinerary.';

  @override
  String get guidesCreateDraft => 'Create draft';

  @override
  String get guidesSaveItinerary => 'Save itinerary';

  @override
  String get guidesStatusDraftCreated => 'Draft created.';

  @override
  String get guidesStatusSaved => 'Guide saved.';

  @override
  String get guidesStatusConflict =>
      'Guide changed elsewhere. Reload before saving.';

  @override
  String get guidesTitleLabel => 'Guide title';

  @override
  String get guidesCountryLabel => 'Country code';

  @override
  String guidesEditingTitle(String title) {
    return 'Editing $title';
  }

  @override
  String get guidesMoveDayUpTooltip => 'Move day up';

  @override
  String get planningTitle => 'Plan routes & budget';

  @override
  String get planningUnavailable => 'Planning access denied or unavailable.';

  @override
  String get planningEmpty =>
      'No guides yet. Create a structured itinerary to plan routes.';

  @override
  String get planningGuideLabel => 'Guide';

  @override
  String get planningDayLabel => 'Day';

  @override
  String planningDay(int day) {
    return 'Day $day';
  }

  @override
  String get planningPartySizeLabel => 'Party size';

  @override
  String get planningDecreasePartySize => 'Fewer travelers';

  @override
  String get planningIncreasePartySize => 'More travelers';

  @override
  String get planningNoMarkers => 'No markers yet. Add places to this day.';

  @override
  String get planningNoCoordinates => 'No coordinates';

  @override
  String get planningUnresolvedLocation =>
      'Provider could not resolve this location.';

  @override
  String get planningGeocodeAttribution => 'Geocode attribution';

  @override
  String get planningNoBudgetEntries => 'No budget entries yet.';

  @override
  String planningBudgetPerPerson(String amount, String currency) {
    return '$amount $currency per person';
  }

  @override
  String planningBudgetPartyTotal(String amount, String currency) {
    return '$amount $currency';
  }

  @override
  String planningSegmentRoute(String origin, String destination) {
    return '$origin → $destination';
  }

  @override
  String planningSegmentMeta(String mode, int minutes) {
    return '$mode · $minutes min';
  }

  @override
  String planningMarkerCoords(String lat, String lng) {
    return '$lat, $lng';
  }

  @override
  String get guideTitle => 'Guide';

  @override
  String get guideNotFound => 'Guide not found.';

  @override
  String get guideViewAuthor => 'View author';

  @override
  String get guidePurchased => 'Purchased. Full guide unlocked.';

  @override
  String get guideForkForEditing => 'Fork for editing';

  @override
  String get guideSaveAsTrip => 'Save as a trip';

  @override
  String get guideCheckoutFailed => 'Checkout failed. Retry to resume.';

  @override
  String get guideFavoritesRemoved => 'Removed from favorites.';

  @override
  String get guideFavoritesAdded => 'Added to favorites.';

  @override
  String get guideFavoritesUnavailable => 'Favorites are unavailable.';

  @override
  String get guideCannotFork => 'Cannot fork this guide right now.';

  @override
  String get guideSavedAsTrip => 'Saved as a trip. Manage it from My library.';

  @override
  String get guideCannotSaveTrip => 'Cannot save this guide as a trip.';

  @override
  String get retry => 'Retry';

  @override
  String get providerUnavailable =>
      'Provider is not configured for this environment.';

  @override
  String get providerDenied => 'You do not have access to this provider.';

  @override
  String get providerOffline =>
      'Provider is unreachable. Check your connection and retry.';

  @override
  String get mapNoCoordinates =>
      'No coordinates yet. Add places with an address.';

  @override
  String mapUnresolvedCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count locations could not be resolved.',
      one: '1 location could not be resolved.',
    );
    return '$_temp0';
  }

  @override
  String get mapUnresolved => 'Unresolved location';

  @override
  String get mapAccessDenied => 'Map access denied for this guide.';

  @override
  String get mapOffline =>
      'Map provider is unreachable. Check your connection and retry.';

  @override
  String reviewsRatingLabel(int rating, int max) {
    return 'Rating: $rating of $max';
  }

  @override
  String get reviewsSubmit => 'Submit review';

  @override
  String get reviewsUnavailable => 'Reviews are unavailable.';

  @override
  String get reviewsNone => 'No reviews yet.';

  @override
  String reviewsReply(String body) {
    return 'Author reply: $body';
  }

  @override
  String get reviewHelperBody => '30–4000 characters';

  @override
  String get reviewBodyLabel => 'Review body';

  @override
  String get reviewRatingLabel => 'Rating';

  @override
  String get reviewBodyRequired => 'Add a review body before submitting.';

  @override
  String get reviewRatingRequired => 'Choose a rating before submitting.';

  @override
  String get menuLoad => 'Load';

  @override
  String get menuNotifications => 'Notifications';

  @override
  String get menuPlugins => 'Plugins';

  @override
  String get menuSystemStatus => 'System status';

  @override
  String get menuReturnHome => 'Return to home';

  @override
  String get menuCancel => 'Cancel';

  @override
  String get menuClose => 'Close';

  @override
  String get menuSend => 'Send';

  @override
  String get creatorDashboardTitle => 'Creator dashboard';

  @override
  String get creatorDashboardUnavailable => 'Creator dashboard is unavailable.';

  @override
  String get creatorDashboardNoRevenue => 'No revenue yet.';

  @override
  String get creatorDashboardReviewSummaryUnavailable =>
      'Review summary unavailable.';

  @override
  String get creatorDashboardOrdersUnavailable => 'Order list is unavailable.';

  @override
  String get creatorDashboardNoOrders => 'No orders yet.';

  @override
  String get creatorDashboardSignedDownloadTitle => 'Signed download';

  @override
  String creatorDashboardAmount(String amount) {
    return '$amount';
  }

  @override
  String get adminOperationsTitle => 'Admin operations';

  @override
  String get adminOperationsAuditUnavailable => 'Audit log unavailable.';

  @override
  String get adminOperationsNoAudit => 'No audit entries yet.';

  @override
  String get adminOperationsUsersUnavailable => 'Users list unavailable.';

  @override
  String get adminOperationsNoUsers => 'No users found.';

  @override
  String get adminOperationsCreatorsUnavailable => 'Creators list unavailable.';

  @override
  String get adminOperationsNoCreators => 'No creators yet.';

  @override
  String get adminOperationsLoadAttachments => 'Load attachments';

  @override
  String get adminOperationsNoAttachments => 'No attachments loaded.';

  @override
  String get adminOperationsDownload => 'Download';

  @override
  String get assistedImportTitle => 'Assisted import';

  @override
  String get assistedImportSubmitText => 'Submit text import';

  @override
  String get assistedImportSubmitObject => 'Submit object import';

  @override
  String get assistedImportApprove => 'Approve';

  @override
  String get assistedImportReject => 'Reject';

  @override
  String get assistedImportTranslateEs => 'Translate (es)';

  @override
  String get pluginsTitle => 'Plugin catalog';

  @override
  String get pluginsInstallationsUnavailable => 'Installations unavailable.';

  @override
  String get pluginsNoInstallations => 'No installations yet.';

  @override
  String get pluginsRemove => 'Remove';

  @override
  String get pluginsCatalogUnavailable => 'Catalog unavailable.';

  @override
  String get pluginsNoPlugins => 'No plugins available yet.';

  @override
  String get pluginsInstall => 'Install';

  @override
  String get licensePanelTitle => 'License policies';

  @override
  String get licensePanelNone => 'No license policies yet.';

  @override
  String get licensePanelCreateDefault => 'Create default license';

  @override
  String get tenantTitle => 'My tenant';

  @override
  String tenantPlan(String plan, String status) {
    return 'Plan $plan ($status)';
  }

  @override
  String get tenantNoQuotas => 'No quotas defined yet.';

  @override
  String tenantQuotaUsage(String used, String limit) {
    return 'Used $used / Limit $limit';
  }

  @override
  String get tenantGenerateExport => 'Generate export';

  @override
  String get selfHostedTitle => 'Self-hosted status';

  @override
  String get selfHostedRunUpgrade => 'Run upgrade';

  @override
  String get selfHostedCaptureBackup => 'Capture backup';

  @override
  String get selfHostedNoFlags => 'No feature flags defined.';

  @override
  String selfHostedFlagEnabled(String state) {
    return 'Enabled $state';
  }

  @override
  String get releaseHistoryUnavailable => 'Release history unavailable.';

  @override
  String get releaseNoReleases => 'No releases yet.';

  @override
  String releaseVersion(String version) {
    return 'v$version';
  }

  @override
  String get signInShellSignedInDestination => 'Signed in';

  @override
  String get signInShellLoading => 'Loading';

  @override
  String get validationEmailInvalid => 'Enter a valid email address';

  @override
  String validationRequired(String field) {
    return '$field is required';
  }

  @override
  String validationMinLength(int min) {
    return 'Use at least $min characters';
  }
}
