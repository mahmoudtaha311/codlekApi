namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// جسم نقطة الإلغاء.
///
/// <para>⚠️ <c>Reason</c> لازم يفضل <c>string?</c> مش
/// <c>string</c>: <c>null</c> في JSON بيعدّي على عدم القابلية للعدم
/// أصلاً، فالنوع غير القابل للعدم كان بيكذب على اللي بيقرا الكود.</para>
///
/// <para>🔴 <b>والرفض على السبب الفاضي بيحصل في المعالج، مش في
/// المتحقّق.</b> المتحقّق بيشتغل <b>قبل</b> المعالج، فكان هيرفض
/// إعادة الإلغاء على أمر ملغي خلاص — واللي القديم بيرجّعها
/// <c>200</c>. راجع <c>RepairTransitions.CancelAsync</c>.</para>
/// </summary>
public sealed record CancelRepairRequest(string? Reason);
