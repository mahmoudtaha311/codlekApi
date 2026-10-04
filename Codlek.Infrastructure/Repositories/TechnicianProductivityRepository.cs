using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class TechnicianProductivityRepository(AppDbContext db)
    : ITechnicianProductivityRepository
{
    private IQueryable<Report> Reports(Guid tenantId) =>
        db.Reports.AsNoTracking().Where(r => r.TenantId == tenantId && !r.IsDeleted);

    /// <summary>
    /// ⚠️ الفحوص اللي فيها اسم فني — لحل الاسم.
    /// </summary>
    private IQueryable<Report> Named(Guid tenantId, string code) =>
        Reports(tenantId).Where(r => r.TechnicianCode == code && r.TechnicianName != "");

    /// <summary>
    /// 🔴 <b>التجميع بيطلع في نوع <u>مجهول</u>، والتحويل للعقد بعد
    /// القراية — ودي مش زينة.</b>
    ///
    /// <para>EF بيعرف يرتّب ويحسب على خاصية نوع مجهول لأنه متتبّع
    /// أصلها. لكن مع <b>باراميتر مُنشئ</b> (<c>record</c> موضعي)
    /// بيبقى مجرد قيمة مالهاش أصل معروف — والاستعلام كله
    /// <b>بيفشل الترجمة وقت التشغيل</b> مع استعلام فرعي مرتّب
    /// جوّاه (اسم الفني من أحدث فحص).</para>
    ///
    /// <para>⚠️ <b>والنقطة كانت بترجّع ٥٠٠ فعلاً</b> — بان من فحص
    /// HTTP حقيقي، مش من قراية. والمشروع القديم فيه نفس التحذير
    /// مكتوب في مكانين (الأعطال على اللوحة، وجهات الجرد) — وقعنا
    /// فيه تالت مرة في نفس الشكل.</para>
    /// </summary>
    public async Task<IReadOnlyList<TestingProductivityFacts>> TestingAsync(
        Guid tenantId, AnalyticsPeriod window, CancellationToken ct = default)
    {
        var rows = await Reports(tenantId)
            .Where(r => r.StartedAtUtc >= window.FromUtc && r.StartedAtUtc < window.ToUtc)

            // ⚠️ الفحص اللي مالوش كود فني مابيدخلش — مفيش حد ينسبه
            // له.
            .Where(r => r.TechnicianCode != "")
            .GroupBy(r => r.TechnicianCode)
            .Select(g => new
            {
                Code = g.Key,

                // ⚠️ الاسم من **أحدث** فحص — دليل المحطة، مش جدول
                // الحسابات.
                Name = g.OrderByDescending(x => x.StartedAtUtc)
                    .Select(x => x.TechnicianName)
                    .FirstOrDefault(),

                Total = g.Count(),
                Pass = g.Sum(x => x.PassCount),
                Fail = g.Sum(x => x.FailCount),
                Error = g.Sum(x => x.ErrorCount),
                NotPresent = g.Sum(x => x.NotPresentCount),
                Skip = g.Sum(x => x.SkipCount),

                // ⚠️ المقام بيتعدّ لوحده عشان الفحوص اللي مالهاش
                // مدة ماتنقّصش المتوسط.
                Timed = g.Count(x => x.DurationMs > 0),
                Duration = g.Sum(x => x.DurationMs),
                LastAtUtc = g.Max(x => (DateTime?)x.StartedAtUtc),
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        return rows
            .Select(r => new TestingProductivityFacts(
                r.Code,
                r.Name ?? "",
                r.Total,
                r.Pass,
                r.Fail,
                r.Error,
                r.NotPresent,
                r.Skip,
                r.Timed,
                r.Duration,
                r.LastAtUtc))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, RepairProductivityFacts>> RepairsAsync(
        Guid tenantId, AnalyticsPeriod window, CancellationToken ct = default)
    {
        var rows = await db.RepairWorkItems.AsNoTracking()
            .Where(w => w.TenantId == tenantId
                     && w.CompletedAtUtc != null
                     && w.CompletedAtUtc >= window.FromUtc
                     && w.CompletedAtUtc < window.ToUtc)
            .Select(w => new
            {
                // ⚠️ الأمر اللي مالوش «اللي قفله» بيرجع للمتسند —
                // الصفوف القديمة كانت بتقفل من غير ما تسجّل مين
                // قفلها.
                TechId = w.CompletedByTechnicianId ?? w.AssignedTechnicianId,
                w.StartedAtUtc,
                w.CompletedAtUtc,
            })
            .Where(x => x.TechId != null)
            .GroupBy(x => x.TechId!.Value)
            .Select(g => new
            {
                TechId = g.Key,
                Count = g.Count(),

                // ⚠️ اللي مالوش بداية مابيدخلش المتوسط، والمقام
                // بيتعدّ لوحده.
                Timed = g.Count(x => x.StartedAtUtc != null),

                /*
                  🔴 **بالثواني، والقسمة بعد الجمع.**

                  فرق الدقايق في SQL Server بيعدّ **عبور حدود
                  الدقيقة** مش الوقت اللي فات: من ١٠:٠٠:٥٩
                  لـ١٠:٠١:٠٠ بيقول دقيقة كاملة.

                  ⚠️ **والجمع على <c>double</c> عن قصد:** مجموع
                  الثواني لشركة فيها عشرات الآلاف من الأوامر بيعدّي
                  حدود <c>int</c>، و<c>SUM</c> على عمود <c>int</c> في
                  SQL Server بيرمي «Arithmetic overflow» بدل ما
                  يوسّع النوع لوحده.
                */
                Seconds = g.Sum(x =>
                    (double)(EF.Functions.DateDiffSecond(x.StartedAtUtc, x.CompletedAtUtc) ?? 0)),

                LastAtUtc = g.Max(x => x.CompletedAtUtc),
            })
            .ToListAsync(ct);

        if (rows.Count == 0) return new Dictionary<string, RepairProductivityFacts>();

        var ids = rows.Select(r => r.TechId).ToList();

        var people = await db.Technicians.AsNoTracking()
            .Where(t => t.TenantId == tenantId && ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Code, t.DisplayName })
            .ToListAsync(ct);

        var byId = people.ToDictionary(p => p.Id);

        /*
          🔴 **والضم بالكود مش بالمعرّف.**

          ده اللي بيوحّد على نفس مفتاح الفحوص، فالجسر يفضل مقفول من
          الناحيتين. والفني اللي صفه اتشال من الجدول بيتستبعد — مفيش
          كود ننسبه له.
        */
        return rows
            .Where(r => byId.ContainsKey(r.TechId))
            .GroupBy(r => byId[r.TechId].Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    int count = g.Sum(x => x.Count);
                    int timed = g.Sum(x => x.Timed);
                    double seconds = g.Sum(x => x.Seconds);

                    double average = timed == 0 ? 0 : Math.Round(seconds / 60.0 / timed, 1);

                    return new RepairProductivityFacts(
                        byId[g.First().TechId].DisplayName,
                        count,

                        // ⚠️ والسالب بيتقص على صفر — وقت راكة
                        // قدّامه.
                        Math.Max(0, average),
                        g.Max(x => x.LastAtUtc));
                },
                StringComparer.OrdinalIgnoreCase);
    }

    public Task<bool> HasAnyReportAsync(
        Guid tenantId, string code, CancellationToken ct = default) =>
        Reports(tenantId).AnyAsync(r => r.TechnicianCode == code, ct);

    public Task<string?> NameFromReportsAsync(
        Guid tenantId, string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return Task.FromResult<string?>(null);

        return Named(tenantId, code)
            .OrderByDescending(r => r.StartedAtUtc)
            .ThenBy(r => r.Id)
            .Select(r => r.TechnicianName)
            .FirstOrDefaultAsync(ct)!;
    }

    /// <summary>
    /// ⚠️ <b>ونفس القاعدة هنا: نوع مجهول في SQL، والتحويل بعد
    /// القراية.</b> الاستعلام ده بيترجم عادي النهاردة (مفيش استعلام
    /// فرعي مرتّب جوّاه)، بس الشكل اللي بيفشل الترجمة اتجنّب في
    /// المكانين — القاعدة «مفيش مُنشئ جوّه تجميع»، مش «مفيش مُنشئ لما
    /// يفشل».
    /// </summary>
    public async Task<TestingProductivityFacts?> OneAsync(
        Guid tenantId, string code, AnalyticsPeriod window, CancellationToken ct = default)
    {
        var row = await Reports(tenantId)
            .Where(r => r.TechnicianCode == code
                     && r.StartedAtUtc >= window.FromUtc
                     && r.StartedAtUtc < window.ToUtc)

            // ⚠️ `GroupBy(_ => 1)` بيخلّي SQL يرجّع صف واحد فيه كل
            // المجاميع — بدل عشر قرايات.
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pass = g.Sum(x => x.PassCount),
                Fail = g.Sum(x => x.FailCount),
                Error = g.Sum(x => x.ErrorCount),
                NotPresent = g.Sum(x => x.NotPresentCount),
                Skip = g.Sum(x => x.SkipCount),
                Timed = g.Count(x => x.DurationMs > 0),
                Duration = g.Sum(x => x.DurationMs),
                LastAtUtc = g.Max(x => (DateTime?)x.StartedAtUtc),
            })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        return new TestingProductivityFacts(
            code,

            // ⚠️ الاسم مش من هنا — بيتحل من كل التاريخ في قراية
            // تانية.
            "",

            row.Total,
            row.Pass,
            row.Fail,
            row.Error,
            row.NotPresent,
            row.Skip,
            row.Timed,
            row.Duration,
            row.LastAtUtc);
    }

    public async Task<IReadOnlyList<Report>> RecentAsync(
        Guid tenantId, string code, AnalyticsPeriod window, int take,
        CancellationToken ct = default) =>
        await Reports(tenantId)
            .Where(r => r.TechnicianCode == code
                     && r.StartedAtUtc >= window.FromUtc
                     && r.StartedAtUtc < window.ToUtc)
            .OrderByDescending(r => r.StartedAtUtc)

            // ⚠️ وفاصل تعادل: دفعة مزامنة بتوصل بنفس اللحظة.
            .ThenBy(r => r.Id)
            .Take(take)
            .ToListAsync(ct);
}
