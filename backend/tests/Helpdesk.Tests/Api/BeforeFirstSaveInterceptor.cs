using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Runs a callback once, just before the first save reaches the database. A test uses it to let another request
/// change the same row between this request's read and its write, which makes a race deterministic.
/// Later saves, including the ones the callback causes, pass straight through.
/// </summary>
internal sealed class BeforeFirstSaveInterceptor(Func<Task> beforeFirstSave) : SaveChangesInterceptor
{
    private int _fired;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        // Set before awaiting, so the save the callback triggers does not run it again.
        if (Interlocked.Exchange(ref _fired, 1) == 0)
        {
            await beforeFirstSave();
        }

        return result;
    }
}
