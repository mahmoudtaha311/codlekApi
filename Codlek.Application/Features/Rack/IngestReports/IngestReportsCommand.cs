using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Sync;
using MediatR;

namespace Codlek.Application.Features.Rack.IngestReports;

/// <summary>
/// استقبال فحوص من محطة <b>متحققة بمفتاحها</b>.
///
/// <para>🔴 <b>والمبدأ الأساسي: التكرار مش بيعمل ضرر.</b> الفني ممكن
/// يرفع نفس الملف مرتين، أو النت يقطع في نص الرفع فيعيد. كل فحص ليه
/// <c>Id</c> بيتولّد <b>على الراكة</b>، فالسجل بيتحدّث مش بيتكرر —
/// ولو اتكرر كان عدد الأجهزة هيزيد غلط، وده رقم بيتبني عليه تقييم
/// الفني.</para>
/// </summary>
/// <param name="TenantId">من المحطة. <b>مش من الطلب.</b></param>
/// <param name="SourceRackId">
/// ⚠️ بيتختم على كل فحص — هو الدليل الوحيد على «الفحص ده جه من أنهي
/// بنش».
/// </param>
public sealed record IngestReportsCommand(
    Guid TenantId,
    Guid SourceRackId,
    IReadOnlyList<LaptopReportPayload> Reports) : IRequest<Result<IngestResult>>;
