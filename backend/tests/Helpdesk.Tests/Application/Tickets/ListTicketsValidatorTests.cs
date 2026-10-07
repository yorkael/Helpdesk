using System.Linq.Expressions;
using FluentValidation.TestHelper;
using Helpdesk.Application.Tickets;

namespace Helpdesk.Tests.Application.Tickets;

public class ListTicketsValidatorTests
{
    private readonly ListTicketsRequestValidator _validator = new();

    [Fact]
    public void Request_without_parameters_passes()
    {
        var result = _validator.TestValidate(EmptyRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Request_with_every_parameter_passes()
    {
        var result = _validator.TestValidate(
            new ListTicketsRequest(2, 50, "InProgress", "High", Guid.NewGuid(), Guid.NewGuid(), "printer"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Page_starts_at_one(int page, bool isValid)
    {
        var result = _validator.TestValidate(EmptyRequest() with { Page = page });

        AssertValidity(result, request => request.Page, isValid);
    }

    [Fact]
    public void Page_whose_offset_does_not_fit_in_an_int_is_rejected()
    {
        var result = _validator.TestValidate(EmptyRequest() with { Page = int.MaxValue, PageSize = 100 });

        result.ShouldHaveValidationErrorFor(request => request.Page)
            .WithErrorMessage("'Page' is too large for the page size.")
            .Only();
    }

    [Fact]
    public void Page_offset_is_checked_against_the_default_page_size()
    {
        var result = _validator.TestValidate(EmptyRequest() with { Page = int.MaxValue });

        result.ShouldHaveValidationErrorFor(request => request.Page);
    }

    [Theory]
    [InlineData(21_474_837, 100, true)]
    [InlineData(21_474_838, 100, false)]
    [InlineData(int.MaxValue, 1, true)]
    public void Largest_page_is_the_one_whose_offset_still_fits_in_an_int(int page, int pageSize, bool isValid)
    {
        var result = _validator.TestValidate(EmptyRequest() with { Page = page, PageSize = pageSize });

        AssertValidity(result, request => request.Page, isValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Page_size_is_between_one_and_the_maximum(int pageSize, bool isValid)
    {
        var result = _validator.TestValidate(EmptyRequest() with { PageSize = pageSize });

        AssertValidity(result, request => request.PageSize, isValid);
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("InProgress")]
    [InlineData("WaitingOnCustomer")]
    [InlineData("Resolved")]
    [InlineData("Closed")]
    public void Every_status_name_is_accepted(string status)
    {
        var result = _validator.TestValidate(EmptyRequest() with { Status = status });

        result.ShouldNotHaveValidationErrorFor(request => request.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("open")]
    [InlineData("Unknown")]
    // Enum.Parse would accept these as a combination of flags; only the validator stops them.
    [InlineData("Open,Closed")]
    [InlineData("Open, Closed")]
    public void Status_must_be_an_exact_status_name(string status)
    {
        var result = _validator.TestValidate(EmptyRequest() with { Status = status });

        result.ShouldHaveValidationErrorFor(request => request.Status)
            .WithErrorMessage("'Status' must be one of: Open, InProgress, WaitingOnCustomer, Resolved, Closed.");
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    [InlineData("Urgent")]
    public void Every_priority_name_is_accepted(string priority)
    {
        var result = _validator.TestValidate(EmptyRequest() with { Priority = priority });

        result.ShouldNotHaveValidationErrorFor(request => request.Priority);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("high")]
    [InlineData("Critical")]
    public void Priority_must_be_an_exact_priority_name(string priority)
    {
        var result = _validator.TestValidate(EmptyRequest() with { Priority = priority });

        result.ShouldHaveValidationErrorFor(request => request.Priority)
            .WithErrorMessage("'Priority' must be one of: Low, Medium, High, Urgent.");
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Search_cannot_exceed_200_characters(int length, bool isValid)
    {
        var result = _validator.TestValidate(EmptyRequest() with { Search = new string('s', length) });

        AssertValidity(result, request => request.Search, isValid);
    }

    [Fact]
    public void Each_invalid_parameter_is_reported()
    {
        var result = _validator.TestValidate(
            new ListTicketsRequest(0, 101, "open", "high", null, null, new string('s', 201)));

        Assert.Equal(
            ["Page", "PageSize", "Priority", "Search", "Status"],
            result.Errors.Select(error => error.PropertyName).Order());
    }

    private static ListTicketsRequest EmptyRequest() => new(null, null, null, null, null, null, null);

    private static void AssertValidity<TProperty>(
        TestValidationResult<ListTicketsRequest> result,
        Expression<Func<ListTicketsRequest, TProperty>> property,
        bool isValid)
    {
        if (isValid)
        {
            result.ShouldNotHaveValidationErrorFor(property);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(property);
        }
    }
}
