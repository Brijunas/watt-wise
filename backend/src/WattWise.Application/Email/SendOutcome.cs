namespace WattWise.Application.Email;

/// <summary>How a send attempt ended.</summary>
public enum SendOutcome
{
    /// <summary>The server accepted the message.</summary>
    Sent,

    /// <summary>The attempt failed in a way that may pass later (a 4xx reply, a connection, TLS or timeout failure).</summary>
    Retryable,

    /// <summary>The server refused the message for good (a 5xx reply).</summary>
    Permanent,
}
