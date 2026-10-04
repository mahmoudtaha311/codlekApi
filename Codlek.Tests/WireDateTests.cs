using System.Text.Json;
using Codlek.Api.Racks;

namespace Codlek.Tests;

/// <summary>
/// شكل التواريخ على السلك.
///
/// <para>🔴 <b>والملف ده اتكتب بعد باج شغّال في كل نقطة في المشروع
/// الجديد.</b> كل تاريخ كان بيخرج من غير <c>Z</c> وبدقة سبع خانات
/// (<c>2026-10-04T07:12:02.9533225</c>) — والمتصفح بيقرا النص ده
/// على إنه <b>توقيت محلي</b>، فتحويل «اعرض بتوقيت القاهرة» في
/// الواجهة بيبقى بلا أثر والمدير بيشوف وقت غلط بساعتين أو
/// تلاتة.</para>
///
/// <para>🔴 <b>والسبب كان سطر واحد في المكان الغلط:</b> القديم
/// بيحط المحوّل بـ<c>ConfigureHttpJsonOptions</c> — وهي بتنفع مع
/// <b>المسارات البسيطة</b> بس. والمشروع الجديد كله كنترولرز،
/// فالسطر ده مالوش أي أثر فيه، والصح
/// <c>AddControllers().AddJsonOptions(...)</c>.</para>
///
/// <para>⚠️ <b>واللي لقطه ضرب HTTP حقيقي</b> — مفيش فحص وحدة كان
/// بيشوفه، لأن الفحوص بتقيس الكائنات قبل التسلسل.</para>
///
/// <para>⚠️ <b>وفي تغذية الراكة الفرق أخطر من العرض:</b> الراكة
/// بتاخد علامة الوقت من الرد وبتبعتها تاني في <c>?since=</c>.
/// تاريخ من غير منطقة بيتقرا محلي، فالعلامة بتتزحلق بفرق التوقيت —
/// يعني يا سحب كل حاجة من تاني كل دورة، يا <b>تخطّي أوامر حقيقية
/// للأبد</b>.</para>
/// </summary>
public class WireDateTests
{
    private static readonly JsonSerializerOptions Wire = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new NullableUtcDateTimeConverter());

        return options;
    }

    private sealed record Payload(DateTime At, DateTime? Maybe);

    // =================================================================
    //  الشكل
    // =================================================================

    /// <summary>
    /// 🔴 <b>الشكل بالحرف: مللي ثانية و<c>Z</c>.</b>
    ///
    /// <para>دقة أعلى من المللي بتكسر مقارنة العلامة: السلك بيكتب
    /// بدقة المللي، فلو السيرفر بعت دقة أعلى، العلامة اللي بترجع
    /// بتبقى <b>أقل</b> من قيمة الصف بجزء من المللي — والصف بيرجع
    /// في كل سحبة للأبد، بيبان كأنه بيتغيّر وهو ساكن.</para>
    /// </summary>
    [Fact]
    public void A_utc_date_is_written_with_milliseconds_and_a_z()
    {
        var at = new DateTime(2026, 10, 4, 7, 12, 2, 953, DateTimeKind.Utc);

        string json = JsonSerializer.Serialize(new Payload(at, null), Wire);

        Assert.Contains("\"at\":\"2026-10-04T07:12:02.953Z\"", json);
    }

    /// <summary>
    /// 🔴 <b>و<c>Unspecified</c> بتتعامل على إنها UTC — ده المسار
    /// اللي كان بيكسر.</b>
    ///
    /// <para>EF بيقرا <c>datetime2</c> من SQL Server و<c>Kind</c>
    /// بيرجع <c>Unspecified</c>، واللي متخزّن فعلاً UTC.</para>
    /// </summary>
    [Fact]
    public void An_unspecified_kind_is_treated_as_utc()
    {
        var at = new DateTime(2026, 10, 4, 7, 12, 2, 953, DateTimeKind.Unspecified);

        string json = JsonSerializer.Serialize(new Payload(at, null), Wire);

        Assert.Contains("2026-10-04T07:12:02.953Z", json);
    }

    /// <summary>⚠️ و<c>Local</c> بتتحوّل، مابتتختمش.</summary>
    [Fact]
    public void A_local_kind_is_converted_not_relabelled()
    {
        var utc = new DateTime(2026, 10, 4, 7, 0, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime();

        string json = JsonSerializer.Serialize(new Payload(local, null), Wire);

        Assert.Contains("2026-10-04T07:00:00.000Z", json);
    }

    /// <summary>
    /// 🔴 <b>والدقة الزايدة بتتقص — مش بتتقرّب.</b>
    ///
    /// <para>السلك بيكتب بدقة المللي، والقص هو اللي بيخلّي اللي
    /// بيخرج هو نفسه اللي بيرجع في <c>?since=</c>. والتقريب لفوق
    /// كان بيخلّي العلامة <b>تتقدّم</b> على الصف — يعني الصف
    /// بيتخطّى للأبد.</para>
    /// </summary>
    [Fact]
    public void Sub_millisecond_precision_is_dropped_not_rounded()
    {
        // ٩٥٣.٣٢٢٥ مللي
        var at = new DateTime(2026, 10, 4, 7, 12, 2, DateTimeKind.Utc)
            .AddTicks(9_533_225);

        string json = JsonSerializer.Serialize(new Payload(at, null), Wire);

        Assert.Contains("02.953Z", json);
        Assert.DoesNotContain("02.954Z", json);
    }

    // =================================================================
    //  الاختياري
    // =================================================================

    /// <summary>
    /// 🔴 <b>المحوّل الأول مابينطبقش على <c>DateTime?</c></b> — ومن
    /// غير النوع التاني، كل تاريخ اختياري بيخرج من غير <c>Z</c>.
    /// وده بالظبط أوقات الإنهاء والتسليم.
    /// </summary>
    [Fact]
    public void A_nullable_date_is_written_the_same_way()
    {
        var at = new DateTime(2026, 10, 4, 7, 12, 2, 953, DateTimeKind.Unspecified);

        string json = JsonSerializer.Serialize(new Payload(at, at), Wire);

        Assert.Contains("\"maybe\":\"2026-10-04T07:12:02.953Z\"", json);
    }

    /// <summary>⚠️ و<c>null</c> بتتكتب <c>null</c> — مش وقت صفر.</summary>
    [Fact]
    public void A_missing_date_stays_null()
    {
        string json = JsonSerializer.Serialize(
            new Payload(DateTime.UtcNow, null), Wire);

        Assert.Contains("\"maybe\":null", json);
    }

    // =================================================================
    //  القراية
    // =================================================================

    /// <summary>
    /// 🔴 <b>القراية مالهاش أي تحويل — عن قصد.</b>
    ///
    /// <para>الإعدادات دي بتربط حمولات الراكة <b>الصاعدة</b> كمان،
    /// فأي تحويل في القراية بيمسّ كل حمولة بترفعها راكة — وده أخطر
    /// مسار في النظام. والعيب اللي اتصلّح عيب <b>كتابة</b>.</para>
    /// </summary>
    [Fact]
    public void Reading_keeps_the_default_behaviour()
    {
        var round = JsonSerializer.Deserialize<Payload>(
            "{\"at\":\"2026-10-04T07:12:02.953Z\",\"maybe\":null}", Wire);

        Assert.NotNull(round);
        Assert.Equal(new DateTime(2026, 10, 4, 7, 12, 2, 953, DateTimeKind.Utc), round.At);
        Assert.Null(round.Maybe);
    }

    /// <summary>
    /// ⚠️ <b>ورحلة كاملة: اللي بيخرج بيرجع نفسه.</b> ودي القاعدة
    /// اللي مقارنة <c>?since=</c> قايمة عليها.
    /// </summary>
    [Fact]
    public void A_round_trip_keeps_the_same_instant()
    {
        var at = new DateTime(2026, 10, 4, 7, 12, 2, 953, DateTimeKind.Utc);

        string json = JsonSerializer.Serialize(new Payload(at, at), Wire);
        var back = JsonSerializer.Deserialize<Payload>(json, Wire);

        Assert.Equal(at, back!.At);
        Assert.Equal(at, back.Maybe);
    }

    // =================================================================
    //  خيارات سلك الراكة
    // =================================================================

    /// <summary>
    /// 🔴 <b>رد الدفعة <u>مافيهوش</u> المحوّل — والتغذيات فيها.</b>
    ///
    /// <para>رد الدفعة عقد مجمّد بايت ببايت: الراكات في الميدان
    /// بتقراه بـ<c>JsonDocument</c>، وأي تغيير في شكله بيمسّ أخطر
    /// مسار في النظام. والتغذيات النازلة بتاخد الشكل الصح من أول
    /// يوم.</para>
    /// </summary>
    [Fact]
    public void The_batch_reply_and_the_feeds_use_different_options()
    {
        Assert.Empty(RackWire.Wire.Converters);
        Assert.Equal(2, RackWire.Downstream.Converters.Count);
    }

    /// <summary>
    /// 🔴 <b>وقراية المتخزّن بتتجاهل حالة الأحرف.</b>
    ///
    /// <para>رد الدفعة كان بيتكتب بطريقتين: الطازة camelCase،
    /// والمخزّن PascalCase. والراكة بتقرا بـ<c>TryGetProperty</c>
    /// وهي حسّاسة لحالة الحروف — فكل إعادة إرسال كانت بتبان لها رد
    /// فاضي، وصف مرفوض كان بيتقفل ويضيع في صمت.</para>
    /// </summary>
    [Fact]
    public void Reading_a_stored_reply_ignores_case()
    {
        Assert.True(RackWire.Stored.PropertyNameCaseInsensitive);
    }

    /// <summary>⚠️ والتغذيات camelCase زي الباقي.</summary>
    [Fact]
    public void The_feeds_write_camel_case()
    {
        string json = JsonSerializer.Serialize(
            new Payload(DateTime.UtcNow, null), RackWire.Downstream);

        Assert.Contains("\"at\":", json);
        Assert.DoesNotContain("\"At\":", json);
    }

    /// <summary>
    /// ⚠️ <b>والشكل ثابت في مكان واحد</b> — مش مكتوب في كل محوّل.
    /// </summary>
    [Fact]
    public void The_wire_format_is_declared_once()
    {
        Assert.Equal("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", UtcDateTimeConverter.Format);
    }

    /// <summary>
    /// 🔴 <b>والمحوّل الاختياري تأكيد مش إصلاح — والفحص ده هو
    /// الدليل.</b>
    ///
    /// <para>كان فيه تعليق في المشروع بيقول إن
    /// <c>JsonConverter&lt;DateTime&gt;</c> «مابينطبقش على
    /// <c>DateTime?</c>» — وده غلط: <c>System.Text.Json</c> بيلفّ
    /// المحوّل تلقائياً (<c>NullableConverterFactory</c>). والفرق
    /// مهم: القديم بيسجّل محوّل <b>واحد</b> وتواريخه الاختيارية
    /// بتخرج بـ<c>Z</c> صح، فلو فهمنا الميكانيكا غلط كنا هنفتكر إن
    /// القديم فيه عطل ونروح نـ«نصلّحه».</para>
    /// </summary>
    [Fact]
    public void One_converter_already_covers_nullable_dates()
    {
        var single = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        single.Converters.Add(new UtcDateTimeConverter());

        string json = JsonSerializer.Serialize(
            new { at = (DateTime?)new DateTime(2026, 10, 4, 7, 12, 2, DateTimeKind.Utc) },
            single);

        Assert.Equal("{\"at\":\"2026-10-04T07:12:02.000Z\"}", json);
    }

    /// <summary>
    /// ⚠️ <b>وبنسجّل الاتنين بردو.</b> الضمانة تبقى في كودنا مش في
    /// تفاصيل المكتبة — ولو STJ غيّر سلوكه بكرة، السطر اللي فوق هو
    /// اللي بيقع، مش سطح الراكة كله.
    /// </summary>
    [Fact]
    public void The_feeds_register_both_converters_anyway()
    {
        Assert.Contains(
            RackWire.Downstream.Converters, c => c is UtcDateTimeConverter);

        Assert.Contains(
            RackWire.Downstream.Converters, c => c is NullableUtcDateTimeConverter);
    }
}
