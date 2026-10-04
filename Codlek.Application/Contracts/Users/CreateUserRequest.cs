namespace Codlek.Application.Contracts.Users;

/// <summary>
/// إنشاء حساب لوحة تحكم.
///
/// <para>⚠️ <c>Role</c> رقم <c>UserRole</c> — وبيتفحص بـ
/// <c>Enum.IsDefined</c> قبل الكاست. الكاست الأعمى شرعي في C#،
/// وبيكتب حساب بدور مالوش اسم ولا حاجز بيقبله.</para>
/// </summary>
public sealed record CreateUserRequest(
    string? Username,
    string? DisplayName,
    string? Password,
    int Role);
