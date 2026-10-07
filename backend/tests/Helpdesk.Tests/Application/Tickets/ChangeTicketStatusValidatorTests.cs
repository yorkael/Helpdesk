using FluentValidation.TestHelper;
using Helpdesk.Application.Tickets;

namespace Helpdesk.Tests.Application.Tickets;

public class ChangeTicketStatusValidatorTests
{
    private readonly ChangeTicketStatusRequestValidator _validator = new();

    [Theory]
    [InlineData("Open")]
    [InlineData("InProgress")]
    [InlineData("WaitingOnCustomer")]
    [InlineData("Resolved")]
    [InlineData("Closed")]
    public void Every_status_name_passes(string status)
    {
        var result = _validator.TestValidate(new ChangeTicketStatusRequest(status));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Status_is_required(string? status)
    {
        var result = _validator.TestValidate(new ChangeTicketStatusRequest(status!));

        result.ShouldHaveValidationErrorFor(request => request.Status);
    }

    // Only exact names: case variants, numbers and padded names would otherwise map to an enum value.
    [Theory]
    [InlineData("inprogress")]
    [InlineData("1")]
    [InlineData(" Closed")]
    [InlineData("Reopened")]
    public void Anything_but_an_exact_status_name_is_rejected(string status)
    {
        var result = _validator.TestValidate(new ChangeTicketStatusRequest(status));

        result.ShouldHaveValidationErrorFor(request => request.Status)
            .WithErrorMessage("'Status' must be one of: Open, InProgress, WaitingOnCustomer, Resolved, Closed.");
    }
}
