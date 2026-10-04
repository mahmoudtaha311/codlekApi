using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using Codlek.Application.Features.Reports;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Export;
using Codlek.Core.Spreadsheets;
using MediatR;

namespace Codlek.Application.Features.Export.ExportReports;

/// <summary>
/// تصدير الفحوص.
///
/// <para>🔴 <b>والفني بيصدّر شغله هو وبس.</b> التضييق جوّه
/// <see cref="ReportFilters"/> — يعني في الاستعلام مش في العرض،
/// فالتصدير مابيقدرش يتخطّاه بتغيير رابط.</para>
///
/// <para>⚠️ <b>ودي النقطة الوحيدة في التصدير من غير سياسة.</b> كل
/// الباقي <c>ManagerOrAbove</c> أو <c>OwnerOnly</c> — ودي مفتوحة
/// لأي مستخدم داخل، بالظبط لأن الفلتر بيضيّقها عليه.</para>
/// </summary>
public sealed class ExportReportsQueryHandler(
    IReportRepository reports, ICurrentUser me)
    : IRequestHandler<ExportReportsQuery, Result<ExportWorkbook>>
{
    public async Task<Result<ExportWorkbook>> Handle(
        ExportReportsQuery query, CancellationToken cancellationToken)
    {
        var filter = ReportFilters.Build(
            me, query.Search, query.Result, query.Technician,
            query.Container, query.From, query.To);

        var rows = await reports.ExportAsync(
            me.TenantId, filter, ExportLimits.MaxRows, cancellationToken);

        /*
          ⚠️ **أكواد المحطات بقراية واحدة — ومن <u>نفس</u> المستودع.**

          عمود «المحطة» بيحتاج الكود مش المعرّف، وقراية لكل صف كانت
          بتبقى N+1 على ملف فيه آلاف الفحوص.

          🔴 **والقراية دي مقصورة على الراكات اللي في الملف فعلاً**
          (`RackLabelsAsync` بتاخد المعرّفات) — مش كل راكات الورشة.
          النسخة الأولى هنا كانت بتجيب القايمة كلها من مستودع
          الراكات، وده تكرار لقدرة موجودة خلاص جنب الفحوص.
        */
        var rackCodes = await reports.RackLabelsAsync(
            me.TenantId, rows.Select(r => r.SourceRackId), cancellationToken);

        var sheet = new Sheet("الفحوص", ExportColumns.Reports,
            ExportLimits.Capped(rows, r => new object?[]
            {
                r.DeviceCode,
                r.Manufacturer,
                DeviceNaming.Model(r.CommercialModelName, r.Model),
                r.Cpu,
                r.RamText,
                r.StorageText,
                r.Gpu,
                r.ScreenSummary,
                r.SerialNumber,
                r.TechnicianName,
                r.TechnicianCode,
                r.SourceRackId is { } id && rackCodes.TryGetValue(id, out var rack)
                    ? rack.Code
                    : "",
                ExportValues.Cairo(r.StartedAtUtc),
                ExportValues.Minutes(r.DurationMs),
                r.PassCount,
                r.FailCount,
                r.ErrorCount,
                r.SkipCount + r.NotPresentCount,

                // ⚠️ عمود محسوب — نفس اللي الشاشة بتلوّنه.
                r.FailCount > 0 ? "محتاج مراجعة" : "سليم",
            }));

        return Result.Success(new ExportWorkbook("الفحوص", [sheet]));
    }
}
