using System.Linq.Expressions;
using FluentValidation.TestHelper;
using Helpdesk.Application.Tickets;

namespace Helpdesk.Tests.Application.Tickets;

public class CreateTicketValidatorTests
{
    private static readonly Guid ExistingCategoryId = Guid.NewGuid();

    private readonly InMemoryCategoryRepository _categories = new(ExistingCategoryId);
    private readonly CreateTicketRequestValidator _validator;

    public CreateTicketValidatorTests()
    {
        _validator = new CreateTicketRequestValidator(_categories);
    }

    [Fact]
    public async Task Valid_request_passes()
    {
        var result = await _validator.TestValidateAsync(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Title_is_required(string title)
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Title = title });

        result.ShouldHaveValidationErrorFor(request => request.Title);
    }

    [Fact]
    public async Task Missing_title_is_rejected()
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Title = null! });

        result.ShouldHaveValidationErrorFor(request => request.Title).Only();
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public async Task Title_cannot_exceed_the_column_size(int length, bool isValid)
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Title = new string('t', length) });

        AssertValidity(result, request => request.Title, isValid);
    }

    [Fact]
    public async Task Title_length_is_measured_before_trimming()
    {
        var title = " " + new string('t', 199) + " ";

        var result = await _validator.TestValidateAsync(ValidRequest() with { Title = title });

        result.ShouldHaveValidationErrorFor(request => request.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Description_is_required(string description)
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Description = description });

        result.ShouldHaveValidationErrorFor(request => request.Description);
    }

    [Fact]
    public async Task Missing_description_is_rejected()
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Description = null! });

        result.ShouldHaveValidationErrorFor(request => request.Description).Only();
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public async Task Description_cannot_exceed_4000_characters(int length, bool isValid)
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Description = new string('d', length) });

        AssertValidity(result, request => request.Description, isValid);
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    [InlineData("Urgent")]
    public async Task Every_priority_name_is_accepted(string priority)
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Priority = priority });

        result.ShouldNotHaveValidationErrorFor(request => request.Priority);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("high")]
    [InlineData("Critical")]
    public async Task Priority_must_be_an_exact_priority_name(string priority)
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Priority = priority });

        result.ShouldHaveValidationErrorFor(request => request.Priority);
    }

    [Fact]
    public async Task Missing_priority_is_reported_once()
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Priority = null! });

        Assert.Single(result.ShouldHaveValidationErrorFor(request => request.Priority));
    }

    [Fact]
    public async Task Invalid_priority_message_lists_the_allowed_values()
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { Priority = "1" });

        result.ShouldHaveValidationErrorFor(request => request.Priority)
            .WithErrorMessage("'Priority' must be one of: Low, Medium, High, Urgent.");
    }

    [Fact]
    public async Task Unknown_category_is_rejected()
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { CategoryId = Guid.NewGuid() });

        result.ShouldHaveValidationErrorFor(request => request.CategoryId)
            .WithErrorMessage("The category does not exist.");
    }

    [Fact]
    public async Task Missing_category_is_rejected_without_a_lookup()
    {
        var result = await _validator.TestValidateAsync(ValidRequest() with { CategoryId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(request => request.CategoryId);
        Assert.Equal(0, _categories.LookupCount);
    }

    private static CreateTicketRequest ValidRequest() =>
        new("Printer offline", "It shows error 42.", ExistingCategoryId, "High");

    private static void AssertValidity<TProperty>(
        TestValidationResult<CreateTicketRequest> result,
        Expression<Func<CreateTicketRequest, TProperty>> property,
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
