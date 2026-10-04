using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Core.Entities.Auth;

/// <summary>
/// دور — صف في <c>AspNetRoles</c>.
///
/// <para>⚠️ <b>الأدوار دي مش بديل لـ<c>UserRole</c>.</b> الـenum هو
/// العقد مع الراكة (رقم بيعدّي على السلك)، والجدول ده للصلاحيات
/// اللي ممكن تتظبّط من غير نشر — زي «مين يقدر يوافق على الصيانة».</para>
///
/// <para>🔴 والجدول بيتزرع بأسماء الـenum بالظبط
/// (<c>Technician · Manager · Owner · FloorManager · Accountant</c>)
/// عشان الاتنين مايفترقوش.</para>
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>الاسم بالعربي — ده اللي بيتعرض في الشاشات.</summary>
    [MaxLength(120)]
    public string DisplayNameAr { get; set; } = "";

    /// <summary>
    /// دور أساسي مايتمسحش.
    ///
    /// <para>⚠️ الخمسة اللي جايين من <c>UserRole</c> أساسيين: مسح
    /// واحد منهم بيسيب مستخدمين بدور مش موجود.</para>
    /// </summary>
    public bool IsSystemRole { get; set; }
}
