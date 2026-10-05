using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Codlek.Infrastructure.Migrations
{
    /// <summary>
    /// نقل حسابات اللوحة من <c>Users</c> القديم لـ<c>AspNetUsers</c> —
    /// <b>مرة واحدة، وقت التحويل</b>.
    ///
    /// <para>🔴 <b>من غير الهجرة دي، كل مستخدم في النظام مايدخلش بعد
    /// التحويل.</b> الدخول في المشروع الجديد بيدوّر في
    /// <c>AspNetUsers</c> بس، والجدول ده بيتولد فاضي. و
    /// <c>LegacyPasswordHasher</c> بيفهم الباسوردات القديمة — بس لازم
    /// حد ينقلها الأول.</para>
    ///
    /// <para>🔴 <b>نفس المعرّف — مش معرّف جديد.</b> سجل التدقيق وأوامر
    /// الصيانة (الموافقة والفتح) بتشاور على المستخدم بمعرّفه، ومعرّف
    /// جديد كان هيخلّي كل التاريخ ده يشاور على حد مش موجود.</para>
    ///
    /// <para>⚠️ <b>الباسورد بيتنقل زي ما هو والملح في <c>LegacySalt</c>.</b>
    /// أول دخول بيترقّى للشكل الجديد لوحده — راجع
    /// <c>LegacyPasswordHasher</c>. والهجرة مابتشوفش باسورد ولا بتحسب
    /// بصمة.</para>
    ///
    /// <para>⚠️ <b>ومابتدهسش حاجة:</b> حساب موجود بنفس المعرّف أو بنفس
    /// الاسم المطبَّع بيتساب. الاسم المطبَّع هنا نفس قاعدة
    /// <c>LoginName.Normalize</c> في المشروعين (تقليم + حروف صغيرة + قص
    /// على ٦٠)، فالعمود القديم بيتنسخ زي ما هو.</para>
    ///
    /// <para>⚠️ <b>والقسم والتخصص مش بيتنقلوا</b> — <c>ApplicationUser</c>
    /// مافيهوش الحقلين دول. القيم بتفضل في <c>Users</c>.</para>
    /// </summary>
    public partial class ImportLegacyUsers : Migration
    {
        /// <summary>
        /// الجملة نفسها — <b>عامة عشان الفحوص وتجربة التحويل يشغّلوها
        /// بالحرف</b> من غير نسخة تانية تتفرّق عنها.
        /// </summary>
        public const string Sql = """
            INSERT INTO AspNetUsers
                (Id, TenantId, DisplayName, Code, Role, CredentialVersion,
                 MustChangePassword, IsActive, SuspendedReason, SuspendedByName,
                 SuspendedAtUtc, CreatedAtUtc, LastLoginUtc, LegacySalt,
                 UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                 PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber,
                 PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled,
                 AccessFailedCount)
            SELECT u.Id, u.TenantId, u.DisplayName, u.Code, u.Role, u.CredentialVersion,
                   u.MustChangePassword, u.IsActive, u.SuspendedReason, u.SuspendedByName,
                   u.SuspendedAtUtc, u.CreatedAtUtc, u.LastLoginUtc, u.Salt,
                   u.Username, u.NormalizedUsername, NULL, NULL, 0,
                   u.PasswordHash,
                   UPPER(REPLACE(CONVERT(nvarchar(36), NEWID()), '-', '')),
                   CONVERT(nvarchar(36), NEWID()),
                   NULL, 0, 0, NULL, 1, 0
              FROM Users u
             WHERE u.NormalizedUsername <> ''
               AND NOT EXISTS (SELECT 1 FROM AspNetUsers a WHERE a.Id = u.Id)
               AND NOT EXISTS (SELECT 1 FROM AspNetUsers a
                                WHERE a.NormalizedUserName = u.NormalizedUsername);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Sql);
        }

        /// <summary>
        /// ⚠️ <b>مابيمسحش — عن قصد.</b> بعد التحويل الحسابات دي بتعيش:
        /// باسوردات اترقّت، وأدوار اتغيّرت، وتوكنات اتعملت. الرجوع للخلف
        /// ماينفعش يمسح حساب حد شغّال بيه.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
