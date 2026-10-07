using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Tests.Application;

internal static class TestUsers
{
    // User has no deactivation method yet (admin user management is issue #19), so tests set the flag directly.
    public static void Deactivate(User user) =>
        typeof(User).GetProperty(nameof(User.IsActive))!.SetValue(user, false);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Exception? ExceptionOnNextSave { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ExceptionOnNextSave is { } exception)
        {
            ExceptionOnNextSave = null;
            throw exception;
        }

        SaveCount++;
        return Task.CompletedTask;
    }
}
