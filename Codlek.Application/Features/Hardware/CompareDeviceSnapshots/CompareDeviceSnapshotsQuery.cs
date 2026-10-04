using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Hardware;
using MediatR;

namespace Codlek.Application.Features.Hardware.CompareDeviceSnapshots;

/// <summary>
/// مقارنة لقطتين على نفس اللاب.
///
/// <para>🔴 <b>المعرّفين اختياريين في النوع عن قصد.</b> الواجهة
/// بتنده النقطة من غيرهم أول ما الصفحة تفتح، والرد لازم يبقى
/// <c>400</c> برسالة مفهومة («لازم تحدّد الفحصين») مش
/// <c>404</c>.</para>
/// </summary>
public sealed record CompareDeviceSnapshotsQuery(Guid DeviceId, Guid? Left, Guid? Right)
    : IRequest<Result<CompareResponse>>;
