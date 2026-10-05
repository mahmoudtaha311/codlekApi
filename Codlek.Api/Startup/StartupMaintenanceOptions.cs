namespace Codlek.Api.Startup;

/// <summary>
/// إعدادات صيانة الإقلاع — قسم <c>StartupMaintenance</c>.
/// </summary>
public sealed class StartupMaintenanceOptions
{
    public const string Section = "StartupMaintenance";

    /// <summary>
    /// شغّالة افتراضياً — زي القديم اللي كان بيعملها مع كل تشغيل.
    ///
    /// <para>🔴 <b>ومقفولة في التطوير</b> (<c>appsettings.Development.json</c>).
    /// فحوص العقد بتقوّم السيرفر الحقيقي في بيئة التطوير، وبعضها على
    /// قاعدة <c>codlek_dev</c> مش قاعدة فحص — والصيانة بتكتب. اللي عايز
    /// يجرّبها محلياً يفتحها بـ<c>StartupMaintenance__Enabled=true</c>.</para>
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// ثواني استنّى بعد ما السيرفر يبتدي يرد.
    ///
    /// <para>⚠️ القاعدة المُدارة بتنام، وأول طلبات بعد الإقلاع (الراكات
    /// بتخبط على <c>/api/health</c> واللوحة بتفتح) أولى بيها من لفّة
    /// صيانة.</para>
    /// </summary>
    public int StartDelaySeconds { get; set; } = 5;
}
