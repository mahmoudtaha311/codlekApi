using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Data;

/// <summary>
/// بيدّي <c>dotnet ef</c> سياق من غير ما يقوّم السيرفر.
///
/// <para>⚠️ <b>العنوان ده للأدوات بس.</b> بيتقرا من
/// <c>CODLEK_DESIGN_CONNECTION</c>، والافتراضي قاعدة
/// <c>codlek_dev</c> — قاعدة تطوير محلية. مش قاعدة الاختبار
/// ولا الإنتاج. يعني أي أمر <c>ef</c> بيتنفّذ بالغلط
/// مايلمسش شغل حقيقي.</para>
///
/// <para>🔴 <b>القاعدة اللي بنطوّر عليها.</b> الهجرات بتتعمل وبتتطبّق
/// هنا بحرية — قاعدة محلية فاضية، وأي غلط فيها مايكلّفش حاجة.</para>
///
/// <para>🔴 <b>والقاعدة الوحيدة اللي بتحكم: الجداول الـ٣١ الموجودة
/// ماتتغيّرش.</b> زيادة جدول جديد أو عمود جديد يقبل الفراغ = آمن،
/// بيتعمل على الإنتاج وقت التحويل في دقيقة. أما تغيير عمود موجود
/// (طوله، نوعه، هل يقبل الفراغ) = تعديل على جدول فيه شغل ورشة
/// حقيقي، وده اللي بوابة <c>SCHEMA-GATE.md</c> موجودة عشانه.</para>
///
/// <para>⚠️ وده اللي بيخلّي التحويل في الآخر رخيص: المشروع الجديد
/// بيتوصّل على قاعدة الإنتاج، وبيطبّق <b>الزيادات بس</b>.</para>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string DevelopmentDatabase =
        "Server=localhost;Database=codlek_dev;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        string connection =
            Environment.GetEnvironmentVariable("CODLEK_DESIGN_CONNECTION") ?? DevelopmentDatabase;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new AppDbContext(options);
    }
}
