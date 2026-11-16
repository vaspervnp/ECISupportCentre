using EciSupportCentre.Core.Models;

namespace EciSupportCentre.Core.Tests;

public class EciStatusTests
{
    [Theory]
    [InlineData("REGISTERED", EciStatus.Registered, InitiativePhase.Upcoming, InitiativeOutcome.Pending)]
    [InlineData("ONGOING", EciStatus.Ongoing, InitiativePhase.Collecting, InitiativeOutcome.Pending)]
    [InlineData("CLOSED", EciStatus.Closed, InitiativePhase.Verification, InitiativeOutcome.Pending)]
    [InlineData("VERIFICATION", EciStatus.Verification, InitiativePhase.Verification, InitiativeOutcome.Pending)]
    [InlineData("SUBMITTED", EciStatus.Submitted, InitiativePhase.Submitted, InitiativeOutcome.Successful)]
    [InlineData("ANSWERED", EciStatus.Answered, InitiativePhase.Answered, InitiativeOutcome.Successful)]
    [InlineData("INSUFFICIENT_SUPPORT", EciStatus.InsufficientSupport, InitiativePhase.Failed, InitiativeOutcome.Failed)]
    [InlineData("INSUFFICIENT_SUPPORT_AFTER_VERIFICATION", EciStatus.InsufficientSupportAfterVerification, InitiativePhase.Failed, InitiativeOutcome.Failed)]
    [InlineData("WITHDRAWN", EciStatus.Withdrawn, InitiativePhase.Withdrawn, InitiativeOutcome.Withdrawn)]
    [InlineData(" ongoing ", EciStatus.Ongoing, InitiativePhase.Collecting, InitiativeOutcome.Pending)]
    [InlineData("SOMETHING_NEW", EciStatus.Unknown, InitiativePhase.Unknown, InitiativeOutcome.Pending)]
    [InlineData(null, EciStatus.Unknown, InitiativePhase.Unknown, InitiativeOutcome.Pending)]
    public void Status_codes_map_to_phase_and_outcome(string? code, EciStatus status, InitiativePhase phase, InitiativeOutcome outcome)
    {
        var parsed = EciStatusExtensions.ParseStatus(code);

        Assert.Equal(status, parsed);
        Assert.Equal(phase, parsed.ToPhase());
        Assert.Equal(outcome, parsed.ToOutcome());
    }

    [Fact]
    public void Only_ongoing_initiatives_can_be_signed()
    {
        var signable = Enum.GetValues<EciStatus>().Where(s => s.CanBeSigned());
        Assert.Equal([EciStatus.Ongoing], signable);
    }
}
