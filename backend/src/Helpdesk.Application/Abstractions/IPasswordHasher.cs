namespace Helpdesk.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string passwordHash, string password);

    /// <summary>
    /// Spends the same time as <see cref="Verify"/> without a stored hash, so a login for an
    /// unknown email cannot be told apart from a wrong password by its response time.
    /// </summary>
    void SimulateVerification(string password);
}

public enum PasswordVerification
{
    Failed,
    Success,
    SuccessRehashNeeded
}
