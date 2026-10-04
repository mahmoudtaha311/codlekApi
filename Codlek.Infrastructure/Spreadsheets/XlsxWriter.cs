using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Codlek.Application.Interfaces;
using Codlek.Core.Spreadsheets;

namespace Codlek.Infrastructure.Spreadsheets;

/// <summary>
/// كاتب ملفات إكسل — <b>مكتوب بالإيد، ومن غير أي حزمة جديدة</b>.
///
/// <para>🔴 <b>ليه مش EPPlus.</b> الراكة بتستعمل <c>EPPlusFree</c>،
/// والسؤال الطبيعي هو ليه مانعملش نفس الحاجة هنا. سببين:</para>
///
/// <list type="number">
///   <item><b>الرخصة.</b> <c>epplusfree</c> رخصته LGPL-3.0. إدخال
///   LGPL في منتج تجاري قرار بيتاخد بالعقل، مش أثر جانبي لاستعادة
///   حزم.</item>
///
///   <item><b><c>ExcelPackage</c> شجرة كاملة في الذاكرة.</b> بيبني
///   الملف كله جوّه الرام قبل ما يكتب بايت واحد — وده بيتعارض
///   مباشرةً مع إن التصدير هنا بيعدّي على آلاف الصفوف.</item>
/// </list>
///
/// <para>⚠️ <b>وسبب تالت كان مكتوب في القديم وطلع غلط، فمتكرروش:</b>
/// إن <c>System.Drawing.Common</c> بترمي على لينكس. الموقع بيترفع
/// بـWebDeploy على ويندوز/IIS — يعني المشكلة دي مكانتش هتحصل أصلاً
/// على الهدف الحقيقي.</para>
///
/// <para>البديل ده <c>ZipArchive</c> + <c>XmlWriter</c>، والاتنين
/// جوّه <c>Microsoft.NETCore.App</c>.</para>
/// </summary>
public sealed class XlsxWriter : IWorkbookWriter
{
    public string ContentType =>
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public string Extension => "xlsx";

    /// <summary>
    /// 🔴 الصفحات بتتكتب الأول و<c>workbook.xml</c> في الآخر: قايمة
    /// الصفحات مابتبقاش معروفة غير بعد ما تعدّي عليهم كلهم،
    /// و<c>ZipArchive</c> بيسمح بترتيب زي ده.
    /// </summary>
    public void Write(Stream output, IReadOnlyList<Sheet> sheets)
    {
        if (sheets.Count == 0)
            throw new ArgumentException("لازم صفحة واحدة على الأقل.", nameof(sheets));

        string[] names = SheetNames(sheets);

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        for (int i = 0; i < sheets.Count; i++)
            WriteSheet(zip, $"xl/worksheets/sheet{i + 1}.xml", sheets[i]);

        WriteContentTypes(zip, sheets.Count);
        WriteRootRels(zip);
        WriteWorkbook(zip, names);
        WriteWorkbookRels(zip, sheets.Count);
        WriteStyles(zip);
    }

    // =================================================================
    //  أسماء الصفحات
    // =================================================================

    /// <summary>
    /// أسماء صالحة ومتفرّدة. <b>دالة نقية.</b>
    ///
    /// <para>🔴 إكسل بيرفض يفتح الملف <b>كله</b> — مش بيصلّح الاسم —
    /// لو الاسم فيه <c>[ ] : * ? / \</c>، أو أطول من ٣١ حرف، أو
    /// فاضي، أو متكرّر. وأسماء الفنيين جاية من إدخال المستخدم، فأي
    /// واحدة من دول ممكن تحصل فعلاً.</para>
    /// </summary>
    internal static string[] SheetNames(IReadOnlyList<Sheet> sheets)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new string[sheets.Count];

        for (int i = 0; i < sheets.Count; i++)
        {
            string name = Sanitize(sheets[i].Name);
            if (name.Length == 0) name = $"صفحة {i + 1}";

            string candidate = name;
            int suffix = 2;

            while (!used.Add(candidate))
            {
                string tail = $" ({suffix++})";
                candidate = name[..Math.Min(name.Length, 31 - tail.Length)] + tail;
            }

            result[i] = candidate;
        }

        return result;
    }

    internal static string Sanitize(string raw)
    {
        var sb = new StringBuilder();

        foreach (char c in (raw ?? "").Trim())
        {
            if (c is '[' or ']' or ':' or '*' or '?' or '/' or '\\') continue;
            if (char.IsControl(c)) continue;
            sb.Append(c);
            if (sb.Length == 31) break;
        }

        // ⚠️ إكسل بيرفض الاسم لو بادئ أو منتهي بعلامة اقتباس مفردة.
        return sb.ToString().Trim().Trim('\'');
    }

    // =================================================================
    //  الصفحة
    // =================================================================

    private static void WriteSheet(ZipArchive zip, string path, Sheet sheet)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
        using var xml = Writer(stream);

        xml.WriteStartElement("worksheet", Ns);
        xml.WriteAttributeString("xmlns", "r", null, RelNs);

        // 🔴 rightToLeft: العمود A بيبقى على اليمين. من غيرها الشيت
        // بيفتح من الشمال والعربي جوّاه بيبان مبعثر للي بيقراه.
        xml.WriteStartElement("sheetViews");
        xml.WriteStartElement("sheetView");
        xml.WriteAttributeString("rightToLeft", "1");
        xml.WriteAttributeString("workbookViewId", "0");

        // تجميد صف العناوين — الجداول هنا بتطول.
        xml.WriteStartElement("pane");
        xml.WriteAttributeString("ySplit", "1");
        xml.WriteAttributeString("topLeftCell", "A2");
        xml.WriteAttributeString("activePane", "bottomLeft");
        xml.WriteAttributeString("state", "frozen");
        xml.WriteEndElement();

        xml.WriteEndElement(); // sheetView
        xml.WriteEndElement(); // sheetViews

        xml.WriteStartElement("cols");
        for (int i = 0; i < sheet.Columns.Length; i++)
        {
            xml.WriteStartElement("col");
            xml.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("width", Num(sheet.Columns[i].Width));
            xml.WriteAttributeString("customWidth", "1");
            xml.WriteEndElement();
        }
        xml.WriteEndElement();

        xml.WriteStartElement("sheetData");

        xml.WriteStartElement("row");
        xml.WriteAttributeString("r", "1");
        for (int i = 0; i < sheet.Columns.Length; i++)
            WriteCell(xml, i, 1, sheet.Columns[i].Header, StyleHeader);
        xml.WriteEndElement();

        int rowNumber = 1;

        foreach (object?[] row in sheet.Rows)
        {
            rowNumber++;
            xml.WriteStartElement("row");
            xml.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < row.Length && i < sheet.Columns.Length; i++)
                WriteValue(xml, i, rowNumber, row[i]);

            xml.WriteEndElement();
        }

        xml.WriteEndElement(); // sheetData

        // فلتر تلقائي على صف العناوين — المدير بيفرز من جوّه إكسل.
        if (sheet.Columns.Length > 0)
        {
            xml.WriteStartElement("autoFilter");
            xml.WriteAttributeString("ref", $"A1:{ColumnName(sheet.Columns.Length - 1)}{rowNumber}");
            xml.WriteEndElement();
        }

        xml.WriteEndElement(); // worksheet
        xml.WriteEndDocument();
    }

    private static void WriteValue(XmlWriter xml, int column, int row, object? value)
    {
        switch (value)
        {
            case null:
                return;

            case string s when s.Length == 0:
                return;

            case string s:
                WriteCell(xml, column, row, s, StyleDefault);
                return;

            case bool b:
                WriteCell(xml, column, row, b ? "نعم" : "لأ", StyleDefault);
                return;

            case DateTime dt:
                WriteNumber(xml, column, row, Serial(dt),
                    dt.TimeOfDay == TimeSpan.Zero ? StyleDate : StyleDateTime);
                return;

            case int i:
                WriteNumber(xml, column, row, i, StyleDefault);
                return;

            case long l:
                WriteNumber(xml, column, row, l, StyleDefault);
                return;

            case double d:
                WriteNumber(xml, column, row, d, StyleNumber);
                return;

            case decimal m:
                WriteNumber(xml, column, row, (double)m, StyleNumber);
                return;

            default:
                WriteCell(xml, column, row, value.ToString() ?? "", StyleDefault);
                return;
        }
    }

    /// <summary>
    /// نص جوّه الخلية — <b><c>inlineStr</c></b> مش جدول نصوص مشترك.
    ///
    /// <para>⚠️ جدول النصوص المشترك بيوفّر حجم، بس بيتطلب إن الملف
    /// كله يتلم في الذاكرة الأول عشان تعرف كل النصوص المتفرّدة —
    /// وده بالظبط اللي بنهرب منه.</para>
    /// </summary>
    private static void WriteCell(XmlWriter xml, int column, int row, string text, int style)
    {
        xml.WriteStartElement("c");
        xml.WriteAttributeString("r", $"{ColumnName(column)}{row}");

        if (style != StyleDefault)
            xml.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));

        xml.WriteAttributeString("t", "inlineStr");

        xml.WriteStartElement("is");
        xml.WriteStartElement("t");

        // ⚠️ المسافات في الأول والآخر بتضيع من غير xml:space.
        xml.WriteAttributeString("xml", "space", null, "preserve");
        xml.WriteString(Clean(text));

        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void WriteNumber(XmlWriter xml, int column, int row, double value, int style)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        xml.WriteStartElement("c");
        xml.WriteAttributeString("r", $"{ColumnName(column)}{row}");

        if (style != StyleDefault)
            xml.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));

        xml.WriteStartElement("v");
        xml.WriteString(Num(value));
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    /// <summary>
    /// بيشيل الحروف اللي XML 1.0 مابيقبلهاش. <b>دالة نقية.</b>
    ///
    /// <para>🔴 ملاحظات الفنيين وأسباب الإيقاف نص حر جاي من الميدان،
    /// وفيه منه اللي بيتلزق من برامج تانية ومعاه حروف تحكّم. حرف
    /// واحد منهم بيخلّي إكسل يقول «الملف تالف» — ومفيش أي إشارة
    /// لمكان المشكلة.</para>
    /// </summary>
    internal static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var sb = new StringBuilder(text.Length);

        foreach (char c in text)
        {
            if (c is '\t' or '\n' or '\r') { sb.Append(c); continue; }
            if (c < 0x20) continue;
            if (c is >= (char)0xD800 and <= (char)0xDFFF) continue; // نص نصف حرف
            if (c is (char)0xFFFE or (char)0xFFFF) continue;
            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// تاريخ إكسل: أيام من ١٨٩٩-١٢-٣٠. <b>دالة نقية.</b>
    ///
    /// <para>⚠️ المبدأ ١٨٩٩-١٢-٣٠ مش ١٩٠٠-٠١-٠١، عشان إكسل بيعتبر
    /// ١٩٠٠ سنة كبيسة وهي مش كده. الفرق يوم واحد، وبيبان في كل
    /// التواريخ.</para>
    /// </summary>
    internal static double Serial(DateTime value) =>
        (value - new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified)).TotalDays;

    /// <summary>A, B, … Z, AA, AB … <b>دالة نقية.</b></summary>
    internal static string ColumnName(int index)
    {
        var sb = new StringBuilder();

        for (int i = index; i >= 0; i = (i / 26) - 1)
            sb.Insert(0, (char)('A' + (i % 26)));

        return sb.ToString();
    }

    private static string Num(double value) =>
        value.ToString("0.##########", CultureInfo.InvariantCulture);

    // =================================================================
    //  الأجزاء الثابتة
    // =================================================================

    private const int StyleDefault = 0;
    private const int StyleHeader = 1;
    private const int StyleDateTime = 2;
    private const int StyleDate = 3;
    private const int StyleNumber = 4;

    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static XmlWriter Writer(Stream stream) =>
        XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            CloseOutput = false,
        });

    private static void WriteContentTypes(ZipArchive zip, int sheetCount)
    {
        using var stream = zip.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal).Open();
        using var xml = Writer(stream);

        xml.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");

        Default(xml, "rels", "application/vnd.openxmlformats-package.relationships+xml");
        Default(xml, "xml", "application/xml");

        Override(xml, "/xl/workbook.xml",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        Override(xml, "/xl/styles.xml",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");

        for (int i = 1; i <= sheetCount; i++)
            Override(xml, $"/xl/worksheets/sheet{i}.xml",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");

        xml.WriteEndElement();
        xml.WriteEndDocument();

        static void Default(XmlWriter w, string ext, string type)
        {
            w.WriteStartElement("Default");
            w.WriteAttributeString("Extension", ext);
            w.WriteAttributeString("ContentType", type);
            w.WriteEndElement();
        }

        static void Override(XmlWriter w, string part, string type)
        {
            w.WriteStartElement("Override");
            w.WriteAttributeString("PartName", part);
            w.WriteAttributeString("ContentType", type);
            w.WriteEndElement();
        }
    }

    private static void WriteRootRels(ZipArchive zip)
    {
        using var stream = zip.CreateEntry("_rels/.rels", CompressionLevel.Optimal).Open();
        using var xml = Writer(stream);

        xml.WriteStartElement("Relationships", PkgRelNs);
        xml.WriteStartElement("Relationship");
        xml.WriteAttributeString("Id", "rId1");
        xml.WriteAttributeString("Type", RelNs + "/officeDocument");
        xml.WriteAttributeString("Target", "xl/workbook.xml");
        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    private static void WriteWorkbook(ZipArchive zip, string[] names)
    {
        using var stream = zip.CreateEntry("xl/workbook.xml", CompressionLevel.Optimal).Open();
        using var xml = Writer(stream);

        xml.WriteStartElement("workbook", Ns);
        xml.WriteAttributeString("xmlns", "r", null, RelNs);
        xml.WriteStartElement("sheets");

        for (int i = 0; i < names.Length; i++)
        {
            xml.WriteStartElement("sheet");
            xml.WriteAttributeString("name", names[i]);
            xml.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("r", "id", RelNs, $"rId{i + 1}");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    private static void WriteWorkbookRels(ZipArchive zip, int sheetCount)
    {
        using var stream = zip.CreateEntry("xl/_rels/workbook.xml.rels", CompressionLevel.Optimal).Open();
        using var xml = Writer(stream);

        xml.WriteStartElement("Relationships", PkgRelNs);

        for (int i = 1; i <= sheetCount; i++)
        {
            xml.WriteStartElement("Relationship");
            xml.WriteAttributeString("Id", $"rId{i}");
            xml.WriteAttributeString("Type", RelNs + "/worksheet");
            xml.WriteAttributeString("Target", $"worksheets/sheet{i}.xml");
            xml.WriteEndElement();
        }

        // ⚠️ معرّف الأنماط لازم يبقى بعد الصفحات عشان مايتصادمش معاهم.
        xml.WriteStartElement("Relationship");
        xml.WriteAttributeString("Id", $"rId{sheetCount + 1}");
        xml.WriteAttributeString("Type", RelNs + "/styles");
        xml.WriteAttributeString("Target", "styles.xml");
        xml.WriteEndElement();

        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    /// <summary>
    /// الأنماط — العناوين تقيلة على خلفية، والتواريخ بصيغة مقروءة.
    ///
    /// <para>⚠️ الترتيب هنا <b>حمّال</b>: أرقام <c>StyleHeader</c>
    /// وإخواتها هي فهارس في <c>cellXfs</c> بالترتيب. أي إضافة في
    /// النص لازم يتقابلها ثابت.</para>
    /// </summary>
    private static void WriteStyles(ZipArchive zip)
    {
        using var stream = zip.CreateEntry("xl/styles.xml", CompressionLevel.Optimal).Open();
        using var xml = Writer(stream);

        xml.WriteStartElement("styleSheet", Ns);

        xml.WriteStartElement("numFmts");
        xml.WriteAttributeString("count", "2");
        NumFmt(xml, 164, "yyyy\\-mm\\-dd\\ hh:mm");
        NumFmt(xml, 165, "yyyy\\-mm\\-dd");
        xml.WriteEndElement();

        xml.WriteStartElement("fonts");
        xml.WriteAttributeString("count", "2");
        Font(xml, bold: false);
        Font(xml, bold: true);
        xml.WriteEndElement();

        xml.WriteStartElement("fills");
        xml.WriteAttributeString("count", "3");
        PatternFill(xml, "none", null);
        PatternFill(xml, "gray125", null);
        PatternFill(xml, "solid", "FFEAEFF7");
        xml.WriteEndElement();

        xml.WriteStartElement("borders");
        xml.WriteAttributeString("count", "1");
        xml.WriteStartElement("border");
        xml.WriteEndElement();
        xml.WriteEndElement();

        xml.WriteStartElement("cellStyleXfs");
        xml.WriteAttributeString("count", "1");
        xml.WriteStartElement("xf");
        xml.WriteEndElement();
        xml.WriteEndElement();

        xml.WriteStartElement("cellXfs");
        xml.WriteAttributeString("count", "5");

        Xf(xml, fontId: 0, fillId: 0, numFmtId: 0);   // 0 — عادي
        Xf(xml, fontId: 1, fillId: 2, numFmtId: 0);   // 1 — عنوان
        Xf(xml, fontId: 0, fillId: 0, numFmtId: 164); // 2 — تاريخ بوقت
        Xf(xml, fontId: 0, fillId: 0, numFmtId: 165); // 3 — تاريخ
        Xf(xml, fontId: 0, fillId: 0, numFmtId: 2);   // 4 — رقم بخانتين

        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndDocument();

        static void NumFmt(XmlWriter w, int id, string code)
        {
            w.WriteStartElement("numFmt");
            w.WriteAttributeString("numFmtId", id.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("formatCode", code);
            w.WriteEndElement();
        }

        static void Font(XmlWriter w, bool bold)
        {
            w.WriteStartElement("font");
            if (bold) { w.WriteStartElement("b"); w.WriteEndElement(); }
            w.WriteStartElement("sz");
            w.WriteAttributeString("val", "11");
            w.WriteEndElement();
            w.WriteStartElement("name");
            w.WriteAttributeString("val", "Calibri");
            w.WriteEndElement();
            w.WriteEndElement();
        }

        static void PatternFill(XmlWriter w, string pattern, string? argb)
        {
            w.WriteStartElement("fill");
            w.WriteStartElement("patternFill");
            w.WriteAttributeString("patternType", pattern);

            if (argb != null)
            {
                w.WriteStartElement("fgColor");
                w.WriteAttributeString("rgb", argb);
                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndElement();
        }

        static void Xf(XmlWriter w, int fontId, int fillId, int numFmtId)
        {
            w.WriteStartElement("xf");
            w.WriteAttributeString("numFmtId", numFmtId.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("fillId", fillId.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("borderId", "0");
            w.WriteAttributeString("xfId", "0");
            if (numFmtId != 0) w.WriteAttributeString("applyNumberFormat", "1");
            if (fontId != 0) w.WriteAttributeString("applyFont", "1");
            if (fillId != 0) w.WriteAttributeString("applyFill", "1");
            w.WriteEndElement();
        }
    }
}
