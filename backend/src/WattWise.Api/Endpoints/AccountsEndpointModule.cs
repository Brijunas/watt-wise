using System.Security.Claims;

using Mediator;

using Microsoft.AspNetCore.Http.HttpResults;

using WattWise.Api.Cors;
using WattWise.Api.ErrorHandling;
using WattWise.Application.Accounts;

namespace WattWise.Api.Endpoints;

/// <summary>The <c>/api/v1/auth</c> route group: sign up, email confirmation, sign-in, refresh, sign-out, password reset and password change. All anonymous except the change; the credentialed CORS policy lets the refresh cookie travel.</summary>
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
        SignInRequest? request, HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<SignedIn> result = await sender.Send(
            new SignInCommand(request?.Email, request?.Password, http.Request.Headers.UserAgent.ToString(), RefreshTokenCookie.Read(http.Request)), cancellationToken);
        return result.ToHttpResult(signedIn => RespondSignedIn(http, signedIn));
    }

    private static async Task<Results<Ok<SignedInResponse>, ProblemHttpResult, ValidationProblem>> Refresh(
        HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<SignedIn> result = await sender.Send(
            new RefreshSessionCommand(RefreshTokenCookie.Read(http.Request)), cancellationToken);
        if (result.IsFailure)
        {
            // A cookie that can't refresh is of no use to the browser either.
            RefreshTokenCookie.Delete(http.Response);
        }

        return result.ToHttpResult(signedIn => RespondSignedIn(http, signedIn));
    }

    private static async Task<NoContent> SignOut(HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        _ = await sender.Send(new SignOutCommand(RefreshTokenCookie.Read(http.Request)), cancellationToken);
        RefreshTokenCookie.Delete(http.Response);
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
        ChangePasswordRequest? request, ClaimsPrincipal user, HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        // The claims keep their token names (MapInboundClaims is off), so the account id is "sub".
        if (!Guid.TryParse(user.FindFirstValue("sub"), out Guid accountId))
        {
            // Defensive only: bearer validation guarantees a GUID "sub" on any token this Api issued.
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }

        Application.Results.Result<SignedIn> result = await sender.Send(
            new ChangePasswordCommand(accountId, request?.CurrentPassword, request?.NewPassword, http.Request.Headers.UserAgent.ToString()),
            cancellationToken);
        return result.ToHttpResult(signedIn => RespondSignedIn(http, signedIn));
    }

    private static Ok<SignedInResponse> RespondSignedIn(HttpContext http, SignedIn signedIn)
    {
        RefreshTokenCookie.Write(http.Response, signedIn.RefreshToken, signedIn.RefreshTokenExpiresAt);
        return TypedResults.Ok(new SignedInResponse(signedIn.AccessToken, signedIn.AccessTokenExpiresAt.ToDateTimeOffset()));
    }
}
