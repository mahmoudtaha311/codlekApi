using System.Globalization;
using Codlek.Application.Interfaces;
using Net.Codecrete.QrCodeGenerator;

namespace Codlek.Infrastructure.Labels;

/// <summary>
/// رمز الـQR للاب — <b>منقول من <c>CodlekWeb/Services/DeviceQr.cs</c>
/// بالحرف</b>.
///
/// <para>🔴 <b>نفس المكتبة ونفس النسخة (<c>[3.2.1]</c> مثبّتة) ونفس
/// مستوى التصحيح ونفس الهامش.</b> أي فرق في واحدة منهم بيطلّع رمز
/// مختلف لنفس الكود — والليبلات المطبوعة من الشاشة القديمة ملزوقة على
/// لابات فعلاً. وفيه فحص بيقارن الناتج بناتج القديم حرف حرف.</para>
/// </summary>
public sealed class QrDeviceLabelRenderer : IDeviceLabelRenderer
{
    /// <summary>
    /// المنطقة الهادية حوالين الرمز، بالوحدات.
    ///
    /// <para>⚠️ المواصفة بتطلب أربعة للـQR الكامل، والقديم اختار
    /// اتنين: الرمز بيتعرض على خلفية بيضا، واتنين بيوفّروا مساحة على
    /// ليبل صغير. ماتتغيّرش من هنا لوحدها.</para>
    /// </summary>
    public const int Border = 2;

    /// <summary>
    /// <c>Medium</c> بيستحمّل ~١٥٪ تلف ووحداته أعرض على نفس المساحة —
    /// أهم لماسح رخيص على ليبل متوسّخ.
    /// </summary>
    private static readonly QrCode.Ecc Level = QrCode.Ecc.Medium;

    public string Svg(string publicCode)
    {
        if (string.IsNullOrWhiteSpace(publicCode))
            throw new ArgumentException("مفيش ليبل لكود فاضي.", nameof(publicCode));

        var qr = QrCode.EncodeText(publicCode, Level);

        int span = qr.Size + Border * 2;
        string size = span.ToString(CultureInfo.InvariantCulture);

        // ⚠️ ToGraphicsPath بيدّي مسار الوحدات السودا وبس. ToSvgString
        //    بيرجّع مستند كامل بترويسة XML وDOCTYPE — واللوحة بتحط
        //    الرمز جوّه الصفحة، والقديم كان بيحطه جوّه HTML كمان.
        string path = qr.ToGraphicsPath(Border);

        return
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" " +
            "shape-rendering=\"crispEdges\" role=\"img\" focusable=\"false\">" +
            $"<rect width=\"{size}\" height=\"{size}\" fill=\"#ffffff\"/>" +
            $"<path d=\"{path}\" fill=\"#000000\"/>" +
            "</svg>";
    }
}
