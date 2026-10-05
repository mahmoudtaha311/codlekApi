namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// ليبل اللاب — <b>الكود اللي اتشفّر والرمز نفسه</b>.
///
/// <para>⚠️ <b>القيمتين من مصدر واحد.</b> لو المكتوب تحت الرمز
/// اتحسب لوحده، ممكن يقول حاجة والرمز يشيل حاجة تانية — ومحدش
/// هيلاحظ غير لما الماسح يفتح اللاب الغلط.</para>
/// </summary>
public sealed record DeviceLabel(string PublicCode, string Svg)
{
    /// <summary>نوع المحتوى اللي بيرجع للمتصفح.</summary>
    public const string ContentType = "image/svg+xml";
}
