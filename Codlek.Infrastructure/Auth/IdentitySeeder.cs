using Codlek.Core.Entities.Auth;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// بيزرع الأدوار الناقصة في <c>AspNetRoles</c> عند الإقلاع.
///
/// <para>⚠️ <b>بيزوّد الناقص وبس — مابيمسحش ومابيعدّلش.</b> لو حد غيّر
/// اسم دور عربي من الشاشة، إعادة التشغيل مابتلغيش تغييره.</para>
/// </summary>
public sealed class IdentitySeeder(RoleManager<ApplicationRole> roles)
{
    public async Task<int> SeedRolesAsync()
    {
        int added = 0;

        foreach (var (_, name, arabicName) in RoleSeed.All())
        {
            // ⚠️ `RoleExistsAsync` بتقارن بالاسم المطبَّع، فـ«manager»
            // و«Manager» بيبقوا نفس الدور — وده المطلوب.
            if (await roles.RoleExistsAsync(name)) continue;

            var result = await roles.CreateAsync(new ApplicationRole
            {
                Name = name,
                DisplayNameAr = arabicName,
                IsSystemRole = true,
            });

            /*
              🔴 **الفشل بيرمي، مابيتسكتش.**

              دور ناقص معناه مستخدمين برقم دور مالوش صف. والنتيجة إن
              الحساب بيدخل عادي وبعدين كل صلاحية بتترفض — يعني شكوى
              «البرنامج مش بيفتح حاجة» من غير ولا سطر في السجل.

              الأحسن إن النظام مايقلّعش من الأول: ده بيبان فوراً.
            */
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"فشل زرع الدور «{name}»: " +
                    string.Join("، ", result.Errors.Select(e => e.Description)));

            added++;
        }

        return added;
    }
}
