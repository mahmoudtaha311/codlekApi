using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Reports;
using MediatR;

namespace Codlek.Application.Features.Reports.GetReportDetail;

/// <summary>
/// صفحة فحص واحد.
///
/// <para>⚠️ <b>وبتفتح الفحص الممسوح كمان</b> — ومعاها سبب المسح
/// ومين مسحه وإمتى. الصف ده <b>دليل</b>: سجل المراجعة بيشاور عليه،
/// وإخفاؤه بيخلّي الرابط يوصل لصفحة ميتة.</para>
/// </summary>
public sealed class GetReportDetailQueryHandler(
    IReportRepository reports,
    ICurrentUser me)
    : IRequestHandler<GetReportDetailQuery, Result<ReportDetail>>
{
    /// <summary>⚠️ النص اللي بيتعرض للقيمة الناقصة في النسخ.</summary>
    private const string Unavailable = "غير متاح";

    public async Task<Result<ReportDetail>> Handle(
        GetReportDetailQuery query, CancellationToken cancellationToken)
    {
        var r = await reports.FindDetailAsync(me.TenantId, query.Id, cancellationToken);

        if (r is null) return Result.Failure<ReportDetail>(ReportErrors.NotFound);

        /*
          🔴 **الحارس بعد القراية — والترتيب ده مقصود.**

          الصف بيتقرا الأول عشان نعرف بتاع مين، فالمعرّف المش موجود
          بياخد ٤٠٤ والموجود بتاع غيره بياخد ٤٠٣. وتوحيدهم كان
          بيخبّي الفرق على اللي بيقرا السجل.
        */
        if (!me.IsManagerOrAbove && r.TechnicianCode != me.Code)
            return Result.Failure<ReportDetail>(ReportErrors.NotYours);

        var racks = await reports.RackLabelsAsync(
            me.TenantId, [r.SourceRackId], cancellationToken);

        var deviceCodes = await reports.DeviceCodesAsync(
            me.TenantId, [r.DeviceId], cancellationToken);

        int components = await reports.SnapshotComponentCountAsync(
            me.TenantId, r.Id, cancellationToken);

        var versions = await reports.VersionsAsync(me.TenantId, r.Id, cancellationToken);

        // ⚠️ الحقايق بتتحسب مرة واحدة للفحص، مش لكل مرحلة.
        var facts = ReportMapping.Facts(r);

        var rack = racks.TryGetValue(r.SourceRackId ?? Guid.Empty, out var label)
            ? label
            : new RackLabel("", "");

        return Result.Success(new ReportDetail(
            Id: r.Id,
            DeviceId: r.DeviceId,

            /*
              ⚠️ **كود الجهاز المربوط هو الأصل، واللي في الفحص
              احتياطي.**

              القديم كان بيقرا الجهاز المربوط وبس ويرجّع فاضي لو مش
              مربوط — والاحتياطي بيخلّي ليبل الفحص المستورد المش
              مربوط يفضل مقروء بدل ما يبقى فاضي.
            */
            DevicePublicCode: ReportMapping.DeviceCode(deviceCodes, r.DeviceId, r.DeviceCode),

            TechnicianId: r.TechnicianId,
            TechnicianName: r.TechnicianName,
            TechnicianCode: r.TechnicianCode,

            /*
              ⚠️ **الناقصة هنا فاضي، مش «غير متاح».**

              القديم كان بيخفي سطر «سلّمه» لما تبقى فاضية أو نفس اسم
              الفني. «غير متاح» كانت هتطلّع السطر على كل فحص قديم من
              قبل الحقل ده.
            */
            CompletedByName: versions?.CompletedByName ?? "",
            CompletedByCode: versions?.CompletedByCode ?? "",

            RackCode: rack.Code,
            RackName: rack.Name,
            StartedAtUtc: r.StartedAtUtc,
            EndedAtUtc: r.EndedAtUtc,
            ReceivedAtUtc: r.ReceivedAtUtc,
            DurationMs: r.DurationMs,
            Counts: ReportMapping.Counts(r),

            Specs: new ReportSpecs(
                r.Manufacturer,
                r.Model,
                r.CommercialModelName ?? "",
                r.CommercialModelSource ?? "",
                r.MachineType ?? "",
                r.Cpu,
                r.RamText,
                r.StorageText,
                r.Gpu,
                r.ScreenSummary,
                r.SerialNumber,
                r.BatteryHealthPercent,
                r.BenchmarkScore,
                r.MaxCpuTemp,
                r.ThrottlingDetected),

            Steps: r.Steps
                // ⚠️ بترتيب الإدخال — ده ترتيب الفحص اللي الفني شافه.
                .OrderBy(s => s.Id)
                .Select(s => new ReportStepItem(
                    Title: s.Title,
                    Status: s.Status.ToString(),

                    /*
                      🔴 **مش ترجمة الرقم على طول.**

                      تلات مراحل مالهاش نتيجة فحص أصلاً (المواصفات
                      والملاحظات والتسليم) وكانوا بيطلعوا «لم
                      يُنفّذ» على **كل فحص متسلّم** في النظام.
                    */
                    StatusText: StageOutcomeText.Arabic(s.StepId, s.Status, facts),
                    Outcome: StageOutcomeText.Tone(s.StepId, s.Status, facts),

                    Note: s.Note,
                    SkipReason: s.SkipReason,
                    Detail: s.Detail,
                    DurationMs: s.DurationMs))
                .ToList(),

            PartsUsed: r.Parts.Select(p => p.Name).ToList(),

            GeneralNote: r.GeneralNote,
            IsDeleted: r.IsDeleted,
            DeletedReason: r.DeletedReason,
            DeletedByName: r.DeletedByName,
            DeletedAtUtc: r.DeletedAtUtc,

            // ⚠️ الاسترجاع مابيمسحش بيانات المسح — الاتنين بيترجعوا.
            RestoredByName: r.RestoredByName,
            RestoredReason: r.RestoredReason,
            RestoredAtUtc: r.RestoredAtUtc,

            // ⚠️ الناقصة بترجع «غير متاح» — زيها زي أي قيمة ناقصة.
            ApplicationVersion: Text(versions?.ApplicationVersion),
            TestDefinitionVersion: Text(versions?.TestDefinitionVersion),

            SnapshotComponentCount: components,
            Scope: r.Scope,
            ScopeText: ReportScopeText.Arabic(r.Scope),
            NotRunCount: r.NotRunCount));
    }

    private static string Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Unavailable : value;
}
