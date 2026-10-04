using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Export;
using Codlek.Core.Spreadsheets;
using MediatR;

namespace Codlek.Application.Features.Export.ExportRacks;

/// <summary>
/// تصدير محطات الفحص.
///
/// <para>🔴 <b>ولا المفتاح ولا بصمته ولا بادئته في الشيت.</b> ملف
/// بينزل على جهاز المالك وبيتبعت في واتساب أحياناً — نفس القاعدة
/// اللي على نقطة القايمة بالظبط، ومطبّقة هنا كمان.</para>
///
/// <para>⚠️ <b>وسبب الإلغاء <u>موجود</u> في الملف.</b> ده سجل
/// مراجعة: المالك اللي بيراجع المحطات محتاج يعرف الملغية اتلغت
/// ليه.</para>
/// </summary>
public sealed class ExportRacksQueryHandler(
    IRackRepository racks, ICurrentUser me)
    : IRequestHandler<ExportRacksQuery, Result<ExportWorkbook>>
{
    public async Task<Result<ExportWorkbook>> Handle(
        ExportRacksQuery query, CancellationToken cancellationToken)
    {
        var rows = await racks.ListAsync(me.TenantId, cancellationToken);

        var sheet = new Sheet("المحطات", ExportColumns.Racks,
            ExportLimits.Capped(rows, r => new object?[]
            {
                r.RackCode,
                r.Name,
                r.Location,
                RackStatusText.Arabic(r.Status),
                r.AppVersion,
                ExportValues.Cairo(r.CreatedAtUtc),
                ExportValues.Cairo(r.RegisteredAtUtc),
                ExportValues.Cairo(r.LastSeenAtUtc),
                r.ReportsReceived,
                r.RevokedReason,
            }));

        return Result.Success(new ExportWorkbook("المحطات", [sheet]));
    }
}
