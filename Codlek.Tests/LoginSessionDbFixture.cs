using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Tests;

/// <summary>
/// قاعدة بيانات للرمي — <b>مخصوصة للفحوص دي وبس</b>.
///
/// <para>🔴 <b>مش <c>codlek_test</c> ومش <c>codlek_dev</c>.</b> دي
/// فحوص بتكتب وتمسح صفوف. لو شاركت قاعدة مع حاجة تانية، فحص بيقع
/// بسبب صف سايب من فحص تاني — وده أسوأ نوع فشل: بيظهر ويختفي
/// على مزاجه.</para>
///
/// <para>⚠️ والقاعدة بتتعمل من الصفر مع كل تشغيل وبتتمسح بعده.</para>
/// </summary>
public sealed class LoginSessionDbFixture : IDisposable
{
    private const string DatabaseName = "codlek_sessions_test";

    public string ConnectionString { get; } =
        "Server=localhost;Database=" + DatabaseName + ";Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    public LoginSessionDbFixture()
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
