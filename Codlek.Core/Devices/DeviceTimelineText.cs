using Codlek.Core.Enums;
using Codlek.Core.Time;

namespace Codlek.Core.Devices;

/// <summary>
/// عناوين وملخّصات خط زمن اللاب — <b>دوال نقية</b>.
///
/// <para>⚠️ <b>والملخّص بيتبني «قطع» بتتوصّل بفاصل.</b> كل قطعة
/// بتتضاف <b>لو</b> ليها قيمة، فالسطر بيطول ويقصر على حسب اللي
/// متسجّل فعلاً — مش قالب فيه خانات فاضية.</para>
///
/// <para>🔴 <b>والأوقات في الملخّص بتوقيت القاهرة.</b> السطر بيتقرا
/// كحكاية («خلص ٠٣:١٥»)، فكتابة UTC فيه بتخلّي الفني يقرا فحص
/// العصر على إنه الضهر.</para>
/// </summary>
public static class DeviceTimelineText
{
    /// <summary>الفاصل بين قطع الملخّص.</summary>
    public const string Separator = " · ";

    /// <summary>
    /// ⚠️ أقصى طول لنص حر في سطر خط زمن — عشان السطر مايبقاش فقرة.
    /// </summary>
    public const int FreeTextLimit = 160;

    // =================================================================
    //  العناوين
    // =================================================================

    public static string Title(DeviceTimelineEventType type) => type switch
    {
        DeviceTimelineEventType.DeviceDiscovered => "تم اكتشاف الجهاز لأول مرة",
        DeviceTimelineEventType.TestPerformed => "تم تنفيذ فحص",
        DeviceTimelineEventType.NoteAdded => "تمت إضافة ملاحظة",
        DeviceTimelineEventType.RepairOpened => "تم فتح أمر صيانة",
        DeviceTimelineEventType.RepairStarted => "بدأت الصيانة",
        DeviceTimelineEventType.RepairCompleted => "تمت الصيانة",
        DeviceTimelineEventType.RepairUnableToRepair => "تعذّر إصلاح الجهاز",
        DeviceTimelineEventType.RepairCancelled => "تم إلغاء أمر الصيانة",

        // ⚠️ الحركة عنوانها بيجي من نوعها — `DeviceMovementTitle`.
        _ => "",
    };

    // =================================================================
    //  الملخّصات
    // =================================================================

    /// <summary>
    /// ملخّص الاكتشاف: الكود، ودرجة الثقة، وأساس الهوية.
    ///
    /// <para>⚠️ <b>ودرجة الثقة بحرف عربي («أ»/«ب»/«ج») مش
    /// لاتيني.</b> السطر كله عربي، و«A» جوّاه بتبان حرف ضايع.</para>
    /// </summary>
    public static string Discovery(
        string publicCode, DeviceIdentityConfidence confidence, string identityBasis)
    {
        var bits = new List<string>(3);

        if (publicCode.Length > 0) bits.Add("الكود " + publicCode);

        bits.Add("ثقة " + ConfidenceLetter(confidence));

        if (identityBasis.Length > 0) bits.Add("أساس الهوية: " + identityBasis);

        return Join(bits);
    }

    /// <summary>
    /// ⚠️ <b>حرف الثقة مش <c>DeviceIdentityConfidenceText</c>.</b>
    /// الأخيرة بترجّع وصف كامل («هوية مؤكّدة») وهو مناسب لخانة في
    /// جدول؛ وده حرف واحد جوّه سطر فيه تلات قطع.
    /// </summary>
    public static string ConfidenceLetter(DeviceIdentityConfidence confidence) =>
        confidence switch
        {
            DeviceIdentityConfidence.A => "أ",
            DeviceIdentityConfidence.B => "ب",
            DeviceIdentityConfidence.C => "ج",
            _ => "مفيش",
        };

    /// <summary>
    /// ملخّص الفحص: وقت الانتهاء ومدّته.
    ///
    /// <para>⚠️ والمدة بالدقايق <b>مقطوعة</b> مش مقرّبة — زي القديم.
    /// فحص ٩٠ ثانية بيتقال عنه «دقيقة».</para>
    /// </summary>
    public static string Test(DateTime? endedAtUtc, long durationMs)
    {
        var bits = new List<string>(2);

        if (endedAtUtc is { } ended)
            bits.Add("خلص " + CairoDay.ToCairo(ended).ToString("HH:mm"));

        if (durationMs > 0) bits.Add(durationMs / 60000 + " دقيقة");

        return Join(bits);
    }

    /// <summary>
    /// ملخّص لحظة صيانة: رقم الأمر، ومعاه النص الحر بتاع اللحظة
    /// (العطل، أو اللي اتعمل، أو السبب).
    /// </summary>
    public static string Repair(string orderCode, string? freeText, int partCount)
    {
        var bits = new List<string>(3);

        if (orderCode.Length > 0) bits.Add("أمر " + orderCode);

        if (!string.IsNullOrWhiteSpace(freeText))
            bits.Add(Clip(freeText, FreeTextLimit));

        if (partCount > 0) bits.Add(partCount + " قطعة غيار");

        return Join(bits);
    }

    /// <summary>
    /// ملخّص الحركة — <b>اللي اختلف وبس</b>.
    ///
    /// <para>🔴 <b>الحركة بتتخزّن بأطرافها كلها حتى لو واحد منهم
    /// مااتغيّرش.</b> تسليم الحيازة لنفس الفني بيتخزّن وفيه
    /// <c>From == To</c>، وطباعة «من فلان لفلان» في الحالة دي بتقول
    /// إن حاجة اتغيّرت وهي ما اتغيّرتش — فبنقارن الطرفين وبنكتب
    /// اللي اختلف وبس.</para>
    ///
    /// <para>⚠️ <b>ووقت الحركة مش وقت تسجيلها.</b> راكة اشتغلت
    /// أوفلاين بترفع حركة عمرها أيام، والسطر بيتحط على وقت
    /// <b>حصولها</b> لأن ده مكانها في التاريخ — وبيقول صراحةً إنها
    /// وصلت السيرفر متأخرة بدل ما يخبّي الفرق.</para>
    ///
    /// <para>⚠️ <b>والعتبة دقيقة مش صفر:</b> الفرق الطبيعي بين حصول
    /// الحركة وتسجيلها أجزاء من الثانية، وطباعته على كل سطر بتحوّل
    /// معلومة مفيدة لضوضاء.</para>
    /// </summary>
    public static string Movement(
        DeviceOperationalStage fromStage,
        DeviceOperationalStage toStage,
        string fromLocation,
        string toLocation,
        bool locationChanged,
        string fromHolder,
        string toHolder,
        bool holderChanged,
        string reason,
        DateTime occurredAtUtc,
        DateTime recordedAtUtc)
    {
        var bits = new List<string>(5);

        if (fromStage != toStage)
        {
            bits.Add("المرحلة: "
                + DeviceOperationalStageText.Arabic(fromStage)
                + " ← "
                + DeviceOperationalStageText.Arabic(toStage));
        }

        if (locationChanged) bits.Add($"المكان: {fromLocation} ← {toLocation}");

        if (holderChanged) bits.Add($"الحائز: {fromHolder} ← {toHolder}");

        if (reason.Length > 0) bits.Add(Clip(reason, FreeTextLimit));

        if ((recordedAtUtc - occurredAtUtc).TotalMinutes >= 1)
        {
            bits.Add("وصل السيرفر "
                + CairoDay.ToCairo(recordedAtUtc).ToString("yyyy-MM-dd HH:mm"));
        }

        return Join(bits);
    }

    /// <summary>
    /// قصّ نص حر <b>للعرض</b> — بعلامة قص.
    ///
    /// <para>🔴 <b>ودي <u>مش</u> <see cref="TextClip.To"/>.</b>
    /// الأخيرة بتفصّل النص على طول <b>العمود في القاعدة</b> قبل
    /// الحفظ، وبتقص <b>من غير</b> علامة — <u>عن قصد</u>: زيادة «…»
    /// على قيمة متخزّنة بتخلّي القيمة نفسها مغلوطة، وأي مقارنة
    /// عليها بعدين بتفشل.</para>
    ///
    /// <para>⚠️ وهنا العكس: القيمة بتتعرض ومش بتتخزّن، فاللي بيقرا
    /// لازم يعرف إن فيه كلام ناقص. والـ<c>Trim</c> هنا كمان لأن
    /// الملاحظات جاية من الميدان وبتبدأ بسطر فاضي كتير.</para>
    /// </summary>
    public static string Clip(string? value, int max)
    {
        string trimmed = (value ?? "").Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }

    private static string Join(List<string> bits) => string.Join(Separator, bits);
}
