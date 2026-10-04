using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Hardware;
using MediatR;

namespace Codlek.Application.Features.Hardware.GetDeviceHardware;

/// <summary>
/// كل قطع اللاب من <b>أحدث لقطة</b> ليه.
///
/// <para>🔴 <b>ليه نقطة مستقلة عن لقطة الفحص.</b> صاحب الشغل عايز
/// تبويب في صفحة <b>اللاب</b> يوري القطع كلها — وهو مش بيعرف أنهي
/// فحص هو الأحدث ولا المفروض يختار.</para>
///
/// <para>⚠️ وتبويب «السيريالات اللي اتقرأت» الموجود بيوري
/// <b>مراسي الهوية الأربعة</b> بس (UUID · بيوس · بوردة · هارد) —
/// دي اللي بيتعرّف بيها اللاب، مش كل قطعه.</para>
/// </summary>
public sealed record GetDeviceHardwareQuery(Guid DeviceId)
    : IRequest<Result<ReportHardwareResponse>>;
