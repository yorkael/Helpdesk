namespace Helpdesk.Domain.Entities;

public class Category
{
    public Category(string name)
    {
        Id = Guid.CreateVersion7();
        Name = Guard.NotBlank(name, nameof(name));
    }

    private Category()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;
}
