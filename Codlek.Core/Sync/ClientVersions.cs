namespace Codlek.Core.Sync;

/// <summary>
/// أقل نسخة برنامج راكة السيرفر بيقبل منها دفعات.
///
/// <para>🔴 <b>ليه الرفض أحسن من القبول.</b> نسخة قديمة بتبعت حمولة
/// شكلها مختلف — ناقصة حقول أو بمعاني مختلفة. لو قبلناها، البيانات
/// بتتخزّن ناقصة ومحدش بياخد باله غير بعد شهور. الرفض برسالة واضحة
/// بيوصل للفني على طول، لأن البرنامج بيعرض رد السيرفر زي ما هو.</para>
///
/// <para>⚠️ <b>والترويسة الناقصة = نسخة مش مقبولة.</b> منقولة من القديم
/// بالحرف: كل نسخة راكة في الميدان بتبعت <c>X-Client-Version</c> مع
/// الدفعة، فالغياب معناه عميل مش معروف.</para>
/// </summary>
public static class ClientVersions
{
    /// <summary>اسم الترويسة — <b>مكتوب حرفياً في برنامج الراكة</b>.</summary>
    public const string Header = "X-Client-Version";

    /// <summary>أقل نسخة مقبولة على <c>/api/v2/sync/batch</c>.</summary>
    public const string Minimum = "1.0.0";

    /// <summary>
    /// أقصى طول بيتخزّن في <c>Racks.AppVersion</c> — <b>طول العمود</b>.
    /// </summary>
    public const int StoredMaxLength = 40;

    public static bool IsSupported(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;

        return Version.TryParse(version.Trim(), out var parsed)
            && parsed >= Version.Parse(Minimum);
    }
}
