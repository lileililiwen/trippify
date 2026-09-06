// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Chinese (`zh`).
class AppLocalizationsZh extends AppLocalizations {
  AppLocalizationsZh([String locale = 'zh']) : super(locale);

  @override
  String get appTitle => 'Trippify';

  @override
  String get loading => '加载中';

  @override
  String get restricted => '受限';

  @override
  String get homeAnonymousTitle => '欢迎使用 Trippify';

  @override
  String get homeAnonymousSubtitle => '请登录以访问您的主页';

  @override
  String get homeSignIn => '登录';

  @override
  String get homeCreateAccount => '创建账户';

  @override
  String get homeBrowseCatalog => '浏览目录';

  @override
  String homeGreeting(String name) {
    return '欢迎回来，$name。';
  }

  @override
  String get accountSummaryTitle => '账户摘要';

  @override
  String get accountEmail => '邮箱';

  @override
  String get accountRoles => '角色';

  @override
  String get accountStatus => '状态';

  @override
  String get accountCreator => '创作者';

  @override
  String get accountRolesNone => '无';

  @override
  String get accountCreatorYes => '是';

  @override
  String get accountCreatorNo => '否';

  @override
  String get verifyEmailTitle => '请验证您的邮箱';

  @override
  String verifyEmailBody(String email) {
    return '请确认 $email 以解锁购买、二创和创作者工具。';
  }

  @override
  String get verifyEmailSending => '发送中…';

  @override
  String get verifyEmailResend => '重新发送';

  @override
  String get signOutTooltip => '退出登录';

  @override
  String get accessDeniedTitle => '访问被拒绝';

  @override
  String accessDeniedBody(String route) {
    return '您的账户无权打开 $route。请返回工作区或使用其他账户登录。';
  }

  @override
  String get accessDeniedReturn => '返回工作区';

  @override
  String get sectionCreatorWorkspace => '创作者工作区';

  @override
  String get sectionGetStarted => '入门';

  @override
  String get sectionDiscoverPlan => '发现与规划';

  @override
  String get sectionAdministration => '管理';

  @override
  String get sectionTenant => '租户';

  @override
  String get sectionAccount => '账户';

  @override
  String get entryCreatorDashboard => '创作者面板';

  @override
  String get entryCreatorDashboardDesc => '销售、评价与收入概览。';

  @override
  String get entryMyGuides => '我的攻略';

  @override
  String get entryMyGuidesDesc => '编辑、规划与发布。';

  @override
  String get entryPlanRoutes => '规划路线与预算';

  @override
  String get entryPlanRoutesDesc => '每日路线、预算项和同行总额。';

  @override
  String get entryLicensePolicies => '授权策略';

  @override
  String get entryLicensePoliciesDesc => '商用与二创默认值。';

  @override
  String get entryBecomeCreator => '成为创作者';

  @override
  String get entryBecomeCreatorDesc => '提交个性标识与简介以发布攻略。';

  @override
  String get entryDiscoverGuides => '发现攻略';

  @override
  String get entryDiscoverGuidesDesc => '浏览公共目录与精选行程。';

  @override
  String get entryMyLibrary => '我的收藏';

  @override
  String get entryMyLibraryDesc => '已购攻略、二创和保存的行程。';

  @override
  String get entryAdminOperations => '管理员操作';

  @override
  String get entryAdminOperationsDesc => '审计日志、用户、创作者与凭证审核。';

  @override
  String get entryMyTenant => '我的租户';

  @override
  String get entryMyTenantDesc => '套餐、配额与导出。';

  @override
  String get entryAssistedImport => '辅助导入';

  @override
  String get entryAssistedImportDesc => '将源文档转换为 AI 辅助草稿。';

  @override
  String get entryMyProfile => '我的资料';

  @override
  String get entryMyProfileDesc => '昵称、头像和语言偏好。';

  @override
  String get entryNotifications => '通知';

  @override
  String get entryNotificationsDesc => '回复、二创决策与审核更新。';

  @override
  String get entryNotificationPreferences => '通知偏好';

  @override
  String get entryNotificationPreferencesDesc => '选择服务触达方式。';

  @override
  String get entryPluginCatalog => '插件目录';

  @override
  String get entryPluginCatalogDesc => '浏览、安装和管理插件。';

  @override
  String get entrySelfHostedStatus => '自托管状态';

  @override
  String get entrySelfHostedStatusDesc => '版本、迁移、升级和备份。';

  @override
  String get entryFindCreator => '查找创作者';

  @override
  String get entryFindCreatorDesc => '搜索公开创作者资料。';

  @override
  String get destinationHome => '首页';

  @override
  String get destinationDiscover => '发现';

  @override
  String get destinationLibrary => '收藏';

  @override
  String get destinationPlan => '规划';

  @override
  String get destinationCreate => '创建';

  @override
  String get signInEmail => '邮箱';

  @override
  String get signInPassword => '密码';

  @override
  String get signInSubmit => '登录';

  @override
  String get signInCreateAccount => '创建账户';

  @override
  String get signInInvalidEmail => '请输入有效的邮箱地址';

  @override
  String get signInMissingEmail => '邮箱不能为空';

  @override
  String get signInMissingPassword => '密码不能为空';

  @override
  String get signInTitle => '登录';

  @override
  String get signInFailure => '邮箱或密码错误，或账户尚未确认。';

  @override
  String get registerTitle => '创建账户';

  @override
  String get registerEmail => '邮箱';

  @override
  String get registerPassword => '密码';

  @override
  String get registerConfirmPassword => '确认密码';

  @override
  String get registerSubmit => '创建账户';

  @override
  String get registerInvalidEmail => '请输入有效的邮箱地址';

  @override
  String get registerPasswordsDoNotMatch => '两次输入的密码不一致';

  @override
  String get registerPasswordTooShort => '密码至少 10 个字符';

  @override
  String get registerPasswordRequiresMixed => '使用字母、数字或符号';

  @override
  String get registerPasswordRequiresLetterAndNumber => '至少包含一个字母和一个数字';

  @override
  String get registerPasswordRequiresSymbol => '至少包含一个符号';

  @override
  String get registerSuccessTitle => '请确认您的邮箱';

  @override
  String get registerSuccessBodyNoEmail => '我们已向您的邮箱发送确认链接。';

  @override
  String registerSuccessBodyWithEmail(String email) {
    return '我们已向 $email 发送确认链接。';
  }

  @override
  String get registerChecklistInbox => '打开收件箱并查找 Trippify 邮件。';

  @override
  String get registerChecklistSpam => '若未收到，请检查垃圾邮件文件夹。';

  @override
  String get registerChecklistClickLink => '点击邮件中的链接激活账户。';

  @override
  String get registerResend => '重新发送验证邮件';

  @override
  String get registerResentSuccess => '验证邮件已重新发送。请稍后查看收件箱。';

  @override
  String get registerResentFailure => '暂时无法重新发送，请稍后再试。';

  @override
  String get registerBackToSignIn => '返回登录';

  @override
  String get discoverTitle => '发现攻略';

  @override
  String get discoverSearchLabel => '搜索攻略';

  @override
  String get discoverSearchTooltip => '搜索';

  @override
  String get discoverPricingLabel => '价格';

  @override
  String get discoverPricingAll => '全部';

  @override
  String get discoverPricingFree => '免费';

  @override
  String get discoverPricingPaid => '付费';

  @override
  String get discoverUnavailable => '暂无法加载发现内容，请稍后重试。';

  @override
  String get discoverEmpty => '暂无符合搜索条件的公开攻略。';

  @override
  String get libraryTitle => '我的收藏';

  @override
  String get libraryUnavailable => '攻略访问被拒绝或不可用。';

  @override
  String get guidesTitle => '我的攻略';

  @override
  String get guidesUnavailable => '攻略访问被拒绝或不可用。';

  @override
  String get guidesEmpty => '暂无攻略，创建您的第一份结构化行程。';

  @override
  String get guidesCreateDraft => '创建草稿';

  @override
  String get guidesSaveItinerary => '保存行程';

  @override
  String get guidesStatusDraftCreated => '已创建草稿。';

  @override
  String get guidesStatusSaved => '攻略已保存。';

  @override
  String get guidesStatusConflict => '攻略已在其他位置修改，请重新加载后再保存。';

  @override
  String get guidesTitleLabel => '攻略标题';

  @override
  String get guidesCountryLabel => '国家代码';

  @override
  String guidesEditingTitle(String title) {
    return '正在编辑 $title';
  }

  @override
  String get guidesMoveDayUpTooltip => '上移一天';

  @override
  String get planningTitle => '规划路线与预算';

  @override
  String get planningUnavailable => '规划访问被拒绝或不可用。';

  @override
  String get planningEmpty => '暂无攻略，请创建结构化行程后规划路线。';

  @override
  String get planningGuideLabel => '攻略';

  @override
  String get planningDayLabel => '天数';

  @override
  String planningDay(int day) {
    return '第 $day 天';
  }

  @override
  String get planningPartySizeLabel => '同行人数';

  @override
  String get planningDecreasePartySize => '减少同行人数';

  @override
  String get planningIncreasePartySize => '增加同行人数';

  @override
  String get planningNoMarkers => '暂无标记，请向本天添加地点。';

  @override
  String get planningNoCoordinates => '无坐标';

  @override
  String get planningUnresolvedLocation => '服务无法解析此位置。';

  @override
  String get planningGeocodeAttribution => '地理编码归属';

  @override
  String get planningNoBudgetEntries => '暂无预算项。';

  @override
  String planningBudgetPerPerson(String amount, String currency) {
    return '每人 $amount $currency';
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
    return '$mode · $minutes 分钟';
  }

  @override
  String planningMarkerCoords(String lat, String lng) {
    return '$lat, $lng';
  }

  @override
  String get guideTitle => '攻略';

  @override
  String get guideNotFound => '未找到攻略。';

  @override
  String get guideViewAuthor => '查看作者';

  @override
  String get guidePurchased => '已购买。完整攻略已解锁。';

  @override
  String get guideForkForEditing => '创建二创进行编辑';

  @override
  String get guideSaveAsTrip => '另存为行程';

  @override
  String get guideCheckoutFailed => '结算失败，请重试以继续。';

  @override
  String get guideFavoritesRemoved => '已从收藏中移除。';

  @override
  String get guideFavoritesAdded => '已加入收藏。';

  @override
  String get guideFavoritesUnavailable => '收藏功能不可用。';

  @override
  String get guideCannotFork => '暂时无法二创此攻略。';

  @override
  String get guideSavedAsTrip => '已保存为行程，可在“我的收藏”中管理。';

  @override
  String get guideCannotSaveTrip => '无法将此攻略保存为行程。';

  @override
  String get retry => '重试';

  @override
  String get providerUnavailable => '当前环境未配置该服务。';

  @override
  String get providerDenied => '您无权访问此服务。';

  @override
  String get providerOffline => '服务无法连接，请检查网络后重试。';

  @override
  String get mapNoCoordinates => '暂无坐标，请添加带地址的地点。';

  @override
  String mapUnresolvedCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count 个位置无法解析。',
      one: '1 个位置无法解析。',
    );
    return '$_temp0';
  }

  @override
  String get mapUnresolved => '位置无法解析';

  @override
  String get mapAccessDenied => '此攻略的地图访问被拒绝。';

  @override
  String get mapOffline => '地图服务无法连接，请检查网络后重试。';

  @override
  String reviewsRatingLabel(int rating, int max) {
    return '评分：$rating / $max';
  }

  @override
  String get reviewsSubmit => '提交评价';

  @override
  String get reviewsUnavailable => '评价暂不可用。';

  @override
  String get reviewsNone => '暂无评价。';

  @override
  String reviewsReply(String body) {
    return '作者回复：$body';
  }

  @override
  String get reviewHelperBody => '30–4000 字符';

  @override
  String get reviewBodyLabel => '评价正文';

  @override
  String get reviewRatingLabel => '评分';

  @override
  String get reviewBodyRequired => '请在提交前填写评价正文。';

  @override
  String get reviewRatingRequired => '请在提交前选择评分。';

  @override
  String get menuLoad => '加载';

  @override
  String get menuNotifications => '通知';

  @override
  String get menuPlugins => '插件';

  @override
  String get menuSystemStatus => '系统状态';

  @override
  String get menuReturnHome => '返回首页';

  @override
  String get menuCancel => '取消';

  @override
  String get menuClose => '关闭';

  @override
  String get menuSend => '发送';

  @override
  String get creatorDashboardTitle => '创作者面板';

  @override
  String get creatorDashboardUnavailable => '创作者面板不可用。';

  @override
  String get creatorDashboardNoRevenue => '暂无收入。';

  @override
  String get creatorDashboardReviewSummaryUnavailable => '评价概览不可用。';

  @override
  String get creatorDashboardOrdersUnavailable => '订单列表不可用。';

  @override
  String get creatorDashboardNoOrders => '暂无订单。';

  @override
  String get creatorDashboardSignedDownloadTitle => '签名下载';

  @override
  String creatorDashboardAmount(String amount) {
    return '$amount';
  }

  @override
  String get adminOperationsTitle => '管理员操作';

  @override
  String get adminOperationsAuditUnavailable => '审计日志不可用。';

  @override
  String get adminOperationsNoAudit => '暂无审计记录。';

  @override
  String get adminOperationsUsersUnavailable => '用户列表不可用。';

  @override
  String get adminOperationsNoUsers => '未找到用户。';

  @override
  String get adminOperationsCreatorsUnavailable => '创作者列表不可用。';

  @override
  String get adminOperationsNoCreators => '暂无创作者。';

  @override
  String get adminOperationsLoadAttachments => '加载附件';

  @override
  String get adminOperationsNoAttachments => '未加载附件。';

  @override
  String get adminOperationsDownload => '下载';

  @override
  String get assistedImportTitle => '辅助导入';

  @override
  String get assistedImportSubmitText => '提交文本导入';

  @override
  String get assistedImportSubmitObject => '提交对象导入';

  @override
  String get assistedImportApprove => '通过';

  @override
  String get assistedImportReject => '拒绝';

  @override
  String get assistedImportTranslateEs => '翻译（西语）';

  @override
  String get pluginsTitle => '插件目录';

  @override
  String get pluginsInstallationsUnavailable => '安装信息不可用。';

  @override
  String get pluginsNoInstallations => '暂无安装。';

  @override
  String get pluginsRemove => '移除';

  @override
  String get pluginsCatalogUnavailable => '目录不可用。';

  @override
  String get pluginsNoPlugins => '暂无可用插件。';

  @override
  String get pluginsInstall => '安装';

  @override
  String get licensePanelTitle => '授权策略';

  @override
  String get licensePanelNone => '暂无授权策略。';

  @override
  String get licensePanelCreateDefault => '创建默认授权';

  @override
  String get tenantTitle => '我的租户';

  @override
  String tenantPlan(String plan, String status) {
    return '套餐 $plan（$status）';
  }

  @override
  String get tenantNoQuotas => '暂无配额。';

  @override
  String tenantQuotaUsage(String used, String limit) {
    return '已用 $used / 上限 $limit';
  }

  @override
  String get tenantGenerateExport => '生成导出';

  @override
  String get selfHostedTitle => '自托管状态';

  @override
  String get selfHostedRunUpgrade => '运行升级';

  @override
  String get selfHostedCaptureBackup => '创建备份';

  @override
  String get selfHostedNoFlags => '未定义功能开关。';

  @override
  String selfHostedFlagEnabled(String state) {
    return '已启用 $state';
  }

  @override
  String get releaseHistoryUnavailable => '发布历史不可用。';

  @override
  String get releaseNoReleases => '暂无发布。';

  @override
  String releaseVersion(String version) {
    return 'v$version';
  }

  @override
  String get signInShellSignedInDestination => '已登录';

  @override
  String get signInShellLoading => '加载中';

  @override
  String get validationEmailInvalid => '请输入有效的邮箱地址';

  @override
  String validationRequired(String field) {
    return '$field不能为空';
  }

  @override
  String validationMinLength(int min) {
    return '至少输入 $min 个字符';
  }
}
