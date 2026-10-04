using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class DeviceRepository(AppDbContext db) : IDeviceRepository
{
    /// <summary>
    /// 🔴 <b>الفلتر مكتوب مرة واحدة — القايمة والتصدير بيناديوه.</b>
    ///
    /// <para>في القديم كان مكتوب <b>مرتين</b> بنفس النص، والتلات
    /// فلاتر الأخيرة (التحذيرات والتسليم والجهة) كانوا ناقصين من
    /// نسخة التصدير. والزرار في الواجهة مكتوب فوقه «اللي مفلتر على
    /// الشاشة مفلتر في الإكسل» — فالمدير يفلتر على «مخزن الجاهز»
    /// ويصدّر فيطلعله كل الأجهزة.</para>
    /// </summary>
    private IQueryable<Device> Filtered(Guid tenantId, DeviceListFilter filter)
    {
        /*
          🔴 **القايمة الافتراضية من غير المدموجين.**

          الجهاز المدموج مش لاب مستقل: صفه بيفضل عشان التاريخ والكود
          المتقاعد، بس عرضه في القايمة بيخلّي العدّ يقول ١٠ لابات
          والحقيقة ٧ — نفس الغلط اللي الدمج اتعمل عشان يصلّحه.

          ⚠️ إلا لما المستخدم يطلب الحالة صراحةً، أو يدوّر — والبحث
          بيشمل المدموجين عشان الكود المتقاعد المطبوع على ليبل حقيقي
          يفضل قابل للوصول.
        */
        var q = db.Devices.AsNoTracking().Where(d => d.TenantId == tenantId);

        if (!filter.IncludeMerged)
            q = q.Where(d => d.Status != DeviceLifecycleStatus.Merged);

        var reports = Reports(tenantId);

        if (filter.SearchPattern is { } pattern)
        {
            string exact = filter.ExactCode ?? "";
            string identity = filter.IdentityValue ?? "";

            /*
              🔴 **تلات طرق للبحث مع بعض: الكود، مرساة الهوية، ونص
              البحث.**

              الفني بيكتب اللي شايفه: كود اللاب من الليبل، أو سيريال
              من ستيكر المصنع، أو اسم موديل. ولو واحدة منهم ناقصة،
              هو بيدوّر ومابيلاقيش ويفتكر إن اللاب مش مسجّل.
            */
            var byAnchor = db.DeviceIdentifiers.AsNoTracking()
                .Where(i => i.TenantId == tenantId
                         && i.IsActive
                         && i.NormalizedValue == identity)
                .Select(i => i.DeviceId);

            q = q.Where(d =>
                d.PublicCode == exact
                || byAnchor.Contains(d.Id)
                || EF.Functions.Like(d.SearchText, pattern, SearchPattern.Escape));
        }

        if (filter.Status is { } status) q = q.Where(d => d.Status == status);

        if (filter.Confidence is { } confidence) q = q.Where(d => d.Confidence == confidence);

        // ⚠️ «اتفحص بالفني ده» على **أي** فحص — دي حقيقة تاريخية.
        if (filter.TechnicianCode is { Length: > 0 } code)
            q = q.Where(d => reports.Any(r => r.DeviceId == d.Id && r.TechnicianCode == code));

        if (filter.RackId is { } rackId)
            q = q.Where(d => reports.Any(r => r.DeviceId == d.Id && r.SourceRackId == rackId));

        if (filter.ContainerId is { } containerId)
            q = q.Where(d => d.ContainerId == containerId);

        if (filter.Stage is { } stage) q = q.Where(d => d.OperationalStage == stage);

        /*
          🔴 **فلتر الجرس — والجرس بيعدّ تلات أسباب.**

          السيرفر كان بيفلتر واحد («قطعة اتغيّرت») وبس، فحتى لما
          الرابط اتصلّح كان سببين من التلاتة مالهمش طريق — وتحذير
          مالوش طريق للأجهزة اللي بيتكلم عنها مجرد إزعاج.
        */
        q = filter.Flag switch
        {
            DeviceAttentionFlag.PartChanged => q.Where(d => d.PartChangedAtUtc != null),

            DeviceAttentionFlag.DuplicateSuspected =>
                q.Where(d => d.Status == DeviceLifecycleStatus.DuplicateSuspected),

            DeviceAttentionFlag.Attention =>
                q.Where(d => d.PartChangedAtUtc != null
                          || d.Status == DeviceLifecycleStatus.DuplicateSuspected),

            _ => q,
        };

        // ⚠️ وجود مكان حالي هو علامة التسليم — مفيش عمود.
        q = filter.Handover switch
        {
            DeviceHandoverFilter.HandedOver => q.Where(d => d.CurrentLocationId != null),
            DeviceHandoverFilter.InWorkshop => q.Where(d => d.CurrentLocationId == null),
            _ => q,
        };

        if (filter.LocationId is { } locationId)
            q = q.Where(d => d.CurrentLocationId == locationId);

        // ⚠️ فلتر التاريخ على **أي** فحص في المدى، مش على آخر فحص.
        if (filter.FromUtc is { } fromUtc)
            q = q.Where(d => reports.Any(r => r.DeviceId == d.Id && r.StartedAtUtc >= fromUtc));

        if (filter.ToUtc is { } toUtc)
            q = q.Where(d => reports.Any(r => r.DeviceId == d.Id && r.StartedAtUtc < toUtc));

        /*
          🔴 **وده الفلتر الوحيد اللي بيقيس <u>آخر</u> فحص.**

          لاب باظ الشهر اللي فات واتصلّح النهاردة **مش** في «فيه
          مشكلة»؛ ولاب اتفحص نضيف وبعدين باظ مش «سليم». وده نفس
          تعريف شاشة التسليم بالحرف.
        */
        q = filter.Outcome switch
        {
            DeviceOutcomeFilter.Failures => q.Where(d => reports
                .Where(r => r.DeviceId == d.Id)
                .OrderByDescending(r => r.StartedAtUtc)
                .Select(r => r.FailCount)
                .FirstOrDefault() > 0),

            DeviceOutcomeFilter.Errors => q.Where(d => reports
                .Where(r => r.DeviceId == d.Id)
                .OrderByDescending(r => r.StartedAtUtc)
                .Select(r => r.ErrorCount)
                .FirstOrDefault() > 0),

            // ⚠️ ولازم يكون فيه فحص أصلاً: مفيش فحص ≠ سليم.
            DeviceOutcomeFilter.Clean => q.Where(d =>
                reports.Any(r => r.DeviceId == d.Id)
                && reports
                    .Where(r => r.DeviceId == d.Id)
                    .OrderByDescending(r => r.StartedAtUtc)
                    .Select(r => r.FailCount + r.ErrorCount)
                    .FirstOrDefault() == 0),

            DeviceOutcomeFilter.NeverTested => q.Where(d =>
                !reports.Any(r => r.DeviceId == d.Id)),

            _ => q,
        };

        return q;
    }

    private IQueryable<Report> Reports(Guid tenantId) =>
        db.Reports.AsNoTracking().Where(r => r.TenantId == tenantId && !r.IsDeleted);

    /// <summary>
    /// 🔴 <b>الترتيب على السيرفر، والفاضي آخر الطابور.</b>
    ///
    /// <para>«آخر تغيير مرحلة» الفاضية معناها «مش معروف» — تقديمها
    /// على لاب واقف فعلاً من أسبوعين بيدفن اللي إحنا بندوّر
    /// عليه.</para>
    /// </summary>
    private static IQueryable<Device> Ordered(IQueryable<Device> q, DeviceListFilter filter) =>
        filter.OldestStageFirst
            ? q.OrderBy(d => d.StageChangedAtUtc == null)
                .ThenBy(d => d.StageChangedAtUtc)
                .ThenBy(d => d.Id)

            // ⚠️ وفاصل تعادل على الاتنين: دفعة مزامنة بتخلّي
            // `LastSeenAtUtc` متساوي لعشرين لاب.
            : q.OrderByDescending(d => d.LastSeenAtUtc).ThenBy(d => d.Id);

    // =================================================================
    //  القايمة
    // =================================================================

    public async Task<(IReadOnlyList<DeviceListRow> Rows, int TotalItems)> ListAsync(
        Guid tenantId, DeviceListFilter filter, CancellationToken ct = default)
    {
        var q = Filtered(tenantId, filter);
        var reports = Reports(tenantId);

        int total = await q.CountAsync(ct);

        var rows = await Ordered(q, filter)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(d => new DeviceListRow(
                d.Id,
                d.PublicCode,
                d.LastKnownManufacturer,
                d.LastKnownModel,
                d.CommercialModelName ?? "",
                d.MachineType ?? "",
                d.Status,
                d.Confidence,
                d.LastSeenAtUtc,
                d.OperationalStage,
                d.StageChangedAtUtc,
                d.CurrentLocationId,
                d.CurrentHolderTechnicianId,
                d.ContainerId,
                d.ContainerId == null ? "" : d.Container!.Code,
                d.PartChangedAtUtc,
                d.PartChangeSummary,

                // 🔴 الكود الكانوني — استعلام فرعي، مش ربط: أغلب
                // الصفوف مش مدموجة فالربط كان بيتكلّف على ولا حاجة.
                d.MergedIntoDeviceId == null
                    ? null
                    : db.Devices
                        .Where(x => x.Id == d.MergedIntoDeviceId)
                        .Select(x => x.PublicCode)
                        .FirstOrDefault(),

                reports.Count(r => r.DeviceId == d.Id),

                reports
                    .Where(r => r.DeviceId == d.Id)
                    .OrderByDescending(r => r.StartedAtUtc)
                    .Select(r => new DeviceLastTestRow(
                        r.Id,
                        r.StartedAtUtc,
                        r.TechnicianId,
                        r.TechnicianName,
                        r.TechnicianCode,
                        r.SourceRackId,
                        r.PassCount,
                        r.FailCount,
                        r.ErrorCount,
                        r.NotPresentCount,
                        r.SkipCount))
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return (rows, total);
    }

    public async Task<IReadOnlyList<DeviceExportRow>> ExportAsync(
        Guid tenantId, DeviceListFilter filter, int cap, CancellationToken ct = default)
    {
        var reports = Reports(tenantId);

        return await Ordered(Filtered(tenantId, filter), filter)

            // ⚠️ `cap + 1` عشان المنادي يعرف إن فيه زيادة ويقولها في
            // الملف — القص الصامت بيتقري على إنه كل البيانات.
            .Take(cap + 1)
            .Select(d => new DeviceExportRow(
                d.PublicCode,
                d.LastKnownManufacturer,
                d.LastKnownModel,
                d.CommercialModelName ?? "",
                d.ContainerId == null ? "" : d.Container!.Code,
                d.Status,
                d.Confidence,
                d.OperationalStage,
                d.CurrentLocationId == null ? "" : d.CurrentLocation!.Name,
                reports.Count(r => r.DeviceId == d.Id),
                d.FirstSeenAtUtc,
                d.LastSeenAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// ⚠️ <b>بيدوّر في المدموجين كمان</b> — الكود المتقاعد مطبوع
    /// على ليبل ملزوق على لاب حقيقي.
    /// </summary>
    public Task<Device?> FindByCodeAsync(
        Guid tenantId, string publicCode, CancellationToken ct = default) =>
        db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.PublicCode == publicCode, ct);

    /// <summary>
    /// 🔴 <b>استعلامين: الكود الحالي، وبعدين التاريخ.</b>
    ///
    /// <para>⚠️ <b>والمدموج داخل في الاتنين</b> — الكود المتقاعد
    /// مطبوع على ليبل ملزوق على لاب حقيقي. (راجع التعليق على
    /// <c>IDeviceRepository.ResolveCodeAsync</c>: تعليق القديم هنا
    /// كان بيقول العكس وكان غلط.)</para>
    ///
    /// <para>⚠️ والاستعلام التاني مابيتعملش خالص لو مفيش قيمة
    /// موحّدة، ومابيتعملش لو التاريخ مالقاش حاجة جديدة — مفيش قراية
    /// تالتة على الفاضي.</para>
    /// </summary>
    public async Task<IReadOnlyList<DeviceCodeHit>> ResolveCodeAsync(
        Guid tenantId, string code, string normalizedCode, CancellationToken ct = default)
    {
        var scoped = db.Devices.AsNoTracking().Where(d => d.TenantId == tenantId);

        var current = await scoped
            .Where(d => d.PublicCode == code)
            .Select(d => new { d.Id, d.PublicCode })
            .ToListAsync(ct);

        var hits = current
            .Select(d => new DeviceCodeHit(d.Id, d.PublicCode, true))
            .ToList();

        if (normalizedCode.Length == 0) return hits;

        var historical = await db.DeviceIdentifiers.AsNoTracking()
            .Where(i => i.TenantId == tenantId
                     && i.Kind == DeviceIdentifierKind.CompanyCode
                     && i.NormalizedValue == normalizedCode)
            .Select(i => i.DeviceId)
            .Distinct()
            .ToListAsync(ct);

        var extra = historical.Where(id => hits.All(h => h.DeviceId != id)).ToList();

        if (extra.Count == 0) return hits;

        /*
          ⚠️ **قراية تانية على الأجهزة، مش ضم في نفس الاستعلام.**

          المرساة بتدّي معرّفات، والعرض محتاج الكود الحالي لكل واحد
          منهم — واللاب اللي مرساته موجودة وصفه مش موجود (تنظيف
          قديم) مالوش يطلع في النتيجة.
        */
        var rows = await scoped
            .Where(d => extra.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicCode })
            .ToListAsync(ct);

        hits.AddRange(rows.Select(d => new DeviceCodeHit(d.Id, d.PublicCode, false)));

        return hits;
    }

    // =================================================================
    //  صفحة اللاب
    // =================================================================

    /// <summary>
    /// ⚠️ <b>من غير فلتر على المدموج</b> — صفحة اللاب هي صفحة
    /// تاريخه. والتقييد بالشركة هو الحاجز.
    /// </summary>
    public Task<Device?> FindDetailAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.TenantId == tenantId, ct);

    /// <summary>
    /// 🔴 <b>من غير <c>IsActive</c></b> — الملغية جزء من التاريخ.
    ///
    /// <para>⚠️ والترتيب <c>IsActive</c> تنازلي عشان النشطة تطلع
    /// الأول، وبعدين بالنوع عشان نفس النوع يبقى جنب بعضه.</para>
    /// </summary>
    public async Task<IReadOnlyList<DeviceIdentifierRow>> IdentifiersAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        await db.DeviceIdentifiers.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.DeviceId == deviceId)
            .OrderByDescending(i => i.IsActive)
            .ThenBy(i => i.Kind)

            // ⚠️ فاصل تعادل: نفس النوع ممكن يكون ليه أكتر من مرساة
            // (هاردين)، ومن غيره الترتيب بيتقلب بين التحديثات.
            .ThenBy(i => i.Id)
            .ToListAsync(ct);

    /// <summary>
    /// ⚠️ <b>عدد المراحل استعلام فرعي، مش <c>Include</c>.</b>
    /// صفحة فيها ٢٥ فحص بـ<c>Include(Steps)</c> كانت بتسحب آلاف صفوف
    /// عشان تعدّها.
    /// </summary>
    public async Task<(IReadOnlyList<DeviceTestRow> Rows, int TotalItems)> TestsAsync(
        Guid tenantId, Guid deviceId, int page, int pageSize,
        CancellationToken ct = default)
    {
        var q = Reports(tenantId).Where(r => r.DeviceId == deviceId);

        int total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(r => r.StartedAtUtc)

            // 🔴 فاصل تعادل: دفعة فحوص اترفعت من نفس المزامنة بتاخد
            //    نفس وقت البداية بالمللي ثانية.
            .ThenBy(r => r.Id)

            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new DeviceTestRow(
                r.Id,
                r.StartedAtUtc,
                r.EndedAtUtc,
                r.DurationMs,
                r.TechnicianId,
                r.TechnicianName,
                r.TechnicianCode,
                r.SourceRackId,
                r.PassCount,
                r.FailCount,
                r.ErrorCount,
                r.NotPresentCount,
                r.SkipCount,
                r.Steps.Count,
                r.GeneralNote))
            .ToListAsync(ct);

        return (rows, total);
    }

    public async Task<IReadOnlyList<DeviceNote>> NotesAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        await db.DeviceNotes.AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.DeviceId == deviceId)
            .OrderByDescending(n => n.CreatedAtUtc)

            // 🔴 فاصل تعادل: دفعة ملاحظات من نفس الإجراء (تسليم ٥٠
            // لاب) بتاخد نفس الوقت بالحرف.
            .ThenByDescending(n => n.Id)
            .ToListAsync(ct);

    // =================================================================
    //  أسماء الصفحة — قراية واحدة لكل جدول
    // =================================================================

    public async Task<IReadOnlyDictionary<Guid, string>> LocationNamesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default)
    {
        var wanted = Wanted(ids);

        if (wanted.Count == 0) return new Dictionary<Guid, string>();

        return await db.Locations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && wanted.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, TechnicianLabel>> HolderNamesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default)
    {
        var wanted = Wanted(ids);

        if (wanted.Count == 0) return new Dictionary<Guid, TechnicianLabel>();

        return await db.Technicians.AsNoTracking()
            .Where(t => t.TenantId == tenantId && wanted.Contains(t.Id))
            .ToDictionaryAsync(
                t => t.Id, t => new TechnicianLabel(t.DisplayName, t.Code), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> RackCodesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default)
    {
        var wanted = Wanted(ids);

        if (wanted.Count == 0) return new Dictionary<Guid, string>();

        return await db.Racks.AsNoTracking()
            .Where(r => r.TenantId == tenantId && wanted.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.RackCode, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> RackLocationsAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default)
    {
        var wanted = Wanted(ids);

        if (wanted.Count == 0) return new Dictionary<Guid, string>();

        return await db.Racks.AsNoTracking()
            .Where(r => r.TenantId == tenantId && wanted.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Location, ct);
    }

    /// <summary>
    /// ⚠️ الفاضي بيتشال والمكرّر بيتلم — الصفحة فيها عشرين صف
    /// بيشاوروا على خمس جهات.
    /// </summary>
    private static List<Guid> Wanted(IEnumerable<Guid?> ids) =>
        ids.OfType<Guid>().Distinct().ToList();
}
