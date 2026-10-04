using System.Text.Json;
using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Sync;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Entities;
using Codlek.Core.Hardware;
using Codlek.Core.Sync;
using Codlek.Core.Text;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Rack.IngestReports;

/// <inheritdoc cref="IngestReportsCommand"/>
public sealed class IngestReportsCommandHandler(
    IReportIngestRepository reports,
    IUnitOfWork unitOfWork,
    ILogger<IngestReportsCommandHandler> log)
    : IRequestHandler<IngestReportsCommand, Result<IngestResult>>
{
    /// <summary>
    /// ⚠️ <b>نفس خيارات القراية بالحرف.</b> <c>RawJson</c> بيتخزّن
    /// بالشكل ده، والمقارنة «نفس النسخة؟» بتقارن النص الناتج — فخيار
    /// مختلف هنا بيخلّي كل فحص يبان متغيّر.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<Result<IngestResult>> Handle(
        IngestReportsCommand command, CancellationToken cancellationToken)
    {
        var result = new IngestResult();
        var list = command.Reports;

        if (list.Count == 0) return Result.Success(result);

        var ids = list.Select(r => r.Id).ToHashSet();

        var existing = await reports.ExistingAsync(command.TenantId, ids, cancellationToken);

        // ⚠️ الأجهزة اللي الفحوص بتشاور عليها — استعلام واحد للدفعة
        //    كلها بدل واحد لكل فحص.
        var referenced = list
            .Where(r => r.DeviceId.HasValue && r.DeviceId.Value != Guid.Empty)
            .Select(r => r.DeviceId!.Value)
            .ToHashSet();

        var known = await reports.KnownDeviceIdsAsync(
            command.TenantId, referenced, cancellationToken);

        // ⚠️ ومين عمل كل فحص — استعلام واحد للدفعة كلها كمان.
        var technicians = await ResolveTechniciansAsync(command, list, cancellationToken);

        foreach (var dto in list)
        {
            // ⚠️ الفحوص دي قبل أي استعلام — مش محتاجة قاعدة.
            if (ReportIngestRules.Reject(dto.Id, dto.StartedAtUtc) is { } why)
            {
                result.Rejected++;
                result.Problems.Add(why);
                continue;
            }

            /*
              🔴 **هوية غلط = رفض، مش نسبة فاضية.**

              الفحص اللي بيدّعي فني من شركة تانية مابيتخزّنش بهوية
              فاضية — بيترفض بالكامل. تخزينه «من غير فني» معناه إن
              محاولة النسبة العابرة للشركات **بتنجح** كصف مجهول،
              والصف المجهول ده بيتنسب بإيد مدير بعدين وهو أصلاً مش
              بتاعه.
            */
            var technician = technicians[dto.Id];

            if (technician.Rejection is { } rejection)
            {
                result.Rejected++;
                result.RejectedById[dto.Id] = rejection;
                result.Problems.Add($"فحص {dto.Id}: {rejection.Message}");
                continue;
            }

            string raw = JsonSerializer.Serialize(dto, Json);

            var link = await ResolveDeviceAsync(command, dto, known, cancellationToken);

            if (existing.TryGetValue(dto.Id, out var row))
            {
                /*
                  🔴 **نفس النسخة بالظبط؟ مفيش داعي نلمس القاعدة.**

                  ودي الحالة الشايعة: الراكة بتعيد إرسال نفس الدفعة
                  لأن الرد ضاع في الشبكة. من غير المقارنة دي، كل
                  إعادة بتمسح الأولاد وتكتبهم من تاني — رحلات كتابة
                  على حاجة ماتغيّرتش.
                */
                if (row.RawJson == raw)
                {
                    result.Unchanged++;
                    continue;
                }

                reports.RemoveChildren(row);

                ReportRowWriter.Apply(
                    row, dto, raw, command.SourceRackId, link,
                    command.TenantId, technician.TechnicianId);

                result.Updated++;
            }
            else
            {
                var fresh = new Report { Id = dto.Id, TenantId = command.TenantId };

                ReportRowWriter.Apply(
                    fresh, dto, raw, command.SourceRackId, link,
                    command.TenantId, technician.TechnicianId);

                reports.Add(fresh);

                // ⚠️ بيتضاف للخريطة عشان نفس المعرّف مرتين في نفس
                //    الدفعة يتحدّث مش يتكرر.
                existing[dto.Id] = fresh;

                result.Added++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await AfterSaveAsync(command, existing, cancellationToken);

        log.LogInformation("استقبال فحوص: {Summary}", result.Summary);

        return Result.Success(result);
    }

    /// <summary>
    /// الاسم التجاري وعلامة «قطعة اتغيّرت» — <b>بعد الحفظ</b>.
    ///
    /// <para>🔴 <b>الاسم التجاري بينزل على الجهاز بعد ما الفحص
    /// يتخزّن.</b> من غير الخطوة دي، الفحص بيشيل
    /// <c>ideapad 330-15ICH</c> وصفحة الأجهزة تفضل تعرض
    /// <c>LENOVO 81FK</c> — وده بالظبط اللي حصل: الاسم اتضاف للفحص
    /// ومااتضافش للجهاز.</para>
    ///
    /// <para>🔴 <b>وتحذير «قطعة اتغيّرت» بيتحسب هنا، مش وقت
    /// الطلب.</b> الأيقونة مكانها ترويسة اللوحة يعني <b>كل فتحة
    /// صفحة</b>. الحساب عند الطلب على ~٥٠٠–٢٠٠٠ جهاز معناه ~١٠٠٠
    /// استعلام و~٢٥٠٠٠ صف في كل مرة، على استضافة صغيرة — ثواني مش
    /// ميلي ثانية.</para>
    ///
    /// <para>🟢 <b>وهنا رخيص:</b> إحنا <b>أصلاً</b> بنحفظ مرة تانية
    /// على نفس الأجهزة دي (الاسم التجاري)، فالعلامة بتترحّل من غير أي
    /// رحلة زيادة.</para>
    /// </summary>
    private async Task AfterSaveAsync(
        IngestReportsCommand command, Dictionary<Guid, Report> touchedReports,
        CancellationToken ct)
    {
        var touched = touchedReports.Values
            .Where(r => r.DeviceId.HasValue)
            .Select(r => r.DeviceId!.Value)
            .Distinct()
            .ToList();

        if (touched.Count == 0) return;

        var devices = await reports.DevicesAsync(command.TenantId, touched, ct);

        if (devices.Count == 0) return;

        bool any = false;

        foreach (var device in devices)
            any |= await HydrateAsync(device, ct);

        any |= await FlagPartChangesAsync(command.TenantId, devices, ct);

        if (any) await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// بيملأ الاسم التجاري على جهاز واحد من فحوصه.
    ///
    /// <para>⚠️ <b>مابيحفظش</b> — المنادي بيحفظ.</para>
    /// </summary>
    private async Task<bool> HydrateAsync(Device device, CancellationToken ct)
    {
        var evidence = await reports.ModelEvidenceAsync(device.Id, ct);

        if (evidence.Count == 0) return false;

        int pick = CommercialModelEvidence.Pick(
            [.. evidence.Select(e => e.CommercialModelSource)],
            [.. evidence.Select(e => e.CommercialModelName)]);

        // ⚠️ مفيش دليل موثوق ← الخام يفضل، ومابنلمسش حاجة.
        if (pick < 0) return false;

        var best = evidence[pick];
        bool changed = false;

        if (!string.Equals(
                device.CommercialModelName, best.CommercialModelName, StringComparison.Ordinal))
        {
            device.CommercialModelName = best.CommercialModelName;
            changed = true;
        }

        if (!string.Equals(
                device.CommercialModelSource, best.CommercialModelSource,
                StringComparison.Ordinal))
        {
            device.CommercialModelSource = best.CommercialModelSource;
            changed = true;
        }

        // ⚠️ كود المصنع بيتملى بس لو موجود — مابنمسحش قيمة بفاضي.
        if (!string.IsNullOrWhiteSpace(best.MachineType)
            && !string.Equals(
                   device.MachineType, best.MachineType, StringComparison.Ordinal))
        {
            device.MachineType = best.MachineType;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// بيحطّ علامة «قطعة اتغيّرت» على الأجهزة اللي وصلها فحص جديد.
    ///
    /// <para>🔴 <b>المقارنة بين <u>آخر فحصين</u> للجهاز، مش بين أي
    /// فحصين.</b> الجهاز اللي اتفحص ١٠ مرات ماينفعش يتقارن بأول فحص —
    /// القطعة ممكن تكون اتغيّرت بإذن من ٦ شهور وخلاص.</para>
    ///
    /// <para>⚠️ <b>والعلامة مابتتشالش لوحدها.</b> فحص جديد من غير
    /// فروقات مابيمسحش تحذير قديم: التبديل حصل فعلاً، وإخفاؤه لأن
    /// الجهاز اتفحص تاني بيضيّع الواقعة. واللي بيشيلها قرار إنسان
    /// (مراجعة) — ولسه مابنيناهاش، فالعلامة بتفضل.</para>
    /// </summary>
    private async Task<bool> FlagPartChangesAsync(
        Guid tenantId, IReadOnlyList<Device> devices, CancellationToken ct)
    {
        var deviceIds = devices.Select(d => d.Id).ToList();

        var cursors = await reports.ReportCursorsAsync(tenantId, deviceIds, ct);

        // ⚠️ أحدث اتنين لكل جهاز — الاستعلام راجع مرتّب من الأحدث.
        var pairs = cursors
            .GroupBy(c => c.DeviceId)
            .Select(g => g.Take(2).ToList())
            .Where(g => g.Count == 2)
            .ToList();

        if (pairs.Count == 0) return false;

        var reportIds = pairs
            .SelectMany(p => p.Select(c => c.ReportId))
            .Distinct()
            .ToList();

        var components = await reports.SnapshotComponentsAsync(reportIds, ct);

        var byReport = components
            .GroupBy(c => c.ReportId)
            .ToDictionary(
                g => g.Key, g => (IReadOnlyList<ReportSnapshotComponent>)[.. g]);

        var byId = devices.ToDictionary(d => d.Id);
        bool touched = false;

        foreach (var pair in pairs)
        {
            var last = pair[0];
            var prev = pair[1];

            if (!byId.TryGetValue(last.DeviceId, out var device)) continue;

            var verdict = PartChangeDetector.Compare(
                byReport.TryGetValue(prev.ReportId, out var before) ? before : [],
                byReport.TryGetValue(last.ReportId, out var after) ? after : [],
                prev.SnapshotIsPartial,
                last.SnapshotIsPartial);

            if (!verdict.Changed) continue;

            /*
              ⚠️ **الوقت هو وقت الفحص اللي كشف التبديل، مش وقت
              الاستقبال** — الفحص ممكن يكون اتعمل أوفلاين من يومين.
            */
            device.PartChangedAtUtc = last.StartedAtUtc;
            device.PartChangeSummary = verdict.Summary;

            touched = true;

            log.LogInformation(
                "تبديل قطعة: جهاز {Device} — {Summary}",
                device.PublicCode, verdict.Summary);
        }

        return touched;
    }

    /// <summary>الفني اللي الفحص هينتسب له، ولو الحمولة غلط — سبب الرفض.</summary>
    private readonly record struct TechnicianLink(
        Guid? TechnicianId, IngestRejection? Rejection);

    /// <summary>
    /// بيحدد كل فحص بتاع أنهي فني — <b>والشركة هي الحكم</b>.
    ///
    /// <para><b>أربع حالات:</b></para>
    ///
    /// <list type="number">
    /// <item>الحقل فاضي أو مش GUID ← <c>null</c>. راكة أقدم من
    /// المصادقة المركزية، أو شيت اترفع بإيد المدير.
    /// <b>بيتقبل</b> باللقطة.</item>
    ///
    /// <item>GUID مالوش وجود ← <c>null</c> مع تحذير. الراكة القديمة
    /// كانت بتحط معرّف <b>حسابها المحلي</b> هنا، وهو GUID من مساحة
    /// تانية خالص. رفضه كان هيوقف مزامنة كل راكة ماترقّتش.</item>
    ///
    /// <item>🔴 GUID لفني في <b>شركة تانية</b> ← <b>رفض</b>. النسبة
    /// العابرة للشركات مش «خطأ في البيانات» — دي محاولة كتابة في
    /// شركة تانية.</item>
    ///
    /// <item>GUID سليم بس الكود اللي معاه لفني تاني ← <b>رفض</b>.
    /// الكود ثابت بعد الإنشاء، فاختلافه معناه إن الحمولة اتلغبطت في
    /// الطريق — وقبولها بيدّي فحص بهويتين.</item>
    /// </list>
    ///
    /// <para>⚠️ <b>والفني الموقوف بيعدّي عادي.</b> الإيقاف بيمنع دخول
    /// جديد — مش بيمسح شغل اتعمل قبله. رفض الفحوص المعلّقة لفني اتوقف
    /// النهارده معناه ضياع شغل يوم كامل عقاباً على قرار إداري.</para>
    /// </summary>
    private async Task<Dictionary<Guid, TechnicianLink>> ResolveTechniciansAsync(
        IngestReportsCommand command, IReadOnlyList<LaptopReportPayload> list,
        CancellationToken ct)
    {
        var wanted = new HashSet<Guid>();

        foreach (var dto in list)
        {
            if (Guid.TryParse(dto.TechnicianId, out var parsed) && parsed != Guid.Empty)
                wanted.Add(parsed);
        }

        var rows = await reports.TechnicianIdentitiesAsync(wanted, ct);
        var found = rows.ToDictionary(r => r.Id);

        var map = new Dictionary<Guid, TechnicianLink>();

        foreach (var dto in list) map[dto.Id] = Resolve(command.TenantId, dto, found);

        return map;
    }

    private TechnicianLink Resolve(
        Guid tenantId, LaptopReportPayload dto,
        Dictionary<Guid, TechnicianIdentityRow> found)
    {
        if (!Guid.TryParse(dto.TechnicianId, out var id) || id == Guid.Empty)
            return new TechnicianLink(null, null);

        if (!found.TryGetValue(id, out var technician))
        {
            log.LogWarning(
                "فحص {Report} بيشاور على فني {Technician} مش موجود — اتخزّن باللقطة من غير هوية",
                dto.Id, id);

            return new TechnicianLink(null, null);
        }

        if (technician.TenantId != tenantId)
        {
            log.LogWarning(
                "فحص {Report} بيدّعي فني {Technician} من شركة تانية — اترفض",
                dto.Id, id);

            return new TechnicianLink(null, new IngestRejection(
                ReportIngestRules.TechnicianTenantMismatch,
                "الفني ده مش تابع لشركة المحطة دي.",
                Retryable: false));
        }

        string code = dto.TechnicianCode ?? "";

        if (code.Length > 0
            && !string.Equals(code, technician.Code, StringComparison.Ordinal))
        {
            log.LogWarning(
                "فحص {Report}: الهوية {Technician} كودها {Actual} والحمولة بتقول {Claimed} — اترفض",
                dto.Id, id, technician.Code, code);

            return new TechnicianLink(null, new IngestRejection(
                ReportIngestRules.TechnicianCodeMismatch,
                $"كود الفني في الفحص ({code}) مش بتاع الهوية المبعوتة.",
                Retryable: false));
        }

        return new TechnicianLink(id, null);
    }

    /// <summary>
    /// بيقرّر الفحص ده بتاع أنهي جهاز.
    ///
    /// <para><b>تلات حالات وبس:</b> الراكة بعتت معرّف والجهاز موجود ←
    /// اربط. الراكة بعتت معرّف والجهاز لسه ماوصلش ← <b>محتاج
    /// مراجعة</b>. مفيش معرّف ← جرّب تطابق بالمراسي، ولو مفيش تطابق
    /// سيبه للمراجعة.</para>
    ///
    /// <para>🔴 <b>ومفيش إنشاء جهاز هنا خالص.</b> الفحوص المستوردة
    /// مراسيها ضعيفة — «Default string» بتتكرر على مئات اللابات.
    /// توليد جهاز من كل فحص قديم معناه <b>آلاف الأجهزة الوهمية</b>
    /// اللي محدّش هيقدر ينضّفها. والإنشاء بيحصل على الراكة وهي ماسكة
    /// اللاب، أو بإيد المدير بعد مراجعة — مش هنا.</para>
    /// </summary>
    private async Task<DeviceLink> ResolveDeviceAsync(
        IngestReportsCommand command, LaptopReportPayload dto,
        HashSet<Guid> known, CancellationToken ct)
    {
        if (dto.DeviceId.HasValue && dto.DeviceId.Value != Guid.Empty)
        {
            /*
              🔴 **الترجمة قبل أي حاجة.**

              الراكة بتبعت بمعرّفها المحلي، واللي ممكن يكون اتعرّف
              عليه كجهاز موجود وقت مزامنة الأجهزة. من غير السطور دي،
              الفحص بيروح **لجهاز مكرر** بدل الكانوني.
            */
            var alias = await reports.CanonicalForAliasAsync(
                command.TenantId, dto.DeviceId.Value, ct);

            if (alias is { } canonical) return new DeviceLink(canonical, false);

            if (known.Contains(dto.DeviceId.Value))
                return new DeviceLink(dto.DeviceId.Value, false);

            /*
              الفحص بيشاور على جهاز السيرفر ما يعرفوش. مسار الدفعات
              بيرفض ده قبل ما يوصل هنا (بإعادة ممكنة) عشان الجهاز
              يسبق فحصه. واللي بيوصل هنا جاي من المسار القديم أو من
              رفع ملف بإيد المدير — فبنقبله ونعلّمه بدل ما نضيّعه.
            */
            log.LogWarning(
                "فحص {Report} بيشاور على جهاز {Device} مش موجود — اتعلّم للمراجعة",
                dto.Id, dto.DeviceId.Value);

            return new DeviceLink(null, true);
        }

        // مفيش معرّف: فحص قديم من قبل هوية الأجهزة، أو نسخة أقدم.
        // بنطابق بالمراسي — **ربط بس مش إنشاء**.
        var matched = await TryMatchAsync(command.TenantId, dto.Specs, ct);

        return matched is { } found
            ? new DeviceLink(found, false)
            : new DeviceLink(null, true);
    }

    /// <summary>
    /// بيدوّر على جهاز بمراسي فحص وصل من غير معرّف.
    ///
    /// <para>🔴 <b>وبنفس ترتيب القوة اللي على الراكة بالظبط.</b> أي
    /// اختلاف بين الطرفين معناه إن <b>نفس اللاب بياخد هوية مختلفة
    /// حسب مين اللي طابق</b>.</para>
    /// </summary>
    private async Task<Guid?> TryMatchAsync(
        Guid tenantId, DeviceSpecsPayload specs, CancellationToken ct)
    {
        foreach (var kind in DeviceIdentityStrength.Order)
        {
            var values = kind == Core.Enums.DeviceIdentifierKind.DiskSerial

                // ⚠️ كل الأقراص — أي واحد فيهم ممكن يكون المرساة.
                ? specs.InternalDisks.Select(d => d.SerialNumber)

                : [Probe(kind, specs)];

            foreach (string value in values)
            {
                if (IdentityValues.IsPlaceholder(value)) continue;

                string normalized = IdentityValues.Normalize(value);

                if (normalized.Length == 0) continue;

                var matches = await reports.DevicesByIdentifierAsync(
                    tenantId, kind, normalized, ct);

                /*
                  🔴 **مرساة واحدة بتشاور على جهازين = عيب بيانات.**

                  بنسيبه للمراجعة بدل ما نختار واحد عشوائي — والاختيار
                  العشوائي هنا معناه فحص بيروح لجهاز غلط ومحدّش بيعرف.
                */
                if (matches.Count == 1) return matches[0];
                if (matches.Count > 1) return null;
            }
        }

        return null;
    }

    private static string Probe(
        Core.Enums.DeviceIdentifierKind kind, DeviceSpecsPayload specs) => kind switch
    {
        Core.Enums.DeviceIdentifierKind.SystemUuid => specs.SystemUuid,
        Core.Enums.DeviceIdentifierKind.BiosSerial => specs.SerialNumber,
        Core.Enums.DeviceIdentifierKind.BoardSerial => specs.BoardSerial,
        _ => "",
    };
}
