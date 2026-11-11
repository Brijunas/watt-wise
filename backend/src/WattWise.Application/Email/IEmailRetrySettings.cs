namespace WattWise.Application.Email;

/// <summary>The retry schedules the worker follows, one per kind. Infrastructure reads them from <c>Email:Retry</c>.</summary>
public interface IEmailRetrySettings
{
    /// <summary>The schedule of one kind.</summary>
    EmailRetrySchedule For(EmailRequestKind kind);
}
