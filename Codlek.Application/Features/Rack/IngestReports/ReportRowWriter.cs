using Codlek.Application.Contracts.Sync;
using Codlek.Core.Entities;
using Codlek.Core.Sync;
using Codlek.Core.Text;

namespace Codlek.Application.Features.Rack.IngestReports;

/// <summary>الجهاز اللي الفحص هيتربط بيه، ولو مفيش — ليه.</summary>
public readonly record struct DeviceLink(Guid? DeviceId, bool NeedsResolution);

/// <summary>
/// بيكتب حمولة الراكة على صف الفحص.
///
/// <para>🔴 <b>والتقرير وثيقة مش شاشة.</b> اللقطة بتتكتب زي ما وصلت
/// مهما كان اسم الفني الحالي على السيرفر — اللي حصل يوم الفحص مش
/// بيتغيّر لأن حد غيّر اسمه بعدين.</para>
///
/// <para>⚠️ <b>وكلاس لوحده عشان يتجرّب لوحده.</b> الدالة دي بتكتب
/// ~٥٠ خانة، وكل قص وكل <c>null</c> فيها قرار — وفحصها من خلال
/// المعالج كان بيحتاج قاعدة ومحطة وفني.</para>
/// </summary>
public static class ReportRowWriter
{
    public static void Apply(
        Report row, LaptopReportPayload dto, string raw, Guid? sourceRackId,
        DeviceLink link, Guid tenantId, Guid? technicianId)
    {
        var specs = dto.Specs;
        var screen = specs.Screen;

        // الهوية الثابتة + اللقطة.
        row.TechnicianId = technicianId;
        row.TechnicianCode = TextClip.To(dto.TechnicianCode, 20);
        row.TechnicianName = TextClip.To(dto.TechnicianName, 120);

        row.StartedAtUtc = ReportIngestRules.Utc(dto.StartedAtUtc);

        row.EndedAtUtc = dto.EndedAtUtc.HasValue
            ? ReportIngestRules.Utc(dto.EndedAtUtc.Value)
            : null;

        row.DurationMs = dto.DurationMs;

        row.Manufacturer = TextClip.To(specs.Manufacturer, 80);
        row.Model = TextClip.To(specs.Model, 120);
        row.Cpu = TextClip.To(specs.Cpu, 160);
        row.Gpu = TextClip.To(specs.Gpu, 160);
        row.SerialNumber = TextClip.To(specs.SerialNumber, 120);

        row.Fingerprint = TextClip.To(
            ReportIngestRules.Fingerprint(
                specs.SerialNumber, specs.BoardSerial, specs.SystemUuid,
                specs.Manufacturer, specs.Model),
            300);

        /*
          ⚠️ **الفاضي بيتخزّن `null` مش `""`.**

          والفرق مش تجميل: الواجهة بتفرّق بين «مفيش اسم تجاري»
          و«الراكة أقدم من الميزة»، والاتنين بيتعرضوا بالخام بس
          بيتحسبوا مختلف.
        */
        row.CommercialModelName =
            ReportIngestRules.Blank(specs.CommercialModelName, 160);

        row.CommercialModelSource =
            ReportIngestRules.Blank(specs.CommercialModelSource, 60);

        row.MachineType = ReportIngestRules.Blank(specs.MachineType, 40);

        /*
          🔴 **عمود البحث لازم يتحسب هنا مع كل كتابة.**

          البحث بيقارن نص مطبَّع بنص مطبَّع — لو العمود ده فضل قديم،
          الفحص **بيختفي من البحث** من غير أي رسالة.

          🔴 **والاسم التجاري داخل فيه.** الفني بيدوّر بـ«Legion» مش
          بـ«82B5».

          🔴 **وكود الجهاز داخل فيه كمان — ودي كانت مشالة.** خانة
          البحث مكتوب عليها بالنص «ابحث بكود الجهاز أو الموديل أو
          الفني»، والكود ماكانش في العمود خالص. مقاس على الإنتاج:
          البحث بكود جهاز كان بيرجّع الجهاز و**صفر فحوص** رغم إن ليه
          فحص فعلاً. والفني اللي ماسك لاب وعليه استيكر ده أول حاجة
          بيكتبها.

          ⚠️ و`DeviceCode` هو اللي كان مكتوب على الفحص **وقتها** —
          اللاب ممكن يكون كوده اتغيّر بعدين، والفحص القديم لازم يفضل
          يتلاقى بالكود اللي كان عليه ساعتها.
        */
        row.SearchText = ArabicText.Combine(
            dto.DeviceCode, specs.Manufacturer, specs.Model,
            specs.CommercialModelName ?? "",
            specs.SystemFamily ?? "", specs.Cpu, specs.SerialNumber,
            specs.BoardSerial, dto.TechnicianName, dto.TechnicianCode, dto.GeneralNote);

        row.RamText = ReportIngestRules.RamText(specs.TotalRamBytes);

        row.StorageText = TextClip.To(
            ReportIngestRules.StorageText(
                specs.InternalDisks.Select(d => (d.SizeBytes, d.MediaType))),
            120);

        row.ScreenSummary = TextClip.To(screen.Summary, 200);
        row.ScreenInches = screen.DiagonalInches;
        row.RefreshRate = screen.RefreshRate;
        row.IsTouch = screen.IsTouch;

        row.BatteryHealthPercent = specs.Battery.HealthPercent;
        row.BenchmarkScore = dto.Background.BenchmarkScore;
        row.MaxCpuTemp = dto.Background.MaxCpuTemp;
        row.ThrottlingDetected = dto.Background.ThrottlingDetected;

        row.PassCount = dto.Steps.Count(s => s.Status == ReportIngestRules.StepPass);
        row.FailCount = dto.Steps.Count(s => s.Status == ReportIngestRules.StepFail);
        row.SkipCount = dto.Steps.Count(s => s.Status == ReportIngestRules.StepSkip);

        row.NotPresentCount =
            dto.Steps.Count(s => s.Status == ReportIngestRules.StepNotPresent);

        row.ErrorCount = dto.Steps.Count(s => s.Status == ReportIngestRules.StepError);

        row.NotRunCount = ReportIngestRules.NotRun(
            dto.Steps.Select(s => (s.RequiresResult, s.Status)));

        /*
          🔴 **النطاق زي ما الراكة ختمته — مش بيتحسب هنا.**

          الراكة هي اللي عارفة أنهي مرحلة كانت «محتاجة نتيجة» في نسخة
          قايمة المراحل بتاعتها. السيرفر لو حسبها بنفسه كان هيستخدم
          تعريفه هو، وأول ما المراحل تتغيّر على الراكة التقارير
          القديمة تتعاد تصنيفها **بأثر رجعي**.

          ⚠️ و`null` بتفضل `null`. تقرير من راكة أقدم من الميزة
          **مش** «كامل».
        */
        row.Scope = dto.Scope;

        row.GeneralNote = dto.GeneralNote ?? "";

        /*
          ⚠️ **والفاضي هنا `null` كمان:** «الفحص ده أقدم من الميزة»
          و«الفني بصّ وماعلّمش حاجة» لازم يفضلوا متفرّقين في القاعدة،
          وإلا أي إحصاء عن حالة اللابات بيخلط الاتنين.
        */
        row.ScreenGrade = ReportIngestRules.Blank(dto.ScreenGrade, 4);
        row.ScreenRepair = ReportIngestRules.Blank(dto.ScreenRepair, 120);
        row.HousingPaint = ReportIngestRules.Blank(dto.HousingPaint, 60);
        row.HousingCrack = ReportIngestRules.Blank(dto.HousingCrack, 80);
        row.BatteryService = ReportIngestRules.Blank(dto.BatteryService, 30);
        row.Disassembly = ReportIngestRules.Blank(dto.Disassembly, 120);

        /*
          🔴 **حالة المسح قرار واحد — وبتتكتب بس لو قرار الراكة أحدث.**

          المسح والاسترجاع من الموقع بيحصلوا على السيرفر بس، والراكة
          لسه شايلة الفحص «مش ممسوح». القديم كان بينسخ حالة الراكة هنا
          مع كل إعادة إرسال متغيّرة — فمسح المدير **كان بيتلغي في صمت**
          أول ما الفني يعدّل أي حاجة في الفحص.

          ⚠️ والخانات السبعة بتتكتب مع بعض أو بتفضل مع بعض: «ممسوح»
          من الموقع وسببه من الراكة كان هيبقى فحص بقصتين.

          ⚠️ والإضافة مش محتاجة فرع لوحدها: الصف الجديد مالوش قرار،
          فالقاعدة بترجّع «الراكة تكسب» وحالتها بتتكتب زي الأول.
          والتفاصيل في `ReportIngestRules.RackDeletionWins`.
        */
        DateTime? rackDeletedAt = dto.DeletedAtUtc.HasValue
            ? ReportIngestRules.Utc(dto.DeletedAtUtc.Value)
            : null;

        DateTime? rackRestoredAt = dto.RestoredAtUtc.HasValue
            ? ReportIngestRules.Utc(dto.RestoredAtUtc.Value)
            : null;

        if (ReportIngestRules.RackDeletionWins(
                row.DeletedAtUtc, row.RestoredAtUtc, rackDeletedAt, rackRestoredAt))
        {
            row.IsDeleted = dto.IsDeleted;
            row.DeletedReason = TextClip.To(dto.DeletedReason, 400);
            row.DeletedByName = TextClip.To(dto.DeletedByName, 120);
            row.DeletedAtUtc = rackDeletedAt;

            row.RestoredByName = TextClip.To(dto.RestoredByName, 120);
            row.RestoredReason = TextClip.To(dto.RestoredReason, 400);
            row.RestoredAtUtc = rackRestoredAt;
        }

        row.ImportedFrom = TextClip.To(dto.ImportedFrom, 200);

        row.ReceivedAtUtc = DateTime.UtcNow;
        row.SourceRackId = sourceRackId;
        row.RawJson = raw;

        // ── الجهاز ───────────────────────────────────────────────────
        /*
          ⚠️ **الربط بيتكسب ومبيتفقدش.**

          فحص كان مربوط بجهاز وبعدين اتبعت تاني ومبقاش فيه تطابق
          واضح — الربط القديم **بيفضل**.

          والحالة دي بتحصل فعلاً: أول ما راكة تانية تشوف نفس اللاب،
          المرساة بتبقى بتشاور على جهازين، والمطابقة بترفض تخمّن (وده
          صح). بس لو سبنا ده يمسح الربط، كل مزامنة كاملة كانت هتفصل
          الفحوص عن أجهزتها **في صمت** — وتبقى المزامنة نفسها هي اللي
          بتضيّع التاريخ.

          القاعدة: `null` مبيدهسش قيمة موجودة.
        */
        if (link.DeviceId.HasValue)
        {
            row.DeviceId = link.DeviceId;
            row.NeedsDeviceResolution = false;
        }
        else if (row.DeviceId is null)
        {
            row.NeedsDeviceResolution = link.NeedsResolution;
        }

        // ⚠️ وكود الجهاز الفاضي مابيدهسش كود موجود — نفس القاعدة.
        if (!string.IsNullOrEmpty(dto.DeviceCode))
            row.DeviceCode = TextClip.To(dto.DeviceCode, 20);

        // ── لقطة العتاد ──────────────────────────────────────────────
        var snapshot = dto.Snapshot;

        row.SnapshotCapturedAtUtc =
            snapshot is not null && snapshot.CapturedAtUtc != default
                ? ReportIngestRules.Utc(snapshot.CapturedAtUtc)
                : null;

        row.SnapshotCollectorVersion = TextClip.To(snapshot?.CollectorVersion, 40);
        row.SnapshotRanAsAdministrator = snapshot?.RanAsAdministrator ?? false;
        row.SnapshotIsPartial = snapshot?.IsPartial ?? false;
        row.SnapshotWarnings = TextClip.To(snapshot?.CollectionWarnings, 1000);

        row.SnapshotComponents =
            [.. (snapshot?.Components ?? []).Select(c => new ReportSnapshotComponent
            {
                TenantId = tenantId,
                Type = c.Type,
                InstanceIndex = c.InstanceIndex,
                SlotOrPosition = TextClip.To(c.SlotOrPosition, 60),
                Manufacturer = TextClip.To(c.Manufacturer, 80),
                Model = TextClip.To(c.Model, 160),
                PartNumber = TextClip.To(c.PartNumber, 80),
                ManufacturerSerial = TextClip.To(c.ManufacturerSerial, 120),
                HardwareFingerprint = TextClip.To(c.HardwareFingerprint, 120),
                PnPDeviceId = TextClip.To(c.PnPDeviceId, 200),
                IdentityMethod = c.IdentityMethod,
                IdentityConfidence = c.IdentityConfidence,
                IsPresent = c.IsPresent,
                CapacityBytes = c.CapacityBytes,
                SpeedMhz = c.SpeedMhz,
                HealthPercent = c.HealthPercent,
                PowerOnHours = c.PowerOnHours,
                Source = TextClip.To(c.Source, 80),
                AttributesJson = c.AttributesJson ?? "",
            })];

        row.Steps = [.. dto.Steps.Select(s => new ReportStep
        {
            StepId = TextClip.To(s.Id, 60),
            Title = TextClip.To(s.Title, 120),
            Status = s.Status,
            Note = s.Note ?? "",
            SkipReason = s.SkipReason ?? "",
            Detail = s.Detail ?? "",
            DurationMs = s.DurationMs,
        })];

        // ⚠️ القطعة الفاضية بتتشال — مش بتتخزّن كسطر فاضي.
        row.Parts = [.. (dto.PartsUsed ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new ReportPart { Name = TextClip.To(p, 200) })];

        row.Edits = [.. (dto.Edits ?? []).Select(e => new ReportEdit
        {
            AtUtc = ReportIngestRules.Utc(e.AtUtc),
            ByName = TextClip.To(e.ByName, 120),
            Field = TextClip.To(e.Field, 80),
            OldValue = e.OldValue ?? "",
            NewValue = e.NewValue ?? "",
            Reason = TextClip.To(e.Reason, 400),
        })];
    }
}
