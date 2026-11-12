using System.Security.Claims;

using Mediator;

using Microsoft.AspNetCore.Http.HttpResults;

using WattWise.Api.Cors;
using WattWise.Api.ErrorHandling;
using WattWise.Application.Accounts;

namespace WattWise.Api.Endpoints;

/// <summary>The <c>/api/v1/auth</c> route group: sign up, email confirmation, sign-in, refresh, sign-out, password reset, password change and the signed-in account's sessions (list, end one, sign out everywhere). All anonymous except the change and the sessions; the credentialed CORS policy lets the refresh cookie travel.</summary>
public sealed class AccountsEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        RouteGroupBuilder auth = group.MapGroup("/auth")
            .WithTags("Accounts")
            .RequireCors(AuthCorsPolicy.Name);

        auth.MapPost("/register", Register)
            .WithName("Register")
            .AllowAnonymous();
        auth.MapPost("/resend-confirmation", ResendConfirmation)
            .WithName("ResendConfirmation")
            .AllowAnonymous();
        auth.MapPost("/confirm-email", ConfirmEmail)
            .WithName("ConfirmEmail")
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        auth.MapPost("/sign-in", SignIn)
            .WithName("SignIn")
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status423Locked);
        auth.MapPost("/refresh", Refresh)
            .WithName("RefreshSession")
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        auth.MapPost("/sign-out", SignOut)
            .WithName("SignOut")
            .AllowAnonymous();
        auth.MapPost("/forgot-password", ForgotPassword)
            .WithName("ForgotPassword")
            .AllowAnonymous();
        auth.MapPost("/reset-password", ResetPassword)
            .WithName("ResetPassword")
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // Signed in, unlike the rest of the group: no AllowAnonymous, so the /api/v1 group's default and RequireAuthorization apply.
        // It stays here for the credentialed CORS policy and the refresh cookie's path.
        auth.MapPost("/change-password", ChangePassword)
            .WithName("ChangePassword")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status423Locked);
        auth.MapGet("/sessions", ListSessions)
            .WithName("ListSessions")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        auth.MapDelete("/sessions/{id:guid}", EndSession)
            .WithName("EndSession")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        auth.MapPost("/sign-out-everywhere", SignOutEverywhere)
            .WithName("SignOutEverywhere")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    // The bodies are optional so that a missing one is a validation error like an empty one.
    private static async Task<Results<Accepted, ProblemHttpResult, ValidationProblem>> Register(
        RegisterRequest? request, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<Unit> result = await sender.Send(
            new RegisterAccountCommand(request?.Email, request?.Password, request?.Language), cancellationToken);
        return result.ToHttpResult(_ => TypedResults.Accepted((string?)null));
    }

    private static async Task<Results<Accepted, ProblemHttpResult, ValidationProblem>> ResendConfirmation(
        ResendConfirmationRequest? request, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<Unit> result = await sender.Send(new ResendConfirmationCommand(request?.Email), cancellationToken);
        return result.ToHttpResult(_ => TypedResults.Accepted((string?)null));
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> ConfirmEmail(
        ConfirmEmailRequest? request, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<Unit> result = await sender.Send(new ConfirmEmailCommand(request?.Id, request?.Token), cancellationToken);
        return result.ToHttpResult(_ => TypedResults.NoContent());
    }

    private static async Task<Results<Ok<SignedInResponse>, ProblemHttpResult, ValidationProblem>> SignIn(
        SignInRequest? request, HttpContext http, RefreshTokenCookie cookie, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<SignedIn> result = await sender.Send(
            new SignInCommand(request?.Email, request?.Password, http.Request.Headers.UserAgent.ToString(), cookie.Read(http.Request)), cancellationToken);
        return result.ToHttpResult(signedIn => RespondSignedIn(http, cookie, signedIn));
    }

    private static async Task<Results<Ok<SignedInResponse>, ProblemHttpResult, ValidationProblem>> Refresh(
        HttpContext http, RefreshTokenCookie cookie, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<SignedIn> result = await sender.Send(
            new RefreshSessionCommand(cookie.Read(http.Request)), cancellationToken);
        if (result.IsFailure)
        {
            // A cookie that can't refresh is of no use to the browser either.
            cookie.Delete(http);
        }

        return result.ToHttpResult(signedIn => RespondSignedIn(http, cookie, signedIn));
    }

    private static async Task<NoContent> SignOut(HttpContext http, RefreshTokenCookie cookie, ISender sender, CancellationToken cancellationToken)
    {
        _ = await sender.Send(new SignOutCommand(cookie.Read(http.Request)), cancellationToken);
        cookie.Delete(http);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Accepted, ProblemHttpResult, ValidationProblem>> ForgotPassword(
        ForgotPasswordRequest? request, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<Unit> result = await sender.Send(new ForgotPasswordCommand(request?.Email), cancellationToken);
        return result.ToHttpResult(_ => TypedResults.Accepted((string?)null));
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> ResetPassword(
        ResetPasswordRequest? request, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<Unit> result = await sender.Send(
            new ResetPasswordCommand(request?.Id, request?.Token, request?.NewPassword), cancellationToken);
        return result.ToHttpResult(_ => TypedResults.NoContent());
    }

    private static async Task<Results<Ok<SignedInResponse>, ProblemHttpResult, ValidationProblem>> ChangePassword(
        ChangePasswordRequest? request, ClaimsPrincipal user, HttpContext http, RefreshTokenCookie cookie, ISender sender, CancellationToken cancellationToken)
    {
        if (!AccessTokenClaims.TryGetAccountId(user, out Guid accountId))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }

        Application.Results.Result<SignedIn> result = await sender.Send(
            new ChangePasswordCommand(accountId, request?.CurrentPassword, request?.NewPassword, http.Request.Headers.UserAgent.ToString()),
            cancellationToken);
        return result.ToHttpResult(signedIn => RespondSignedIn(http, cookie, signedIn));
    }

    private static async Task<Results<Ok<IReadOnlyList<SessionResponse>>, ProblemHttpResult, ValidationProblem>> ListSessions(
        ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!AccessTokenClaims.TryGetAccountId(user, out Guid accountId))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }

        Application.Results.Result<IReadOnlyList<SessionSummary>> result = await sender.Send(
            new ListSessionsQuery(accountId, AccessTokenClaims.GetSessionId(user)), cancellationToken);
        return result.ToHttpResult(sessions => TypedResults.Ok<IReadOnlyList<SessionResponse>>(
            [.. sessions.Select(session => new SessionResponse(
                session.Id, session.DeviceLabel, session.SignedInAt.ToDateTimeOffset(), session.LastUsedAt.ToDateTimeOffset(), session.IsCurrent))]));
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> EndSession(
        Guid id, ClaimsPrincipal user, HttpContext http, RefreshTokenCookie cookie, ISender sender, CancellationToken cancellationToken)
    {
        if (!AccessTokenClaims.TryGetAccountId(user, out Guid accountId))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }

        Application.Results.Result<Unit> result = await sender.Send(new EndSessionCommand(accountId, id), cancellationToken);
        if (result.IsSuccess && AccessTokenClaims.GetSessionId(user) == id)
        {
            // Ending the current session is a sign-out: the cookie is of no use any more.
            cookie.Delete(http);
        }

        return result.ToHttpResult(_ => TypedResults.NoContent());
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> SignOutEverywhere(
        ClaimsPrincipal user, HttpContext http, RefreshTokenCookie cookie, ISender sender, CancellationToken cancellationToken)
    {
        if (!AccessTokenClaims.TryGetAccountId(user, out Guid accountId))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }

        Application.Results.Result<Unit> result = await sender.Send(new SignOutEverywhereCommand(accountId), cancellationToken);
        cookie.Delete(http);
        return result.ToHttpResult(_ => TypedResults.NoContent());
    }

    private static Ok<SignedInResponse> RespondSignedIn(HttpContext http, RefreshTokenCookie cookie, SignedIn signedIn)
    {
        cookie.Write(http, signedIn.RefreshToken, signedIn.RefreshTokenExpiresAt);
        return TypedResults.Ok(new SignedInResponse(signedIn.AccessToken, signedIn.AccessTokenExpiresAt.ToDateTimeOffset()));
    }
}
