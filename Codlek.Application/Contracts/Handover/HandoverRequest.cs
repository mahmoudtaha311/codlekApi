namespace Codlek.Application.Contracts.Handover;

/// <summary>جسم نقطة التسليم.</summary>
/// <param name="OverrideReason">
/// 🔴 <b>سبب التسليم من غير مراجعة — إجباري لو فيه لاب مش
/// مراجَع.</b>
///
/// <para>الباب المقفول بالكامل بيتحايل عليه: حالة مستعجلة
/// والمراجعة مقفولة معناها إن حد هيعلّم «جاهز» على السريع عشان
/// يعدّي — وساعتها المراجعة تبقى ختم مالوش معنى وإحنا مش عارفين إن
/// ده حصل.</para>
///
/// <para>⚠️ والفرق بين استثناء وخرق إن الاستثناء <b>متسجّل</b>:
/// السبب بيتكتب في سبب الحركة نفسه وفي سجل التدقيق.</para>
/// </param>
public sealed record HandoverRequest(
    List<Guid>? DeviceIds,
    Guid DestinationId,
    string? ReceivedByName,
    string? Reason,
    string? Notes,
    string? OverrideReason = null);
