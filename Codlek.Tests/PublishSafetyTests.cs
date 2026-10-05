using System.Xml.Linq;

namespace Codlek.Tests;

/// <summary>
/// حاجات في ملف المشروع لو اتشالت <b>النشر بيكسر الإنتاج</b>.
/// </summary>
public class PublishSafetyTests
{
    private static XDocument ApiProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CodlekApi.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);

        return XDocument.Load(Path.Combine(dir!.FullName, "Codlek.Api", "Codlek.Api.csproj"));
    }

    /// <summary>
    /// 🔴 <b>النشر مايولّدش <c>web.config</c>.</b> اللي على المستضيف
    /// شايل نص الاتصال بقاعدة الإنتاج ومفتاح JWT؛ ملف مولَّد بيترفع فوقه
    /// بيقطع القاعدة عن الموقع. السطر ده كان ناقص من المشروع الجديد لحد
    /// مراجعة التحويل — والقديم عنده من أول يوم.
    /// </summary>
    [Fact]
    public void Publishing_never_generates_a_web_config()
    {
        string? value = ApiProject().Descendants("IsTransformWebConfigDisabled")
            .Select(e => e.Value.Trim())
            .SingleOrDefault();

        Assert.Equal("true", value);
    }
}
