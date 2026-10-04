using Codlek.Application.Interfaces;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// ⚠️ تمريرة على <see cref="PasswordHasher"/> — مفيش نسخة تانية من
/// البصمة هنا، عشان التحقق وقت التسجيل يفضل شغّال بنفس المعاملات.
/// </summary>
public sealed class ActivationCodeHasher : IActivationCodeHasher
{
    public (string Hash, string Salt) Create(string code) =>
        PasswordHasher.Create(code);
}
