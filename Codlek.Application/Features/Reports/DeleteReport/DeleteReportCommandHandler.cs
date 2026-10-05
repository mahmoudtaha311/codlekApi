using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Reports.DeleteReport;

/// <summary>
/// مسح فحص — <b>بعلامة مش بحذف، ولازم معاه سبب مكتوب</b>.
///
/// <para>🔴 <b>الصف بيفضل، وبيخرج من كل عدّ لوحده.</b> كل قراية
/// عدّ بتفلتر <c>!IsDeleted</c> (اللوحة والجرد والأجهزة والصيانة)،
/// فمفيش أي رقم بيتحسب هنا.</para>
///
/// <para>⚠️ <b>ومفيش متحقّق للأمر ده — عن قصد.</b> المتحقّق بيشتغل
/// <b>قبل</b> المعالج، والقديم بيقرا الصف الأول: معرّف مش موجود
/// بياخد <c>404</c> حتى لو السبب فاضي. فالفحوص كلها هنا بعد
/// القراية، بنفس ترتيب القديم.</para>
/// </summary>
public sealed class DeleteReportCommandHandler(
    IReportRepository reports,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<DeleteReportCommand, Result<ReportActionResponse>>
{
    public const string Done = "الفحص اتمسح واتسجّل في المراجعة";

    public async Task<Result<ReportActionResponse>> Handle(
        DeleteReportCommand command, CancellationToken cancellationToken)
    {
        var row = await reports.FindForUpdateAsync(me.TenantId, command.Id, cancellationToken);

        if (row is null) return Result.Failure<ReportActionResponse>(ReportErrors.NotFound);

        // ⚠️ السياسة على المسار بتمنع ده قبل ما يوصل هنا — والفحص ده
        // عشان النص يفضل نص القديم لو المعالج اتنده من مكان تاني.
        if (!me.IsManagerOrAbove)
            return Result.Failure<ReportActionResponse>(ReportErrors.DeleteForbidden);

        // ⚠️ جديد — القديم كان بيكتب فوق مين مسح وليه (التفاصيل في
        // `ReportErrors.AlreadyDeleted`).
        if (row.IsDeleted)
            return Result.Failure<ReportActionResponse>(ReportErrors.AlreadyDeleted);

        string reason = (command.Reason ?? "").Trim();

        if (reason.Length < ReportDeletionRules.MinDeleteReasonLength)
            return Result.Failure<ReportActionResponse>(ReportErrors.DeleteReasonRequired);

        if (reason.Length > ReportDeletionRules.MaxReasonLength)
            return Result.Failure<ReportActionResponse>(ReportErrors.ReasonTooLong);

        row.IsDeleted = true;
        row.DeletedReason = reason;
        row.DeletedByName = TextClip.To(me.DisplayName, TextClip.Lengths.PersonName);
        row.DeletedAtUtc = DateTime.UtcNow;

        // ⚠️ بيانات الاسترجاع القديمة بتفضل زي ما هي — زي القديم.
        //    فحص اتمسح واترجع واتمسح تاني تاريخه كله باين.

        /*
          ⚠️ **سطر السجل ده زيادة عن القديم.**

          القديم ماكانش بيكتب سطر — الصف الممسوح نفسه كان هو السجل،
          وصفحة المراجعة بتقراه. بس رسالته بتقول «اتسجّل في المراجعة»،
          والمالك في الجديد بيقرا سجل النشاطات. فالسطر بيخلّي الرسالة
          صادقة في الجديد كمان.
        */
        audit.Record(
            AuditActions.ReportDeleted, "Report", row.Id, row.DeviceCode,
            $"فحص {ReportDeletionRules.DeviceLabel(row.DeviceCode)} اتمسح — {reason}");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReportActionResponse(row.Id, true, Done));
    }
}
