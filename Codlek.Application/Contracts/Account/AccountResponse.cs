namespace Codlek.Application.Contracts.Account;

/// <summary>
/// بيانات الحساب الحالي.
///
/// <para>🔴 <b>أسماء الحقول دي عقد.</b> صفحة «حسابي» في الداش بورد
/// بتقراها بالأسماء دي بالظبط.</para>
/// </summary>
public sealed record AccountResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string Code,
    string Role,

    /// <summary>
    /// الدور بالعربي — <b>ده اللي بيتعرض في الشاشة</b>.
    ///
    /// <para>⚠️ جايّ من <c>UserRoleText.Arabic</c>، مكان واحد. وكان
    /// مكتوب في مكانين واختلفوا فعلاً.</para>
    /// </summary>
    string RoleText,

    string TenantName,
    bool IsActive,
    string SuspendedReason,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    DateTime? LastLoginUtc);
