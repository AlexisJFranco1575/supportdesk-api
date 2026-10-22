using SupportDesk.Application.Common;
using SupportDesk.Domain.Users;

namespace SupportDesk.Application.Auth;

internal static class AuthValidator
{
    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 100;

    public static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    public static void ValidateRegistration(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            errors["email"] = ["The email is required."];
        }
        else if (!User.IsValidEmail(email))
        {
            errors["email"] = ["The email is not a valid address."];
        }

        var fullName = request.FullName?.Trim();
        if (string.IsNullOrEmpty(fullName))
        {
            errors["fullName"] = ["The full name is required."];
        }
        else if (fullName.Length > User.FullNameMaxLength)
        {
            errors["fullName"] = [$"The full name cannot exceed {User.FullNameMaxLength} characters."];
        }

        var password = request.Password;
        if (string.IsNullOrEmpty(password))
        {
            errors["password"] = ["The password is required."];
        }
        else if (password.Length < PasswordMinLength || password.Length > PasswordMaxLength)
        {
            errors["password"] = [$"The password must be between {PasswordMinLength} and {PasswordMaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }
}