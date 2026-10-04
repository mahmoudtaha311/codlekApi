using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// صف مرشّح خام — <b>المرحلة كـenum مش كنص</b>.
///
/// <para>⚠️ ومفيش ولا حرف عربي هنا: الترجمة بتحصل في الـHandler بعد
/// <c>ToListAsync</c>. أي <c>...Text(...)</c> جوّه الإسقاط بيترجم
/// عادي وبيعدّي فحوص الوحدة، وبيرمي على قاعدة حقيقية.</para>
///
/// <para>⚠️ وده أنضف من إننا نرجّع النص ونعيد تحليله: الرد بيحتاج
/// <b>الاسم والنص</b> الاتنين، والـenum هو المصدر الطبيعي
/// للاتنين.</para>
/// </summary>
public sealed record HandoverCandidateRow(
    Guid Id,
    string PublicCode,
    string Manufacturer,
    string Model,
    string ContainerCode,
    DeviceOperationalStage Stage,
    string LocationName,
    DateTime? LastTestAtUtc,
    DateTime? ReadyAtUtc,
    string ReadyByName);
