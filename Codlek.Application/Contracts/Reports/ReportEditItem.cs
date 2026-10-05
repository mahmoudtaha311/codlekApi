namespace Codlek.Application.Contracts.Reports;

/// <summary>
/// تعديل واحد على الفحص بعد التسليم — القيمة القديمة والجديدة ومين.
///
/// <para>⚠️ <b>الوقت UTC</b> والشاشة بتحوّله لتوقيت القاهرة — زي
/// باقي الأوقات في الفحص.</para>
/// </summary>
public sealed record ReportEditItem(
    DateTime AtUtc,
    string ByName,
    string Field,
    string OldValue,
    string NewValue,
    string Reason);
