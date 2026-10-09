using Helpdesk.Domain.Entities;

namespace Helpdesk.Tests.Domain;

public class CategoryTests
{
    [Fact]
    public void Name_is_trimmed()
    {
        var category = new Category("  Hardware ");

        Assert.Equal("Hardware", category.Name);
        Assert.NotEqual(Guid.Empty, category.Id);

        // HU-12 negative check: fails on purpose to prove that the backend check blocks the merge.
        Assert.True(false);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_is_rejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new Category(name));
    }
}
