namespace Codlek.Core.Reports;

/// <summary>
/// الحقايق اللي حكم المرحلة محتاجها.
///
/// <para>🔴 <b>نوع صغير بدل كيان الفحص كامل — عشان الحكم يبقى
/// نقي.</b> الدالة اللي بتاخد الكيان مابتتجرّبش من غير قاعدة، والحكم
/// ده بيتعرض على كل سطر في كل فحص.</para>
/// </summary>
/// <param name="RepairRecorded">
/// ⚠️ فيه صيانة متسجّلة؟ — بيتحسب من أربع خانات في الفحص (دهان
/// الهيكل · كسر الهيكل · الفك · خدمة البطارية).
/// </param>
/// <param name="Handed">
/// ⚠️ <c>EndedAtUtc</c> بيتكتب عند التسليم وبس، فهو علامة التسليم
/// الموجودة أصلاً.
/// </param>
public sealed record StageFacts(bool RepairRecorded, bool HasGeneralNote, bool Handed);
