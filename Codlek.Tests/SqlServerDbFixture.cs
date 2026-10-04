using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Tests;

/// <summary>
/// قاعدة بيانات للرمي — <b>واحدة لكل مجموعة فحوص</b>.
///
/// <para>🔴 <b>مش <c>codlek_test</c> ومش <c>codlek_dev</c>.</b> دي
/// فحوص بتكتب وتمسح صفوف. لو شاركت قاعدة مع حاجة تانية، فحص بيقع
/// بسبب صف سايب من فحص تاني — وده أسوأ نوع فشل: بيظهر ويختفي على
/// مزاجه.</para>
///
/// <para>🔴 <b>وكل كلاس فحوص ليه قاعدة باسم لوحده.</b> xunit بيشغّل
/// الكلاسات <b>بالتوازي</b>، وكل واحد بيعمل نسخة من الـfixture
/// بتاعته. فلو الاسم مشترك، اتنين بيعملوا
/// <c>EnsureDeleted</c>/<c>EnsureCreated</c> على نفس القاعدة في نفس
/// اللحظة — والنتيجة فحوص بتقع بأخطاء مالهاش علاقة بالكود.</para>
/// </summary>
public abstract class SqlServerDbFixture : IDisposable
{
    protected abstract string DatabaseName { get; }

    /// <summary>
    /// لاحقة على اسم القاعدة من <c>CODLEK_TEST_DB_SUFFIX</c> — فاضية
    /// في العادي.
    ///
    /// <para>⚠️ <b>وموجودة عشان مجموعتين فحوص يشتغلوا مع بعض.</b>
    /// التحوير المقصود بيشغّل المجموعة كلها عشرات المرات في نسخة
    /// منفصلة من الريبو، والشغل العادي بيشغّلها في الأصلية — ومن غير
    /// لاحقة الاتنين بيعملوا حذف وإنشاء على <b>نفس القواعد</b> في نفس
    /// اللحظة، والفحوص بتقع في الناحيتين بأخطاء مالهاش علاقة بالكود.
    /// نفس فخ «كل كلاس ليه قاعدة باسم لوحده» بس بين عمليتين.</para>
    /// </summary>
    private static readonly string Suffix =
        Environment.GetEnvironmentVariable("CODLEK_TEST_DB_SUFFIX") ?? "";

    public string ConnectionString =>
        "Server=localhost;Database=" + DatabaseName + Suffix + ";Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    protected SqlServerDbFixture()
    {
        using var db = Create();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    public AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    public void Dispose()
    {
        using var db = Create();
        db.Database.EnsureDeleted();
    }
}
