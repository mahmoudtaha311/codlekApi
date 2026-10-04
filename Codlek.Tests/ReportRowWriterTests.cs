using Codlek.Application.Contracts.Sync;
using Codlek.Application.Features.Rack.IngestReports;
using Codlek.Core.Entities;
using Codlek.Core.Sync;

namespace Codlek.Tests;

/// <summary>
/// كتابة حمولة الراكة على صف الفحص — <b>خانة خانة</b>.
///
/// <para>🔴 <b>والتقرير وثيقة مش شاشة.</b> اللقطة بتتكتب زي ما وصلت
/// مهما كان اسم الفني الحالي على السيرفر — اللي حصل يوم الفحص مش
/// بيتغيّر لأن حد غيّر اسمه بعدين.</para>
///
/// <para>⚠️ <b>والملف ده موجود لأن الدالة بتكتب ~٥٠ خانة، وكل قص وكل
/// <c>null</c> فيها قرار.</b> فحصها من خلال المعالج كان بيحتاج قاعدة
/// ومحطة وفني — فالقرارات كانت بتفضل غير مقاسة.</para>
/// </summary>
public class ReportRowWriterTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static Report Write(
        LaptopReportPayload dto,
        Report? onto = null,
        DeviceLink link = default,
        Guid? technicianId = null)
    {
        var row = onto ?? new Report { Id = dto.Id, TenantId = Tenant };

        ReportRowWriter.Apply(row, dto, "{}", Guid.NewGuid(), link, Tenant, technicianId);

        return row;
    }

    private static LaptopReportPayload Dto() => new()
    {
        Id = Guid.NewGuid(),
        StartedAtUtc = new DateTime(2026, 10, 4, 9, 0, 0),
        Specs = new DeviceSpecsPayload(),
    };

    // =================================================================
    //  عدّادات المراحل
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الأرقام دي على السلك</b> — تغييرها معناه إن كل عدّاد في
    /// كل تقرير قديم يبقى غلط.
    /// </summary>
    [Fact]
    public void Each_status_lands_in_its_own_counter()
    {
        var dto = Dto();

        dto.Steps =
        [
            new StepResultPayload { Status = ReportIngestRules.StepPass },
            new StepResultPayload { Status = ReportIngestRules.StepPass },
            new StepResultPayload { Status = ReportIngestRules.StepFail },
            new StepResultPayload { Status = ReportIngestRules.StepSkip },
            new StepResultPayload { Status = ReportIngestRules.StepNotPresent },
            new StepResultPayload { Status = ReportIngestRules.StepError },
        ];

        var row = Write(dto);

        Assert.Equal(2, row.PassCount);
        Assert.Equal(1, row.FailCount);
        Assert.Equal(1, row.SkipCount);
        Assert.Equal(1, row.NotPresentCount);
        Assert.Equal(1, row.ErrorCount);
        Assert.Equal(0, row.NotRunCount);
    }

    /// <summary>
    /// 🔴 <b>«ماتفحصتش» عدّاد سادس، مش غياب.</b>
    ///
    /// <para>من غيره، المرحلة اللي محدّش لمسها بتختفي من العدّادات
    /// خالص والتقرير بيبان كأنه اتفحص بالكامل.</para>
    ///
    /// <para>⚠️ <b>وبس للمراحل اللي محتاجة نتيجة.</b> مرحلة التسليم
    /// مش تست، وعدّها كانت بتخلّي <b>كل</b> تقرير يبان ناقص
    /// مرحلة.</para>
    /// </summary>
    [Fact]
    public void Only_steps_that_need_a_result_count_as_not_run()
    {
        var dto = Dto();

        dto.Steps =
        [
            new StepResultPayload { Status = 0, RequiresResult = true },
            new StepResultPayload { Status = 0, RequiresResult = true },

            // ⚠️ مرحلة التسليم — مش تست.
            new StepResultPayload { Status = 0, RequiresResult = false },
        ];

        Assert.Equal(2, Write(dto).NotRunCount);
    }

    /// <summary>
    /// ⚠️ <b>والافتراضي <c>true</c>:</b> حمولة قديمة مافيهاش الحقل
    /// بنعدّ كل مراحلها بدل ما نعدّ ولا واحدة.
    /// </summary>
    [Fact]
    public void An_old_payload_without_the_flag_counts_every_step()
    {
        Assert.True(new StepResultPayload().RequiresResult);
    }

    // =================================================================
    //  النطاق
    // =================================================================

    /// <summary>
    /// 🔴 <b>النطاق زي ما الراكة ختمته — و<c>null</c> بتفضل
    /// <c>null</c>.</b>
    ///
    /// <para>تقرير من راكة أقدم من الميزة <b>مش</b> «كامل». خلط
    /// الاتنين بيخلّي كل تقرير قديم يتقال عليه «كامل» من غير
    /// دليل.</para>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    public void The_scope_is_carried_not_computed(int? scope)
    {
        var dto = Dto();
        dto.Scope = scope;

        Assert.Equal(scope, Write(dto).Scope);
    }

    // =================================================================
    //  الفاضي بيبقى null
    // =================================================================

    /// <summary>
    /// ⚠️ <b>والفرق مش تجميل:</b> «الفحص ده أقدم من الميزة» و«الفني
    /// بصّ وماعلّمش حاجة» لازم يفضلوا متفرّقين في القاعدة، وإلا أي
    /// إحصاء عن حالة اللابات بيخلط الاتنين.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_condition_field_is_stored_as_null(string? blank)
    {
        var dto = Dto();

        dto.ScreenGrade = blank;
        dto.ScreenRepair = blank;
        dto.HousingPaint = blank;
        dto.HousingCrack = blank;
        dto.BatteryService = blank;
        dto.Disassembly = blank;

        var row = Write(dto);

        Assert.Null(row.ScreenGrade);
        Assert.Null(row.ScreenRepair);
        Assert.Null(row.HousingPaint);
        Assert.Null(row.HousingCrack);
        Assert.Null(row.BatteryService);
        Assert.Null(row.Disassembly);
    }

    [Fact]
    public void A_filled_condition_field_is_trimmed_and_kept()
    {
        var dto = Dto();
        dto.ScreenGrade = "  أ  ";

        Assert.Equal("أ", Write(dto).ScreenGrade);
    }

    /// <summary>⚠️ والاسم التجاري الفاضي <c>null</c> كمان.</summary>
    [Fact]
    public void An_empty_commercial_name_is_null_not_empty_string()
    {
        var dto = Dto();
        dto.Specs.CommercialModelName = "";

        var row = Write(dto);

        Assert.Null(row.CommercialModelName);
        Assert.Null(row.CommercialModelSource);
        Assert.Null(row.MachineType);
    }

    // =================================================================
    //  عمود البحث
    // =================================================================

    /// <summary>
    /// 🔴 <b>وكود الجهاز داخل في عمود البحث — ودي كانت مشالة.</b>
    ///
    /// <para>خانة البحث مكتوب عليها بالنص «ابحث بكود الجهاز أو
    /// الموديل أو الفني»، والكود ماكانش في العمود خالص. مقاس على
    /// الإنتاج: البحث بكود جهاز كان بيرجّع الجهاز و<b>صفر فحوص</b>
    /// رغم إن ليه فحص فعلاً. والفني اللي ماسك لاب وعليه استيكر ده
    /// أول حاجة بيكتبها.</para>
    /// </summary>
    [Fact]
    public void The_search_column_carries_the_device_code()
    {
        var dto = Dto();
        dto.DeviceCode = "LP-00018425";

        Assert.Contains("LP-00018425", Write(dto).SearchText);
    }

    /// <summary>
    /// 🔴 <b>والاسم التجاري داخل فيه.</b> الفني بيدوّر بـ«Legion» مش
    /// بـ«82B5».
    /// </summary>
    [Fact]
    public void The_search_column_carries_the_commercial_name()
    {
        var dto = Dto();

        dto.Specs.Model = "82B5";
        dto.Specs.CommercialModelName = "Legion 5 15ARH05";

        string text = Write(dto).SearchText;

        Assert.Contains("82B5", text);

        /*
          ⚠️ **والعمود مطبَّع — بحروف كبيرة.**

          `ArabicText.Normalize` بتكبّر الحروف اللاتينية، فالبحث لازم
          يطبّع اللي بيدوّر بيه كمان. لو حد قارن نص خام بالعمود ده،
          «Legion» عمرها ما هتلاقي حاجة — والفحص بيثبّت الشكل
          المتخزّن عشان الطرفين مايتفرّقوش.
        */
        Assert.Contains("LEGION", text);
        Assert.DoesNotContain("Legion", text);
    }

    [Fact]
    public void The_search_column_carries_the_technician_and_the_note()
    {
        var dto = Dto();

        dto.TechnicianName = "أحمد فني";
        dto.TechnicianCode = "100200";
        dto.GeneralNote = "الشاشة فيها خط";

        string text = Write(dto).SearchText;

        Assert.Contains("100200", text);
        Assert.Contains("خط", text);
    }

    // =================================================================
    //  النصوص المحسوبة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>و«No RAM» مش <c>0GB</c>:</b> صفر معناه إن القراءة
    /// فشلت، واللاب اللي بيشتغل مستحيل يكون بغير رام.
    /// </summary>
    [Theory]
    [InlineData(0L, "No RAM")]
    [InlineData(8589934592L, "8GB")]
    [InlineData(17179869184L, "16GB")]
    public void The_ram_text_is_binary_gigabytes(long bytes, string expected)
    {
        var dto = Dto();
        dto.Specs.TotalRamBytes = bytes;

        Assert.Equal(expected, Write(dto).RamText);
    }

    /// <summary>
    /// ⚠️ <b>وحجم الهارد بالنظام العشري زي ما المصنّع بيكتبه.</b>
    /// القرص اللي مكتوب عليه ٥٠٠ جيجا بيتعرض ٥٠٠، مش ٤٦٦ — الفني
    /// بيقارن باللي على الاستيكر.
    /// </summary>
    [Fact]
    public void The_storage_text_joins_the_disks_in_decimal_gigabytes()
    {
        var dto = Dto();

        dto.Specs.InternalDisks =
        [
            new DiskInfoPayload { SizeBytes = 500_000_000_000, MediaType = "SSD" },
            new DiskInfoPayload { SizeBytes = 1_000_000_000_000, MediaType = "HDD" },
        ];

        Assert.Equal("500 GB SSD + 1 TB HDD", Write(dto).StorageText);
    }

    /// <summary>
    /// ⚠️ <b>و«No Hard» مش فاضي:</b> لاب من غير هارد حالة حقيقية
    /// (اتسحب منه)، والخانة الفاضية بتتقري «مقريناش».
    /// </summary>
    [Fact]
    public void A_laptop_with_no_disk_says_so()
    {
        Assert.Equal("No Hard", Write(Dto()).StorageText);
    }

    /// <summary>
    /// ⚠️ <b>وبصمة الجهاز مابتاخدش الحشو.</b>
    /// <c>To Be Filled By O.E.M.</c> بتتكرر على مئات اللابات، ووجودها
    /// في البصمة بيخلّي لابات مختلفة تبان بنفس البصمة.
    /// </summary>
    [Fact]
    public void The_fingerprint_drops_the_oem_placeholder()
    {
        var dto = Dto();

        dto.Specs.SerialNumber = ReportIngestRules.OemPlaceholder;
        dto.Specs.BoardSerial = "BOARD-999";

        string print = Write(dto).Fingerprint;

        Assert.DoesNotContain("O.E.M.", print);
        Assert.Contains("BOARD-999", print);
    }

    [Fact]
    public void A_device_with_nothing_readable_is_unknown()
    {
        Assert.Equal("UNKNOWN", Write(Dto()).Fingerprint);
    }

    /// <summary>
    /// ⚠️ <b>وملخّص الشاشة بنفس الترتيب والفواصل بالحرف</b> — النص
    /// ده بيتخزّن في عمود وبيتعرض في عشر شاشات، فتغيير الفاصل بيخلّي
    /// الصفوف القديمة والجديدة شكلين.
    /// </summary>
    [Fact]
    public void The_screen_summary_keeps_its_exact_shape()
    {
        var dto = Dto();

        dto.Specs.Screen = new ScreenInfoPayload
        {
            Width = 1920, Height = 1080, RefreshRate = 144,
            DiagonalInches = 15.6, IsTouch = false,
        };

        Assert.Equal("15.6\" | 1920x1080 | 144Hz | Non-Touch", Write(dto).ScreenSummary);
    }

    /// <summary>
    /// ⚠️ <b>وصحة البطارية مسقوفة على ١٠٠.</b> بطارية بسعة كاملة
    /// أكبر من التصميم (بيحصل) كانت بتطلّع ١٠٤٪.
    /// </summary>
    [Fact]
    public void The_battery_health_is_capped()
    {
        var dto = Dto();

        dto.Specs.Battery = new BatteryInfoPayload
        {
            DesignCapacity = 50_000, FullChargeCapacity = 55_000,
        };

        Assert.Equal(100, Write(dto).BatteryHealthPercent);
    }

    [Fact]
    public void A_battery_with_no_design_capacity_is_zero_not_a_crash()
    {
        var dto = Dto();

        dto.Specs.Battery = new BatteryInfoPayload { FullChargeCapacity = 40_000 };

        Assert.Equal(0, Write(dto).BatteryHealthPercent);
    }

    // =================================================================
    //  التواريخ
    // =================================================================

    /// <summary>
    /// ⚠️ <b>التاريخ الجايّ من الراكة بيتعلّم <c>Utc</c>.</b> الراكة
    /// بتبعته من غير منطقة فبيتقرا <c>Unspecified</c>، والعمود
    /// بيتقارن بتواريخ UTC.
    /// </summary>
    [Fact]
    public void Dates_arrive_unzoned_and_are_labelled_utc()
    {
        var dto = Dto();

        dto.StartedAtUtc = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Unspecified);
        dto.EndedAtUtc = new DateTime(2026, 10, 4, 9, 30, 0, DateTimeKind.Unspecified);

        var row = Write(dto);

        Assert.Equal(DateTimeKind.Utc, row.StartedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, row.EndedAtUtc!.Value.Kind);

        // ⚠️ والقيمة ماتغيّرتش — اللافتة بس.
        Assert.Equal(9, row.StartedAtUtc.Hour);
    }

    /// <summary>⚠️ والتاريخ اللي بإزاحة بيتحوّل مش بيتعلّم.</summary>
    [Fact]
    public void A_local_date_is_converted()
    {
        var local = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Local);

        Assert.Equal(local.ToUniversalTime(), ReportIngestRules.Utc(local));
    }

    [Fact]
    public void A_missing_end_date_stays_null()
    {
        Assert.Null(Write(Dto()).EndedAtUtc);
    }

    // =================================================================
    //  الأولاد
    // =================================================================

    [Fact]
    public void The_steps_and_parts_and_edits_are_written()
    {
        var dto = Dto();

        dto.Steps =
        [
            new StepResultPayload
            {
                Id = "battery", Title = "البطارية", Status = 1,
                Note = "تمام", DurationMs = 1200,
            },
        ];

        dto.PartsUsed = ["شاشة", "كيبورد"];

        dto.Edits =
        [
            new EditEntryPayload
            {
                AtUtc = new DateTime(2026, 10, 4, 10, 0, 0),
                ByName = "كريم", Field = "ScreenGrade",
                OldValue = "أ", NewValue = "ب", Reason = "مراجعة",
            },
        ];

        var row = Write(dto);

        var step = Assert.Single(row.Steps);

        Assert.Equal("battery", step.StepId);
        Assert.Equal("البطارية", step.Title);
        Assert.Equal(1200, step.DurationMs);

        Assert.Equal(2, row.Parts.Count);

        var edit = Assert.Single(row.Edits);

        Assert.Equal("كريم", edit.ByName);
        Assert.Equal(DateTimeKind.Utc, edit.AtUtc.Kind);
    }

    /// <summary>⚠️ والقطعة الفاضية بتتشال — مش بتتخزّن كسطر فاضي.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_part_name_is_dropped(string blank)
    {
        var dto = Dto();
        dto.PartsUsed = [blank, "شاشة"];

        Assert.Equal("شاشة", Assert.Single(Write(dto).Parts).Name);
    }

    /// <summary>
    /// ⚠️ <b>واللقطة بتتكتب بشركة الفحص</b> — صفوف المكوّنات بتتفلتر
    /// بالشركة في كل استعلام.
    /// </summary>
    [Fact]
    public void The_snapshot_components_carry_the_tenant()
    {
        var dto = Dto();

        dto.Snapshot = new HardwareSnapshotPayload
        {
            CapturedAtUtc = new DateTime(2026, 10, 4, 8, 0, 0),
            CollectorVersion = "1.4.0",
            RanAsAdministrator = true,
            IsPartial = false,
            Components =
            [
                new SnapshotComponentPayload
                {
                    Type = 5, ManufacturerSerial = "DISK-1",
                    IdentityConfidence = 1, IsPresent = true,
                },
            ],
        };

        var row = Write(dto);

        Assert.Equal(Tenant, Assert.Single(row.SnapshotComponents).TenantId);
        Assert.True(row.SnapshotRanAsAdministrator);
        Assert.False(row.SnapshotIsPartial);
        Assert.NotNull(row.SnapshotCapturedAtUtc);
    }

    /// <summary>
    /// ⚠️ <b>ومفيش لقطة = مفيش وقت لقطة.</b> والقيمة الافتراضية
    /// (<c>default</c>) بتتقري «مفيش» كمان، مش «سنة ١».
    /// </summary>
    [Fact]
    public void No_snapshot_means_no_capture_time()
    {
        Assert.Null(Write(Dto()).SnapshotCapturedAtUtc);

        var dto = Dto();
        dto.Snapshot = new HardwareSnapshotPayload { CapturedAtUtc = default };

        Assert.Null(Write(dto).SnapshotCapturedAtUtc);
    }

    // =================================================================
    //  القص
    // =================================================================

    /// <summary>
    /// ⚠️ <b>كل خانة نصية مقصوصة على طول عمودها.</b> جسم الطلب مش
    /// مضمون، والحفظ بخطأ من القاعدة في نص الاستقبال معناه إن
    /// <b>الدفعة كلها</b> بتقع عشان فحص واحد فيه نص طويل.
    /// </summary>
    [Fact]
    public void Every_text_column_is_clipped_to_its_width()
    {
        var dto = Dto();

        string huge = new('x', 5000);

        dto.TechnicianCode = huge;
        dto.TechnicianName = huge;
        dto.DeviceCode = huge;
        dto.ImportedFrom = huge;
        dto.DeletedReason = huge;
        dto.DeletedByName = huge;
        dto.RestoredByName = huge;
        dto.RestoredReason = huge;

        dto.Specs.Manufacturer = huge;
        dto.Specs.Model = huge;
        dto.Specs.Cpu = huge;
        dto.Specs.Gpu = huge;
        dto.Specs.SerialNumber = huge;

        var row = Write(dto);

        Assert.Equal(20, row.TechnicianCode.Length);
        Assert.Equal(120, row.TechnicianName.Length);
        Assert.Equal(20, row.DeviceCode.Length);
        Assert.Equal(200, row.ImportedFrom.Length);
        Assert.Equal(400, row.DeletedReason.Length);
        Assert.Equal(120, row.DeletedByName.Length);
        Assert.Equal(120, row.RestoredByName.Length);
        Assert.Equal(400, row.RestoredReason.Length);

        Assert.Equal(80, row.Manufacturer.Length);
        Assert.Equal(120, row.Model.Length);
        Assert.Equal(160, row.Cpu.Length);
        Assert.Equal(160, row.Gpu.Length);
        Assert.Equal(120, row.SerialNumber.Length);
        Assert.Equal(300, row.Fingerprint.Length);
    }

    /// <summary>⚠️ والأولاد مقصوصين كمان.</summary>
    [Fact]
    public void The_children_are_clipped_too()
    {
        var dto = Dto();

        string huge = new('x', 5000);

        dto.Steps = [new StepResultPayload { Id = huge, Title = huge }];
        dto.PartsUsed = [huge];
        dto.Edits = [new EditEntryPayload { ByName = huge, Field = huge, Reason = huge }];

        var row = Write(dto);

        Assert.Equal(60, row.Steps[0].StepId.Length);
        Assert.Equal(120, row.Steps[0].Title.Length);
        Assert.Equal(200, row.Parts[0].Name.Length);
        Assert.Equal(120, row.Edits[0].ByName.Length);
        Assert.Equal(80, row.Edits[0].Field.Length);
        Assert.Equal(400, row.Edits[0].Reason.Length);
    }

    // =================================================================
    //  الهوية واللقطة
    // =================================================================

    /// <summary>
    /// 🔴 <b>اللقطة بتتكتب زي ما وصلت — والتقرير وثيقة مش شاشة.</b>
    /// اسم الفني في الصف ده هو اللي كان وقت الفحص، مهما اتغيّر
    /// بعدين.
    /// </summary>
    [Fact]
    public void The_technician_snapshot_is_whatever_arrived()
    {
        var dto = Dto();

        dto.TechnicianName = "أحمد زمان";
        dto.TechnicianCode = "100200";

        var id = Guid.NewGuid();
        var row = Write(dto, technicianId: id);

        Assert.Equal(id, row.TechnicianId);
        Assert.Equal("أحمد زمان", row.TechnicianName);
        Assert.Equal("100200", row.TechnicianCode);
    }

    /// <summary>⚠️ ولمسة الاستقبال بتتختم بوقت السيرفر.</summary>
    [Fact]
    public void The_received_stamp_is_the_server_clock()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var row = Write(Dto());

        Assert.InRange(row.ReceivedAtUtc, before, DateTime.UtcNow.AddSeconds(1));
    }
}
