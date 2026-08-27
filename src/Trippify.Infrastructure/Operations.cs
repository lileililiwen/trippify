namespace Trippify.Infrastructure;

public sealed record RevenueByCurrency(string CurrencyCode, long GrossMinorUnits, long CommissionMinorUnits, long NetMinorUnits, int PaidOrderCount, int RefundedOrderCount);

public sealed record CreatorDashboardOverview(int GuideCount, int ActiveGuideCount, int PaidOrderCount, int RefundedOrderCount, IReadOnlyList<RevenueByCurrency> Revenue, DateTimeOffset GeneratedAt);

public sealed record CreatorOrderRow(Guid OrderId, Guid GuideId, string GuideTitle, long AmountMinorUnits, string CurrencyCode, string Status, DateTimeOffset CreatedAt);

public sealed record CreatorOrdersResponse(int Total, IReadOnlyList<CreatorOrderRow> Items);

public sealed record CreatorReviewSummaryRow(int VisibleCount, int FlaggedCount, int HiddenCount, int ReportsOpen);

public sealed record AdminAuditEntryRow(Guid Id, Guid ActorUserId, Guid TargetUserId, string Action, string Reason, DateTimeOffset OccurredAt);

public sealed record AdminAuditResponse(int Total, IReadOnlyList<AdminAuditEntryRow> Items);

public sealed record AdminUserRow(Guid UserId, string Email, string Status, bool EmailConfirmed, DateTimeOffset CreatedAt);

public sealed record AdminUsersResponse(int Total, IReadOnlyList<AdminUserRow> Items);

public sealed record AdminCreatorRow(Guid UserId, string Slug, string Status, DateTimeOffset CreatedAt);

public sealed record AdminCreatorsResponse(int Total, IReadOnlyList<AdminCreatorRow> Items);
