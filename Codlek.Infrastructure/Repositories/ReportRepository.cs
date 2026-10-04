using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class ReportRepository(AppDbContext db) : IReportRepository
{
    /// <summary>
    /// 🔴 <b>الفلتر مكتوب مرة واحدة — القايمة والتصدير بيناديوه.</b>
    /// </summary>
    private IQueryable<Report> Filtered(Guid tenantId, ReportListFilter filter)
    {
        // ⚠️ الممسوح مستبعد: لو الفني مسح فحص المفروض العدد يقل.
        var q = db.Reports.AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

        // 🔴 التضييق على الفني — الـHandler هو اللي بيقرّر، والقراية
        // بتطبّق.
        if (filter.TechnicianCode is { Length: > 0 } code)
            q = q.Where(r => r.TechnicianCode == code);

        if (filter.SearchPattern is { } pattern)
        {
            string exact = filter.ExactDeviceCode ?? "";

            /*
              ⚠️ **المطابقة الحرفية على الكود جمب البحث النصي.**

              العمود المطبَّع بيتبني وقت الاستقبال، فالفحوص اللي
              اتخزّنت بنسخة أقدم عمودها مافيهوش الكود لحد ما الملء
              يعدّي عليها. والمقارنة دي بتخلّيهم يتلاقوا فوراً.
            */
            q = q.Where(r =>
                r.DeviceCode == exact
                || EF.Functions.Like(r.SearchText, pattern, SearchPattern.Escape));
        }

        q = filter.Result switch
        {
            ReportResultFilter.Healthy => q.Where(r => r.FailCount == 0),
            ReportResultFilter.NeedsRepair => q.Where(r => r.FailCount > 0),

            // ⚠️ المقارنة على النص بالحرف — ده اللي الراكة بتكتبه.
            ReportResultFilter.NoHard => q.Where(r => r.StorageText == "No Hard"),

            ReportResultFilter.NeedsDeviceResolution => q.Where(r => r.NeedsDeviceResolution),

            _ => q,
        };

        /*
          🔴 **الفلتر ده كان في الشاشة والسيرفر مابياخدوش.**

          قايمة «الحاوية» اتضافت في الواجهة وبتبعت الباراميتر،
          وASP.NET بيرمي أي باراميتر مش معرَّف **في صمت** — فالمدير
          بيختار شحنة، الفلتر بيولّع بلون، والصفوف تفضل صفوف الكل.
          مفيش رسالة ومفيش قايمة فاضية: أرقام غلط معروضة كأنها
          مفلترة.

          ⚠️ والحاوية على **الجهاز** مش على الفحص.
        */
        if (filter.ContainerId is { } containerId && containerId != Guid.Empty)
            q = q.Where(r => r.Device != null && r.Device.ContainerId == containerId);

        if (filter.FromUtc is { } fromUtc) q = q.Where(r => r.StartedAtUtc >= fromUtc);

        // 🔴 أصغر من، مش أصغر من أو يساوي.
        if (filter.ToUtc is { } toUtc) q = q.Where(r => r.StartedAtUtc < toUtc);

        return q;
    }

    /// <summary>
    /// 🔴 <b>الترتيب بوقت السيرفر، مش بساعة الراكة.</b>
    ///
    /// <para>وقت بداية الفحص بيجي من الراكة، وساعة الراكة مش
    /// موثوقة — ودي مش نظرية: راكة في الميدان ساعتها كانت مقدّمة
    /// <b>٥٧ دقيقة</b>، فكان فحصها «١٧:٢٢ بتاعها» بيقعد فوق فحص
    /// اترفع بعده فعلاً الساعة ١٦:٥٢ بتوقيت السيرفر.</para>
    ///
    /// <para>⚠️ <b>والحل مش تعديل وقت البداية</b> — دي دليل تجاري
    /// على وقت الفحص وبتفضل زي ما هي. الترتيب بس هو اللي بينتقل
    /// لحقل السيرفر بيكتبه بنفسه ومحدش برّه بيأثّر عليه.</para>
    /// </summary>
    private static IQueryable<Report> Ordered(IQueryable<Report> q) =>
        q.OrderByDescending(r => r.ReceivedAtUtc)
            .ThenByDescending(r => r.StartedAtUtc)

            // ⚠️ وفاصل تعادل: دفعة مزامنة بتوصل بنفس اللحظة بالحرف.
            .ThenBy(r => r.Id);

    public async Task<(IReadOnlyList<Report> Rows, int TotalItems)> ListAsync(
        Guid tenantId, ReportListFilter filter, CancellationToken ct = default)
    {
        var q = Filtered(tenantId, filter);

        int total = await q.CountAsync(ct);

        var rows = await Ordered(q)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return (rows, total);
    }

    public async Task<IReadOnlyList<Report>> ExportAsync(
        Guid tenantId, ReportListFilter filter, int cap, CancellationToken ct = default) =>

        // ⚠️ `cap + 1` عشان المنادي يعرف إن فيه زيادة ويقولها في
        // الملف.
        await Ordered(Filtered(tenantId, filter)).Take(cap + 1).ToListAsync(ct);

    /// <summary>
    /// ⚠️ <b>وبترجّع الممسوح كمان — بخلاف القايمة.</b> صفحة الفحص
    /// بتعرض سبب المسح ومين مسحه وإمتى، والصف ده <b>دليل</b>: سجل
    /// المراجعة بيشاور عليه، وإخفاؤه بيخلّي الرابط يوصل لصفحة ميتة.
    /// </summary>
    public Task<Report?> FindDetailAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.Reports.AsNoTracking()
            .Include(r => r.Steps)
            .Include(r => r.Parts)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

    public Task<int> SnapshotComponentCountAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default) =>
        db.SnapshotComponents.AsNoTracking()
            .CountAsync(c => c.TenantId == tenantId && c.ReportId == reportId, ct);

    /// <summary>
    /// 🔴 <b><c>JSON_VALUE</c> في SQL، و<c>ISJSON</c> حارس
    /// إجباري.</b>
    ///
    /// <para>الحمولة الخام حوالي ٢٠ كيلوبايت للفحص الواحد، وقراية
    /// نصين منها في الذاكرة كانت بتسحبها كلها. و<c>JSON_VALUE</c>
    /// على نص مش JSON سليم <b>بيرمي</b> — فصف واحد بايظ كان هيوقّع
    /// الصفحة.</para>
    ///
    /// <para>⚠️ <b>والمعرّفات وسائط، مش ملزوقة في النص.</b></para>
    /// </summary>
    public async Task<ReportVersionFacts?> VersionsAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default)
    {
        const string Sql = """
            SELECT CASE WHEN ISJSON(RawJson) = 1
                        THEN JSON_VALUE(RawJson, '$.ApplicationVersion') END
                       AS ApplicationVersion,
                   CASE WHEN ISJSON(RawJson) = 1
                        THEN JSON_VALUE(RawJson, '$.TestDefinitionVersion') END
                       AS TestDefinitionVersion
              FROM Reports
             WHERE TenantId = @tenant AND Id = @id
            """;

        var rows = await db.Database
            .SqlQueryRaw<ReportVersionFacts>(
                Sql,
                new SqlParameter("@tenant", tenantId),
                new SqlParameter("@id", reportId))
            .ToListAsync(ct);

        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyDictionary<Guid, RackLabel>> RackLabelsAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default)
    {
        var wanted = Wanted(ids);

        if (wanted.Count == 0) return new Dictionary<Guid, RackLabel>();

        // ⚠️ الكود والاسم في قراية واحدة — القديم كان بيعمل اتنين.
        return await db.Racks.AsNoTracking()
            .Where(r => r.TenantId == tenantId && wanted.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => new RackLabel(r.RackCode, r.Name), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> DeviceCodesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default)
    {
        var wanted = Wanted(ids);

        if (wanted.Count == 0) return new Dictionary<Guid, string>();

        return await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && wanted.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.PublicCode, ct);
    }

    private static List<Guid> Wanted(IEnumerable<Guid?> ids) =>
        ids.OfType<Guid>().Distinct().ToList();
}
