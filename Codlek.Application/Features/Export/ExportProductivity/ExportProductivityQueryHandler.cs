using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using Codlek.Application.Features.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Export;
using Codlek.Core.Spreadsheets;
using MediatR;

namespace Codlek.Application.Features.Export.ExportProductivity;

/// <summary>
/// تصدير جدول الإنتاجية — <b>نفس جدول الشاشة بالحرف</b>.
///
/// <para>🔴 <b>وده غير تصدير الفنيين.</b> التاني بيطلّع الفحوص
/// <b>الخام</b> صف صف مع جدول الحسابات — ملف مراجعة. ده بيطلّع
/// الجدول اللي المدير شايفه: صف لكل فني بنفس الأعمدة وبنفس
/// الترتيب.</para>
///
/// <para>⚠️ <b>ونفس الفترة ونفس البحث ونفس الترتيب.</b> كل خطوة
/// هنا هي <u>نفس الدالة</u> اللي نقطة الشاشة بتناديها — مش نسخة
/// منها.</para>
/// </summary>
public sealed class ExportProductivityQueryHandler(
    ITechnicianProductivityRepository productivity, ICurrentUser me)
    : IRequestHandler<ExportProductivityQuery, Result<ExportWorkbook>>
{
    public async Task<Result<ExportWorkbook>> Handle(
        ExportProductivityQuery query, CancellationToken cancellationToken)
    {
        var window = TechnicianProductivity.Window(query.Range, query.From, query.To);

        var testing = await productivity.TestingAsync(me.TenantId, window, cancellationToken);
        var repairs = await productivity.RepairsAsync(me.TenantId, window, cancellationToken);

        var rows = TechnicianProductivity.Sorted(
            TechnicianProductivity.OnlyTechnician(
                TechnicianProductivity.Matching(
                    TechnicianProductivity.Rows(testing, repairs), query.Search),
                query.Technician),
            query.Sort);

        var sheet = new Sheet("الإنتاجية", ExportColumns.Productivity,
            ExportLimits.Capped(rows, r => new object?[]
            {
                // ⚠️ نفس البديل اللي على الشاشة — مش خانة فاضية.
                r.NameAvailable ? r.Name : "فني بدون اسم مسجّل",

                r.Code,
                r.TotalReports,
                r.AverageMinutes,
                r.RepairsCompleted,
                r.RepairAverageMinutes,
                ExportValues.Cairo(r.LastAtUtc),

                // النتايج — نفس العدّادات اللي الشريط الملوّن بيرسمها.
                r.Counts.Pass,
                r.Counts.Fail,
                r.Counts.Error,
                r.Counts.NotPresent,
                r.Counts.Skip,
            }));

        return Result.Success(new ExportWorkbook("الإنتاجية", [sheet]));
    }
}
