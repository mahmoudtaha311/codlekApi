namespace Codlek.Infrastructure.Repositories;

/// <summary>
/// صف نسخ فحص من استعلام <c>JSON_VALUE</c> — <b>نتيجة استعلام، مش
/// جدول</b>.
///
/// <para>⚠️ <b>ومعاه <c>ReportId</c></b> عكس
/// <c>ReportVersionFacts</c>: الاستعلام هنا لصفحة فحوص كاملة، فلازم
/// كل صف يقول هو بتاع أنهي فحص.</para>
/// </summary>
internal sealed record ReportVersionRow(
    Guid ReportId,
    string? ApplicationVersion,
    string? TestDefinitionVersion);
