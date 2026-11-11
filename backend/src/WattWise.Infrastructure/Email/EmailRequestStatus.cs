namespace WattWise.Infrastructure.Email;

/// <summary>Where an outbox row is in its life; stored as lowercase text.</summary>
public enum EmailRequestStatus
{
    Pending,
    Sending,
    Sent,
    Superseded,
    Failed,
}
