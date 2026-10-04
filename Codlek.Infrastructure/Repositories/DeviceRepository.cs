using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
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
    /// 🔴 <b>أربع عدّادات بتلات نطاقات مختلفة.</b>
    ///
    /// <para>الفحوص بـ<c>!IsDeleted</c>؛ المراسي <b>كلها</b> والملغية
    /// معاها؛ واللقطات هي الفحوص اللي معاها لقطة. ولو واحد منهم أخد
    /// نطاق غيره، الرقم بيخالف عدد الصفوف اللي النقطة التانية
    /// بترجّعها — والمدير بيشوف «٣ مراسي» وبيفتح التاب يلاقي
    /// اتنين.</para>
    ///
    /// <para>⚠️ <b>وأقدم اسم فني من <u>الفحوص</u> مش من جدول
    /// الحسابات.</b> فيه فنيين بيشتغلوا على الراكة ومالهمش حساب في
    /// اللوحة خالص؛ ولو الاسم اتقرا من الجدول، صفحتهم كانت بتفضل
    /// بلا اسم للأبد. وكمان كود الراكة وكود مستخدم اللوحة الاتنين
    /// ستة أرقام وبيتصادموا — فالضم على الجدول كان بيدّي اسم غلط.</para>
    /// </summary>
    public async Task<DeviceDetailFacts> DetailFactsAsync(
        Guid tenantId, Device device, CancellationToken ct = default)
    {
        var reports = Reports(tenantId).Where(r => r.DeviceId == device.Id);

        int reportCount = await reports.CountAsync(ct);

        int noteCount = await db.DeviceNotes.AsNoTracking()
            .CountAsync(n => n.TenantId == tenantId && n.DeviceId == device.Id, ct);

        // 🔴 كل المراسي — نفس نطاق `IdentifiersAsync`.
        int identifierCount = await db.DeviceIdentifiers.AsNoTracking()
            .CountAsync(i => i.TenantId == tenantId && i.DeviceId == device.Id, ct);

        // ⚠️ عدد الفحوص اللي معاها لقطة، مش عدد القطع.
        int snapshotCount = await reports
            .CountAsync(r => r.SnapshotCapturedAtUtc != null, ct);

        var latest = await reports
            .OrderByDescending(r => r.StartedAtUtc)

            // ⚠️ فاصل تعادل: فحصين اترفعوا في نفس المزامنة بياخدوا
            // نفس وقت البداية، ومن غيره «آخر فني» بيتقلب بين
            // التحديثات.
            .ThenBy(r => r.Id)
            .Select(r => new
            {
                r.StartedAtUtc,
                r.TechnicianCode,
                r.TechnicianName,
                r.SourceRackId,
            })
            .FirstOrDefaultAsync(ct);

        /*
          🔴 **أقدم فحص فيه اسم — ومقيّد باللاب ده.**

          الحدث بيقول «مين اكتشف اللاب»، وده سؤال تاريخي: الاسم
          متكرر على كل فحص، فلو اتصحّح يوم ما، «أحدث فحص» بيكتب على
          واقعة قديمة اسم ماكانش موجود وقتها.

          ⚠️ والشرط `TechnicianName != ""` مقصود: فيه فحوص قديمة
          اسمها مش مكتوب، وأخدها بيرجّع فراغ وكأن الاسم مش موجود
          خالص.
        */
        string firstSeenName = device.FirstSeenByTechnicianCode.Length == 0
            ? ""
            : await reports
                .Where(r => r.TechnicianCode == device.FirstSeenByTechnicianCode
                         && r.TechnicianName != "")
                .OrderBy(r => r.StartedAtUtc)
                .ThenBy(r => r.Id)
                .Select(r => r.TechnicianName)
                .FirstOrDefaultAsync(ct) ?? "";

        string containerCode = device.ContainerId is { } containerId
            ? await db.Containers.AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.Id == containerId)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(ct) ?? ""
            : "";

        return new DeviceDetailFacts(
            ReportCount: reportCount,
            NoteCount: noteCount,
            IdentifierCount: identifierCount,
            SnapshotCount: snapshotCount,
            LatestReportAtUtc: latest?.StartedAtUtc,
            LatestTechnicianCode: latest?.TechnicianCode ?? "",
            LatestTechnicianName: latest?.TechnicianName ?? "",
            LatestRackId: latest?.SourceRackId,
            FirstSeenTechnicianName: firstSeenName,
            ContainerCode: containerCode);
    }

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

    // =================================================================
    //  خط الزمن
    // =================================================================

    /// <summary>
    /// ⚠️ <b>حد أمان لصفوف التعادل عند حافة الصفحة.</b> مجموعة
    /// ضخمة بنفس التوقيت (استيراد دفعة) مالهاش تسحب القاعدة.
    /// </summary>
    private const int MaxTieRows = 1000;

    public async Task<DeviceTimelineCounts> TimelineCountsAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        new(
            Reports: await Reports(tenantId).CountAsync(r => r.DeviceId == deviceId, ct),

            Notes: await db.DeviceNotes.AsNoTracking()
                .CountAsync(n => n.TenantId == tenantId && n.DeviceId == deviceId, ct),

            // 🔴 العدّ على **نفس** الاتحاد اللي الصفوف بتتقرا منه.
            RepairMoments: await RepairMoments(tenantId, deviceId).CountAsync(ct),

            Movements: await Movements(tenantId, deviceId).CountAsync(ct));

    public async Task<IReadOnlyList<TimelineReportRow>> TimelineReportsAsync(
        Guid tenantId, Guid deviceId, int need, CancellationToken ct = default)
    {
        var source = Reports(tenantId).Where(r => r.DeviceId == deviceId);

        var rows = await source
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(need)
            .Select(ReportRow)
            .ToListAsync(ct);

        // الصفحة ما اتملتش؟ يبقى جبنا كل الفحوص أصلاً، مفيش حافة.
        if (rows.Count < need) return rows;

        var boundary = rows[^1].StartedAtUtc;

        var ties = await source
            .Where(r => r.StartedAtUtc == boundary)
            .Take(MaxTieRows)
            .Select(ReportRow)
            .ToListAsync(ct);

        return Merge(rows, ties, r => r.StartedAtUtc != boundary, r => r.ReportId);
    }

    public async Task<IReadOnlyList<TimelineNoteRow>> TimelineNotesAsync(
        Guid tenantId, Guid deviceId, int need, CancellationToken ct = default) =>
        await db.DeviceNotes.AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.DeviceId == deviceId)
            .OrderByDescending(n => n.CreatedAtUtc)

            // ⚠️ المفتاح `bigint` وترتيبه في SQL هو نفسه ترتيبنا —
            // فمفيش فخ حافة هنا.
            .ThenByDescending(n => n.Id)
            .Take(need)
            .Select(n => new TimelineNoteRow(n.Id, n.CreatedAtUtc, n.CreatedByName, n.Body))
            .ToListAsync(ct);

    /// <summary>
    /// 🔴 <b>قرايتين: اللحظات ضيّقة، وبعدين بيانات الأوامر.</b>
    ///
    /// <para>الاتحاد بيضم <b>تلات أعمدة بس</b> (الأمر، الوقت،
    /// اللحظة). والنسخة الأولى ضمّت الصف الكامل، وEF رمت:</para>
    ///
    /// <para><c>Unable to translate set operation after client
    /// projection has been applied.</c></para>
    ///
    /// <para>⚠️ <b>وده بيبني وبيعدّي كل فحوص الوحدة</b> — المستودع
    /// المزيّف مابيستعملش EF. اللي لقطها فحص على قاعدة حقيقية، ودي
    /// نفس العائلة بتاعة العيب اللي خلّى
    /// <c>/api/v1/technicians</c> ترجّع ٥٠٠ والفحوص خضرا.</para>
    ///
    /// <para>⚠️ والقراية التانية على <b>الأوامر المميّزة</b> اللي
    /// طلعت فعلاً — أمر واحد بيدّي خمس لحظات، فقراية لكل لحظة كانت
    /// بتجيب نفس الصف خمس مرات.</para>
    /// </summary>
    public async Task<IReadOnlyList<TimelineRepairMomentRow>> TimelineRepairMomentsAsync(
        Guid tenantId, Guid deviceId, int need, CancellationToken ct = default)
    {
        var moments = await RepairMoments(tenantId, deviceId)
            .OrderByDescending(m => m.AtUtc)
            .Take(need)
            .ToListAsync(ct);

        if (moments.Count >= need)
        {
            var boundary = moments[^1].AtUtc;

            var ties = await RepairMoments(tenantId, deviceId)
                .Where(m => m.AtUtc == boundary)
                .Take(MaxTieRows)
                .ToListAsync(ct);

            // ⚠️ مفتاح التفرّد (الأمر + اللحظة) — نفس الأمر بيدّي
            // خمس لحظات مختلفة.
            moments = Merge(
                moments, ties, m => m.AtUtc != boundary, m => (m.RepairId, m.Moment));
        }

        if (moments.Count == 0) return [];

        var ids = moments.Select(m => m.RepairId).Distinct().ToList();

        var facts = await db.RepairWorkItems.AsNoTracking()
            .Where(w => w.TenantId == tenantId && ids.Contains(w.Id))
            .Select(w => new
            {
                w.Id,
                w.PublicCode,
                w.FaultSummary,
                w.RepairActions,
                w.OutcomeReason,
                PartCount = w.Parts.Count,
                w.OpenedByActorType,
                w.OpenedByName,
                w.AssignedTechnicianId,
                w.CompletedByTechnicianId,
            })
            .ToDictionaryAsync(w => w.Id, ct);

        return moments
            .Select(m =>
            {
                var f = facts.GetValueOrDefault(m.RepairId);

                return new TimelineRepairMomentRow(
                    RepairId: m.RepairId,
                    AtUtc: m.AtUtc,
                    Moment: m.Moment,
                    PublicCode: f?.PublicCode ?? "",
                    FaultSummary: f?.FaultSummary ?? "",
                    RepairActions: f?.RepairActions ?? "",
                    OutcomeReason: f?.OutcomeReason ?? "",
                    PartCount: f?.PartCount ?? 0,
                    OpenedByActorType: f?.OpenedByActorType ?? "",
                    OpenedByName: f?.OpenedByName ?? "",
                    AssignedTechnicianId: f?.AssignedTechnicianId,
                    CompletedByTechnicianId: f?.CompletedByTechnicianId);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<TimelineMovementRow>> TimelineMovementsAsync(
        Guid tenantId, Guid deviceId, int need, CancellationToken ct = default) =>
        await Movements(tenantId, deviceId)
            .OrderByDescending(e => e.OccurredAtUtc)

            // ⚠️ المفتاح `bigint` — نفس حجّة الملاحظات.
            .ThenByDescending(e => e.Id)
            .Take(need)
            .Select(e => new TimelineMovementRow(
                e.Id,
                e.EventType,
                e.OccurredAtUtc,
                e.RecordedAtUtc,
                e.FromStage,
                e.ToStage,
                e.FromLocationId,
                e.ToLocationId,
                e.FromTechnicianId,
                e.ToTechnicianId,
                e.Reason,
                e.ActorType,
                e.ActorName))
            .ToListAsync(ct);

    /// <summary>
    /// 🔴 <b>حركات اللاب <u>من غير</u> حركات الصيانة التلاتة.</b>
    ///
    /// <para>التلاتة دول بيتكتبوا مع كل تغيير حالة صيانة، وأمر
    /// الصيانة نفسه معروض بمعلومات أكتر (الرقم، الفني، اللي اتعمل)
    /// — فعرضهم كمان معناه كل صيانة مكتوبة <b>مرتين في نفس
    /// اللحظة</b>.</para>
    ///
    /// <para>⚠️ والاستبعاد <b>بالنوع</b> مش بمعرّف أمر الصيانة: نقل
    /// مكان أو تسليم حيازة ممكن يبقى مربوط بأمر صيانة وهو برضه حركة
    /// حقيقية لازم تبان. والقايمة في
    /// <c>DeviceMovementTitle.HiddenFromTimeline</c> عشان العنوان
    /// والفلتر ميختلفوش.</para>
    /// </summary>
    private IQueryable<DeviceWorkflowEvent> Movements(Guid tenantId, Guid deviceId) =>
        db.DeviceWorkflowEvents.AsNoTracking()
            .Where(e => e.TenantId == tenantId
                     && e.DeviceId == deviceId
                     && !DeviceMovementTitle.HiddenFromTimeline.Contains(e.EventType));

    /// <summary>
    /// كل لحظات الصيانة للّاب ده كاستعلام واحد — <b><c>UNION
    /// ALL</c></b>.
    ///
    /// <para>🔴 <b>اتحاد مش خمس استعلامات.</b> الاتحاد بينزل SQL
    /// كاستعلام واحد بيقصّ أعلى <c>need</c> صف <b>بعد</b> ما يضم
    /// الخمسة، وده بالظبط اللي الترتيب محتاجه. خمس استعلامات كل
    /// واحد بياخد <c>need</c> كانت هتجيب خمس أضعاف الصفوف عشان ترمي
    /// معظمها، وكمان بتضاعف عدد الذهابات للقاعدة في كل صفحة.</para>
    ///
    /// <para>⚠️ <b>و«اتلغى» وقته <c>UpdatedAtUtc</c> مش عمود
    /// مخصّص.</b> الإلغاء بيغيّر الحالة وبيكتب السبب، ومابيحطّش ولا
    /// ختم وقت ومابيسجّلش حركة — يعني مفيش في القاعدة ولا صف بيقول
    /// «اتلغى الساعة كام». و«ملغي» حالة نهائية، فآخر لمسة على الصف
    /// هي الإلغاء نفسه. ده أقرب دليل متخزّن، مش وقت مخترع.</para>
    ///
    /// <para>⚠️ <b>والإنهاء بيملا وقت البداية لو كانت فاضية</b>،
    /// فأمر اتقفل من غير بداية حقيقية بيدّي «بدأت» و«خلصت» في نفس
    /// اللحظة. ده اللي الصف بيقوله فعلاً، وإخفاؤه بيخفي إن مفيش
    /// بداية اتسجّلت.</para>
    /// </summary>
    /// <summary>
    /// 🔴 <b>مفتاح لحظة صيانة — تلات أعمدة وبس.</b>
    ///
    /// <para>⚠️ <b>كلاس بخصائص <c>init</c> مش <c>record</c>
    /// موضعي.</b> الـ<c>record</c> الموضعي بيتعامل عند EF كنداء
    /// مُنشئ (إسقاط عميل)، و<c>UNION</c> بعده مش قابل للترجمة —
    /// وده اللي رمى على قاعدة حقيقية.</para>
    /// </summary>
    private sealed class MomentKey
    {
        public Guid RepairId { get; init; }
        public DateTime AtUtc { get; init; }
        public RepairMoment Moment { get; init; }
    }

    private IQueryable<MomentKey> RepairMoments(Guid tenantId, Guid deviceId)
    {
        var source = db.RepairWorkItems.AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.DeviceId == deviceId);

        /*
          🔴 **الاتحاد ضيّق: تلات أعمدة بس.**

          النسخة الأولى ضمّت الصف الكامل (بالكود والعطل وعدد القطع)،
          فEF رمت:

              Unable to translate set operation after client
              projection has been applied.

          السبب إن الإسقاط لنوع مركّب بيبقى «إسقاط عميل» عند EF،
          و`UNION` بعده مش قابل للترجمة. وبيانات الأوامر بتتجيب في
          قراية تانية على الأوامر المميّزة — نفس اللي القديم بيعمله
          بالظبط.

          ⚠️ **وده بيبني وبيعدّي كل فحوص الوحدة** — المستودع المزيّف
          مابيستعملش EF. اللي لقطه فحص على قاعدة حقيقية، ودي نفس
          العائلة بتاعة العيب اللي خلّى `/api/v1/technicians` ترجّع
          ٥٠٠ والفحوص خضرا.

          ⚠️ والكلاس بخصائص `init` مش `record` موضعي: الـ`record`
          الموضعي بيتعامل كنداء مُنشئ، وده بالظبط اللي EF مابتقدرش
          تضم بعده.
        */

        // ⚠️ الفتح مالوش شرط: الصف مايتكتبش من غير وقت فتح أصلاً.
        var opened = source.Select(w => new MomentKey
        {
            RepairId = w.Id, AtUtc = w.OpenedAtUtc, Moment = RepairMoment.Opened,
        });

        var started = source
            .Where(w => w.StartedAtUtc != null)
            .Select(w => new MomentKey
            {
                RepairId = w.Id,
                AtUtc = w.StartedAtUtc!.Value,
                Moment = RepairMoment.Started,
            });

        var completed = source
            .Where(w => w.Status == RepairStatus.Completed && w.CompletedAtUtc != null)
            .Select(w => new MomentKey
            {
                RepairId = w.Id,
                AtUtc = w.CompletedAtUtc!.Value,
                Moment = RepairMoment.Completed,
            });

        var unable = source
            .Where(w => w.Status == RepairStatus.UnableToRepair && w.CompletedAtUtc != null)
            .Select(w => new MomentKey
            {
                RepairId = w.Id,
                AtUtc = w.CompletedAtUtc!.Value,
                Moment = RepairMoment.Unable,
            });

        var cancelled = source
            .Where(w => w.Status == RepairStatus.Cancelled)
            .Select(w => new MomentKey
            {
                RepairId = w.Id, AtUtc = w.UpdatedAtUtc, Moment = RepairMoment.Cancelled,
            });

        return opened.Concat(started).Concat(completed).Concat(unable).Concat(cancelled);
    }

    /// <summary>
    /// ⚠️ <b>تعبير مش دالة</b> — EF لازم تترجمه، فمينفعش يبقى نداء
    /// عادي جوّه <c>Select</c>.
    /// </summary>
    private static readonly System.Linq.Expressions.Expression<
        Func<Report, TimelineReportRow>> ReportRow =
        r => new TimelineReportRow(
            r.Id,
            r.StartedAtUtc,
            r.EndedAtUtc,
            r.DurationMs,
            r.TechnicianName,
            r.TechnicianCode,
            r.PassCount,
            r.FailCount,
            r.ErrorCount,
            r.NotPresentCount,
            r.SkipCount);

    /// <summary>
    /// بيدمج صفوف الحافة مع المتعادلين — <b>من غير تكرار</b>.
    ///
    /// <para>⚠️ الصفوف اللي <b>قبل</b> الحافة بتتاخد زي ما هي،
    /// والحافة نفسها بتتبدّل بالمجموعة الكاملة اللي رجعت من
    /// الاستعلام التاني.</para>
    /// </summary>
    private static List<T> Merge<T, TKey>(
        List<T> rows, List<T> ties, Func<T, bool> beforeBoundary, Func<T, TKey> key)
    {
        var merged = rows.Where(beforeBoundary).ToList();
        var seen = new HashSet<TKey>(merged.Select(key));

        foreach (var tie in ties)
            if (seen.Add(key(tie))) merged.Add(tie);

        return merged;
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
