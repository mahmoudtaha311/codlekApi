using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetPartsDemand;

/// <summary>
/// «يبان إيه أكتر قطع غيار مطلوبة».
///
/// <para>🔴 <b>الأرقام من القطع اللي <i>اتركّبت</i> فعلاً.</b> وفي
/// الرد علم بيقول إن «مطلوب» مالهاش مصدر النهاردة — الرقم المخترع
/// أسوأ من الرقم الناقص، لأن «قطعة مطلوبة كتير ومااتركّبتش» هو
/// بالظبط اللي بيقول إن المخزن ناقص، ولو اتلفّق هياخد قرار شراء
/// غلط.</para>
/// </summary>
public sealed class GetPartsDemandQueryHandler(
    IAnalyticsRepository analytics,
    ICurrentUser me)
    : IRequestHandler<GetPartsDemandQuery, Result<PartsDemandResponse>>
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;

    public async Task<Result<PartsDemandResponse>> Handle(
        GetPartsDemandQuery query, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriod.Resolve(query.Range, query.From, query.To);

        int take = Math.Clamp(query.Limit ?? DefaultTake, 1, MaxTake);

        var fitted = await analytics.FittedPartsAsync(
            me.TenantId, period, cancellationToken);

        var noted = await analytics.NotedPartNamesAsync(
            me.TenantId, period, cancellationToken);

        /*
          🔴 **التجميع بالاسم بعد التطبيع.**

          الفني بيكتب «شاشة» و«شاشه» و«شاشة  » — ومن غير التطبيع دي
          تلات صفوف وكل واحد رقمه صغير، فأكتر قطعة بتختفي في
          التفتيت.

          ⚠️ والتطبيع بيحصل **في الذاكرة** مش في SQL: الدالة مالهاش
          ترجمة، وEF كان هيجيب الصفوف كلها في صمت لو حطّيناها في
          التجميع. والجلب محدود بالفترة.
        */
        var notedByKey = noted
            .Select(Key)
            .Where(k => k.Length > 0)
            .GroupBy(k => k, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var items = fitted
            .Select(p => new { Key = Key(p.Name), Row = p })
            .Where(x => x.Key.Length > 0)
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .Select(g => new PartDemandItem(
                /*
                  ⚠️ **الاسم المعروض هو أشهر كتابة للقطعة، مش النص
                  المطبّع.** المطبّع مفتاح تجميع وبس — عرضه كان
                  بيدّي «شاشه» بدل «شاشة».
                */
                Name: g.GroupBy(x => x.Row.Name.Trim(), StringComparer.Ordinal)
                    .OrderByDescending(n => n.Count())
                    .ThenBy(n => n.Key, StringComparer.Ordinal)
                    .First().Key,

                // ⚠️ أول كود مخزن مكتوب — الخانة دي بتفضل فاضية كتير.
                InventoryCode: g.Select(x => x.Row.InventoryCode.Trim())
                    .FirstOrDefault(c => c.Length > 0) ?? "",

                FittedTimes: g.Count(),

                // ⚠️ الكمية الفاضية أو السالبة بتتحسب واحد — القطعة
                // اتركّبت فعلاً.
                FittedQuantity: g.Sum(x => Math.Max(1, x.Row.Quantity)),

                Devices: g.Select(x => x.Row.DeviceId)
                    .Where(d => d is not null)
                    .Distinct()
                    .Count(),

                NotedOnTests: notedByKey.TryGetValue(g.Key, out int times) ? times : 0))
            .OrderByDescending(i => i.FittedQuantity)
            .ThenByDescending(i => i.FittedTimes)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .Take(take)
            .ToList();

        return Result.Success(new PartsDemandResponse(
            AnalyticsScope.Info(period),
            items,

            /*
              🔴 <b>دايماً <c>false</c> النهاردة.</b>

              «مطلوب» جدول محلي على الراكة، مالوش كيان على السيرفر
              ولا نوع مزامنة — يعني عمره ما بيوصل هنا. والعلم ده
              بيقولها للواجهة صريح بدل ما نرجّع صفر ويتقرا «مفيش
              طلبات».
            */
            RequestedAvailable: false));
    }

    /// <summary>
    /// مفتاح تجميع اسم القطعة — <b>دالة نقية</b>.
    ///
    /// <para>⚠️ بتستعمل نفس التطبيع اللي المشروع كله بيستعمله
    /// للبحث، عشان «شاشة» و«شاشه» يبقوا نفس القطعة. والاسم اللي
    /// بيتعرض بييجي من الصفوف نفسها مش من هنا.</para>
    /// </summary>
    private static string Key(string? name) => ArabicText.Normalize((name ?? "").Trim());
}
