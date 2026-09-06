import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_en.dart';
import 'app_localizations_zh.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'generated/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale)
    : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations? of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations);
  }

  static const LocalizationsDelegate<AppLocalizations> delegate =
      _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates =
      <LocalizationsDelegate<dynamic>>[
        delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
      ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[
    Locale('en'),
    Locale('zh'),
  ];

  /// No description provided for @appTitle.
  ///
  /// In en, this message translates to:
  /// **'Trippify'**
  String get appTitle;

  /// No description provided for @loading.
  ///
  /// In en, this message translates to:
  /// **'Loading'**
  String get loading;

  /// No description provided for @restricted.
  ///
  /// In en, this message translates to:
  /// **'Restricted'**
  String get restricted;

  /// No description provided for @homeAnonymousTitle.
  ///
  /// In en, this message translates to:
  /// **'Welcome to Trippify'**
  String get homeAnonymousTitle;

  /// No description provided for @homeAnonymousSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Please sign in to access your home screen'**
  String get homeAnonymousSubtitle;

  /// No description provided for @homeSignIn.
  ///
  /// In en, this message translates to:
  /// **'Sign in'**
  String get homeSignIn;

  /// No description provided for @homeCreateAccount.
  ///
  /// In en, this message translates to:
  /// **'Create account'**
  String get homeCreateAccount;

  /// No description provided for @homeBrowseCatalog.
  ///
  /// In en, this message translates to:
  /// **'Browse the catalog'**
  String get homeBrowseCatalog;

  /// No description provided for @homeGreeting.
  ///
  /// In en, this message translates to:
  /// **'Welcome back, {name}.'**
  String homeGreeting(String name);

  /// No description provided for @accountSummaryTitle.
  ///
  /// In en, this message translates to:
  /// **'Account summary'**
  String get accountSummaryTitle;

  /// No description provided for @accountEmail.
  ///
  /// In en, this message translates to:
  /// **'Email'**
  String get accountEmail;

  /// No description provided for @accountRoles.
  ///
  /// In en, this message translates to:
  /// **'Roles'**
  String get accountRoles;

  /// No description provided for @accountStatus.
  ///
  /// In en, this message translates to:
  /// **'Status'**
  String get accountStatus;

  /// No description provided for @accountCreator.
  ///
  /// In en, this message translates to:
  /// **'Creator'**
  String get accountCreator;

  /// No description provided for @accountRolesNone.
  ///
  /// In en, this message translates to:
  /// **'None'**
  String get accountRolesNone;

  /// No description provided for @accountCreatorYes.
  ///
  /// In en, this message translates to:
  /// **'Yes'**
  String get accountCreatorYes;

  /// No description provided for @accountCreatorNo.
  ///
  /// In en, this message translates to:
  /// **'No'**
  String get accountCreatorNo;

  /// No description provided for @verifyEmailTitle.
  ///
  /// In en, this message translates to:
  /// **'Verify your email'**
  String get verifyEmailTitle;

  /// No description provided for @verifyEmailBody.
  ///
  /// In en, this message translates to:
  /// **'Confirm {email} to unlock purchases, forks, and creator tools.'**
  String verifyEmailBody(String email);

  /// No description provided for @verifyEmailSending.
  ///
  /// In en, this message translates to:
  /// **'Sending…'**
  String get verifyEmailSending;

  /// No description provided for @verifyEmailResend.
  ///
  /// In en, this message translates to:
  /// **'Resend'**
  String get verifyEmailResend;

  /// No description provided for @signOutTooltip.
  ///
  /// In en, this message translates to:
  /// **'Sign out'**
  String get signOutTooltip;

  /// No description provided for @accessDeniedTitle.
  ///
  /// In en, this message translates to:
  /// **'Access denied'**
  String get accessDeniedTitle;

  /// No description provided for @accessDeniedBody.
  ///
  /// In en, this message translates to:
  /// **'Your account does not have permission to open {route}. Return to your workspace or sign in with a different account.'**
  String accessDeniedBody(String route);

  /// No description provided for @accessDeniedReturn.
  ///
  /// In en, this message translates to:
  /// **'Back to workspace'**
  String get accessDeniedReturn;

  /// No description provided for @sectionCreatorWorkspace.
  ///
  /// In en, this message translates to:
  /// **'Creator workspace'**
  String get sectionCreatorWorkspace;

  /// No description provided for @sectionGetStarted.
  ///
  /// In en, this message translates to:
  /// **'Get started'**
  String get sectionGetStarted;

  /// No description provided for @sectionDiscoverPlan.
  ///
  /// In en, this message translates to:
  /// **'Discover & plan'**
  String get sectionDiscoverPlan;

  /// No description provided for @sectionAdministration.
  ///
  /// In en, this message translates to:
  /// **'Administration'**
  String get sectionAdministration;

  /// No description provided for @sectionTenant.
  ///
  /// In en, this message translates to:
  /// **'Tenant'**
  String get sectionTenant;

  /// No description provided for @sectionAccount.
  ///
  /// In en, this message translates to:
  /// **'Account'**
  String get sectionAccount;

  /// No description provided for @entryCreatorDashboard.
  ///
  /// In en, this message translates to:
  /// **'Creator dashboard'**
  String get entryCreatorDashboard;

  /// No description provided for @entryCreatorDashboardDesc.
  ///
  /// In en, this message translates to:
  /// **'Sales, reviews, and revenue summary.'**
  String get entryCreatorDashboardDesc;

  /// No description provided for @entryMyGuides.
  ///
  /// In en, this message translates to:
  /// **'My guides'**
  String get entryMyGuides;

  /// No description provided for @entryMyGuidesDesc.
  ///
  /// In en, this message translates to:
  /// **'Authoring, planning, and releases.'**
  String get entryMyGuidesDesc;

  /// No description provided for @entryPlanRoutes.
  ///
  /// In en, this message translates to:
  /// **'Plan routes & budget'**
  String get entryPlanRoutes;

  /// No description provided for @entryPlanRoutesDesc.
  ///
  /// In en, this message translates to:
  /// **'Day-by-day routes, budget lines, and party totals.'**
  String get entryPlanRoutesDesc;

  /// No description provided for @entryLicensePolicies.
  ///
  /// In en, this message translates to:
  /// **'License policies'**
  String get entryLicensePolicies;

  /// No description provided for @entryLicensePoliciesDesc.
  ///
  /// In en, this message translates to:
  /// **'Commercial and remix defaults.'**
  String get entryLicensePoliciesDesc;

  /// No description provided for @entryBecomeCreator.
  ///
  /// In en, this message translates to:
  /// **'Become a creator'**
  String get entryBecomeCreator;

  /// No description provided for @entryBecomeCreatorDesc.
  ///
  /// In en, this message translates to:
  /// **'Submit a slug and bio to publish your own guides.'**
  String get entryBecomeCreatorDesc;

  /// No description provided for @entryDiscoverGuides.
  ///
  /// In en, this message translates to:
  /// **'Discover guides'**
  String get entryDiscoverGuides;

  /// No description provided for @entryDiscoverGuidesDesc.
  ///
  /// In en, this message translates to:
  /// **'Browse the public catalog and curated trips.'**
  String get entryDiscoverGuidesDesc;

  /// No description provided for @entryMyLibrary.
  ///
  /// In en, this message translates to:
  /// **'My library'**
  String get entryMyLibrary;

  /// No description provided for @entryMyLibraryDesc.
  ///
  /// In en, this message translates to:
  /// **'Purchased guides, forks, and saved trips.'**
  String get entryMyLibraryDesc;

  /// No description provided for @entryAdminOperations.
  ///
  /// In en, this message translates to:
  /// **'Admin operations'**
  String get entryAdminOperations;

  /// No description provided for @entryAdminOperationsDesc.
  ///
  /// In en, this message translates to:
  /// **'Audit log, users, creators, and evidence review.'**
  String get entryAdminOperationsDesc;

  /// No description provided for @entryMyTenant.
  ///
  /// In en, this message translates to:
  /// **'My tenant'**
  String get entryMyTenant;

  /// No description provided for @entryMyTenantDesc.
  ///
  /// In en, this message translates to:
  /// **'Plan, quotas, and exports for your deployment.'**
  String get entryMyTenantDesc;

  /// No description provided for @entryAssistedImport.
  ///
  /// In en, this message translates to:
  /// **'Assisted import'**
  String get entryAssistedImport;

  /// No description provided for @entryAssistedImportDesc.
  ///
  /// In en, this message translates to:
  /// **'Convert sources into AI-assisted drafts.'**
  String get entryAssistedImportDesc;

  /// No description provided for @entryMyProfile.
  ///
  /// In en, this message translates to:
  /// **'My profile'**
  String get entryMyProfile;

  /// No description provided for @entryMyProfileDesc.
  ///
  /// In en, this message translates to:
  /// **'Display name, avatar, and locale preferences.'**
  String get entryMyProfileDesc;

  /// No description provided for @entryNotifications.
  ///
  /// In en, this message translates to:
  /// **'Notifications'**
  String get entryNotifications;

  /// No description provided for @entryNotificationsDesc.
  ///
  /// In en, this message translates to:
  /// **'Replies, remix decisions, and reviewer updates.'**
  String get entryNotificationsDesc;

  /// No description provided for @entryNotificationPreferences.
  ///
  /// In en, this message translates to:
  /// **'Notification preferences'**
  String get entryNotificationPreferences;

  /// No description provided for @entryNotificationPreferencesDesc.
  ///
  /// In en, this message translates to:
  /// **'Choose how the service reaches you.'**
  String get entryNotificationPreferencesDesc;

  /// No description provided for @entryPluginCatalog.
  ///
  /// In en, this message translates to:
  /// **'Plugin catalog'**
  String get entryPluginCatalog;

  /// No description provided for @entryPluginCatalogDesc.
  ///
  /// In en, this message translates to:
  /// **'Browse, install, and manage plugins.'**
  String get entryPluginCatalogDesc;

  /// No description provided for @entrySelfHostedStatus.
  ///
  /// In en, this message translates to:
  /// **'Self-hosted status'**
  String get entrySelfHostedStatus;

  /// No description provided for @entrySelfHostedStatusDesc.
  ///
  /// In en, this message translates to:
  /// **'Version, migrations, upgrades, and backups.'**
  String get entrySelfHostedStatusDesc;

  /// No description provided for @entryFindCreator.
  ///
  /// In en, this message translates to:
  /// **'Find a creator'**
  String get entryFindCreator;

  /// No description provided for @entryFindCreatorDesc.
  ///
  /// In en, this message translates to:
  /// **'Search public creator profiles.'**
  String get entryFindCreatorDesc;

  /// No description provided for @destinationHome.
  ///
  /// In en, this message translates to:
  /// **'Home'**
  String get destinationHome;

  /// No description provided for @destinationDiscover.
  ///
  /// In en, this message translates to:
  /// **'Discover'**
  String get destinationDiscover;

  /// No description provided for @destinationLibrary.
  ///
  /// In en, this message translates to:
  /// **'Library'**
  String get destinationLibrary;

  /// No description provided for @destinationPlan.
  ///
  /// In en, this message translates to:
  /// **'Plan'**
  String get destinationPlan;

  /// No description provided for @destinationCreate.
  ///
  /// In en, this message translates to:
  /// **'Create'**
  String get destinationCreate;

  /// No description provided for @signInEmail.
  ///
  /// In en, this message translates to:
  /// **'Email'**
  String get signInEmail;

  /// No description provided for @signInPassword.
  ///
  /// In en, this message translates to:
  /// **'Password'**
  String get signInPassword;

  /// No description provided for @signInSubmit.
  ///
  /// In en, this message translates to:
  /// **'Sign in'**
  String get signInSubmit;

  /// No description provided for @signInCreateAccount.
  ///
  /// In en, this message translates to:
  /// **'Create account'**
  String get signInCreateAccount;

  /// No description provided for @signInInvalidEmail.
  ///
  /// In en, this message translates to:
  /// **'Enter a valid email address'**
  String get signInInvalidEmail;

  /// No description provided for @signInMissingEmail.
  ///
  /// In en, this message translates to:
  /// **'Email is required'**
  String get signInMissingEmail;

  /// No description provided for @signInMissingPassword.
  ///
  /// In en, this message translates to:
  /// **'Password is required'**
  String get signInMissingPassword;

  /// No description provided for @signInTitle.
  ///
  /// In en, this message translates to:
  /// **'Sign in'**
  String get signInTitle;

  /// No description provided for @signInFailure.
  ///
  /// In en, this message translates to:
  /// **'Email or password is incorrect, or the account has not been confirmed.'**
  String get signInFailure;

  /// No description provided for @registerTitle.
  ///
  /// In en, this message translates to:
  /// **'Create account'**
  String get registerTitle;

  /// No description provided for @registerEmail.
  ///
  /// In en, this message translates to:
  /// **'Email'**
  String get registerEmail;

  /// No description provided for @registerPassword.
  ///
  /// In en, this message translates to:
  /// **'Password'**
  String get registerPassword;

  /// No description provided for @registerConfirmPassword.
  ///
  /// In en, this message translates to:
  /// **'Confirm password'**
  String get registerConfirmPassword;

  /// No description provided for @registerSubmit.
  ///
  /// In en, this message translates to:
  /// **'Create account'**
  String get registerSubmit;

  /// No description provided for @registerInvalidEmail.
  ///
  /// In en, this message translates to:
  /// **'Enter a valid email address'**
  String get registerInvalidEmail;

  /// No description provided for @registerPasswordsDoNotMatch.
  ///
  /// In en, this message translates to:
  /// **'Passwords do not match'**
  String get registerPasswordsDoNotMatch;

  /// No description provided for @registerPasswordTooShort.
  ///
  /// In en, this message translates to:
  /// **'Password must be at least 10 characters'**
  String get registerPasswordTooShort;

  /// No description provided for @registerPasswordRequiresMixed.
  ///
  /// In en, this message translates to:
  /// **'Use letters, numbers, or symbols'**
  String get registerPasswordRequiresMixed;

  /// No description provided for @registerPasswordRequiresLetterAndNumber.
  ///
  /// In en, this message translates to:
  /// **'Include at least one letter and one number'**
  String get registerPasswordRequiresLetterAndNumber;

  /// No description provided for @registerPasswordRequiresSymbol.
  ///
  /// In en, this message translates to:
  /// **'Include at least one symbol'**
  String get registerPasswordRequiresSymbol;

  /// No description provided for @registerSuccessTitle.
  ///
  /// In en, this message translates to:
  /// **'Confirm your email'**
  String get registerSuccessTitle;

  /// No description provided for @registerSuccessBodyNoEmail.
  ///
  /// In en, this message translates to:
  /// **'We sent a confirmation link to your email.'**
  String get registerSuccessBodyNoEmail;

  /// No description provided for @registerSuccessBodyWithEmail.
  ///
  /// In en, this message translates to:
  /// **'We sent a confirmation link to {email}.'**
  String registerSuccessBodyWithEmail(String email);

  /// No description provided for @registerChecklistInbox.
  ///
  /// In en, this message translates to:
  /// **'Open your inbox and look for the Trippify email.'**
  String get registerChecklistInbox;

  /// No description provided for @registerChecklistSpam.
  ///
  /// In en, this message translates to:
  /// **'If it is not there, check your spam or junk folder.'**
  String get registerChecklistSpam;

  /// No description provided for @registerChecklistClickLink.
  ///
  /// In en, this message translates to:
  /// **'Click the link in the email to activate your account.'**
  String get registerChecklistClickLink;

  /// No description provided for @registerResend.
  ///
  /// In en, this message translates to:
  /// **'Resend verification'**
  String get registerResend;

  /// No description provided for @registerResentSuccess.
  ///
  /// In en, this message translates to:
  /// **'Verification email re-sent. Check your inbox in a few minutes.'**
  String get registerResentSuccess;

  /// No description provided for @registerResentFailure.
  ///
  /// In en, this message translates to:
  /// **'Resend is unavailable right now. Please try later.'**
  String get registerResentFailure;

  /// No description provided for @registerBackToSignIn.
  ///
  /// In en, this message translates to:
  /// **'Back to sign in'**
  String get registerBackToSignIn;

  /// No description provided for @discoverTitle.
  ///
  /// In en, this message translates to:
  /// **'Discover guides'**
  String get discoverTitle;

  /// No description provided for @discoverSearchLabel.
  ///
  /// In en, this message translates to:
  /// **'Search guides'**
  String get discoverSearchLabel;

  /// No description provided for @discoverSearchTooltip.
  ///
  /// In en, this message translates to:
  /// **'Search'**
  String get discoverSearchTooltip;

  /// No description provided for @discoverPricingLabel.
  ///
  /// In en, this message translates to:
  /// **'Pricing'**
  String get discoverPricingLabel;

  /// No description provided for @discoverPricingAll.
  ///
  /// In en, this message translates to:
  /// **'All'**
  String get discoverPricingAll;

  /// No description provided for @discoverPricingFree.
  ///
  /// In en, this message translates to:
  /// **'Free'**
  String get discoverPricingFree;

  /// No description provided for @discoverPricingPaid.
  ///
  /// In en, this message translates to:
  /// **'Paid'**
  String get discoverPricingPaid;

  /// No description provided for @discoverUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Discovery is unavailable. Try again later.'**
  String get discoverUnavailable;

  /// No description provided for @discoverEmpty.
  ///
  /// In en, this message translates to:
  /// **'No published guides match your search yet.'**
  String get discoverEmpty;

  /// No description provided for @libraryTitle.
  ///
  /// In en, this message translates to:
  /// **'My library'**
  String get libraryTitle;

  /// No description provided for @libraryUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Guide access denied or unavailable.'**
  String get libraryUnavailable;

  /// No description provided for @guidesTitle.
  ///
  /// In en, this message translates to:
  /// **'My guides'**
  String get guidesTitle;

  /// No description provided for @guidesUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Guide access denied or unavailable.'**
  String get guidesUnavailable;

  /// No description provided for @guidesEmpty.
  ///
  /// In en, this message translates to:
  /// **'No guides yet. Create your first structured itinerary.'**
  String get guidesEmpty;

  /// No description provided for @guidesCreateDraft.
  ///
  /// In en, this message translates to:
  /// **'Create draft'**
  String get guidesCreateDraft;

  /// No description provided for @guidesSaveItinerary.
  ///
  /// In en, this message translates to:
  /// **'Save itinerary'**
  String get guidesSaveItinerary;

  /// No description provided for @guidesStatusDraftCreated.
  ///
  /// In en, this message translates to:
  /// **'Draft created.'**
  String get guidesStatusDraftCreated;

  /// No description provided for @guidesStatusSaved.
  ///
  /// In en, this message translates to:
  /// **'Guide saved.'**
  String get guidesStatusSaved;

  /// No description provided for @guidesStatusConflict.
  ///
  /// In en, this message translates to:
  /// **'Guide changed elsewhere. Reload before saving.'**
  String get guidesStatusConflict;

  /// No description provided for @guidesTitleLabel.
  ///
  /// In en, this message translates to:
  /// **'Guide title'**
  String get guidesTitleLabel;

  /// No description provided for @guidesCountryLabel.
  ///
  /// In en, this message translates to:
  /// **'Country code'**
  String get guidesCountryLabel;

  /// No description provided for @guidesEditingTitle.
  ///
  /// In en, this message translates to:
  /// **'Editing {title}'**
  String guidesEditingTitle(String title);

  /// No description provided for @guidesMoveDayUpTooltip.
  ///
  /// In en, this message translates to:
  /// **'Move day up'**
  String get guidesMoveDayUpTooltip;

  /// No description provided for @planningTitle.
  ///
  /// In en, this message translates to:
  /// **'Plan routes & budget'**
  String get planningTitle;

  /// No description provided for @planningUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Planning access denied or unavailable.'**
  String get planningUnavailable;

  /// No description provided for @planningEmpty.
  ///
  /// In en, this message translates to:
  /// **'No guides yet. Create a structured itinerary to plan routes.'**
  String get planningEmpty;

  /// No description provided for @planningGuideLabel.
  ///
  /// In en, this message translates to:
  /// **'Guide'**
  String get planningGuideLabel;

  /// No description provided for @planningDayLabel.
  ///
  /// In en, this message translates to:
  /// **'Day'**
  String get planningDayLabel;

  /// No description provided for @planningDay.
  ///
  /// In en, this message translates to:
  /// **'Day {day}'**
  String planningDay(int day);

  /// No description provided for @planningPartySizeLabel.
  ///
  /// In en, this message translates to:
  /// **'Party size'**
  String get planningPartySizeLabel;

  /// No description provided for @planningDecreasePartySize.
  ///
  /// In en, this message translates to:
  /// **'Fewer travelers'**
  String get planningDecreasePartySize;

  /// No description provided for @planningIncreasePartySize.
  ///
  /// In en, this message translates to:
  /// **'More travelers'**
  String get planningIncreasePartySize;

  /// No description provided for @planningNoMarkers.
  ///
  /// In en, this message translates to:
  /// **'No markers yet. Add places to this day.'**
  String get planningNoMarkers;

  /// No description provided for @planningNoCoordinates.
  ///
  /// In en, this message translates to:
  /// **'No coordinates'**
  String get planningNoCoordinates;

  /// No description provided for @planningUnresolvedLocation.
  ///
  /// In en, this message translates to:
  /// **'Provider could not resolve this location.'**
  String get planningUnresolvedLocation;

  /// No description provided for @planningGeocodeAttribution.
  ///
  /// In en, this message translates to:
  /// **'Geocode attribution'**
  String get planningGeocodeAttribution;

  /// No description provided for @planningNoBudgetEntries.
  ///
  /// In en, this message translates to:
  /// **'No budget entries yet.'**
  String get planningNoBudgetEntries;

  /// No description provided for @planningBudgetPerPerson.
  ///
  /// In en, this message translates to:
  /// **'{amount} {currency} per person'**
  String planningBudgetPerPerson(String amount, String currency);

  /// No description provided for @planningBudgetPartyTotal.
  ///
  /// In en, this message translates to:
  /// **'{amount} {currency}'**
  String planningBudgetPartyTotal(String amount, String currency);

  /// No description provided for @planningSegmentRoute.
  ///
  /// In en, this message translates to:
  /// **'{origin} → {destination}'**
  String planningSegmentRoute(String origin, String destination);

  /// No description provided for @planningSegmentMeta.
  ///
  /// In en, this message translates to:
  /// **'{mode} · {minutes} min'**
  String planningSegmentMeta(String mode, int minutes);

  /// No description provided for @planningMarkerCoords.
  ///
  /// In en, this message translates to:
  /// **'{lat}, {lng}'**
  String planningMarkerCoords(String lat, String lng);

  /// No description provided for @guideTitle.
  ///
  /// In en, this message translates to:
  /// **'Guide'**
  String get guideTitle;

  /// No description provided for @guideNotFound.
  ///
  /// In en, this message translates to:
  /// **'Guide not found.'**
  String get guideNotFound;

  /// No description provided for @guideViewAuthor.
  ///
  /// In en, this message translates to:
  /// **'View author'**
  String get guideViewAuthor;

  /// No description provided for @guidePurchased.
  ///
  /// In en, this message translates to:
  /// **'Purchased. Full guide unlocked.'**
  String get guidePurchased;

  /// No description provided for @guideForkForEditing.
  ///
  /// In en, this message translates to:
  /// **'Fork for editing'**
  String get guideForkForEditing;

  /// No description provided for @guideSaveAsTrip.
  ///
  /// In en, this message translates to:
  /// **'Save as a trip'**
  String get guideSaveAsTrip;

  /// No description provided for @guideCheckoutFailed.
  ///
  /// In en, this message translates to:
  /// **'Checkout failed. Retry to resume.'**
  String get guideCheckoutFailed;

  /// No description provided for @guideFavoritesRemoved.
  ///
  /// In en, this message translates to:
  /// **'Removed from favorites.'**
  String get guideFavoritesRemoved;

  /// No description provided for @guideFavoritesAdded.
  ///
  /// In en, this message translates to:
  /// **'Added to favorites.'**
  String get guideFavoritesAdded;

  /// No description provided for @guideFavoritesUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Favorites are unavailable.'**
  String get guideFavoritesUnavailable;

  /// No description provided for @guideCannotFork.
  ///
  /// In en, this message translates to:
  /// **'Cannot fork this guide right now.'**
  String get guideCannotFork;

  /// No description provided for @guideSavedAsTrip.
  ///
  /// In en, this message translates to:
  /// **'Saved as a trip. Manage it from My library.'**
  String get guideSavedAsTrip;

  /// No description provided for @guideCannotSaveTrip.
  ///
  /// In en, this message translates to:
  /// **'Cannot save this guide as a trip.'**
  String get guideCannotSaveTrip;

  /// No description provided for @retry.
  ///
  /// In en, this message translates to:
  /// **'Retry'**
  String get retry;

  /// No description provided for @providerUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Provider is not configured for this environment.'**
  String get providerUnavailable;

  /// No description provided for @providerDenied.
  ///
  /// In en, this message translates to:
  /// **'You do not have access to this provider.'**
  String get providerDenied;

  /// No description provided for @providerOffline.
  ///
  /// In en, this message translates to:
  /// **'Provider is unreachable. Check your connection and retry.'**
  String get providerOffline;

  /// No description provided for @mapNoCoordinates.
  ///
  /// In en, this message translates to:
  /// **'No coordinates yet. Add places with an address.'**
  String get mapNoCoordinates;

  /// No description provided for @mapUnresolvedCount.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 location could not be resolved.} other{{count} locations could not be resolved.}}'**
  String mapUnresolvedCount(int count);

  /// No description provided for @mapUnresolved.
  ///
  /// In en, this message translates to:
  /// **'Unresolved location'**
  String get mapUnresolved;

  /// No description provided for @mapAccessDenied.
  ///
  /// In en, this message translates to:
  /// **'Map access denied for this guide.'**
  String get mapAccessDenied;

  /// No description provided for @mapOffline.
  ///
  /// In en, this message translates to:
  /// **'Map provider is unreachable. Check your connection and retry.'**
  String get mapOffline;

  /// No description provided for @reviewsRatingLabel.
  ///
  /// In en, this message translates to:
  /// **'Rating: {rating} of {max}'**
  String reviewsRatingLabel(int rating, int max);

  /// No description provided for @reviewsSubmit.
  ///
  /// In en, this message translates to:
  /// **'Submit review'**
  String get reviewsSubmit;

  /// No description provided for @reviewsUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Reviews are unavailable.'**
  String get reviewsUnavailable;

  /// No description provided for @reviewsNone.
  ///
  /// In en, this message translates to:
  /// **'No reviews yet.'**
  String get reviewsNone;

  /// No description provided for @reviewsReply.
  ///
  /// In en, this message translates to:
  /// **'Author reply: {body}'**
  String reviewsReply(String body);

  /// No description provided for @reviewHelperBody.
  ///
  /// In en, this message translates to:
  /// **'30–4000 characters'**
  String get reviewHelperBody;

  /// No description provided for @reviewBodyLabel.
  ///
  /// In en, this message translates to:
  /// **'Review body'**
  String get reviewBodyLabel;

  /// No description provided for @reviewRatingLabel.
  ///
  /// In en, this message translates to:
  /// **'Rating'**
  String get reviewRatingLabel;

  /// No description provided for @reviewBodyRequired.
  ///
  /// In en, this message translates to:
  /// **'Add a review body before submitting.'**
  String get reviewBodyRequired;

  /// No description provided for @reviewRatingRequired.
  ///
  /// In en, this message translates to:
  /// **'Choose a rating before submitting.'**
  String get reviewRatingRequired;

  /// No description provided for @menuLoad.
  ///
  /// In en, this message translates to:
  /// **'Load'**
  String get menuLoad;

  /// No description provided for @menuNotifications.
  ///
  /// In en, this message translates to:
  /// **'Notifications'**
  String get menuNotifications;

  /// No description provided for @menuPlugins.
  ///
  /// In en, this message translates to:
  /// **'Plugins'**
  String get menuPlugins;

  /// No description provided for @menuSystemStatus.
  ///
  /// In en, this message translates to:
  /// **'System status'**
  String get menuSystemStatus;

  /// No description provided for @menuReturnHome.
  ///
  /// In en, this message translates to:
  /// **'Return to home'**
  String get menuReturnHome;

  /// No description provided for @menuCancel.
  ///
  /// In en, this message translates to:
  /// **'Cancel'**
  String get menuCancel;

  /// No description provided for @menuClose.
  ///
  /// In en, this message translates to:
  /// **'Close'**
  String get menuClose;

  /// No description provided for @menuSend.
  ///
  /// In en, this message translates to:
  /// **'Send'**
  String get menuSend;

  /// No description provided for @creatorDashboardTitle.
  ///
  /// In en, this message translates to:
  /// **'Creator dashboard'**
  String get creatorDashboardTitle;

  /// No description provided for @creatorDashboardUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Creator dashboard is unavailable.'**
  String get creatorDashboardUnavailable;

  /// No description provided for @creatorDashboardNoRevenue.
  ///
  /// In en, this message translates to:
  /// **'No revenue yet.'**
  String get creatorDashboardNoRevenue;

  /// No description provided for @creatorDashboardReviewSummaryUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Review summary unavailable.'**
  String get creatorDashboardReviewSummaryUnavailable;

  /// No description provided for @creatorDashboardOrdersUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Order list is unavailable.'**
  String get creatorDashboardOrdersUnavailable;

  /// No description provided for @creatorDashboardNoOrders.
  ///
  /// In en, this message translates to:
  /// **'No orders yet.'**
  String get creatorDashboardNoOrders;

  /// No description provided for @creatorDashboardSignedDownloadTitle.
  ///
  /// In en, this message translates to:
  /// **'Signed download'**
  String get creatorDashboardSignedDownloadTitle;

  /// No description provided for @creatorDashboardAmount.
  ///
  /// In en, this message translates to:
  /// **'{amount}'**
  String creatorDashboardAmount(String amount);

  /// No description provided for @adminOperationsTitle.
  ///
  /// In en, this message translates to:
  /// **'Admin operations'**
  String get adminOperationsTitle;

  /// No description provided for @adminOperationsAuditUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Audit log unavailable.'**
  String get adminOperationsAuditUnavailable;

  /// No description provided for @adminOperationsNoAudit.
  ///
  /// In en, this message translates to:
  /// **'No audit entries yet.'**
  String get adminOperationsNoAudit;

  /// No description provided for @adminOperationsUsersUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Users list unavailable.'**
  String get adminOperationsUsersUnavailable;

  /// No description provided for @adminOperationsNoUsers.
  ///
  /// In en, this message translates to:
  /// **'No users found.'**
  String get adminOperationsNoUsers;

  /// No description provided for @adminOperationsCreatorsUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Creators list unavailable.'**
  String get adminOperationsCreatorsUnavailable;

  /// No description provided for @adminOperationsNoCreators.
  ///
  /// In en, this message translates to:
  /// **'No creators yet.'**
  String get adminOperationsNoCreators;

  /// No description provided for @adminOperationsLoadAttachments.
  ///
  /// In en, this message translates to:
  /// **'Load attachments'**
  String get adminOperationsLoadAttachments;

  /// No description provided for @adminOperationsNoAttachments.
  ///
  /// In en, this message translates to:
  /// **'No attachments loaded.'**
  String get adminOperationsNoAttachments;

  /// No description provided for @adminOperationsDownload.
  ///
  /// In en, this message translates to:
  /// **'Download'**
  String get adminOperationsDownload;

  /// No description provided for @assistedImportTitle.
  ///
  /// In en, this message translates to:
  /// **'Assisted import'**
  String get assistedImportTitle;

  /// No description provided for @assistedImportSubmitText.
  ///
  /// In en, this message translates to:
  /// **'Submit text import'**
  String get assistedImportSubmitText;

  /// No description provided for @assistedImportSubmitObject.
  ///
  /// In en, this message translates to:
  /// **'Submit object import'**
  String get assistedImportSubmitObject;

  /// No description provided for @assistedImportApprove.
  ///
  /// In en, this message translates to:
  /// **'Approve'**
  String get assistedImportApprove;

  /// No description provided for @assistedImportReject.
  ///
  /// In en, this message translates to:
  /// **'Reject'**
  String get assistedImportReject;

  /// No description provided for @assistedImportTranslateEs.
  ///
  /// In en, this message translates to:
  /// **'Translate (es)'**
  String get assistedImportTranslateEs;

  /// No description provided for @pluginsTitle.
  ///
  /// In en, this message translates to:
  /// **'Plugin catalog'**
  String get pluginsTitle;

  /// No description provided for @pluginsInstallationsUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Installations unavailable.'**
  String get pluginsInstallationsUnavailable;

  /// No description provided for @pluginsNoInstallations.
  ///
  /// In en, this message translates to:
  /// **'No installations yet.'**
  String get pluginsNoInstallations;

  /// No description provided for @pluginsRemove.
  ///
  /// In en, this message translates to:
  /// **'Remove'**
  String get pluginsRemove;

  /// No description provided for @pluginsCatalogUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Catalog unavailable.'**
  String get pluginsCatalogUnavailable;

  /// No description provided for @pluginsNoPlugins.
  ///
  /// In en, this message translates to:
  /// **'No plugins available yet.'**
  String get pluginsNoPlugins;

  /// No description provided for @pluginsInstall.
  ///
  /// In en, this message translates to:
  /// **'Install'**
  String get pluginsInstall;

  /// No description provided for @licensePanelTitle.
  ///
  /// In en, this message translates to:
  /// **'License policies'**
  String get licensePanelTitle;

  /// No description provided for @licensePanelNone.
  ///
  /// In en, this message translates to:
  /// **'No license policies yet.'**
  String get licensePanelNone;

  /// No description provided for @licensePanelCreateDefault.
  ///
  /// In en, this message translates to:
  /// **'Create default license'**
  String get licensePanelCreateDefault;

  /// No description provided for @tenantTitle.
  ///
  /// In en, this message translates to:
  /// **'My tenant'**
  String get tenantTitle;

  /// No description provided for @tenantPlan.
  ///
  /// In en, this message translates to:
  /// **'Plan {plan} ({status})'**
  String tenantPlan(String plan, String status);

  /// No description provided for @tenantNoQuotas.
  ///
  /// In en, this message translates to:
  /// **'No quotas defined yet.'**
  String get tenantNoQuotas;

  /// No description provided for @tenantQuotaUsage.
  ///
  /// In en, this message translates to:
  /// **'Used {used} / Limit {limit}'**
  String tenantQuotaUsage(String used, String limit);

  /// No description provided for @tenantGenerateExport.
  ///
  /// In en, this message translates to:
  /// **'Generate export'**
  String get tenantGenerateExport;

  /// No description provided for @selfHostedTitle.
  ///
  /// In en, this message translates to:
  /// **'Self-hosted status'**
  String get selfHostedTitle;

  /// No description provided for @selfHostedRunUpgrade.
  ///
  /// In en, this message translates to:
  /// **'Run upgrade'**
  String get selfHostedRunUpgrade;

  /// No description provided for @selfHostedCaptureBackup.
  ///
  /// In en, this message translates to:
  /// **'Capture backup'**
  String get selfHostedCaptureBackup;

  /// No description provided for @selfHostedNoFlags.
  ///
  /// In en, this message translates to:
  /// **'No feature flags defined.'**
  String get selfHostedNoFlags;

  /// No description provided for @selfHostedFlagEnabled.
  ///
  /// In en, this message translates to:
  /// **'Enabled {state}'**
  String selfHostedFlagEnabled(String state);

  /// No description provided for @releaseHistoryUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Release history unavailable.'**
  String get releaseHistoryUnavailable;

  /// No description provided for @releaseNoReleases.
  ///
  /// In en, this message translates to:
  /// **'No releases yet.'**
  String get releaseNoReleases;

  /// No description provided for @releaseVersion.
  ///
  /// In en, this message translates to:
  /// **'v{version}'**
  String releaseVersion(String version);

  /// No description provided for @signInShellSignedInDestination.
  ///
  /// In en, this message translates to:
  /// **'Signed in'**
  String get signInShellSignedInDestination;

  /// No description provided for @signInShellLoading.
  ///
  /// In en, this message translates to:
  /// **'Loading'**
  String get signInShellLoading;

  /// No description provided for @validationEmailInvalid.
  ///
  /// In en, this message translates to:
  /// **'Enter a valid email address'**
  String get validationEmailInvalid;

  /// No description provided for @validationRequired.
  ///
  /// In en, this message translates to:
  /// **'{field} is required'**
  String validationRequired(String field);

  /// No description provided for @validationMinLength.
  ///
  /// In en, this message translates to:
  /// **'Use at least {min} characters'**
  String validationMinLength(int min);
}

class _AppLocalizationsDelegate
    extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) =>
      <String>['en', 'zh'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'en':
      return AppLocalizationsEn();
    case 'zh':
      return AppLocalizationsZh();
  }

  throw FlutterError(
    'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
    'an issue with the localizations generation tool. Please file an issue '
    'on GitHub with a reproducible sample app and the gen-l10n configuration '
    'that was used.',
  );
}
