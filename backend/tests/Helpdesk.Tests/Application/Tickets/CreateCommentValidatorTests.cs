using FluentValidation.TestHelper;
using Helpdesk.Application.Tickets;

namespace Helpdesk.Tests.Application.Tickets;

public class CreateCommentValidatorTests
{
    private readonly CreateCommentRequestValidator _validator = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Content_with_an_explicit_visibility_passes(bool isInternal)
    {
        var result = _validator.TestValidate(new CreateCommentRequest("Any update?", isInternal));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Content_is_required(string? content)
    {
        var result = _validator.TestValidate(new CreateCommentRequest(content!, false));

        result.ShouldHaveValidationErrorFor(request => request.Content);
    }

    [Fact]
    public void Content_of_4000_characters_passes()
    {
        var result = _validator.TestValidate(new CreateCommentRequest(new string('a', 4000), false));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Content_longer_than_4000_characters_is_rejected()
    {
        var result = _validator.TestValidate(new CreateCommentRequest(new string('a', 4001), false));

        result.ShouldHaveValidationErrorFor(request => request.Content);
    }

    [Fact]
    public void Missing_visibility_is_rejected_instead_of_defaulting_to_public()
    {
        var result = _validator.TestValidate(new CreateCommentRequest("Any update?", null));

        result.ShouldHaveValidationErrorFor(request => request.IsInternal);
    }
}
