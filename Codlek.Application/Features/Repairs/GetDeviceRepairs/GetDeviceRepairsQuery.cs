using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetDeviceRepairs;

/// <summary>
/// كل أوامر صيانة جهاز واحد — تبويب في صفحة الجهاز.
///
/// <para>🔴 <b>الصلاحية هنا أضيق من قايمة الصيانة.</b> المحاسب
/// بيقرا <c>/repairs</c> ومابيقراش ده: التبويب جمبه كل بيانات
/// اللاب، وهو مالوش شغل بيها. وتوسيع المجموعة هنا بيفتح له صفحة
/// الجهاز كلها.</para>
/// </summary>
public sealed record GetDeviceRepairsQuery(Guid DeviceId)
    : IRequest<Result<IReadOnlyList<RepairListItem>>>;
