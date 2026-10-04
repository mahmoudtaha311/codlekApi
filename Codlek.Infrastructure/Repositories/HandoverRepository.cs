using Codlek.Application.Contracts.Handover;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Handover;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class HandoverRepository(AppDbContext db) : IHandoverRepository
{
    /// <summary>
    /// اللابات اللي <b>ينفع</b> تتسلّم — <b>قاعدة واحدة، مكان
    /// واحد</b>.
    ///
    /// <para>🔴 <b>العيب اللي الدالة دي اتعملت عشانه:</b> قايمة
    /// المرشّحين كانت بتبص على <b>نتيجة آخر فحص وبس</b>. يعني لاب
    /// عليه أمر صيانة مفتوح، أو لاب في إيد فني دلوقتي، كان بيظهر
    /// «جاهز للتسليم» لو آخر فحص عليه كان نضيف — وينفع يتسلّم
    /// فعلاً. لاب تحت التصليح كان ممكن يروح المخزن.</para>
    ///
    /// <para>🔴 <b>وكانت متكتوبة مرتين:</b> مرة في القايمة ومرة في
    /// التحقّق وقت التسليم. أي قاعدة جديدة لازم تتكتب في الاتنين،
    /// وأي نسيان بيخلّي الشاشة تعرض لاب والنقطة ترفضه — أو أسوأ،
    /// العكس.</para>
    /// </summary>
    private IQueryable<Device> Eligible(Guid tenantId)
    {
        var reports = db.Reports.AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

        return db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.Status == DeviceLifecycleStatus.Active)

            // ⚠️ واللي عمره ما اتفحص مابيظهرش — مفيش فحص ≠ سليم.
            .Where(d => reports.Any(r => r.DeviceId == d.Id))

            /*
              ⚠️ **«سليم» = صفر فشل <u>وصفر</u> خطأ قراءة.**

              فيه تعريف تاني في التقارير بيتجاهل أخطاء القراءة؛
              اتاخد الأشد هنا عن قصد — التسليم مالوش رجعة، والقطعة
              اللي مااتقرتش مش «سليمة».
            */
            .Where(d => reports
                .Where(r => r.DeviceId == d.Id)
                .OrderByDescending(r => r.StartedAtUtc)
                .Select(r => r.FailCount + r.ErrorCount)
                .FirstOrDefault() == 0)

            /*
              🔴 **أمر صيانة مفتوح = اللاب مشغول**، مهما كان آخر فحص
              بيقول إيه. و«تمت» و«تعذّر» و«ملغاة» أوامر مقفولة.
            */
            .Where(d => !db.RepairWorkItems.Any(w =>
                w.TenantId == tenantId
                && w.DeviceId == d.Id
                && w.Status != RepairStatus.Completed
                && w.Status != RepairStatus.UnableToRepair
                && w.Status != RepairStatus.Cancelled))

            // 🔴 في إيد فني = مش على الرف. تسليمه معناه إن السجل
            // بيقول إنه في المخزن وهو مع الفني.
            .Where(d => d.CurrentHolderTechnicianId == null)

            .Where(d => d.OperationalStage != DeviceOperationalStage.NeedsRepair
                     && d.OperationalStage != DeviceOperationalStage.UnderRepair);
    }

    /// <summary>
    /// 🔴 <b>الفلتر مكتوب مرة واحدة ومستعمل في القايمة وفي
    /// المعرّفات.</b> ده اللي بيضمن إن «اختر كل اللي طلع» بيختار
    /// بالظبط اللي الشاشة عارضاها.
    /// </summary>
    private IQueryable<Device> Filtered(Guid tenantId, HandoverCandidateFilter filter)
    {
        var q = Eligible(tenantId);

        q = filter.Review switch
        {
            ReviewFilter.NotReviewed => q.Where(d => d.ReadyForHandoverAtUtc == null),
            ReviewFilter.Any => q,
            _ => q.Where(d => d.ReadyForHandoverAtUtc != null),
        };

        if (filter.ContainerId is { } containerId)
            q = q.Where(d => d.ContainerId == containerId);

        if (filter.SearchPattern is { } pattern)
        {
            string exact = filter.ExactCode ?? "";

            q = q.Where(d =>
                d.PublicCode == exact
                || EF.Functions.Like(d.SearchText, pattern, SearchPattern.Escape));
        }

        return q;
    }

    // =================================================================
    //  الجهات
    // =================================================================

    public async Task<IReadOnlyList<HandoverDestination>> DestinationsAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Locations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.IsActive)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Name)
            .Select(l => new HandoverDestination(l.Id, l.Code, l.Name, l.Kind.ToString()))
            .ToListAsync(ct);

    // ⚠️ متتبّعة: التسليم بيقرا نوعها وحالتها ويكتب حركة عليها.
    public Task<Location?> FindDestinationAsync(
        Guid tenantId, Guid destinationId, CancellationToken ct = default) =>
        db.Locations.FirstOrDefaultAsync(
            l => l.Id == destinationId && l.TenantId == tenantId, ct);

    // =================================================================
    //  المرشّحون
    // =================================================================

    public async Task<(IReadOnlyList<HandoverCandidateRow> Rows, int TotalItems)> CandidatesAsync(
        Guid tenantId, HandoverCandidateFilter filter, CancellationToken ct = default)
    {
        var reports = db.Reports.AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

        var q = Filtered(tenantId, filter);

        int total = await q.CountAsync(ct);

        var rows = await q

            // ⚠️ بالكود — ده الترتيب اللي المدير بيقرا بيه الرف،
            // والكود فريد فمفيش حاجة لفاصل تعادل.
            .OrderBy(d => d.PublicCode)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(d => new HandoverCandidateRow(
                d.Id,
                d.PublicCode,
                d.LastKnownManufacturer,
                string.IsNullOrWhiteSpace(d.CommercialModelName)
                    ? d.LastKnownModel
                    : d.CommercialModelName!,
                d.ContainerId == null ? "" : d.Container!.Code,
                d.OperationalStage,
                d.CurrentLocationId == null ? "" : d.CurrentLocation!.Name,

                reports.Where(r => r.DeviceId == d.Id)
                    .OrderByDescending(r => r.StartedAtUtc)
                    .Select(r => (DateTime?)r.StartedAtUtc)
                    .FirstOrDefault(),

                d.ReadyForHandoverAtUtc,
                d.ReadyByName ?? ""))
            .ToListAsync(ct);

        return (rows, total);
    }

    public async Task<(IReadOnlyList<Guid> Ids, int TotalItems)> CandidateIdsAsync(
        Guid tenantId, HandoverCandidateFilter filter, int cap, CancellationToken ct = default)
    {
        var q = Filtered(tenantId, filter);

        /*
          🔴 **العدّ قبل القص، مش بعده.**

          لو العدد اتحسب بعد القص، هيساوي عدد اللي رجع دايماً
          و«اتقص» هيبقى `false` على طول — يعني مدير عنده ٦٠٠ مرشّح
          يدوس «اختر الكل»، ياخد ٥٠٠، والشاشة تقوله «٥٠٠ من ٥٠٠».
          يسلّمهم وهو فاكر إنه خلّص الستمية.
        */
        int total = await q.CountAsync(ct);

        var ids = await q
            .OrderBy(d => d.PublicCode)
            .Take(cap)
            .Select(d => d.Id)
            .ToListAsync(ct);

        return (ids, total);
    }

    public async Task<IReadOnlyList<Guid>> EligibleIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default) =>
        await Eligible(tenantId)
            .Where(d => deviceIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> CodesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, int take,
        CancellationToken ct = default) =>
        await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && deviceIds.Contains(d.Id))
            .OrderBy(d => d.PublicCode)
            .Select(d => d.PublicCode)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> UnreviewedCodesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, int take,
        CancellationToken ct = default) =>
        await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId
                     && deviceIds.Contains(d.Id)
                     && d.ReadyForHandoverAtUtc == null)
            .OrderBy(d => d.PublicCode)
            .Select(d => d.PublicCode)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> CountExistingAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default) =>
        db.Devices.AsNoTracking()
            .CountAsync(d => d.TenantId == tenantId && deviceIds.Contains(d.Id), ct);

    /// <summary>
    /// 🔴 <b>متتبّعة — مش <c>AsNoTracking</c>.</b> الصفوف اللي بتيجي
    /// من قراية غير متتبّعة بتتعدّل في الذاكرة
    /// و<c>SaveChangesAsync</c> مابتكتبش حاجة: النقطة بترجّع
    /// «اتغيّر ٣» والقاعدة زي ما هي.
    /// </summary>
    public async Task<IReadOnlyList<Device>> TrackedForReviewAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, bool markedOnly,
        CancellationToken ct = default) =>
        await db.Devices
            .Where(d => d.TenantId == tenantId && deviceIds.Contains(d.Id))
            .Where(d => markedOnly
                ? d.ReadyForHandoverAtUtc != null
                : d.ReadyForHandoverAtUtc == null)
            .ToListAsync(ct);

    // =================================================================
    //  المستلمون والسجل
    // =================================================================

    /// <summary>
    /// ⚠️ <b>النوعين دول هما التسليم.</b> «نقل مكان» للمخزن،
    /// و«اتسلّم للمبيعات» للمبيعات — وشرط «فيه اسم مستلم» هو اللي
    /// بيفرّق بين تسليم لبني آدم ونقل داخلي.
    /// </summary>
    private IQueryable<DeviceWorkflowEvent> Handovers(
        Guid tenantId, DateTime? fromUtc, DateTime? toUtc)
    {
        var q = db.DeviceWorkflowEvents.AsNoTracking()
            .Where(e => e.TenantId == tenantId
                     && (e.EventType == DeviceWorkflowEventType.LocationMoved
                      || e.EventType == DeviceWorkflowEventType.DispatchedToSales)
                     && e.ReceivedByName != "");

        if (fromUtc is { } from) q = q.Where(e => e.OccurredAtUtc >= from);

        // 🔴 أصغر من، مش أصغر من أو يساوي — المدى نصف مفتوح.
        if (toUtc is { } to) q = q.Where(e => e.OccurredAtUtc < to);

        return q;
    }

    public async Task<IReadOnlyList<HandoverRecipientItem>> RecipientsAsync(
        Guid tenantId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var events = Handovers(tenantId, fromUtc, toUtc);

        /*
          ⚠️ **الترتيب على العدد جوّه SQL، والتحويل للعقد بعد
          القراية.**

          الترتيب بعد التحويل لنوع خاص مش قابل للترجمة لـSQL — نفس
          الغلط اللي كان بيوقّع ملخّص الجرد.
        */
        var grouped = await events
            .GroupBy(e => e.ReceivedByName)
            .Select(g => new
            {
                Name = g.Key,

                // ⚠️ أجهزة مميّزة: اللاب اللي اتسلّم مرتين لنفس
                // الشخص بيتعدّ واحد.
                Devices = g.Select(e => e.DeviceId).Distinct().Count(),
                Last = g.Max(e => e.OccurredAtUtc),
            })
            .OrderByDescending(x => x.Devices)
            .ToListAsync(ct);

        if (grouped.Count == 0) return [];

        // ⚠️ الجهات لكل مستلم — استعلام واحد مش واحد لكل صف.
        var pairs = await events
            .Where(e => e.ToLocationId != null)
            .Select(e => new { e.ReceivedByName, e.ToLocationId })
            .Distinct()
            .ToListAsync(ct);

        var names = await LocationNamesAsync(
            tenantId, pairs.Select(p => p.ToLocationId), ct);

        var byName = pairs
            .GroupBy(p => p.ReceivedByName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(p => p.ToLocationId is { } l && names.TryGetValue(l, out var n)
                        ? n
                        : "")
                    .Where(n => n.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        return grouped
            .Select(x => new HandoverRecipientItem(
                x.Name,
                x.Devices,
                x.Last,
                byName.TryGetValue(x.Name, out var places) ? places : []))
            .ToList();
    }

    public async Task<(IReadOnlyList<HandoverLogItem> Rows, int TotalItems)> LogAsync(
        Guid tenantId, HandoverLogFilter filter, CancellationToken ct = default)
    {
        var q = Handovers(tenantId, filter.FromUtc, filter.ToUtc);

        if (filter.DestinationId is { } destinationId)
            q = q.Where(e => e.ToLocationId == destinationId);

        if (filter.ReceiverPattern is { } receiver)
            q = q.Where(e => EF.Functions.Like(
                e.ReceivedByName, receiver, SearchPattern.Escape));

        int total = await q.CountAsync(ct);

        var raw = await q
            .OrderByDescending(e => e.OccurredAtUtc)

            // ⚠️ وفاصل تعادل: دفعة تسليم كاملة بتاخد نفس
            // `OccurredAtUtc` بالحرف — وقت واحد لكل اللابات.
            .ThenByDescending(e => e.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(e => new
            {
                e.Id,
                e.DeviceId,
                DeviceCode = e.Device!.PublicCode,
                e.ToLocationId,
                e.ReceivedByName,
                e.ActorName,
                e.OccurredAtUtc,
                e.Reason,
            })
            .ToListAsync(ct);

        // ⚠️ مفيش نافذة على جهة الحدث، فالأسماء بتتجاب باستعلام
        // واحد للصفحة — مش واحد لكل صف.
        var names = await LocationNamesAsync(tenantId, raw.Select(r => r.ToLocationId), ct);

        var rows = raw
            .Select(e => new HandoverLogItem(
                e.Id,
                e.DeviceId,
                e.DeviceCode,
                e.ToLocationId,
                e.ToLocationId is { } l && names.TryGetValue(l, out var name) ? name : "",
                e.ReceivedByName,
                e.ActorName,
                e.OccurredAtUtc,
                e.Reason))
            .ToList();

        return (rows, total);
    }

    private async Task<Dictionary<Guid, string>> LocationNamesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.OfType<Guid>().Distinct().ToList();

        if (wanted.Count == 0) return [];

        return await db.Locations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && wanted.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);
    }
}
