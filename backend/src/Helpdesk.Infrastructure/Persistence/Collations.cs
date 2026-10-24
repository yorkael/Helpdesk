namespace Helpdesk.Infrastructure.Persistence;

internal static class Collations
{
    // ICU's root collation sorts the same on every server; the database default follows the server's OS locale.
    public const string NameOrder = "und-x-icu";
}
