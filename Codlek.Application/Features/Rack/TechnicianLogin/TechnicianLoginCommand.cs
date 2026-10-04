using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Rack;
using MediatR;

namespace Codlek.Application.Features.Rack.TechnicianLogin;

/// <summary>
/// دخول فني من محطة <b>متحققة بمفتاحها</b>.
///
/// <para>🔴 <b>والترتيب هو الأمان نفسه:</b></para>
///
/// <para><c>مفتاح المحطة ← المحطة ← شركتها ← الفني جوّه الشركة
/// دي</c></para>
///
/// <para>🔴 <b>والطلب مالوش شركة خالص.</b> الشركة بتتقرا من المحطة
/// المتحققة بمفتاحها، فالدخول العابر للشركات مش «ممنوع» — هو
/// <b>مش موجود كمسار</b>. ولو الطلب أخد <c>tenantId</c>، أي مفتاح
/// محطة مسروق كان بيفتح فنيي كل الشركات.</para>
/// </summary>
/// <param name="TenantId">من المحطة. مش من الطلب.</param>
/// <param name="RackId">من المحطة — وعدّاد المحاولات مربوط بيها.</param>
/// <param name="RackCode">بيرجع في الرد عشان الفني يشوف هو على أنهي محطة.</param>
/// <param name="OfflineValidityDays">
/// ⚠️ من الإعدادات (٧ افتراضياً) — والسالب بيتصفّر.
/// </param>
public sealed record TechnicianLoginCommand(
    Guid TenantId,
    Guid RackId,
    string RackCode,
    string? Username,
    string? Password,
    int OfflineValidityDays) : IRequest<Result<TechnicianLoggedIn>>;
