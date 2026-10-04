using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Codlek.Infrastructure.Data;

/// <summary>
/// نص اتصال SQL Server — <b>بنفس اسم المشروع القديم بالحرف</b>.
///
/// <para>🔴 <b>الاسم <c>SqlServer</c> مش <c>DefaultConnection</c>، وده
/// مش تفصيلة.</b> الاستضافة فيها <c>ConnectionStrings__SqlServer</c>
/// متحطوط في <c>web.config</c>. فلو المشروع الجديد دوّر على اسم تاني،
/// مش هيلاقي حاجة — وفي التطوير بيرجع للقاعدة المحلية في سكوت. يعني
/// <b>السيرفر المنشور يقوم على قاعدة فاضية ويقول إنه شغّال</b>،
/// والشغل بيتكتب في مكان غلط.</para>
///
/// <para>⚠️ والنسخة دي مختصرة عن القديمة عن قصد: فحص
/// <c>DATABASE_URL</c> القديم (بقايا PostgreSQL) مكانه المشروع
/// القديم — هو اللي منشور. لو الجديد اتنشر على نفس المكان، الفحص
/// ده يتنقل معاه.</para>
/// </summary>
public static class SqlServerConnection
{
    /// <summary>الاسم في الإعدادات — نفس القديم.</summary>
    public const string Name = "SqlServer";

    /// <summary>
    /// قاعدة التطوير.
    ///
    /// <para>⚠️ <b>instance حقيقي مش LocalDB.</b> LocalDB بتنام
    /// وبتدّي نتيجة مضلّلة في أي حاجة بتعتمد على سلوك تحت منافسة —
    /// زي استهلاك أكواد الأجهزة ودفعات المزامنة.</para>
    /// </summary>
    public const string DevelopmentDefault =
        "Server=localhost;Database=codlek;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    /// <summary>بيرجّع نص الاتصال، أو بيرمي برسالة واضحة.</summary>
    public static string Resolve(IConfiguration configuration, bool isDevelopment)
    {
        string? configured = configuration.GetConnectionString(Name);

        if (!string.IsNullOrWhiteSpace(configured)) return Harden(configured);

        if (isDevelopment) return Harden(DevelopmentDefault);

        // 🔴 الوقوف مقصود. سيرفر قام من غير قاعدة صح بيكتب الشغل في
        // مكان غلط، ومحدش بياخد باله غير بعد فوات الأوان.
        throw new InvalidOperationException(
            "مفيش نص اتصال لقاعدة البيانات.\n" +
            "حط ConnectionStrings__SqlServer في متغيّرات البيئة.");
    }

    /// <summary>
    /// بيتأكد إن الاتصال متشفّر.
    ///
    /// <para>⚠️ <c>Microsoft.Data.SqlClient</c> من الإصدار ٤ بيفترض
    /// <c>Encrypt=true</c>. وعلى instance محلي بشهادة موقّعة ذاتياً ده
    /// بيقع بخطأ TLS مالوش أي معنى — عشان كده التطوير محتاج
    /// <c>TrustServerCertificate=True</c>.</para>
    /// </summary>
    private static string Harden(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { Encrypt = true };

        if (builder.ConnectTimeout <= 0) builder.ConnectTimeout = 15;

        return builder.ConnectionString;
    }
}
