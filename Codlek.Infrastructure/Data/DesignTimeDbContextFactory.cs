using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Codlek.Infrastructure.Data;

/// <summary>
/// بيدّي <c>dotnet ef</c> سياق من غير ما يقوّم السيرفر.
///
/// <para>⚠️ <b>العنوان ده للأدوات بس.</b> بيتقرا من
/// <c>CODLEK_DESIGN_CONNECTION</c>، والافتراضي قاعدة
/// <c>codlek_shape</c> — وهي قاعدة <b>فاضية مخصوصة للمقارنة</b>، مش
/// قاعدة الاختبار ولا الإنتاج. يعني أي أمر <c>ef</c> بيتنفّذ بالغلط
/// مايلمسش شغل حقيقي.</para>
///
/// <para>🔴 والمشروع ده <b>عمره ما بيعمل هجرات على الإنتاج</b> —
/// المشروع القديم هو اللي بيملك الـschema. الهجرات هنا أداة قياس:
/// بنخلّي EF يبني القاعدة من فهمه، ونقارنها بالحقيقية. أي فرق معناه
/// إن النقل غلط.</para>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string ShapeDatabase =
        "Server=localhost;Database=codlek_shape;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        string connection =
            Environment.GetEnvironmentVariable("CODLEK_DESIGN_CONNECTION") ?? ShapeDatabase;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new AppDbContext(options);
    }
}
