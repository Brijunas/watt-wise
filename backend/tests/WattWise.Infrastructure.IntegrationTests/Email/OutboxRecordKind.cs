namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>Which of the outbox's three Record* methods a test calls.</summary>
public enum OutboxRecordKind
{
    Sent,
    Retry,
    Failed,
}
