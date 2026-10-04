using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using Codlek.Application.Features.Export.ExportAudit;
using Codlek.Application.Features.Export.ExportDevices;
using Codlek.Application.Features.Export.ExportProductivity;
using Codlek.Application.Features.Export.ExportRacks;
using Codlek.Application.Features.Export.ExportRepairs;
using Codlek.Application.Features.Export.ExportReports;
using Codlek.Application.Features.Export.ExportTechnicians;
using Codlek.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/export/*.xlsx</c> — تصدير إكسل لكل صفحة.
///
/// <para>🔴 <b>وكل نقطة هنا بتاخد <u>نفس</u> معاملات الصفحة اللي
/// طالعة منها.</b> الزرار في الواجهة مكتوب فوقه «اللي مفلتر على
/// الشاشة مفلتر في الإكسل» — وملف بيرجّع صفوف غير اللي قدام المدير
/// أسوأ من مفيش ملف: هو بيقارنهم وبيلاقي فرق مالوش تفسير.</para>
///
/// <para>⚠️ <b>والسياسة على كل إجراء مش على الكلاس.</b> الفحوص
/// مفتوحة لأي مستخدم داخل (والفني بيشوف شغله هو — التضييق في
/// الاستعلام)، والباقي <c>ManagerOrAbove</c>، والمحطات والسجل
/// <c>OwnerOnly</c>. وحارس الكلاس من غير سياسة لأن ASP.NET بيجمع
/// سياسة الكلاس وسياسة الإجراء بـ«و».</para>
///
/// <para>⚠️ <b>والملف بيتبني في الذاكرة مش بيتصبّ على الرد.</b>
/// <c>ZipArchive</c> بيكتب متزامن، وASP.NET Core بيمنع الكتابة
/// المتزامنة على <c>Response.Body</c> افتراضياً
/// (<c>AllowSynchronousIO=false</c>) — وفتح الباب ده عشان التصدير
/// بيفتحه للتطبيق كله. والسقف في <c>ExportLimits</c> هو اللي بيخلّي
/// الحجم معروف.</para>
/// </summary>
[ApiController]
[Route("api/v1/export")]
[Authorize]
public sealed class ExportController(ISender sender, IWorkbookWriter workbook)
    : ControllerBase
{
    /// <summary>
    /// 🔴 <b>من غير سياسة — ودي مقصودة.</b> الفني بيصدّر شغله هو،
    /// والتضييق جوّه الفلتر يعني في الاستعلام مش في العرض.
    /// </summary>
    [HttpGet("reports.xlsx")]
    public async Task<IActionResult> Reports(
        string? search, string? result, string? technician, Guid? container,
        DateTime? from, DateTime? to, CancellationToken ct) =>
        File(await sender.Send(
            new ExportReportsQuery(search, result, technician, container, from, to), ct));

    [HttpGet("devices.xlsx")]
    [Authorize(Policies.ManagerOrAbove)]
    public async Task<IActionResult> Devices(
        string? search, string? status, string? confidence, string? outcome,
        string? technician, Guid? rack, DateTime? from, DateTime? to,
        string? stage, string? sort, Guid? container,
        string? flag, string? handover, Guid? location, CancellationToken ct) =>
        File(await sender.Send(
            new ExportDevicesQuery(
                search, status, confidence, outcome, technician, rack, from, to,
                stage, sort, container, flag, handover, location), ct));

    [HttpGet("repairs.xlsx")]
    [Authorize(Policies.ManagerOrAbove)]
    public async Task<IActionResult> Repairs(
        string? search, string? status, string? approval, Guid? technician,
        DateTime? from, DateTime? to, string? sort, CancellationToken ct) =>
        File(await sender.Send(
            new ExportRepairsQuery(search, status, approval, technician, from, to, sort), ct));

    [HttpGet("productivity.xlsx")]
    [Authorize(Policies.ManagerOrAbove)]
    public async Task<IActionResult> Productivity(
        string? range, DateTime? from, DateTime? to, string? search,
        string? sort, string? technician, CancellationToken ct) =>
        File(await sender.Send(
            new ExportProductivityQuery(range, from, to, search, sort, technician), ct));

    [HttpGet("technicians.xlsx")]
    [Authorize(Policies.ManagerOrAbove)]
    public async Task<IActionResult> Technicians(
        string? range, DateTime? from, DateTime? to, string? technician,
        CancellationToken ct) =>
        File(await sender.Send(new ExportTechniciansQuery(range, from, to, technician), ct));

    [HttpGet("racks.xlsx")]
    [Authorize(Policies.OwnerOnly)]
    public async Task<IActionResult> Racks(CancellationToken ct) =>
        File(await sender.Send(new ExportRacksQuery(), ct));

    [HttpGet("audit.xlsx")]
    [Authorize(Policies.OwnerOnly)]
    public async Task<IActionResult> Audit(
        string? action, string? entityType, string? actor, string? search,
        DateTime? from, DateTime? to, CancellationToken ct) =>
        File(await sender.Send(
            new ExportAuditQuery(from, to, action, entityType, actor, search), ct));

    /// <summary>
    /// ⚠️ <b>الاسم العربي بيعدّي سليم.</b> ASP.NET Core بيعمل
    /// للاسم ترميز RFC 5987 لوحده في <c>Content-Disposition</c>،
    /// فمفيش حاجة تتعمل هنا.
    /// </summary>
    private IActionResult File(Result<ExportWorkbook> result)
    {
        if (result.IsFailure) return result.ToProblem();

        var buffer = new MemoryStream();
        workbook.Write(buffer, result.Value.Sheets);
        buffer.Position = 0;

        return File(buffer, workbook.ContentType,
            $"{result.Value.FileName}.{workbook.Extension}");
    }
}
