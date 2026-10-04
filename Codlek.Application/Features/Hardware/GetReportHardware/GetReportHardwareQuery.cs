using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Hardware;
using MediatR;

namespace Codlek.Application.Features.Hardware.GetReportHardware;

/// <summary>
/// لقطة عتاد فحص واحد.
///
/// <para>⚠️ <b>دي النقطة الوحيدة في العتاد اللي الفني بيوصلها.</b>
/// السياسة «مسجّل دخول» بس، والحارس الحقيقي جوّه المعالج: الفني
/// يشوف شغله هو، والمدير يشوف الكل.</para>
/// </summary>
public sealed record GetReportHardwareQuery(Guid ReportId)
    : IRequest<Result<ReportHardwareResponse>>;
