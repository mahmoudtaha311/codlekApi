using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using Codlek.Application.Features.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Export;
using Codlek.Core.Spreadsheets;
using MediatR;

namespace Codlek.Application.Features.Export.ExportTechnicians;

/// <summary>
/// تصدير الفنيين — <b>ملخّص + حسابات + صفحة لكل فني</b>.
///
/// <para>🔴 <b>وده غير تصدير الإنتاجية.</b> اللي هناك بيطلّع الجدول
/// اللي المدير شايفه على الشاشة؛ ده بيطلّع الفحوص <b>الخام</b> صف
/// صف مع جدول الحسابات — ملف مراجعة.</para>
///
/// <para>🔴 <b>ولا بصمة ولا ملح في جدول الحسابات.</b> الملف ده
/// بينزل على جهاز المدير وبيتبعت أحياناً — فعمود «البصمة» فيه معناه
/// تسريب كل باسوردات الفنيين في ملف واحد.</para>
/// </summary>
public sealed class ExportTechniciansQueryHandler(
    IReportRepository reports,
    ITechnicianAccountRepository accounts,
    IDepartmentRepository departments,
    ICurrentUser me)
    : IRequestHandler<ExportTechniciansQuery, Result<ExportWorkbook>>
{
    public async Task<Result<ExportWorkbook>> Handle(
        ExportTechniciansQuery query, CancellationToken cancellationToken)
    {
        var window = TechnicianProductivity.Window(query.Range, query.From, query.To);

        string? code = string.IsNullOrWhiteSpace(query.Technician)
            ? null
            : query.Technician.Trim();

        /*
          🔴 **الفحوص الخام بتتقرا من <u>نفس</u> فلتر قايمة الفحوص.**

          لو اتكتب استعلام تاني هنا، أول تضييق بيتزاد على القايمة
          (زي تضييق الفني على شغله هو) كان هيبقى ناقص من الملف ده.
        */
        var filter = Reports.ReportFilters.Build(
            me, search: null, result: null, technician: code,
            container: null, from: null, to: null) with
        {
            FromUtc = window.FromUtc,
            ToUtc = window.ToUtc,
        };

        var flat = await reports.ExportAsync(
            me.TenantId, filter, ExportLimits.MaxRows, cancellationToken);

        // ⚠️ أكواد الراكات اللي في الملف بس — مش كل راكات الورشة.
        var rackCodes = await reports.RackLabelsAsync(
            me.TenantId, flat.Select(r => r.SourceRackId), cancellationToken);

        var sheets = new List<Sheet>();

        /*
          ⚠️ **التجميع بالكود <u>والاسم</u> زي القديم.**

          الفني اللي اتغيّر اسمه على الراكة بيطلع في صفحتين. ومنقول
          زي ما هو: الصفحتين بأسماء مختلفة أوضح للي بيراجع من صف
          واحد باسم عشوائي من الاتنين.
        */
        var groups = flat
            .GroupBy(r => new { r.TechnicianCode, r.TechnicianName })
            .OrderByDescending(g => g.Count())
            .ToList();

        var summary = groups
            .Select(g => new object?[]
            {
                g.Key.TechnicianName,
                g.Key.TechnicianCode,
                g.Count(),
                g.Sum(x => x.PassCount),
                g.Sum(x => x.FailCount),
                g.Sum(x => x.ErrorCount),
                ExportValues.Minutes((long)g.Average(x => x.DurationMs)),
                ExportValues.Cairo(g.Max(x => x.StartedAtUtc)),
            })
            .ToList();

        /*
          ⚠️ **سقف على عدد الصفحات.** إكسل بيقبل صفحات كتير، بس ملف
          فيه ٢٠٠ صفحة مالوش أي فايدة عملية — والسقف بيتقال في
          الملخّص.
        */
        if (groups.Count > ExportLimits.MaxSheets)
        {
            summary.Add([
                $"⚠️ الملف فيه أول {ExportLimits.MaxSheets} فني بصفحة لكل واحد — "
                + $"الباقي ({groups.Count - ExportLimits.MaxSheets}) في الملخّص ده بس."
            ]);
        }

        sheets.Add(new Sheet("ملخّص الفنيين", ExportColumns.TechnicianSummary, summary));

        sheets.Add(await AccountsSheetAsync(cancellationToken));

        foreach (var group in groups.Take(ExportLimits.MaxSheets))
        {
            string name = string.IsNullOrWhiteSpace(group.Key.TechnicianName)
                ? group.Key.TechnicianCode
                : group.Key.TechnicianName;

            sheets.Add(new Sheet(name, ExportColumns.TechnicianReports,
                group.Select(r => new object?[]
                {
                    r.DeviceCode,
                    r.Manufacturer,
                    DeviceNaming.Model(r.CommercialModelName, r.Model),
                    ExportValues.Cairo(r.StartedAtUtc),
                    ExportValues.Minutes(r.DurationMs),
                    r.PassCount,
                    r.FailCount,
                    r.ErrorCount,
                    r.SkipCount + r.NotPresentCount,
                    rackCodes.TryGetValue(r.SourceRackId ?? Guid.Empty, out var rack)
                        ? rack.Code
                        : "",
                }).ToList()));
        }

        string label = code ?? "الفنيين";

        return Result.Success(new ExportWorkbook($"{label} — {window.Label}", sheets));
    }

    /// <summary>
    /// جدول الحسابات — <b>«كل البيانات» في سياق الإدارة</b>.
    ///
    /// <para>⚠️ وده مش مفلتر بالفترة: الحساب مش حدث، هو حالة
    /// النهاردة.</para>
    /// </summary>
    private async Task<Sheet> AccountsSheetAsync(CancellationToken ct)
    {
        var rows = await accounts.ListAsync(me.TenantId, ct);

        var names = (await departments.ListAsync(me.TenantId, ct))
            .ToDictionary(d => d.Id, d => d.Name);

        return new Sheet("الحسابات", ExportColumns.TechnicianAccounts,
            ExportLimits.Capped(rows, (Technician a) => new object?[]
            {
                a.DisplayName,
                a.Code,
                a.Username,
                a.IsActive ? "مفعّل" : "موقوف",
                TechnicianSpecialtyText.Arabic(a.Specialty),
                a.DepartmentId is { } id && names.TryGetValue(id, out string? dept) ? dept : "",
                ExportValues.YesNo(a.CanTest),
                ExportValues.YesNo(a.CanRepair),
                a.SuspendedReason,
                ExportValues.YesNo(a.MustChangePassword),
                ExportValues.Cairo(a.LastSuccessfulLoginUtc),
            }));
    }
}
