using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetAlerts;

public sealed class GetAlertsSummaryQueryHandler(
    IAnalyticsRepository analytics,
    ICurrentUser me)
    : IRequestHandler<GetAlertsSummaryQuery, Result<AlertsSummary>>
{
    public async Task<Result<AlertsSummary>> Handle(
        GetAlertsSummaryQuery query, CancellationToken cancellationToken)
    {
        /*
          🔴 **الفني بياخد أصفار، مش <c>403</c>.**

          الأيقونة دي بتتنده على **كل صفحة**، و<c>403</c> كان بيطلّع
          رسالة خطأ في ترويسة صفحة الفني كل مرة. والعدّادات على
          مستوى الشركة مالهاش معنى في صفحته أصلاً.

          ⚠️ والاستعلامات نفسها مابتتنفّذش — مش إخفاء في الواجهة.
        */
        if (!me.IsManagerOrAbove)
            return Result.Success(new AlertsSummary(0, 0, 0, 0));

        return Result.Success(await analytics.AlertsAsync(me.TenantId, cancellationToken));
    }
}
