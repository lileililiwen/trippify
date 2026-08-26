using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;

namespace Trippify.Api;

public sealed class IdentityEmailSender(Trippify.Application.IEmailSender email) : Microsoft.AspNetCore.Identity.IEmailSender<AppUser>
{
    public Task SendConfirmationLinkAsync(AppUser user, string emailAddress, string confirmationLink) => email.SendAsync(emailAddress, "Confirm your Trippify account", confirmationLink, CancellationToken.None);
    public Task SendPasswordResetLinkAsync(AppUser user, string emailAddress, string resetLink) => email.SendAsync(emailAddress, "Reset your Trippify password", resetLink, CancellationToken.None);
    public Task SendPasswordResetCodeAsync(AppUser user, string emailAddress, string resetCode) => email.SendAsync(emailAddress, "Reset your Trippify password", resetCode, CancellationToken.None);
}

public static class IdentityEndpoints
{
    public static void MapIdentity(this WebApplication app)
    {
        app.MapPost("/api/v1/auth/register", Register);
        app.MapPost("/api/v1/auth/login", Login);
        app.MapPost("/api/v1/auth/refresh", Refresh);
        app.MapGet("/api/v1/auth/confirm-email", ConfirmEmail);
        app.MapPost("/api/v1/auth/forgot-password", ForgotPassword);
        app.MapPost("/api/v1/auth/reset-password", ResetPassword);
        app.MapPost("/api/v1/auth/logout", Logout).RequireAuthorization();
        app.MapGet("/api/v1/me/profile", GetMyProfile).RequireAuthorization();
        app.MapPut("/api/v1/me/profile", UpdateMyProfile).RequireAuthorization();
        app.MapPost("/api/v1/creators/enroll", EnrollCreator).RequireAuthorization();
        app.MapGet("/api/v1/creator/workspace", GetCreatorWorkspace).RequireAuthorization();
        app.MapGet("/api/v1/creators/{slug}", GetCreator);
        app.MapPut("/api/v1/admin/users/{userId:guid}/status", SetAccountStatus).RequireAuthorization(p => p.RequireRole("Administrator"));
        app.MapPut("/api/v1/admin/creators/{userId:guid}/status", SetCreatorStatus).RequireAuthorization(p => p.RequireRole("Administrator"));
    }

    private static async Task<IResult> Register(RegisterRequest request, UserManager<AppUser> users, Microsoft.AspNetCore.Identity.IEmailSender<AppUser> email, HttpContext context)
    {
        var normalized = request.Email.Trim().ToLowerInvariant();
        if (await users.FindByEmailAsync(normalized) is not null) return Results.Ok();
        var user = new AppUser { Id = Guid.NewGuid(), UserName = normalized, Email = normalized };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return Results.ValidationProblem(result.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(y => y.Description).ToArray()));
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user)));
        var link = $"{context.Request.Scheme}://{context.Request.Host}/api/v1/auth/confirm-email?userId={user.Id}&code={code}";
        await email.SendConfirmationLinkAsync(user, normalized, link);
        return Results.Ok();
    }

    private static async Task<IResult> Login(LoginRequest request, HttpContext context, UserManager<AppUser> users, SignInManager<AppUser> signIn)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.Status != AccountStatus.Active || !user.EmailConfirmed) return Results.Problem("Failed", statusCode: StatusCodes.Status401Unauthorized);
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Results.Problem("Failed", statusCode: StatusCodes.Status401Unauthorized);
        var principal = await signIn.CreateUserPrincipalAsync(user);
        await context.SignInAsync(IdentityConstants.BearerScheme, principal);
        return Results.Empty;
    }

    private static async Task<IResult> Refresh(RefreshRequest request, HttpContext context, IOptionsMonitor<BearerTokenOptions> options, SignInManager<AppUser> signIn)
    {
        var ticket = options.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is null || ticket.Properties.ExpiresUtc <= DateTimeOffset.UtcNow) return Results.Challenge();
        var user = await signIn.ValidateSecurityStampAsync(ticket.Principal);
        if (user is null || user.Status != AccountStatus.Active) return Results.Challenge();
        await context.SignInAsync(IdentityConstants.BearerScheme, await signIn.CreateUserPrincipalAsync(user));
        return Results.Empty;
    }

    private static async Task<IResult> ConfirmEmail(Guid userId, string code, UserManager<AppUser> users)
    {
        var user = await users.FindByIdAsync(userId.ToString()); if (user is null) return Results.BadRequest();
        var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        return (await users.ConfirmEmailAsync(user, decoded)).Succeeded ? Results.NoContent() : Results.BadRequest();
    }

    private static async Task<IResult> ForgotPassword(EmailRequest request, UserManager<AppUser> users, Microsoft.AspNetCore.Identity.IEmailSender<AppUser> email)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null && user.EmailConfirmed) await email.SendPasswordResetCodeAsync(user, user.Email!, await users.GeneratePasswordResetTokenAsync(user));
        return Results.Ok();
    }

    private static async Task<IResult> ResetPassword(ResetPasswordRequest request, UserManager<AppUser> users)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return Results.BadRequest(new { detail = "Invalid reset request." });
        var result = await users.ResetPasswordAsync(user, request.Code, request.NewPassword);
        return result.Succeeded ? Results.NoContent() : Results.ValidationProblem(result.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(y => y.Description).ToArray()));
    }

    private static async Task<IResult> Logout(HttpContext context, AppDbContext db, UserManager<AppUser> users, IClock clock)
    {
        var token = BearerToken(context.Request);
        if (token is not null)
        {
            db.RevokedAccessTokens.Add(new RevokedAccessToken { TokenHash = Hash(token), ExpiresAt = clock.UtcNow.AddHours(1) });
        }
        var user = await users.FindByIdAsync(CurrentUserId(context.User).ToString());
        if (user is not null) await users.UpdateSecurityStampAsync(user);
        await db.SaveChangesAsync(context.RequestAborted);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMyProfile(ClaimsPrincipal principal, AppDbContext db)
    {
        var id = CurrentUserId(principal);
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id);
        return Results.Ok(new PrivateProfile(user.Email!, profile?.DisplayName ?? string.Empty, profile?.AvatarUrl, profile?.Locale, user.EmailConfirmed, user.Status.ToString()));
    }

    private static async Task<IResult> UpdateMyProfile(ProfileRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var displayName = request.DisplayName.Trim();
        if (displayName.Length is < 1 or > 80) return Results.ValidationProblem(new Dictionary<string, string[]> { ["displayName"] = ["Display name must contain 1 to 80 characters."] });
        var id = CurrentUserId(principal);
        var profile = await db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == id);
        if (profile is null) db.UserProfiles.Add(new UserProfile { UserId = id, DisplayName = displayName, AvatarUrl = request.AvatarUrl, Locale = request.Locale });
        else { profile.DisplayName = displayName; profile.AvatarUrl = request.AvatarUrl; profile.Locale = request.Locale; }
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> EnrollCreator(CreatorRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (slug.Length is < 3 or > 80 || slug.Any(x => !char.IsAsciiLetterOrDigit(x) && x != '-')) return Results.ValidationProblem(new Dictionary<string, string[]> { ["slug"] = ["Use 3 to 80 lowercase letters, numbers, or hyphens."] });
        var id = CurrentUserId(principal);
        if (await db.CreatorProfiles.AnyAsync(x => x.UserId == id)) return Results.Conflict(new { detail = "Creator enrollment already exists." });
        if (await db.CreatorProfiles.AnyAsync(x => x.Slug == slug)) return Results.Conflict(new { detail = "Creator slug is unavailable." });
        db.CreatorProfiles.Add(new CreatorProfile { UserId = id, Slug = slug, Biography = request.Biography.Trim(), TravelCountries = request.TravelCountries.Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToArray() });
        await db.SaveChangesAsync();
        return Results.Accepted($"/api/v1/creators/{slug}");
    }

    private static async Task<IResult> GetCreatorWorkspace(ClaimsPrincipal principal, AppDbContext db)
    {
        var id = CurrentUserId(principal);
        return await db.CreatorProfiles.AnyAsync(x => x.UserId == id && x.Status == CreatorStatus.Active) ? Results.NoContent() : Results.Forbid();
    }

    private static async Task<IResult> GetCreator(string slug, AppDbContext db)
    {
        var creator = await db.CreatorProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug.ToLower() && x.Status == CreatorStatus.Active);
        if (creator is null) return Results.NotFound();
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == creator.UserId);
        return Results.Ok(new PublicCreator(creator.Slug, profile?.DisplayName ?? string.Empty, profile?.AvatarUrl, creator.Biography, creator.TravelCountries));
    }

    private static async Task<IResult> SetAccountStatus(Guid userId, StatusRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (!Enum.TryParse<AccountStatus>(request.Status, true, out var status) || string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Valid status and reason are required."] });
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId); if (user is null) return Results.NotFound();
        user.Status = status; db.IdentityAuditEntries.Add(Audit(CurrentUserId(principal), userId, "account-status:" + status, request.Reason)); await db.SaveChangesAsync(); return Results.NoContent();
    }

    private static async Task<IResult> SetCreatorStatus(Guid userId, StatusRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (!Enum.TryParse<CreatorStatus>(request.Status, true, out var status) || string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Valid status and reason are required."] });
        var creator = await db.CreatorProfiles.SingleOrDefaultAsync(x => x.UserId == userId); if (creator is null) return Results.NotFound();
        creator.Status = status; db.IdentityAuditEntries.Add(Audit(CurrentUserId(principal), userId, "creator-status:" + status, request.Reason)); await db.SaveChangesAsync(); return Results.NoContent();
    }

    private static IdentityAuditEntry Audit(Guid actor, Guid target, string action, string reason) => new() { Id = Guid.NewGuid(), ActorUserId = actor, TargetUserId = target, Action = action, Reason = reason.Trim(), OccurredAt = DateTimeOffset.UtcNow };
    internal static Guid CurrentUserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    internal static string? BearerToken(HttpRequest request) { var value = request.Headers.Authorization.ToString(); return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..] : null; }
    internal static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class ActiveSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var id = IdentityEndpoints.CurrentUserId(context.User);
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, context.RequestAborted);
            if (user is null || user.Status != AccountStatus.Active) { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            var token = IdentityEndpoints.BearerToken(context.Request);
            if (token is not null && await db.RevokedAccessTokens.AnyAsync(x => x.TokenHash == IdentityEndpoints.Hash(token), context.RequestAborted)) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        }
        await next(context);
    }
}

public sealed record ProfileRequest(string DisplayName, string? AvatarUrl, string? Locale);
public sealed record CreatorRequest(string Slug, string Biography, string[] TravelCountries);
public sealed record StatusRequest(string Status, string Reason);
public sealed record PrivateProfile(string Email, string DisplayName, string? AvatarUrl, string? Locale, bool EmailConfirmed, string Status);
public sealed record PublicCreator(string Slug, string DisplayName, string? AvatarUrl, string Biography, string[] TravelCountries);
public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record EmailRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
