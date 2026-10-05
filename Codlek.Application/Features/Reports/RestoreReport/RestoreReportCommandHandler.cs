using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Reports.RestoreReport;

/// <summary>
/// استرجاع فحص اتمسح بالغلط — <b>المالك بس</b>.
///
/// <para>🔴 <b>بيانات المسح بتفضل زي ما هي.</b> الاسترجاع بيرجّع
/// الفحص للعدّ، وبيفضل مسجّل إنه اتمسح واترجع — مين مسحه وليه
/// وإمتى، ومين رجّعه وليه وإمتى. مسح بيانات المسح هنا كان هيخلّي
/// «اتمسح بالغلط» حكاية مالهاش أثر.</para>
///
/// <para>⚠️ <b>والسبب اختياري ومالوش حد أدنى</b> — زي القديم.</para>
/// </summary>
public sealed class RestoreReportCommandHandler(
    IReportRepository reports,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<RestoreReportCommand, Result<ReportActionResponse>>
{
    public const string Done = "الفحص رجع للحساب";

    public async Task<Result<ReportActionResponse>> Handle(
        RestoreReportCommand command, CancellationToken cancellationToken)
    {
        var row = await reports.FindForUpdateAsync(me.TenantId, command.Id, cancellationToken);

        if (row is null) return Result.Failure<ReportActionResponse>(ReportErrors.NotFound);

        // ⚠️ السياسة على المسار بتمنع ده قبل ما يوصل هنا.
        if (!me.IsOwner)
            return Result.Failure<ReportActionResponse>(ReportErrors.RestoreForbidden);

        // ⚠️ جديد — القديم كان بيكتب «اترجع» على فحص عمره ما اتمسح.
        if (!row.IsDeleted)
            return Result.Failure<ReportActionResponse>(ReportErrors.NotDeleted);

        string reason = (command.Reason ?? "").Trim();

        if (reason.Length > ReportDeletionRules.MaxReasonLength)
            return Result.Failure<ReportActionResponse>(ReportErrors.ReasonTooLong);

        row.IsDeleted = false;
        row.RestoredByName = TextClip.To(me.DisplayName, TextClip.Lengths.PersonName);
        row.RestoredReason = reason;
        row.RestoredAtUtc = DateTime.UtcNow;

        // ⚠️ سطر السجل زيادة عن القديم — نفس سبب سطر المسح.
        string summary = $"فحص {ReportDeletionRules.DeviceLabel(row.DeviceCode)} رجع للحساب";

        audit.Record(
            AuditActions.ReportRestored, "Report", row.Id, row.DeviceCode,
            reason.Length > 0 ? $"{summary} — {reason}" : summary);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReportActionResponse(row.Id, false, Done));
    }
}
