using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetAssignableTechnicians;

/// <summary>
/// فنيو الصيانة المتاحين.
///
/// <para>🔴 <b>نقطة مستقلة عن قايمة الفنيين العامة عن قصد:</b>
/// الواجهة محتاجة تعرف <b>صفر</b> بوضوح عشان تعرض «لا يوجد فني صيانة
/// مفعّل حاليًا» بدل قايمة فاضية.</para>
///
/// <para>🔴 <b>وسياستها <c>RepairsViewer</c> مش
/// <c>ManagerOrAbove</c>.</b> صاحب الشغل قرّر إن المحاسب «يوافق
/// <b>ويغيّر الفني</b>» في نفس الخطوة — والقايمة دي هي مصدر الأسماء.
/// لو فضلت للمديرين بس، المنسدلة بتطلع فاضية عند المحاسب وشطر من
/// قراره بيختفي <b>في صمت</b>.</para>
/// </summary>
public sealed record GetAssignableTechniciansQuery
    : IRequest<Result<IReadOnlyList<RepairTechnicianOption>>>;
