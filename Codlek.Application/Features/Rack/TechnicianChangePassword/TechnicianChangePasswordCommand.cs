using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Rack;
using MediatR;

namespace Codlek.Application.Features.Rack.TechnicianChangePassword;

/// <summary>
/// الفني بيغيّر باسورده <b>بنفسه</b> من المحطة.
///
/// <para>🔴 <b>النقطة دي اتعملت عشان عطل حقيقي في الميدان.</b>
/// شاشة «حسابي» على الراكة كانت بترد «الباسورد الحالي غلط» على
/// الباسورد الصح، لأنها بتقارن ببصمة محلية والفني المركزي بصمته
/// المحلية <b>فاضية</b> عن قصد (باسورده على السيرفر). يعني مكانش
/// فيه أي طريق في النظام كله يغيّر بيه فني باسورده.</para>
///
/// <para>⚠️ <b>ونفس حراسة الدخول بالحرف:</b> نفس عدّاد (محطة +
/// اسم) ونفس دلو الحد على HTTP. النقطة دي بتاخد باسورد وبترد «صح
/// ولا غلط» — يعني عرّافة باسوردات زي الدخول بالظبط، ودلو منفصل
/// معناه إن اللي بيخمّن عنده <b>ضعف</b> المحاولات.</para>
/// </summary>
public sealed record TechnicianChangePasswordCommand(
    Guid TenantId,
    Guid RackId,
    string RackCode,
    string? Username,
    string? CurrentPassword,
    string? NewPassword,
    string? ConfirmPassword) : IRequest<Result<TechnicianPasswordChanged>>;
