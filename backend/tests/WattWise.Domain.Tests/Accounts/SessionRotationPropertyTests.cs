using CsCheck;

using NodaTime;

using WattWise.Domain.Accounts;

namespace WattWise.Domain.Tests.Accounts;

public class SessionRotationPropertyTests
{
    [Fact]
    public void After_any_number_of_rotations_exactly_the_newest_token_is_unused_and_the_last_use_is_the_last_rotation()
    {
        Instant start = Instant.FromUtc(2026, 11, 11, 12, 0);

        Gen.Int[1, 30].Sample(rotations =>
        {
            byte[] hash = [1];
            Session session = Session.Start(Guid.CreateVersion7(), "Firefox on Linux", "stamp", hash, start, start + Duration.FromDays(7));
            Instant now = start;
            for (int step = 1; step <= rotations; step++)
            {
                now += Duration.FromMinutes(10);
                byte[] next = [(byte)(step + 1)];
                session.Rotate(hash, next, now, now + Duration.FromDays(7));
                hash = next;
            }

            RefreshToken[] unused = [.. session.RefreshTokens.Where(token => token.UsedAt is null)];
            return unused.Length == 1 && unused[0].TokenHash.SequenceEqual(hash) && session.RefreshTokens.Count == rotations + 1 && session.LastUsedAt == now;
        });
    }
}
