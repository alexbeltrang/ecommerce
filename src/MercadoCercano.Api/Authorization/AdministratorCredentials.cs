using System.Net.Mail;

namespace MercadoCercano.Api.Authorization;

public static class AdministratorCredentials
{
    public static bool IsValid(string? name, string? email, string? password) =>
        IsValidProfile(name, email) && IsValidPassword(password);

    public static bool IsValidProfile(string? name, string? email) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 160 &&
        !string.IsNullOrWhiteSpace(email) && email.Trim().Length <= 254 &&
        MailAddress.TryCreate(email.Trim(), out var parsedEmail) &&
        string.Equals(parsedEmail.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool IsValidPassword(string? password) =>
        password is { Length: >= 8 and <= 32 } &&
        password.Any(char.IsUpper) && password.Any(char.IsLower) &&
        password.Any(char.IsDigit) && password.Any(character => !char.IsLetterOrDigit(character)) &&
        !password.Any(char.IsWhiteSpace);
}
