using System.IO.Compression;
using System.Xml.Linq;
using Codlek.Core.Export;
using Codlek.Core.Spreadsheets;
using Codlek.Infrastructure.Spreadsheets;

namespace Codlek.Tests;

/// <summary>
/// كاتب الإكسل.
///
/// <para>🔴 <b>والفحوص دي بتفتح الملف فعلاً، مش بتقيس دوال نقية
/// وبس.</b> صيغة OOXML بتفشل بشكل واحد: إكسل بيقول «الملف تالف»
/// ومابيقولش فين. فالدوال النقية ممكن تكون كلها صح والملف مايفتحش
/// لأن جزء ناقص أو معرّف علاقة متصادم.</para>
/// </summary>
public class XlsxWriterTests
{
    private static readonly SheetColumn[] TwoColumns =
        [new("الكود", 12), new("الاسم", 20)];

    private static byte[] Bytes(params Sheet[] sheets)
    {
        var buffer = new MemoryStream();
        new XlsxWriter().Write(buffer, sheets);
        return buffer.ToArray();
    }

    private static ZipArchive Open(byte[] bytes) =>
        new(new MemoryStream(bytes), ZipArchiveMode.Read);

    private static XDocument Part(ZipArchive zip, string path)
    {
        using var stream = zip.GetEntry(path)!.Open();
        return XDocument.Load(stream);
    }

    private static readonly XNamespace Main =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    // =================================================================
    //  الملف ككل
    // =================================================================

    /// <summary>
    /// 🔴 <b>كل جزء لازم يكون موجود.</b> إكسل بيرفض الملف كله لو
    /// واحد ناقص — ومابيقولش أنهي واحد.
    /// </summary>
    [Fact]
    public void A_written_file_has_every_part_excel_requires()
    {
        byte[] bytes = Bytes(new Sheet("الأجهزة", TwoColumns, [["DV-1", "لاب"]]));

        using var zip = Open(bytes);
        var names = zip.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("[Content_Types].xml", names);
        Assert.Contains("_rels/.rels", names);
        Assert.Contains("xl/workbook.xml", names);
        Assert.Contains("xl/_rels/workbook.xml.rels", names);
        Assert.Contains("xl/styles.xml", names);
        Assert.Contains("xl/worksheets/sheet1.xml", names);
    }

    /// <summary>
    /// 🔴 <b>معرّف الأنماط بعد الصفحات.</b> لو اتصادم مع معرّف
    /// صفحة، إكسل بيفتح الملف بأنماط غلط أو بيرفضه.
    /// </summary>
    [Fact]
    public void The_style_relationship_id_does_not_collide_with_a_sheet()
    {
        byte[] bytes = Bytes(
            new Sheet("واحد", TwoColumns, []),
            new Sheet("اتنين", TwoColumns, []),
            new Sheet("تلاتة", TwoColumns, []));

        using var zip = Open(bytes);
        var rels = Part(zip, "xl/_rels/workbook.xml.rels").Root!.Elements().ToList();

        var ids = rels.Select(r => (string)r.Attribute("Id")!).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(4, ids.Count);

        string styles = (string)rels.Single(
            r => ((string)r.Attribute("Target")!) == "styles.xml").Attribute("Id")!;

        Assert.Equal("rId4", styles);
    }

    /// <summary>
    /// ⚠️ كل صفحة لازم يبقى ليها <c>Override</c> في
    /// <c>[Content_Types]</c> — ناقصة واحدة = ملف تالف.
    /// </summary>
    [Fact]
    public void Every_sheet_is_declared_in_the_content_types()
    {
        byte[] bytes = Bytes(
            new Sheet("واحد", TwoColumns, []),
            new Sheet("اتنين", TwoColumns, []));

        using var zip = Open(bytes);
        var parts = Part(zip, "[Content_Types].xml").Root!
            .Elements().Where(e => e.Name.LocalName == "Override")
            .Select(e => (string)e.Attribute("PartName")!)
            .ToList();

        Assert.Contains("/xl/worksheets/sheet1.xml", parts);
        Assert.Contains("/xl/worksheets/sheet2.xml", parts);
        Assert.Contains("/xl/styles.xml", parts);
        Assert.Contains("/xl/workbook.xml", parts);
    }

    [Fact]
    public void A_workbook_with_no_sheet_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => Bytes());

        Assert.Contains("صفحة واحدة", error.Message);
    }

    // =================================================================
    //  اتجاه الصفحة والعناوين
    // =================================================================

    /// <summary>
    /// 🔴 <b>من غير <c>rightToLeft</c> الشيت بيفتح من الشمال
    /// والعربي جوّاه بيبان مبعثر.</b>
    /// </summary>
    [Fact]
    public void The_sheet_opens_right_to_left_with_a_frozen_header()
    {
        byte[] bytes = Bytes(new Sheet("الأجهزة", TwoColumns, [["DV-1", "لاب"]]));

        using var zip = Open(bytes);
        var view = Part(zip, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "sheetView").Single();

        Assert.Equal("1", (string)view.Attribute("rightToLeft")!);

        var pane = view.Element(Main + "pane")!;

        Assert.Equal("1", (string)pane.Attribute("ySplit")!);
        Assert.Equal("frozen", (string)pane.Attribute("state")!);
    }

    [Fact]
    public void The_header_row_carries_the_column_titles()
    {
        byte[] bytes = Bytes(new Sheet("الأجهزة", TwoColumns, []));

        using var zip = Open(bytes);
        var cells = Part(zip, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "row").First()
            .Descendants(Main + "t").Select(t => t.Value).ToList();

        Assert.Equal(["الكود", "الاسم"], cells);
    }

    // =================================================================
    //  القيم
    // =================================================================

    /// <summary>
    /// ⚠️ <b>المسافات في الأول والآخر بتضيع من غير
    /// <c>xml:space="preserve"</c>.</b>
    ///
    /// <para>🔴 <b>والفحص بيقيس <u>السمة</u> مش النص — ودي نسخة
    /// تانية.</b> النسخة الأولى كانت بتقرا قيمة الخلية وبتقارنها
    /// بـ«  مسافات  »، وعدّت وهي مش بتقيس حاجة: <c>XDocument</c>
    /// بيحفظ المسافات في عقد النص <b>على أي حال</b>، فشيل السمة
    /// مابيبانش فيه. (اتجرّب: التحوير <b>نجا</b>.)</para>
    ///
    /// <para>⚠️ و<c>xml:space</c> تعليمة لـ<b>إكسل</b> مش للمحلّل —
    /// فالحاجة الوحيدة اللي ينفع تتقاس من غير إكسل هي إن السمة
    /// اتكتبت فعلاً.</para>
    /// </summary>
    [Fact]
    public void A_cell_with_edge_spaces_declares_xml_space_preserve()
    {
        byte[] bytes = Bytes(new Sheet("ورقة", TwoColumns, [["  مسافات  ", "x"]]));

        using var zip = Open(bytes);
        var text = Part(zip, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "row").Skip(1).First()
            .Descendants(Main + "t").First();

        var space = text.Attribute(XNamespace.Xml + "space");

        Assert.NotNull(space);
        Assert.Equal("preserve", space.Value);

        // ⚠️ والنص نفسه كامل — ده بيقيس `Clean` مش السمة.
        Assert.Equal("  مسافات  ", text.Value);
    }

    /// <summary>
    /// ⚠️ والسمة على <b>كل</b> خلية نص — مش على اللي فيها مسافات
    /// بس. التفريق كان هيخلّي الكاتب يفحص كل نص قبل ما يكتبه،
    /// والمكسب صفر.
    /// </summary>
    [Fact]
    public void Every_text_cell_declares_xml_space_preserve()
    {
        byte[] bytes = Bytes(new Sheet("ورقة", TwoColumns, [["عادي", "x"]]));

        using var zip = Open(bytes);
        var texts = Part(zip, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "t")
            .ToList();

        Assert.NotEmpty(texts);
        Assert.All(texts, t =>
            Assert.Equal("preserve", t.Attribute(XNamespace.Xml + "space")?.Value));
    }

    /// <summary>
    /// 🔴 <b>حرف تحكّم واحد بيخلّي إكسل يقول «الملف تالف»</b> —
    /// ومفيش أي إشارة لمكان المشكلة. وملاحظات الفنيين نص حر جاي من
    /// الميدان وبيتلزق من برامج تانية.
    /// </summary>
    [Fact]
    public void A_control_character_from_the_field_does_not_corrupt_the_file()
    {
        string dirty = "ملاحظة\u0007 فيها\u0000 حروف تحكّم";

        byte[] bytes = Bytes(new Sheet("ورقة", TwoColumns, [[dirty, "x"]]));

        // ⚠️ أهم حاجة: الملف نفسه بيتقرا بعدها.
        using var zip = Open(bytes);
        var value = Part(zip, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "row").Skip(1).First()
            .Descendants(Main + "t").First().Value;

        Assert.Equal("ملاحظة فيها حروف تحكّم", value);
    }

    /// <summary>⚠️ والسطر الجديد والتاب بيفضلوا — دول نص حقيقي.</summary>
    [Fact]
    public void Newlines_and_tabs_are_kept()
    {
        Assert.Equal("سطر\nتاني", XlsxWriter.Clean("سطر\nتاني"));
        Assert.Equal("أ\tب", XlsxWriter.Clean("أ\tب"));
    }

    [Fact]
    public void An_empty_cell_is_written_as_nothing_at_all()
    {
        byte[] bytes = Bytes(new Sheet("ورقة", TwoColumns, [["", "موجود"]]));

        using var zip = Open(bytes);
        var cells = Part(zip, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "row").Skip(1).First()
            .Elements(Main + "c").ToList();

        // ⚠️ خلية واحدة بس، وعلى العمود B — الفاضية مش بتتكتب خالص.
        var cell = Assert.Single(cells);

        Assert.Equal("B2", (string)cell.Attribute("r")!);
    }

    [Fact]
    public void A_boolean_reads_in_arabic()
    {
        byte[] bytes = Bytes(new Sheet("ورقة", TwoColumns, [[true, false]]));

        using var zip = Open(bytes);
        var values = Part(zip, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "row").Skip(1).First()
            .Descendants(Main + "t").Select(t => t.Value).ToList();

        Assert.Equal(["نعم", "لأ"], values);
    }

    /// <summary>
    /// 🔴 <b>الرقم بيتكتب كرقم مش نص.</b> لو اتكتب نص، المدير
    /// مايقدرش يجمع عمود في إكسل — وده نص سبب وجود الملف.
    /// </summary>
    [Fact]
    public void Numbers_are_written_as_numbers()
    {
        byte[] bytes = Bytes(new Sheet("ورقة", TwoColumns, [[42, 3.5]]));

        using var zip = Open(bytes);
        var cells = Part(zip, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "row").Skip(1).First()
            .Elements(Main + "c").ToList();

        Assert.All(cells, c => Assert.Null(c.Attribute("t")));
        Assert.Equal(["42", "3.5"],
            cells.Select(c => c.Element(Main + "v")!.Value));
    }

    // =================================================================
    //  التاريخ
    // =================================================================

    /// <summary>
    /// ⚠️ <b>المبدأ ١٨٩٩-١٢-٣٠ مش ١٩٠٠-٠١-٠١</b>، عشان إكسل
    /// بيعتبر ١٩٠٠ سنة كبيسة وهي مش كده. الفرق يوم واحد، وبيبان في
    /// <b>كل</b> التواريخ.
    ///
    /// <para>⚠️ <b>والأرقام دي متأكَّد منها من برّه</b> (حساب تواريخ
    /// مستقل)، مش مأخوذة من الدالة نفسها. ١٩٠٠-٠٣-٠١ = ٦١ و
    /// ٢٠٠٠-٠١-٠١ = ٣٦٥٢٦ هما نفس اللي إكسل بيعرضهم.</para>
    ///
    /// <para>⚠️ <b>وقبل ١٩٠٠-٠٣-٠١ الاتفاق ده بيفرق عن إكسل بيوم</b>
    /// — بسبب ٢٩ فبراير ١٩٠٠ الوهمي اللي إكسل بيعدّه. مش مشكلة
    /// عملية: مفيش فحص لاب بتاريخ ١٨٩٩.</para>
    /// </summary>
    [Theory]
    [InlineData(1900, 3, 1, 61)]
    [InlineData(2000, 1, 1, 36526)]
    [InlineData(2026, 10, 4, 46299)]
    public void The_excel_date_serial_uses_the_1899_epoch(int y, int m, int d, double expected)
    {
        Assert.Equal(expected, XlsxWriter.Serial(new DateTime(y, m, d)));
    }

    /// <summary>⚠️ والوقت كسر من اليوم — نص الليل بالظبط = .5</summary>
    [Fact]
    public void A_time_of_day_is_the_fraction_of_the_serial()
    {
        double noon = XlsxWriter.Serial(new DateTime(2026, 10, 4, 12, 0, 0));
        double midnight = XlsxWriter.Serial(new DateTime(2026, 10, 4));

        Assert.Equal(0.5, noon - midnight, 6);
    }

    /// <summary>
    /// ⚠️ تاريخ من غير وقت بياخد نمط «يوم» وبوقت بياخد «يوم وساعة»
    /// — عشان المدير مايشوفش <c>00:00</c> على كل صف.
    /// </summary>
    [Fact]
    public void A_date_without_a_time_uses_the_short_format()
    {
        byte[] bytes = Bytes(new Sheet("ورقة", TwoColumns,
            [[new DateTime(2026, 10, 4), new DateTime(2026, 10, 4, 9, 30, 0)]]));

        using var zip = Open(bytes);
        var styles = Part(zip, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "row").Skip(1).First()
            .Elements(Main + "c").Select(c => (string)c.Attribute("s")!).ToList();

        Assert.Equal(["3", "2"], styles);
    }

    // =================================================================
    //  أسماء الصفحات
    // =================================================================

    /// <summary>
    /// 🔴 <b>إكسل بيرفض يفتح الملف كله</b> — مش بيصلّح الاسم — لو
    /// فيه حرف ممنوع. وأسماء الفنيين جاية من إدخال المستخدم.
    /// </summary>
    [Theory]
    [InlineData("محمد/أحمد", "محمدأحمد")]
    [InlineData("فني [1]", "فني 1")]
    [InlineData("a:b*c?d", "abcd")]
    [InlineData("back\\slash", "backslash")]
    [InlineData("  مسافات  ", "مسافات")]
    [InlineData("'اقتباس'", "اقتباس")]
    public void A_forbidden_character_is_stripped_from_a_sheet_name(string raw, string expected)
    {
        Assert.Equal(expected, XlsxWriter.Sanitize(raw));
    }

    [Fact]
    public void A_sheet_name_is_cut_at_thirty_one_characters()
    {
        string name = XlsxWriter.Sanitize(new string('م', 60));

        Assert.Equal(31, name.Length);
    }

    /// <summary>
    /// 🔴 <b>الاسم المتكرّر بيرفض الملف كمان.</b> وفنيين باسم واحد
    /// حاجة عادية في ورشة.
    /// </summary>
    [Fact]
    public void Duplicate_sheet_names_are_made_unique()
    {
        var names = XlsxWriter.SheetNames([
            new Sheet("محمود", TwoColumns, []),
            new Sheet("محمود", TwoColumns, []),
            new Sheet("محمود", TwoColumns, []),
        ]);

        Assert.Equal(3, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("محمود", names[0]);
    }

    /// <summary>
    /// ⚠️ والاسم الطويل المتكرّر لازم يفضل تحت ٣١ حرف <b>بعد</b>
    /// إضافة الرقم — لو زاد، الملف بيترفض برضه.
    /// </summary>
    [Fact]
    public void A_long_duplicate_name_stays_within_the_limit()
    {
        string tooLong = new('م', 40);

        var names = XlsxWriter.SheetNames([
            new Sheet(tooLong, TwoColumns, []),
            new Sheet(tooLong, TwoColumns, []),
        ]);

        Assert.All(names, n => Assert.True(n.Length <= 31, $"«{n}» طوله {n.Length}"));
        Assert.NotEqual(names[0], names[1]);
    }

    /// <summary>⚠️ واسم فاضي بياخد اسم افتراضي — مش بيترفض.</summary>
    [Fact]
    public void An_empty_sheet_name_gets_a_default()
    {
        var names = XlsxWriter.SheetNames([new Sheet("   ", TwoColumns, [])]);

        Assert.Equal("صفحة 1", names[0]);
    }

    // =================================================================
    //  أسماء الأعمدة
    // =================================================================

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void Column_names_roll_over_like_excel(int index, string expected)
    {
        Assert.Equal(expected, XlsxWriter.ColumnName(index));
    }

    // =================================================================
    //  القص
    // =================================================================

    /// <summary>
    /// 🔴 <b>ملف مقصوص في صمت بيتقري على إنه كل البيانات</b> —
    /// والمدير بيبني عليه قرار جرد.
    /// </summary>
    [Fact]
    public void A_truncated_export_says_so_inside_the_file()
    {
        var rows = Enumerable.Range(0, ExportLimits.MaxRows + 5).ToList();

        var capped = ExportLimits.Capped(rows, i => new object?[] { i });

        Assert.Equal(ExportLimits.MaxRows + 1, capped.Count);
        Assert.Equal(ExportLimits.TruncationNote, capped[^1][0]);
    }

    [Fact]
    public void An_export_that_fits_carries_no_note()
    {
        var capped = ExportLimits.Capped([1, 2, 3], i => new object?[] { i });

        Assert.Equal(3, capped.Count);
        Assert.DoesNotContain(capped, row => Equals(row[0], ExportLimits.TruncationNote));
    }

    /// <summary>
    /// ⚠️ والقايمة اللي فيها بالظبط <c>MaxRows</c> مابتاخدش تحذير
    /// — الحد مش متعدّى.
    /// </summary>
    [Fact]
    public void Exactly_the_cap_is_not_a_truncation()
    {
        var rows = Enumerable.Range(0, ExportLimits.MaxRows).ToList();

        var capped = ExportLimits.Capped(rows, i => new object?[] { i });

        Assert.Equal(ExportLimits.MaxRows, capped.Count);
        Assert.DoesNotContain(capped, row => Equals(row[0], ExportLimits.TruncationNote));
    }
}
