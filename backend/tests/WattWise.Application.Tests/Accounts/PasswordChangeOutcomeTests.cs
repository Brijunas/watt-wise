using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class PasswordChangeOutcomeTests
{
    [Fact]
    public void A_changed_outcome_carries_its_stamp_and_is_changed()
    {
        PasswordChangeOutcome outcome = PasswordChangeOutcome.Changed("stamp-1");

        Assert.Equal((PasswordChangeKind.Changed, "stamp-1", true), (outcome.Kind, outcome.SecurityStamp, outcome.IsChanged));
    }

    [Fact]
    public void The_failure_outcomes_have_their_kind_no_stamp_and_are_not_changed()
    {
        PasswordChangeOutcome[] outcomes =
            [PasswordChangeOutcome.WrongPassword(), PasswordChangeOutcome.NotFound(), PasswordChangeOutcome.Conflict()];

        Assert.Equal(
            "WrongPassword:,False NotFound:,False Conflict:,False",
            string.Join(" ", outcomes.Select(outcome => $"{outcome.Kind}:{outcome.SecurityStamp},{outcome.IsChanged}")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_changed_outcome_rejects_a_missing_or_blank_stamp(string? stamp)
    {
        Assert.ThrowsAny<ArgumentException>(() => PasswordChangeOutcome.Changed(stamp!));
    }

    [Fact]
    public void The_text_of_a_changed_outcome_hides_the_stamp()
    {
        Assert.Equal("PasswordChangeOutcome Changed", PasswordChangeOutcome.Changed("secret-stamp").ToString());
    }
}
