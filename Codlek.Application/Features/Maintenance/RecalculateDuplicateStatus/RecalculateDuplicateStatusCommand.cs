using Codlek.Application.Abstractions;
using MediatR;

namespace Codlek.Application.Features.Maintenance.RecalculateDuplicateStatus;

/// <summary>
/// بيعيد حساب «مشكوك إنه مكرر» من المراسي <b>الموجودة فعلاً</b>.
///
/// <para>🔴 <b>من غيره العلم ده مابيتشالش أبداً.</b> المزامنة بتحطّه بس
/// (<c>DeviceSyncApplier.FlagDuplicatesAsync</c>)، ومفيش أي كود بيرجّع
/// الجهاز «شغّال». فلو التوأم اتدمج أو مرساته اتوقفت، الجهاز يفضل متعلّم
/// على سبب مبقاش موجود — وعدّاد الجرس بيكبر وبس. القديم كان بيعيد
/// الحساب مع كل إقلاع (<c>DeviceSyncService.cs:628-686</c>).</para>
///
/// <para>🔴 <b>بيحرّك بين «شغّال» و«مشكوك إنه مكرر» وبس.</b> المدموج
/// والمتقاعد قرارات بني آدم — إعادة حساب آلية ماينفعش تلغيها.</para>
///
/// <para>⚠️ <b>والمدموجين مستبعدين من الحساب كله.</b> الصف المدموج
/// بيحتفظ بمراسيه نشطة عن قصد (تاريخ)، فمن غير الاستبعاد الجهاز الكانوني
/// بيرجع يتعلّم في كل إقلاع.</para>
///
/// <para>⚠️ <b>ومفيش حالة «علّمها المدير بإيده» تتحمى.</b> مفيش شاشة ولا
/// نقطة بتعمل كده النهارده. لو اتعملت بعدين، الأمر ده لازم يعدّيها.</para>
/// </summary>
/// <returns>عدد الأجهزة اللي حالتها اتغيّرت.</returns>
public sealed record RecalculateDuplicateStatusCommand(Guid TenantId, int BatchSize = 500)
    : IRequest<Result<int>>;
