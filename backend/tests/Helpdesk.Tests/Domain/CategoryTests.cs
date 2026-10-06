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
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_is_rejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new Category(name));
    }
}
