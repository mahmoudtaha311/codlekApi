using Codlek.Core.Devices;
using Codlek.Core.Text;

namespace Codlek.Tests;

/// <summary>
/// قراية كود اللاب من ماسح باركود.
///
/// <para>🔴 <b>والملف ده بيحمي حاجة مادية:</b> آلاف الاستيكرات
/// ملزوقة على لابات شغّالة في الورشة، بأشكال من تلات أجيال مختلفة.
/// أي شكل بيتكسر هنا معناه فني ماسك لاب في إيده والشاشة بتقوله «مش
/// موجود».</para>
/// </summary>
public class DeviceScanTests
{
    // =================================================================
    //  الشكل الأساسي
    // =================================================================

    [Theory]
    [InlineData("LP-00018425")]
    [InlineData("LP-00000001")]
    public void A_plain_code_is_read_as_is(string code)
    {
        Assert.Equal(code, DeviceScan.Parse(code));
    }

    /// <summary>
    /// ⚠️ الماسح بيبعت الصقة (Enter/Tab) كجزء من القيمة، والنسخ
    /// واللصق بيجيب مساحات.
    /// </summary>
    [Theory]
    [InlineData("  LP-00018425  ")]
    [InlineData("LP-00018425\r\n")]
    [InlineData("\tLP-00018425")]
    public void Whitespace_from_the_scanner_is_stripped(string raw)
    {
        Assert.Equal("LP-00018425", DeviceScan.Parse(raw));
    }

    /// <summary>
    /// 🔴 <b>وتكبير الحروف حمّال.</b> البحث التاريخي بيقارن
    /// بـ<c>ArabicText.Normalize</c> اللي <b>مابتكبّرش</b> — فالكود
    /// لازم يطلع من هنا كبير، وإلا المرساة التاريخية مابتتلاقاش.
    /// </summary>
    [Fact]
    public void A_lowercase_code_comes_back_uppercase()
    {
        Assert.Equal("LP-00018425", DeviceScan.Parse("lp-00018425"));
    }

    /// <summary>
    /// 🔴 <b>الفحص اللي بيثبّت الترابط بين الملفين.</b>
    ///
    /// <para>الكود الخارج من <see cref="DeviceScan.Parse"/> لازم
    /// يفضل زي ما هو بعد <c>ArabicText.Normalize</c> — عشان المقارنة
    /// مع عمود <c>NormalizedValue</c> تنفع. ولو <c>Parse</c> سابته
    /// بحروف صغيرة، التطبيع العربي مش هيكبّره والمقارنة تفشل.</para>
    /// </summary>
    [Theory]
    [InlineData("lp-00018425")]
    [InlineData("LP-00018425")]
    [InlineData("  Lp-00018425  ")]
    public void The_parsed_code_survives_arabic_normalisation_unchanged(string raw)
    {
        string? code = DeviceScan.Parse(raw);

        Assert.NotNull(code);
        Assert.Equal(code, ArabicText.Normalize(code));
    }

    // =================================================================
    //  أرقام عربية
    // =================================================================

    /// <summary>
    /// 🔴 <b>اللي بيكتب على لوحة عربية لازم يلاقي اللاب.</b> من غير
    /// التحويل، <c>LP-٠٠٠١٨٤٢٥</c> كان بيعدّي التحقق (لو الشكل
    /// استعمل <c>\d</c>) وبعدين يفشل في القاعدة — «مش موجود» لجهاز
    /// قدامه.
    /// </summary>
    [Theory]
    [InlineData("LP-٠٠٠١٨٤٢٥")]
    [InlineData("LP-۰۰۰۱۸۴۲۵")]
    public void Arabic_indic_and_persian_digits_are_folded(string raw)
    {
        Assert.Equal("LP-00018425", DeviceScan.Parse(raw));
    }

    [Fact]
    public void Folding_digits_leaves_everything_else_alone()
    {
        Assert.Equal("LP-00018425", DeviceScan.FoldDigits("LP-٠٠٠١٨٤٢٥"));
        Assert.Equal("ABC-123", DeviceScan.FoldDigits("ABC-123"));
        Assert.Equal("", DeviceScan.FoldDigits(""));
    }

    // =================================================================
    //  أشكال الليبل القديمة — ملزوقة على أجهزة شغّالة
    // =================================================================

    /// <summary>⚠️ الراكة لمّا ماكانتش مسجّلة كانت بتطبع كده.</summary>
    [Fact]
    public void The_legacy_codlek_device_scheme_is_unwrapped()
    {
        Assert.Equal("LP-00018425", DeviceScan.Parse("codlek:device/LP-00018425"));
        Assert.Equal("LP-00018425", DeviceScan.Parse("CODLEK:DEVICE/lp-00018425"));
    }

    /// <summary>⚠️ ولمّا كانت مسجّلة كانت بتطبع رابط.</summary>
    [Fact]
    public void The_legacy_short_link_is_unwrapped()
    {
        Assert.Equal("LP-00018425", DeviceScan.Parse("https://codlek.runasp.net/d/LP-00018425"));
        Assert.Equal("LP-00018425", DeviceScan.Parse("http://localhost:5097/d/LP-00018425"));
    }

    /// <summary>
    /// 🔴 <b>ومعرّف الفحص بيترفض.</b> ده معرّف داخلي مش هوية لاب،
    /// وقبوله كان بيخلّي ورقة مطبوعة تقول إن اللاب هو الفحص.
    /// </summary>
    [Fact]
    public void A_report_identifier_is_refused()
    {
        Assert.Null(DeviceScan.Parse(
            "codlek:report/7e8d6ed9-41e8-4801-5663-08df21e6b49d"));
    }

    [Fact]
    public void An_unknown_codlek_scheme_is_refused()
    {
        Assert.Null(DeviceScan.Parse("codlek:something/LP-00018425"));
    }

    /// <summary>
    /// 🔴 <b>الرابط بيتقرا بمحلّل روابط، مش بتفتيش عن نص.</b> «كلام
    /// فاضي فيه /d/LP-00018425» مش رابط ومش كود.
    /// </summary>
    [Theory]
    [InlineData("كلام فاضي /d/LP-00018425")]
    [InlineData("https://host/x/d/LP-00018425")]
    [InlineData("https://host/d/LP-00018425/extra")]
    [InlineData("ftp://host/d/LP-00018425")]
    public void Something_that_merely_contains_a_path_is_refused(string raw)
    {
        Assert.Null(DeviceScan.Parse(raw));
    }

    // =================================================================
    //  حمولة الليبل الجديدة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الليبل الجديد بيحط المواصفات ورا الكود</b> عشان أي
    /// موبايل يعرض وصف اللاب من غير نت — والكود أول حقل دايماً.
    /// </summary>
    [Fact]
    public void The_code_is_taken_from_the_first_field_of_a_label_payload()
    {
        Assert.Equal("LP-00000001", DeviceScan.Parse(
            "LP-00000001|LENOVO|Legion 5 15ARH05|Ryzen 5 4600H|16 GB|512 GB SSD"));
    }

    /// <summary>⚠️ واستيكر قديم (كود عاري، مفيش فاصل) بيعدّي زي ما هو.</summary>
    [Fact]
    public void A_bare_code_with_no_separator_still_works()
    {
        Assert.Equal("LP-00000001", DeviceScan.Parse("LP-00000001"));
    }

    /// <summary>
    /// ⚠️ وحمولة فيها الفاصل بس الكود فاضي بتترفض — مش بترجّع أول
    /// حقل فاضي.
    /// </summary>
    [Fact]
    public void A_payload_with_an_empty_first_field_is_refused()
    {
        Assert.Null(DeviceScan.Parse("|LENOVO|Legion 5"));
    }

    // =================================================================
    //  أكواد المخزن
    // =================================================================

    /// <summary>
    /// 🔴 <b>المخزن بيطبع الباركود بتاعه على اللاب قبل الفحص</b> —
    /// فالكود بقى بتاعهم، ومالوش شكل إحنا بنفرضه.
    /// </summary>
    [Theory]
    [InlineData("WH-4821")]
    [InlineData("A1B2C3")]
    [InlineData("STORE_99")]
    [InlineData("X1Z")]
    public void A_warehouse_code_is_accepted(string code)
    {
        Assert.Equal(code, DeviceScan.Parse(code));
    }

    /// <summary>
    /// ⚠️ <b>والشكل ضيّق عن قصد:</b> ده مش تحقّق من صحة ترقيم
    /// المخزن — ده حاجز ضد إن أي نص عشوائي يتحوّل لاستعلام قاعدة.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AB")]
    [InlineData("-LP-00018425")]
    [InlineData("LP-00018425-")]
    [InlineData("LP 00018425")]
    [InlineData("لاب-١٢٣")]
    [InlineData("SELECT * FROM Devices")]
    [InlineData("{\"code\":\"LP-00018425\"}")]
    public void Anything_that_is_not_a_code_shape_is_refused(string? raw)
    {
        Assert.Null(DeviceScan.Parse(raw));
    }

    [Fact]
    public void Null_is_refused()
    {
        Assert.Null(DeviceScan.Parse(null));
    }

    /// <summary>
    /// ⚠️ وكود أطول من ٢٠ محرف بيترفض — السقف ده بيمنع نص ضخم من
    /// الوصول للقاعدة.
    /// </summary>
    [Fact]
    public void A_code_longer_than_twenty_characters_is_refused()
    {
        Assert.Null(DeviceScan.Parse(new string('A', 21)));
        Assert.Equal(new string('A', 20), DeviceScan.Parse(new string('A', 20)));
    }

    /// <summary>
    /// ⚠️ ومدخل ضخم بيترفض من غير أي شغل — قبل تحويل الأرقام وقبل
    /// تحليل الروابط.
    /// </summary>
    [Fact]
    public void A_huge_input_is_refused_outright()
    {
        Assert.Null(DeviceScan.Parse(new string('A', 401)));
    }

    /// <summary>
    /// 🔴 <b>وحمولة ليبل طويلة بتعدّي</b> — المواصفات بتخلّي المدخل
    /// أطول من الكود بكتير، والسقف لازم يسمح بيها.
    /// </summary>
    [Fact]
    public void A_long_label_payload_still_yields_its_code()
    {
        string payload = "LP-00000001|" + new string('X', 300);

        Assert.Equal("LP-00000001", DeviceScan.Parse(payload));
    }
}
