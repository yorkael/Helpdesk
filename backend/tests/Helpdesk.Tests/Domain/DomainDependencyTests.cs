using Helpdesk.Domain.Entities;

namespace Helpdesk.Tests.Domain;

public class DomainDependencyTests
{
    [Fact]
    public void Domain_references_only_base_class_library_assemblies()
    {
        var referencedAssemblies = typeof(Ticket).Assembly.GetReferencedAssemblies();

        var externalReferences = referencedAssemblies
            .Select(assembly => assembly.Name!)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(externalReferences);
    }
}
