using Codlek.Infrastructure.Labels;

namespace Codlek.Tests;

/// <summary>
/// رسّام ليبل الـQR — <b>مقارن بناتج القديم حرف حرف</b>.
///
/// <para>🔴 <b>النصوص المتوقّعة هنا متولّدة من
/// <c>CodlekWeb/Services/DeviceQr.cs</c> نفسه</b> (نفس المكتبة
/// <c>Net.Codecrete.QrCodeGenerator</c> 3.2.1). الليبلات المطبوعة من
/// الشاشة القديمة ملزوقة على لابات؛ أي فرق في النسخة أو الهامش بيطلّع
/// رمز تاني لنفس الكود — والفحص ده هو اللي بيقول.</para>
///
/// <para>⚠️ مستوى التصحيح المطلوب مش متثبّت هنا: المكتبة بتعلّيه لوحدها
/// (boostEcl)، فالأكواد دي بتطلع High بحجم ٢١ سواء طلبنا Low أو Medium أو
/// High. <c>Medium</c> في الرسّام هو الحد الأدنى بس.</para>
///
/// <para>⚠️ <b>لو الفحص ده وقع بعد ترقية المكتبة: ماتحدّثش النص
/// المتوقّع.</b> رجّع النسخة المثبّتة، أو اسأل الأول لو الليبلات
/// القديمة هتتطبع تاني.</para>
/// </summary>
public class DeviceLabelRendererTests
{
    [Fact]
    public void The_label_matches_the_legacy_output_byte_for_byte()
    {
        var renderer = new QrDeviceLabelRenderer();

        Assert.Equal(Golden("LP-00000001"), renderer.Svg("LP-00000001"));
        Assert.Equal(Golden("LP-00018425"), renderer.Svg("LP-00018425"));
    }

    /// <summary>
    /// ⚠️ <b>عنصر <c>&lt;svg&gt;</c> لوحده</b> — من غير ترويسة XML ولا
    /// DOCTYPE، عشان اللوحة تحطه جوّه الصفحة.
    /// </summary>
    [Fact]
    public void The_label_is_a_bare_svg_element()
    {
        string svg = new QrDeviceLabelRenderer().Svg("LP-00000001");

        Assert.StartsWith("<svg ", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.DoesNotContain("<?xml", svg);
        Assert.DoesNotContain("DOCTYPE", svg);
    }

    /// <summary>
    /// 🔴 <b>الكود مابيتكتبش جوّه الـSVG كنص</b> — الناتج مسار متولّد
    /// من الرمز وبس، فحطّه جوّه الصفحة آمن.
    /// </summary>
    [Fact]
    public void No_caller_text_ends_up_in_the_svg()
    {
        string svg = new QrDeviceLabelRenderer().Svg("<script>x</script>");

        Assert.DoesNotContain("script", svg);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_code_is_refused(string code) =>
        Assert.Throws<ArgumentException>(() => new QrDeviceLabelRenderer().Svg(code));

    /// <summary>
    /// ⚠️ <b>بيثبّت الحقيقة اللي التعليقات بتقولها</b>: المكتبة بتعلّي
    /// مستوى التصحيح لوحدها، فكود الـ١١ حرف بيطلع High بحجم ٢١ مهما كان
    /// المستوى المطلوب — يعني الـgolden مش بيحرس المستوى. لو الفحص ده وقع
    /// بعد ترقية، التعليقات في <c>QrDeviceLabelRenderer</c> محتاجة مراجعة.
    /// </summary>
    [Theory]
    [InlineData("LP-00000001")]
    [InlineData("LP-00018425")]
    public void The_library_boosts_the_correction_level_to_high_for_a_label_code(string code)
    {
        foreach (var requested in new[]
                 {
                     Net.Codecrete.QrCodeGenerator.QrCode.Ecc.Low,
                     Net.Codecrete.QrCodeGenerator.QrCode.Ecc.Medium,
                     Net.Codecrete.QrCodeGenerator.QrCode.Ecc.High,
                 })
        {
            var qr = Net.Codecrete.QrCodeGenerator.QrCode.EncodeText(code, requested);

            Assert.Equal(Net.Codecrete.QrCodeGenerator.QrCode.Ecc.High, qr.ErrorCorrectionLevel);
            Assert.Equal(21, qr.Size);
        }
    }

    private static string Golden(string code) => code switch
    {
        "LP-00000001" => """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 25 25" shape-rendering="crispEdges" role="img" focusable="false"><rect width="25" height="25" fill="#ffffff"/><path d="M2,2h7v7h-7z M10,2h2v4h-2v-1h1v-2h-1z M16,2h7v7h-7z M3,3v5h5v-5z M13,3h2v3h-1v-1h-1z M17,3v5h5v-5z M4,4h3v3h-3z M18,4h3v3h-3z M11,7h1v1h-1z M13,7h2v3h1v1h-2v-3h-1z M10,8h1v1h-1z M12,8h1v1h-1z M11,9h1v1h1v1h1v1h2v-1h3v-1h1v1h2v-1h1v2h-4v1h-1v-1h-1v1h1v1h-3v-1h-2v-1h-2v-1h-1v-1h1z M4,10h1v1h1v1h1v1h-1v1h-1v-2h-1v2h-1v1h-1v-3h1v-1h1z M6,10h3v1h-3z M8,12h1v1h-1z M10,12h1v1h-1z M7,13h1v1h-1z M9,13h1v1h-1z M12,13h1v1h-1z M21,13h1v2h-2v-1h1z M8,14h1v1h-1z M10,14h1v2h-1z M13,14h1v1h-1z M18,14h1v1h-1z M12,15h1v1h2v1h-1v1h-1v-1h-1z M15,15h1v1h-1z M17,15h1v1h-1z M22,15h1v1h-1z M2,16h7v7h-7z M18,16h1v1h-1z M20,16h1v1h-1z M3,17v5h5v-5z M10,17h1v2h-1z M17,17h1v1h-1z M19,17h1v1h-1z M4,18h3v3h-3z M15,18h2v1h1v1h-2v-1h-1z M18,18h1v1h-1z M20,18h1v1h1v1h-1v1h-1v-1h-1v-1h1z M22,18h1v1h-1z M11,19h1v3h-1v-1h-1v-1h1z M13,19h2v1h1v2h1v1h-2v-2h-2z M18,20h1v1h-1z M22,20h1v3h-1v-1h-1v-1h1z M17,21h1v1h-1z M19,21h1v1h-1z M13,22h1v1h-1z M18,22h1v1h-1z M20,22h1v1h-1z" fill="#000000"/></svg>""",
        "LP-00018425" => """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 25 25" shape-rendering="crispEdges" role="img" focusable="false"><rect width="25" height="25" fill="#ffffff"/><path d="M2,2h7v7h-7z M10,2h1v1h1v-1h1v1h1v-1h1v2h-1v4h-1v-4h-2v1h-1z M16,2h7v7h-7z M3,3v5h5v-5z M17,3v5h5v-5z M4,4h3v3h-3z M18,4h3v3h-3z M11,5h1v1h-1z M10,7h2v1h-1v2h1v1h-1v2h-1z M12,8h1v1h-1z M14,8h1v1h-1z M13,9h1v1h-1z M4,10h3v1h-2v1h-1v1h1v2h-1v-1h-1v1h-1v-3h1v-1h1z M8,10h1v1h-1z M15,10h3v1h1v1h-1v1h-1v-1h-1v1h-2v-1h-1v-1h2z M20,10h3v3h-1v-2h-2z M5,12h2v1h-2z M8,12h1v1h-1z M12,12h1v2h-1z M9,13h1v2h-2v-1h1z M16,13h1v1h-1z M18,13h3v1h2v3h-1v-1h-1v1h1v1h-1v1h-1v-3h-1v-1h1v-1h-2z M14,14h1v1h-1z M17,14h1v2h1v3h-1v4h-1v-2h-1v-1h1v-2h1v-1h-4v-1h2v-1h1z M10,15h2v1h-2z M13,15h1v1h-1z M2,16h7v7h-7z M3,17v5h5v-5z M13,17h1v1h-1z M4,18h3v3h-3z M10,18h1v1h2v1h-2v1h-1z M14,18h1v1h-1z M13,20h2v1h1v2h-2v-2h-1z M20,20h1v3h-1z M22,21h1v1h-1z M11,22h1v1h-1z" fill="#000000"/></svg>""",
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };
}
