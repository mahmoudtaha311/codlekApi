using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Reports;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

/// <summary>
/// أرقام اللوحة — <b>كل تجميعة بتنزل SQL</b>.
/// </summary>
public sealed class AnalyticsRepository(AppDbContext db) : IAnalyticsRepository
{
    /// <summary>
    /// فحوص الشركة، مضيّقة على الفني لو اللي داخل مش مدير.
    ///
    /// <para>🔴 <b>التضييق ده موجود في كل دالة.</b> الفني بيشوف
    /// شغله هو — نفس قاعدة الصفحة الرئيسية، ولازم تتكرر هنا لأن
    /// مفيش فلتر عام بيفرضها.</para>
    ///
    /// <para>⚠️ <b>والممسوح مستبعد دايماً.</b> لو الفني مسح فحص
    /// المفروض العدد يقل، ودي كانت مشكلة حقيقية في البرنامج
    /// المكتبي.</para>
    /// </summary>
    private IQueryable<Report> Scoped(Guid tenantId, string? technicianCode)
    {
        var reports = db.Reports.AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

        // ⚠️ `null` أو فاضي = كل الفنيين. التوحيد بيحصل في الـHandler
        // مرة واحدة.
        return technicianCode is { Length: > 0 } code
            ? reports.Where(r => r.TechnicianCode == code)
            : reports;
    }

    private IQueryable<Report> InPeriod(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period) =>
        Scoped(tenantId, technicianCode)
            .Where(r => r.StartedAtUtc >= period.FromUtc && r.StartedAtUtc < period.ToUtc);

    // =================================================================
    //  الأرقام الرئيسية
    // =================================================================

    public async Task<(DashboardKpis Kpis, TestCounts Counts)> KpisAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        bool includeStations, CancellationToken ct = default)
    {
        var inRange = InPeriod(tenantId, technicianCode, period);

        /*
          🔴 **تجميعة واحدة لكل العدّادات.**

          `GroupBy(_ => 1)` بيخلّي SQL يرجّع صف واحد فيه كل المجاميع.
          والبديل — عشر `CountAsync` منفصلة — كان عشر رحلات للقاعدة
          على كل فتحة لوحة.
        */
        var agg = await inRange
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pass = g.Sum(x => x.PassCount),
                Fail = g.Sum(x => x.FailCount),
                Error = g.Sum(x => x.ErrorCount),
                NotPresent = g.Sum(x => x.NotPresentCount),
                Skip = g.Sum(x => x.SkipCount),

                // 🔴 «نضيف» = صفر فشل **وصفر** خطأ قراءة. فحص فيه
                // خطوة ما اشتغلتش مش ناجح.
                Clean = g.Count(x => x.FailCount == 0 && x.ErrorCount == 0),

                NeedsReview = g.Count(x => x.FailCount > 0 || x.ErrorCount > 0),

                // ⚠️ المتوسط على اللي ليه مدة بس.
                Timed = g.Count(x => x.DurationMs > 0),
                Duration = g.Sum(x => x.DurationMs),
            })
            .FirstOrDefaultAsync(ct);

        int devicesTested = await inRange
            .Where(r => r.DeviceId != null)
            .Select(r => r.DeviceId)
            .Distinct()
            .CountAsync(ct);

        int activeTechnicians = await inRange
            .Where(r => r.TechnicianCode != "")
            .Select(r => r.TechnicianCode)
            .Distinct()
            .CountAsync(ct);

        /*
          🔴 **رقم على مستوى الشركة كلها — للمدير وبس.**

          باقي اللوحة بيتضيّق فالفني بيشوف فحوصاته هو، زي ما الصفحة
          بتقول له بالنص. والعدّاد ده كان بيعدّي من غير تضييق، فالفني
          كان بيعرف كام محطة في الورشة — رقم إداري الصفحة المقابلة
          بتمنعه عنه صراحةً.

          ⚠️ والتضييق هنا مش إخفاء في الواجهة: **الاستعلام نفسه
          مابيتنفّذش**.
        */
        int activeStations = includeStations
            ? await db.Racks.AsNoTracking()
                .CountAsync(r => r.TenantId == tenantId && r.Status == RackStatus.Active, ct)
            : 0;

        int total = agg?.Total ?? 0;

        var kpis = new DashboardKpis(
            total,
            devicesTested,
            agg?.NeedsReview ?? 0,
            total == 0 ? 0 : Math.Round((agg?.Clean ?? 0) * 100.0 / total, 1),
            agg is null || agg.Timed == 0 ? 0 : Math.Round(agg.Duration / 60000.0 / agg.Timed, 1),
            activeTechnicians,
            activeStations);

        var counts = new TestCounts(
            agg?.Pass ?? 0, agg?.Fail ?? 0, agg?.Error ?? 0,
            agg?.NotPresent ?? 0, agg?.Skip ?? 0);

        return (kpis, counts);
    }

    // =================================================================
    //  المنحنى
    // =================================================================

    public async Task<IReadOnlyList<TrendPoint>> TrendAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default)
    {
        var inRange = InPeriod(tenantId, technicianCode, period);

        /*
          🔴 **الإزاحة بتتحسب مرة واحدة وبتتحقن في الاستعلام.**

          التجميع لازم يبقى بأيام **القاهرة** والقاعدة شايلة UTC؛
          وتحويل كل صف في SQL مستحيل (مفيش دالة مناطق زمنية مترجمة).
          راجع `CairoDay.OffsetMinutes` للحد المعروف.
        */
        int offset = period.OffsetMinutes;

        if (period.Hourly)
        {
            var hours = await inRange
                .GroupBy(r => r.StartedAtUtc.AddMinutes(offset).Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Total = g.Count(),
                    Clean = g.Count(x => x.FailCount == 0 && x.ErrorCount == 0),
                })
                .ToListAsync(ct);

            var byHour = hours.ToDictionary(h => h.Hour);

            // ⚠️ ٢٤ ساعة كاملة — الساعة اللي ماشتغلش فيها حد بتبان
            // صفر، مش بتختفي.
            return Enumerable.Range(0, 24)
                .Select(h =>
                {
                    byHour.TryGetValue(h, out var row);

                    int total = row?.Total ?? 0;
                    int clean = row?.Clean ?? 0;

                    return new TrendPoint(
                        h.ToString("00"), h.ToString("00") + ":00", total, clean, total - clean);
                })
                .ToList();
        }

        var days = await inRange
            .GroupBy(r => r.StartedAtUtc.AddMinutes(offset).Date)
            .Select(g => new
            {
                Day = g.Key,
                Total = g.Count(),
                Clean = g.Count(x => x.FailCount == 0 && x.ErrorCount == 0),
            })
            .ToListAsync(ct);

        var byDay = days.ToDictionary(d => d.Day.Date);

        var points = new List<TrendPoint>();

        // 🔴 كل يوم في الفترة — والفاضي بصفر.
        for (var d = period.FirstCairoDay; d <= period.LastCairoDay; d = d.AddDays(1))
        {
            byDay.TryGetValue(d.Date, out var row);

            int total = row?.Total ?? 0;
            int clean = row?.Clean ?? 0;

            points.Add(new TrendPoint(
                d.ToString("yyyy-MM-dd"), d.ToString("dd/MM"), total, clean, total - clean));
        }

        return points;
    }

    // =================================================================
    //  الفنيين والراكات
    // =================================================================

    public async Task<IReadOnlyList<TechnicianActivityItem>> TechnicianActivityAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period, int take,
        CancellationToken ct = default)
    {
        var rows = await InPeriod(tenantId, technicianCode, period)
            .Where(r => r.TechnicianCode != "")
            .GroupBy(r => r.TechnicianCode)
            .Select(g => new
            {
                Code = g.Key,
                Reports = g.Count(),
                NeedsReview = g.Count(x => x.FailCount > 0 || x.ErrorCount > 0),
                Timed = g.Count(x => x.DurationMs > 0),
                Duration = g.Sum(x => x.DurationMs),

                // ⚠️ لقطة الاسم من أي صف — الاسم متخزّن على الفحص
                // نفسه، فمفيش ربط محتاجينه، والاسم بيفضل اسم وقته.
                Name = g.Max(x => x.TechnicianName),
            })
            .OrderByDescending(x => x.Reports)
            .ThenBy(x => x.Code)
            .Take(take)
            .ToListAsync(ct);

        return rows
            .Select(r => new TechnicianActivityItem(
                r.Code,
                r.Name ?? "",
                r.Reports,
                r.NeedsReview,
                r.Timed == 0 ? 0 : Math.Round(r.Duration / 60000.0 / r.Timed, 1)))
            .ToList();
    }

    public async Task<IReadOnlyList<RackLoadFacts>> RackActivityAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default)
    {
        var counts = await InPeriod(tenantId, technicianCode, period)
            .Where(r => r.SourceRackId != null)
            .GroupBy(r => r.SourceRackId!.Value)
            .Select(g => new { RackId = g.Key, Reports = g.Count() })
            .ToListAsync(ct);

        var byRack = counts.ToDictionary(c => c.RackId, c => c.Reports);

        /*
          ⚠️ **الراكات كلها بترجع** — راكة ماشتغلتش في الفترة معلومة
          برضو، وإخفاؤها بيخلّي «مفيش شغل عليها» و«مش موجودة» شكلهم
          واحد.
        */
        var racks = await db.Racks.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Status != RackStatus.Revoked)
            .Select(r => new { r.Id, r.RackCode, r.Name, r.Status, r.LastSeenAtUtc })
            .ToListAsync(ct);

        return racks
            .Select(r => new RackLoadFacts(
                r.Id,
                r.RackCode,
                r.Name,
                r.Status,
                byRack.TryGetValue(r.Id, out int n) ? n : 0,
                r.LastSeenAtUtc))
            .OrderByDescending(r => r.Reports)
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .ToList();
    }

    // =================================================================
    //  الأعطال والمدة والأجهزة
    // =================================================================

    public async Task<IReadOnlyList<NamedCountItem>> FailureHotspotsAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period, int take,
        CancellationToken ct = default)
    {
        var scoped = InPeriod(tenantId, technicianCode, period);

        /*
          ⚠️ **الربط بالفحوص بيتعمل بـ<c>Any</c> على الاستعلام
          المضيّق** — فالخطوات بتتفلتر بالشركة وبالفترة جوّه SQL.
          جدول الخطوات أكبر من جدول الفحوص بمراحل، فسحبه للذاكرة مش
          خيار.
        */
        var rows = await db.Steps.AsNoTracking()
            .Where(s => s.Status == FailedStep && scoped.Any(r => r.Id == s.ReportId))
            .GroupBy(s => s.Title)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(take)
            .ToListAsync(ct);

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => new NamedCountItem(r.Name, r.Count))
            .ToList();
    }

    /// <summary>
    /// 🔴 الحالة <c>٢</c> = «فيه مشكلة» — نفس الترقيم اللي بيحسب
    /// بيه عدّاد الفشل على الفحص.
    /// </summary>
    private const int FailedStep = 2;

    public async Task<IReadOnlyList<int>> DurationBucketsAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default)
    {
        var rows = await InPeriod(tenantId, technicianCode, period)

            // ⚠️ اللي مالوش مدة مابيدخلش خالص.
            .Where(r => r.DurationMs > 0)
            .GroupBy(r =>
                r.DurationMs < DurationBucket.UnderFive ? 0 :
                r.DurationMs < DurationBucket.UnderTen ? 1 :
                r.DurationMs < DurationBucket.UnderTwenty ? 2 : 3)
            .Select(g => new { Bucket = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byBucket = rows.ToDictionary(r => r.Bucket, r => r.Count);

        return Enumerable.Range(0, DurationBucket.All().Count)
            .Select(i => byBucket.TryGetValue(i, out int n) ? n : 0)
            .ToList();
    }

    public async Task<DeviceMix> DeviceMixAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default)
    {
        var all = Scoped(tenantId, technicianCode);

        /*
          🔴 **التجميع على كل تاريخ الجهاز، مش على الفترة.**

          والفرق ده هو المقصود: جهاز اتفحص خمس مرات الشهر ده وأول مرة
          كانت السنة اللي فاتت هو «إعادة فحص»، مش خمس أجهزة جديدة.

          ⚠️ والاستعلام بيمشي على فهرس (الجهاز، وقت البداية) الموجود
          أصلاً، فالتجميع مابيقراش الجدول كله.
        */
        var linked = await all
            .Where(r => r.DeviceId != null)
            .GroupBy(r => r.DeviceId!.Value)
            .Select(g => new
            {
                First = g.Min(x => x.StartedAtUtc),
                InPeriod = g.Any(x =>
                    x.StartedAtUtc >= period.FromUtc && x.StartedAtUtc < period.ToUtc),
            })
            .Where(x => x.InPeriod)
            .GroupBy(x => x.First >= period.FromUtc)
            .Select(g => new { IsNew = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int unlinked = await InPeriod(tenantId, technicianCode, period)
            .CountAsync(r => r.DeviceId == null, ct);

        return new DeviceMix(
            linked.FirstOrDefault(x => x.IsNew)?.Count ?? 0,
            linked.FirstOrDefault(x => !x.IsNew)?.Count ?? 0,
            unlinked);
    }

    // =================================================================
    //  القوايم
    // =================================================================

    public async Task<(IReadOnlyList<ReportListItem> Attention,
                       IReadOnlyList<ReportListItem> Recent)>
        ListsAsync(
            Guid tenantId, string? technicianCode, AnalyticsPeriod period, int take,
            CancellationToken ct = default)
    {
        var inRange = InPeriod(tenantId, technicianCode, period);

        var attention = await inRange
            .Where(r => r.FailCount > 0 || r.ErrorCount > 0)
            .OrderByDescending(r => r.StartedAtUtc)

            // ⚠️ فاصل تعادل: دفعة مزامنة بتوصل بنفس اللحظة.
            .ThenBy(r => r.Id)
            .Take(take)
            .ToListAsync(ct);

        var recent = await inRange
            .OrderByDescending(r => r.StartedAtUtc)
            .ThenBy(r => r.Id)
            .Take(take)
            .ToListAsync(ct);

        var all = attention.Concat(recent).ToList();

        // ⚠️ الأكواد للقايمتين في استعلام واحد لكل جدول — مش واحد
        // لكل صف.
        var rackCodes = await RackTextAsync(tenantId, all, name: false, ct);
        var rackNames = await RackTextAsync(tenantId, all, name: true, ct);
        var deviceCodes = await DeviceCodesAsync(tenantId, all, ct);

        ReportListItem Row(Report r) => new(
            r.Id,
            r.DeviceId,

            // 🔴 كود الجهاز المربوط هو الأصل، واللي في الفحص احتياطي.
            r.DeviceId is { } id && deviceCodes.TryGetValue(id, out var live) && live.Length > 0
                ? live
                : r.DeviceCode,

            r.Manufacturer,
            r.Model,
            r.CommercialModelName ?? "",
            r.Cpu,
            r.RamText,
            r.StorageText,
            r.TechnicianId,
            r.TechnicianName,
            r.TechnicianCode,
            Text(rackCodes, r.SourceRackId),
            Text(rackNames, r.SourceRackId),
            r.StartedAtUtc,
            r.ReceivedAtUtc,
            r.DurationMs,
            new TestCounts(
                r.PassCount, r.FailCount, r.ErrorCount, r.NotPresentCount, r.SkipCount),
            r.Scope,
            ReportScopeText.Arabic(r.Scope),
            r.NotRunCount);

        return (attention.Select(Row).ToList(), recent.Select(Row).ToList());
    }

    private static string Text(IReadOnlyDictionary<Guid, string> map, Guid? id) =>
        id is { } key && map.TryGetValue(key, out var value) ? value : "";

    private async Task<Dictionary<Guid, string>> RackTextAsync(
        Guid tenantId, IEnumerable<Report> reports, bool name, CancellationToken ct)
    {
        var wanted = reports.Select(r => r.SourceRackId).OfType<Guid>().Distinct().ToList();

        if (wanted.Count == 0) return [];

        return await db.Racks.AsNoTracking()
            .Where(r => r.TenantId == tenantId && wanted.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => name ? r.Name : r.RackCode, ct);
    }

    private async Task<Dictionary<Guid, string>> DeviceCodesAsync(
        Guid tenantId, IEnumerable<Report> reports, CancellationToken ct)
    {
        var wanted = reports.Select(r => r.DeviceId).OfType<Guid>().Distinct().ToList();

        if (wanted.Count == 0) return [];

        return await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && wanted.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.PublicCode, ct);
    }

    // =================================================================
    //  تقرير اليوم
    // =================================================================

    public async Task<long> TotalDurationMsAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default) =>
        await InPeriod(tenantId, technicianCode, period).SumAsync(r => r.DurationMs, ct);

    public Task<int> DeletedCountAsync(
        Guid tenantId, AnalyticsPeriod period, CancellationToken ct = default) =>
        db.Reports.AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId
                          && r.IsDeleted
                          && r.StartedAtUtc >= period.FromUtc
                          && r.StartedAtUtc < period.ToUtc, ct);

    public async Task<IReadOnlyList<NamedCountItem>> ReportPartsAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period, int take,
        CancellationToken ct = default)
    {
        var inRange = InPeriod(tenantId, technicianCode, period);

        var rows = await db.Parts.AsNoTracking()
            .Where(p => inRange.Any(r => r.Id == p.ReportId))
            .GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(take)
            .ToListAsync(ct);

        return rows
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new NamedCountItem(p.Name, p.Count))
            .ToList();
    }

    // =================================================================
    //  التحذيرات والجرد — بلا فترة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>تلات عدّادات على فهارس مفلترة</b> — كل واحد بيمسّ
    /// الصفوف اللي فيها المشكلة بس. ده اللي بيخلّي النقطة دي تتحمّل
    /// إنها بتتنده على كل صفحة.
    /// </summary>
    public async Task<AlertsSummary> AlertsAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        int partChanged = await db.Devices.AsNoTracking()
            .CountAsync(d => d.TenantId == tenantId && d.PartChangedAtUtc != null, ct);

        int duplicates = await db.Devices.AsNoTracking()
            .CountAsync(d => d.TenantId == tenantId
                          && d.Status == DeviceLifecycleStatus.DuplicateSuspected, ct);

        int unresolved = await db.Reports.AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId
                          && !r.IsDeleted
                          && r.NeedsDeviceResolution, ct);

        return new AlertsSummary(
            partChanged, duplicates, unresolved, partChanged + duplicates + unresolved);
    }

    public async Task<InventorySummary> InventoryAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        /*
          ⚠️ **كل اللي مش مدموج — زي القديم بالحرف (`ScopedActive`).**
          المدموج صفه بيفضل للتاريخ، وعدّه بيقول ١٠ لابات والحقيقة ٧.

          🔴 **بس «مش مدموج» مش «نشط».** المشتبه في تكراره والمتقاعد لسه
          لابات حقيقية في الورشة. نسخة سابقة كانت بتعدّ النشط بس، فلاب
          اتعلّم «مشتبه في تكراره» كان بيختفي من الجرد كله — والمشروعين
          شغّالين على نفس القاعدة فترة التحويل، فنفس الشاشة كانت هتدّي
          رقمين. اتلقط بتشغيل فحوص القديم على الجديد.
        */
        var devices = db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.Status != DeviceLifecycleStatus.Merged);

        var reports = db.Reports.AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

        var tested = devices.Where(d => reports.Any(r => r.DeviceId == d.Id));

        // 🔴 «سليم» = آخر فحص مفيهوش فشل ولا خطأ — نفس تعريف شاشة
        // التسليم بالحرف، عشان الرقمين يطابقوا بعض.
        var healthy = tested.Where(d => reports
            .Where(r => r.DeviceId == d.Id)
            .OrderByDescending(r => r.StartedAtUtc)
            .Select(r => r.FailCount + r.ErrorCount)
            .FirstOrDefault() == 0);

        int total = await devices.CountAsync(ct);
        int neverTested = await devices.CountAsync(d => !reports.Any(r => r.DeviceId == d.Id), ct);
        int healthyCount = await healthy.CountAsync(ct);
        int needsAttention = await tested.CountAsync(ct) - healthyCount;
        int handedOver = await devices.CountAsync(d => d.CurrentLocationId != null, ct);

        // ⚠️ استعلام مستقل مش طرح — «سليم» و«اتسلّم» مجموعتين
        // متقاطعتين.
        int readyToHand = await healthy.CountAsync(d => d.CurrentLocationId == null, ct);

        /*
          🔴 **الاستعلام ده كان بيوقّع النقطة كلها بـ٥٠٠.**

          كان بيحوّل للعقد الأول وبعدين يرتّب على خاصية النوع الخاص.
          والترتيب بعد التحويل لنوع خاص **مش قابل للترجمة لـSQL**،
          وEF بيرمي وقت ما يبني الاستعلام — مش وقت القراية. يعني مش
          مسألة بيانات: بيقع دايماً، على أي قاعدة، حتى لو مفيش ولا
          جهاز اتسلّم.

          ⚠️ **والنقطة كلها بتروح معاه**، مش رقم الجهات بس — «سليم»
          و«اتسلّم» و«لم تُفحص» كلهم في نفس الرد. وعشان كده السؤال
          كان «فين الأجهزة السليمة».

          فالترتيب دلوقتي على نوع مجهول، والتحويل للعقد بعد ما الصفوف
          توصل.
        */
        var destinations = await devices
            .Where(d => d.CurrentLocationId != null)
            .GroupBy(d => d.CurrentLocation!.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        return new InventorySummary(
            total,
            healthyCount,
            needsAttention,
            neverTested,
            handedOver,
            readyToHand,
            destinations.Select(x => new NamedCountItem(x.Name, x.Count)).ToList());
    }

    // =================================================================
    //  قطع الغيار
    // =================================================================

    public async Task<IReadOnlyList<FittedPartFacts>> FittedPartsAsync(
        Guid tenantId, AnalyticsPeriod period, CancellationToken ct = default)
    {
        // ⚠️ الفترة على **فتح أمر الصيانة** — القطعة مالهاش تاريخ
        // خاص بيها.
        var work = db.RepairWorkItems.AsNoTracking()
            .Where(w => w.TenantId == tenantId
                     && w.OpenedAtUtc >= period.FromUtc
                     && w.OpenedAtUtc < period.ToUtc);

        return await db.RepairParts.AsNoTracking()
            .Where(p => p.TenantId == tenantId && work.Any(w => w.Id == p.WorkItemId))
            .Select(p => new FittedPartFacts(
                p.Name,
                p.InventoryCode,
                p.Quantity,
                work.Where(w => w.Id == p.WorkItemId)
                    .Select(w => (Guid?)w.DeviceId)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> NotedPartNamesAsync(
        Guid tenantId, AnalyticsPeriod period, CancellationToken ct = default) =>
        await db.Parts.AsNoTracking()
            .Where(p => db.Reports.Any(r => r.Id == p.ReportId
                                         && r.TenantId == tenantId
                                         && !r.IsDeleted
                                         && r.StartedAtUtc >= period.FromUtc
                                         && r.StartedAtUtc < period.ToUtc))
            .Select(p => p.Name)
            .ToListAsync(ct);
}
