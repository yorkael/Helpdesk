using System.Reflection;
using System.Runtime.CompilerServices;
using Helpdesk.Tests.Api;
using Helpdesk.Tests.Persistence;

namespace Helpdesk.Tests.Application;

/// <summary>
/// The test project references the API, so nothing at compile time stops a unit test from using the database.
/// This keeps the Application and Domain tests runnable without Docker by rejecting the ways tests here get one.
/// </summary>
public class ApplicationTestIsolationTests
{
    private static readonly string[] UnitTestNamespaces = ["Helpdesk.Tests.Application", "Helpdesk.Tests.Domain"];

    private static readonly Type[] DatabaseFixtures = [typeof(PostgreSqlFixture), typeof(HelpdeskApiFactory)];

    [Fact]
    public void Unit_tests_never_use_a_database_fixture()
    {
        var violations = typeof(ApplicationTestIsolationTests).Assembly.GetTypes()
            .Where(type => type.IsClass && IsInUnitTestNamespace(type) && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .SelectMany(Violations)
            .ToList();

        // Assert.Empty would truncate long type names; each violation gets its own full line instead.
        Assert.True(
            violations.Count == 0,
            "Unit tests must not use the database:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static bool IsInUnitTestNamespace(Type type) =>
        UnitTestNamespaces.Any(name => type.Namespace == name || type.Namespace?.StartsWith(name + ".") == true);

    private static IEnumerable<string> Violations(Type type)
    {
        if (type.IsDefined(typeof(CollectionAttribute), inherit: true))
        {
            yield return $"{type.FullName} has [Collection]";
        }

        foreach (var fixture in type.GetInterfaces().Where(IsFixtureInterface))
        {
            var name = fixture.Name[..fixture.Name.IndexOf('`')];
            yield return $"{type.FullName} implements {name}<{fixture.GenericTypeArguments[0].Name}>";
        }

        var databaseParameters = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => DatabaseFixtures.Any(fixture => fixture.IsAssignableFrom(parameter.ParameterType)));

        foreach (var parameter in databaseParameters)
        {
            yield return $"{type.FullName} receives {parameter.ParameterType.Name} in its constructor";
        }
    }

    private static bool IsFixtureInterface(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(IClassFixture<>)
            || type.GetGenericTypeDefinition() == typeof(ICollectionFixture<>));
}
