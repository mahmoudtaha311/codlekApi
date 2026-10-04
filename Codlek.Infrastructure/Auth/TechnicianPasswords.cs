using Codlek.Application.Interfaces;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// ⚠️ تمريرة على <see cref="PasswordHasher"/> — المعاملات مشتركة
/// مع الراكة بايت ببايت، فمفيش نسخة تانية منها هنا.
/// </summary>
public sealed class TechnicianPasswords : ITechnicianPasswords
{
    public (string Hash, string Salt) Create(string password) =>
        PasswordHasher.Create(password);
}
