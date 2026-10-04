using Codlek.Core.Enums;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// الأدوار اللي لازم تبقى موجودة في <c>AspNetRoles</c>.
///
/// <para>🔴 <b>القائمة مشتقّة من <see cref="UserRole"/>، مش مكتوبة
/// بالإيد.</b> لو حد زوّد دور جديد في الـenum وفضلت القائمة مكتوبة،
/// الدور الجديد مايتزرعش — والمستخدم اللي بياخده بيبقى عنده رقم دور
/// مالوش صف في الجدول. وساعتها أي صلاحية بتتقرا بالاسم بترجع فاضية،
/// يعني <b>الحساب بيدخل وبعدين مايعرفش يعمل حاجة</b> من غير أي رسالة
/// خطأ.</para>
///
/// <para>بـ<c>Enum.GetValues</c> ده مستحيل: أي دور بيتزاد في الـenum
/// بيتزرع لوحده أول تشغيل.</para>
/// </summary>
public static class RoleSeed
{
    /// <summary>
    /// الأسماء العربية — <b>دي بس اللي مكتوبة بالإيد</b>.
    ///
    /// <para>⚠️ ولو دور جديد مالوش اسم عربي هنا، بناخد اسم الـenum زي
    /// ما هو بدل ما نرمي. الدور الناقص اسمه بيبان وحش في الشاشة —
    /// وده أهون بكتير من إن النظام مايقلّعش.</para>
    /// </summary>
    private static readonly Dictionary<UserRole, string> ArabicNames = new()
    {
        [UserRole.Technician]   = "فني",
        [UserRole.Manager]      = "مدير",
        [UserRole.Owner]        = "مالك",
        [UserRole.FloorManager] = "مشرف صالة",
        [UserRole.Accountant]   = "محاسب",
    };

    /// <summary>
    /// كل الأدوار اللي لازم تكون في الجدول، باسمها الإنجليزي والعربي.
    ///
    /// <para>⚠️ الاسم الإنجليزي = اسم الـenum بالحرف، عشان
    /// <c>[Authorize(Roles = "Manager")]</c> يطابق.</para>
    /// </summary>
    public static IEnumerable<(UserRole Role, string Name, string ArabicName)> All() =>
        Enum.GetValues<UserRole>()
            .Select(r => (
                Role: r,
                Name: r.ToString(),
                ArabicName: ArabicNames.TryGetValue(r, out var ar) ? ar : r.ToString()));
}
