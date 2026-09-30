using Microsoft.AspNetCore.Identity;
using ParkFlow.Application.Interfaces;

public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public string HashPassword(string password)
    {
        return _hasher.HashPassword(null!, password);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            return false;

        if (hashedPassword == providedPassword)
            return true;

        try
        {
            var result = _hasher.VerifyHashedPassword(null!, hashedPassword, providedPassword);
            return result != PasswordVerificationResult.Failed;
        }
        catch
        {
            return hashedPassword == providedPassword;
        }
    }
}