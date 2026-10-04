using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Features.Reports;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Technicians;
using MediatR;

namespace Codlek.Application.Features.Technicians.GetTechnicianDetail;

public sealed class GetTechnicianDetailQueryHandler(
    ITechnicianProductivityRepository productivity,
    IReportRepository reports,
    ICurrentUser me)
    : IRequestHandler<GetTechnicianDetailQuery, Result<TechnicianDetail>>
{
    /// <summary>⚠️ عشرين فحص — دي صفحة مش تقرير.</summary>
    private const int RecentTake = 20;

    public async Task<Result<TechnicianDetail>> Handle(
        GetTechnicianDetailQuery query, CancellationToken cancellationToken)
    {
        /*
          ⚠️ **الوجود بيتقاس على كل التاريخ، مش على المدى المختار.**

          اختيار أسبوع فاضي مالوش يخلّي الفني يختفي — والصفحة لازم
          تفتح وتقول «مفيش شغل في الفترة دي».
        */
        if (!await productivity.HasAnyReportAsync(me.TenantId, query.Code, cancellationToken))
            return Result.Failure<TechnicianDetail>(TechnicianErrors.NotFound);

        // ⚠️ والاسم كمان بيتحل من كل التاريخ.
        string? name = await productivity.NameFromReportsAsync(
            me.TenantId, query.Code, cancellationToken);

        var window = TechnicianProductivity.Window(query.Range, query.From, query.To);

        var facts = await productivity.OneAsync(
            me.TenantId, query.Code, window, cancellationToken);

        var recent = await productivity.RecentAsync(
            me.TenantId, query.Code, window, RecentTake, cancellationToken);

        // ⚠️ أكواد الراكات والأجهزة في قراية واحدة لكل جدول.
        var racks = await reports.RackLabelsAsync(
            me.TenantId, recent.Select(r => r.SourceRackId), cancellationToken);

        var deviceCodes = await reports.DeviceCodesAsync(
            me.TenantId, recent.Select(r => r.DeviceId), cancellationToken);

        return Result.Success(new TechnicianDetail(
            Code: query.Code,

            // ⚠️ «فني محطة T001» أنفع من «الاسم غير متاح».
            Name: string.IsNullOrWhiteSpace(name)
                ? RackTechnicianLabel.Neutral(query.Code)
                : name,

            NameAvailable: !string.IsNullOrWhiteSpace(name),

            TotalReports: facts?.Total ?? 0,

            Counts: new TestCounts(
                facts?.Pass ?? 0,
                facts?.Fail ?? 0,
                facts?.Error ?? 0,
                facts?.NotPresent ?? 0,
                facts?.Skip ?? 0),

            AverageMinutes: facts is null || facts.Timed == 0
                ? 0
                : Math.Round(facts.DurationMs / 60000.0 / facts.Timed, 1),

            LastAtUtc: facts?.LastAtUtc,

            Recent: recent.Select(r => ReportMapping.Row(r, racks, deviceCodes)).ToList()));
    }
}
