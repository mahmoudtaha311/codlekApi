using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// صف خام من قاعدة البيانات — <b>قبل أي ترجمة أو حساب</b>.
///
/// <para>🔴 <b>ليه صف مخصوص بدل الكيان نفسه.</b> صف القايمة محتاج
/// <b>عدد</b> الأعطال والقطع مش قايمتهم. ولو رجّعنا الكيان
/// بـ<c>Include</c>، صفحة ٢٠٠ أمر كانت بتسحب كل عطل وكل قطعة فيهم —
/// آلاف صفوف بتترمي عشان نعدّها.</para>
///
/// <para>⚠️ <b>ومفيش ولا حرف عربي ولا حسابة وقت هنا.</b> النصوص
/// والأعمار بتتحسب في الـHandler بعد <c>ToListAsync</c>. أي
/// <c>RepairStatusRules.Text(...)</c> جوّه الإسقاط بيترجم وبيرمي
/// على قاعدة حقيقية — نفس ثقب <c>LoginName.Normalize</c>.</para>
/// </summary>
public sealed record RepairListRow(
    Guid Id,
    string PublicCode,
    Guid DeviceId,
    string DeviceCode,

    /// <summary>الشركة المصنّعة — بتتجمّع مع الموديل في الـHandler.</summary>
    string Manufacturer,

    string CommercialModelName,
    string RawModel,

    RepairStatus Status,
    string FaultSummary,
    Guid? AssignedTechnicianId,
    string AssignedTechnicianName,
    DateTime OpenedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string LocationName,
    int IssueCount,
    int PartCount,
    RepairApproval Approval);
