using Codlek.Core.Entities;
using Codlek.Core.Hardware;

namespace Codlek.Tests;

/// <summary>
/// مقارنة لقطتين عتاد.
///
/// <para>🔴 <b>الفحوص دي هي اللي بتحمي بني آدم من اتهام.</b>
/// المقارنة بتتحوّل لكلام زي «القطعة دي اتشالت» — وده بيتقال على
/// موظف. فكل فحص تحت بيثبت إن الملف <b>مابيقولش</b> حاجة الدليل
/// مش بيسمح بيها.</para>
/// </summary>
public class SnapshotComparisonTests
{
    /// <summary>قطعة بهوية قوية — ثقة أ وسيريال حقيقي.</summary>
    private static ReportSnapshotComponent Strong(
        int type, string serial, string model = "Model-A", int index = 0) => new()
        {
            Type = type,
            InstanceIndex = index,
            IdentityConfidence = 1,
            ManufacturerSerial = serial,
            Model = model,
            IsPresent = true,
        };

    /// <summary>
    /// قطعة بهوية ضعيفة — بصمة بدل سيريال.
    ///
    /// <para>⚠️ ودي بالظبط اللي القارئ بيكتبها لما قراية السيريال
    /// تفشل.</para>
    /// </summary>
    private static ReportSnapshotComponent Weak(
        int type, string fingerprint = "FP-1", string model = "Model-A",
        int index = 0, string pnp = "") => new()
        {
            Type = type,
            InstanceIndex = index,
            IdentityConfidence = 2,
            HardwareFingerprint = fingerprint,
            Model = model,
            PnPDeviceId = pnp,
            IsPresent = true,
        };

    /// <summary>صف غياب صريح: «المكان ده فاضي».</summary>
    private static ReportSnapshotComponent Absent(int type) => new()
    {
        Type = type,
        IsPresent = false,
    };

    private static ReportSnapshotComponent Dimm(
        string serial, string bank, string locator, int index = 0) => new()
    {
        Type = ComponentType.Memory,
        InstanceIndex = index,
        IdentityConfidence = 1,
        ManufacturerSerial = serial,
        Model = "DDR4",
        IsPresent = true,
        AttributesJson = $"{{\"bank\":\"{bank}\",\"locator\":\"{locator}\"}}",
    };

    private static ComponentDiff One(
        IReadOnlyList<ReportSnapshotComponent> a,
        IReadOnlyList<ReportSnapshotComponent> b,
        bool aPartial = false, bool bPartial = false) =>
        Assert.Single(SnapshotComparison.Compare(a, b, aPartial, bPartial));

    // =================================================================
    //  المطابقة بالسيريال
    // =================================================================

    /// <summary>
    /// ⚠️ المطابقة مستقلة عن الترتيب — ترتيب التعداد بيتغيّر بين
    /// الفحوص.
    /// </summary>
    [Fact]
    public void Serial_matching_ignores_the_order_rows_came_back_in()
    {
        var a = new[]
        {
            Strong(ComponentType.Storage, "SSD-1", index: 0),
            Strong(ComponentType.Storage, "SSD-2", index: 1),
        };

        var b = new[]
        {
            Strong(ComponentType.Storage, "SSD-2", index: 0),
            Strong(ComponentType.Storage, "SSD-1", index: 1),
        };

        var diffs = SnapshotComparison.Compare(a, b);

        Assert.Equal(2, diffs.Count);
        Assert.All(diffs, d => Assert.Equal(ChangeKind.Unchanged, d.Kind));
        Assert.All(diffs, d => Assert.True(d.StrongBothSides));
    }

    /// <summary>
    /// ⚠️ نفس السيريال = نفس القطعة. فرق الاسم قراءة أو فيرموير —
    /// <b>مش تبديل</b>، فمابياخدش أحمر.
    /// </summary>
    [Fact]
    public void Same_serial_with_a_different_model_text_is_a_model_change_not_a_swap()
    {
        var d = One(
            [Strong(ComponentType.Storage, "SSD-1", "Samsung 860")],
            [Strong(ComponentType.Storage, "SSD-1", "Samsung SSD 860 EVO")]);

        Assert.Equal(ChangeKind.ModelChanged, d.Kind);
        Assert.Equal("info", ChangeKindText.Tone(d.Kind));
    }

    /// <summary>⚠️ المساحات وحالة الأحرف مابتفرّقش في السيريال.</summary>
    [Fact]
    public void Serial_comparison_trims_and_ignores_case()
    {
        var d = One(
            [Strong(ComponentType.Storage, "  ssd-1 ")],
            [Strong(ComponentType.Storage, "SSD-1")]);

        Assert.Equal(ChangeKind.Unchanged, d.Kind);
    }

    // =================================================================
    //  أخطر قاعدة في الملف
    // =================================================================

    /// <summary>
    /// 🔴 <b>أسوأ خطأ ممكن الملف ده يعمله: يخفي تبديل حقيقي.</b>
    ///
    /// <para>هاردين مختلفين بنفس الموديل بالظبط. لو مرحلة البصمة
    /// طابقت القطع اللي عندها سيريال، الاتنين كانوا هيطلعوا «زي ما
    /// هما» — والتبديل الحقيقي يختفي.</para>
    ///
    /// <para>⚠️ المفروض يطلع <c>Removed</c> + <c>Added</c>: الاتنين
    /// سيريالهم مقروء، فالقايمة كاملة والغياب مثبت.</para>
    /// </summary>
    [Fact]
    public void Two_different_drives_with_the_same_model_are_never_called_unchanged()
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(ComponentType.Storage, "OLD-SERIAL", "WDC WD10SPZX")],
            [Strong(ComponentType.Storage, "NEW-SERIAL", "WDC WD10SPZX")]);

        Assert.Equal(2, diffs.Count);
        Assert.Contains(diffs, d => d.Kind == ChangeKind.Removed);
        Assert.Contains(diffs, d => d.Kind == ChangeKind.Added);
        Assert.DoesNotContain(diffs, d => d.Kind == ChangeKind.Unchanged);
    }

    /// <summary>
    /// 🔴 <b>ونفس القاعدة في الاتجاه التاني — والمسخ أثبت إنها كانت
    /// مش مغطّاة.</b>
    ///
    /// <para>قطعة بصمتها بس (قراءة فشلت) على الشمال، وقطعة بسيريال
    /// <b>جديد</b> على اليمين، ونفس الموديل. لو مرحلة البصمة قبلت
    /// تطابقهم، الرد بيبقى «زي ما هي» — ويخبّي إن اللي في اللاب
    /// دلوقتي سيريـاله مختلف.</para>
    ///
    /// <para>⚠️ الفحص اللي فوق بيحمي الاتجاه الأول (الاتنين
    /// بسيريال) وبس: لفّة البصمة بتعدّي على الشمال القوي من غير ما
    /// تبصّ على اليمين، فتوسيع شرط اليمين لوحده كان بيعدّي على
    /// الفحوص كلها.</para>
    /// </summary>
    [Fact]
    public void A_weak_row_is_never_matched_to_a_row_that_has_a_serial()
    {
        /*
          ⚠️ **القطعة الضعيفة دي من غير بصمة عن قصد.**

          `WeakKey` بتفضّل البصمة لو موجودة، فلو حطّينا بصمة هنا
          المفتاح كان بيبقى `fp:...` والقطعة القوية مفتاحها
          `model:...` — فمكانوش بيتطابقوا أصلاً، والفحص بيعدّي على
          المسخ من غير ما يلمس السطر المقصود. (وده حصل فعلاً في أول
          نسخة من الفحص ده.)

          والحالة دي حقيقية: القارئ بيسجّل الموديل لوحده لما قراية
          السيريال **والبصمة** الاتنين يفشلوا.
        */
        var failedRead = Weak(ComponentType.Storage, fingerprint: "", model: "WDC WD10SPZX");

        var diffs = SnapshotComparison.Compare(
            [failedRead],
            [Strong(ComponentType.Storage, "NEW-SERIAL", "WDC WD10SPZX")]);

        Assert.Equal(2, diffs.Count);
        Assert.DoesNotContain(diffs, d => d.Kind == ChangeKind.Unchanged);
        Assert.All(diffs, d => Assert.Equal(ChangeKind.IdentityUncertain, d.Kind));
    }

    /// <summary>
    /// 🔴 <b>القراءة اللي فشلت مش قطعة اتشالت.</b>
    ///
    /// <para>هارد سيريـاله اتقرا المرة اللي فاتت وفشل المرة دي.
    /// القارئ مابيشيلوش — بيسجّله ببصمة وثقة ب. فلو قلنا «اتشال»،
    /// قراية SMART فشلت = موظف اتّهم بتغيير قطعة.</para>
    /// </summary>
    [Fact]
    public void A_failed_serial_read_on_the_other_side_is_uncertain_not_removed()
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(ComponentType.Storage, "SSD-1", "Samsung 860")],
            [Weak(ComponentType.Storage, "FP-SAMSUNG", "Samsung 860")]);

        Assert.DoesNotContain(diffs, d => d.Kind == ChangeKind.Removed);
        Assert.DoesNotContain(diffs, d => d.Kind == ChangeKind.Added);
        Assert.All(diffs, d => Assert.Equal(ChangeKind.IdentityUncertain, d.Kind));
    }

    // =================================================================
    //  المكان الثابت
    // =================================================================

    /// <summary>
    /// 🔴 <b>المكان الثابت هو المكان الوحيد اللي «السيريال اتغيّر»
    /// مسموحة فيه.</b>
    ///
    /// <para>شريحة ذاكرة في نفس البانك ونفس المكان، وسيريال مختلف —
    /// دي القطعة اللي في المكان ده اتغيّرت فعلاً.</para>
    /// </summary>
    [Fact]
    public void Same_memory_slot_with_a_different_serial_is_a_serial_change()
    {
        var d = One(
            [Dimm("RAM-OLD", "BANK 0", "DIMM 0")],
            [Dimm("RAM-NEW", "BANK 0", "DIMM 0")]);

        Assert.Equal(ChangeKind.SerialChanged, d.Kind);
        Assert.True(d.StrongBothSides);
        Assert.Contains("مكان ثابت", d.MatchedBy);
        Assert.Equal("bad", ChangeKindText.Tone(d.Kind));
    }

    /// <summary>
    /// 🔴 <b>الشرط ده بيقع على عتاد حقيقي.</b>
    ///
    /// <para>فيه لابات بترجّع <c>DIMM 0</c> <b>للشريحتين</b>، والفرق
    /// في البانك بس. ولو المفتاح مش فريد، كنا بنطابق شريحة بشريحة
    /// تانية بالصدفة ونقول «السيريال اتغيّر» على الاتنين.</para>
    /// </summary>
    [Fact]
    public void A_duplicated_slot_key_disables_location_matching_entirely()
    {
        // الشريحتين بنفس المكان بالظبط — المفتاح مش فريد.
        var a = new[]
        {
            Dimm("RAM-A", "BANK 0", "DIMM 0", index: 0),
            Dimm("RAM-B", "BANK 0", "DIMM 0", index: 1),
        };

        var b = new[]
        {
            Dimm("RAM-C", "BANK 0", "DIMM 0", index: 0),
            Dimm("RAM-D", "BANK 0", "DIMM 0", index: 1),
        };

        var diffs = SnapshotComparison.Compare(a, b);

        Assert.DoesNotContain(diffs, d => d.Kind == ChangeKind.SerialChanged);
    }

    /// <summary>
    /// ⚠️ <c>SlotOrPosition</c> مش موثوق لوحده: القارئ بيرجع
    /// لـ<c>"DIMM " + index</c> لما SMBIOS ما تدّيش مكان — وده
    /// <b>ترتيب تعداد</b> مش مكان. فمفيش تفاصيل = مفيش مفتاح مكان.
    /// </summary>
    [Fact]
    public void Memory_with_no_attributes_has_no_location_key()
    {
        var withoutAttributes = Strong(ComponentType.Memory, "RAM-OLD");
        withoutAttributes.SlotOrPosition = "DIMM 0";

        var replaced = Strong(ComponentType.Memory, "RAM-NEW");
        replaced.SlotOrPosition = "DIMM 0";

        var diffs = SnapshotComparison.Compare([withoutAttributes], [replaced]);

        Assert.DoesNotContain(diffs, d => d.Kind == ChangeKind.SerialChanged);
    }

    /// <summary>⚠️ تفاصيل بايظة مابتوقّعش المقارنة.</summary>
    [Fact]
    public void Broken_attribute_json_is_treated_as_no_location()
    {
        var broken = Strong(ComponentType.Memory, "RAM-1");
        broken.AttributesJson = "{ this is not json";

        var diffs = SnapshotComparison.Compare([broken], [Strong(ComponentType.Memory, "RAM-1")]);

        Assert.Equal(ChangeKind.Unchanged, Assert.Single(diffs).Kind);
    }

    /// <summary>
    /// ⚠️ مكان واحد والهوية ضعيفة على طرف — «التكوين مختلف»، مش
    /// «السيريال اتغيّر». مينفعش نقول القطعة اتغيّرت.
    /// </summary>
    [Fact]
    public void Same_slot_with_a_weak_side_is_only_a_configuration_difference()
    {
        var weakInSlot = Weak(ComponentType.Memory, "FP-1", "DDR4");
        weakInSlot.AttributesJson = "{\"bank\":\"BANK 0\",\"locator\":\"DIMM 0\"}";

        var d = One([Dimm("RAM-OLD", "BANK 0", "DIMM 0")], [weakInSlot]);

        Assert.Equal(ChangeKind.ConfigurationDifferent, d.Kind);
        Assert.False(d.StrongBothSides);
    }

    // =================================================================
    //  رقم البوردة والبصمة
    // =================================================================

    /// <summary>
    /// ⚠️ رقم البوردة بيفرّق بين كاميرتين متطابقتين على نفس اللاب،
    /// والبصمة مابتفرّقش — عشان كده بييجي قبلها.
    /// </summary>
    [Fact]
    public void The_bus_path_matches_weak_components_and_stays_weak()
    {
        const string Path = @"USB\VID_5986&PID_212B&MI_00&13219401&0&0000";

        var d = One(
            [Weak(ComponentType.Camera, "FP-X", "HD Camera", pnp: Path)],
            [Weak(ComponentType.Camera, "FP-Y", "HD Camera", pnp: Path)]);

        Assert.Equal(ChangeKind.Unchanged, d.Kind);
        Assert.Equal("رقم البوردة", d.MatchedBy);

        // 🔴 ومابيبقاش دليل قوي: المسار بيتكرّر بين لابين متطابقين،
        // فهو بيحدّد **المكان** مش **الوحدة**.
        Assert.False(d.StrongBothSides);
    }

    /// <summary>
    /// 🔴 ورقم البوردة ممنوع يلمس قطعة عندها سيريال — نفس قاعدة
    /// البصمة.
    /// </summary>
    [Fact]
    public void The_bus_path_never_matches_a_component_that_has_a_serial()
    {
        const string Path = @"SCSI\DISK&VEN_WDC";

        var left = Strong(ComponentType.Storage, "OLD");
        left.PnPDeviceId = Path;

        var right = Strong(ComponentType.Storage, "NEW");
        right.PnPDeviceId = Path;

        var diffs = SnapshotComparison.Compare([left], [right]);

        Assert.Equal(2, diffs.Count);
        Assert.DoesNotContain(diffs, d => d.MatchedBy == "رقم البوردة");
    }

    /// <summary>
    /// ⚠️ نفس التكوين ≠ نفس القطعة: البصمة بتحدّد <b>الموديل</b> مش
    /// الوحدة.
    /// </summary>
    [Fact]
    public void A_fingerprint_match_is_unchanged_but_never_strong()
    {
        var d = One(
            [Weak(ComponentType.Gpu, "FP-INTEL", "UHD 620")],
            [Weak(ComponentType.Gpu, "FP-INTEL", "UHD 620")]);

        Assert.Equal(ChangeKind.Unchanged, d.Kind);
        Assert.Equal("بصمة/موديل", d.MatchedBy);
        Assert.False(d.StrongBothSides);
    }

    /// <summary>
    /// ⚠️ قطعة من غير بصمة ومن غير موديل مالهاش مفتاح — فمابتتطابقش
    /// بالغلط مع أي قطعة تانية من غير مفتاح.
    /// </summary>
    [Fact]
    public void Components_with_no_key_at_all_are_not_matched_to_each_other()
    {
        var a = new ReportSnapshotComponent
        { Type = ComponentType.Audio, IdentityConfidence = 3, IsPresent = true };

        var b = new ReportSnapshotComponent
        { Type = ComponentType.Audio, IdentityConfidence = 3, IsPresent = true };

        var diffs = SnapshotComparison.Compare([a], [b]);

        Assert.Equal(2, diffs.Count);
        Assert.All(diffs, d => Assert.Equal(ChangeKind.IdentityUncertain, d.Kind));
    }

    // =================================================================
    //  إثبات الغياب
    // =================================================================

    /// <summary>
    /// 🔴 <b>لقطة ناقصة = الغياب مش مثبت.</b>
    ///
    /// <para>ودي الحالة الطبيعية لما الفني يشغّل الفحص من غير
    /// صلاحيات مسؤول.</para>
    /// </summary>
    [Fact]
    public void A_partial_snapshot_can_never_prove_that_something_was_removed()
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(ComponentType.Storage, "SSD-1")],
            [Strong(ComponentType.Storage, "SSD-9")],
            aPartial: false,
            bPartial: true);

        Assert.DoesNotContain(diffs, d => d.Kind == ChangeKind.Removed);
        Assert.Contains(diffs, d => d.Kind == ChangeKind.NotObserved);
    }

    /// <summary>
    /// 🔴 <b>فئة ماطلّعتش ولا صف = «ماحدش بصّ»، مش «كل حاجة
    /// اتشالت».</b>
    ///
    /// <para>الفرق ده هو الفرق بين تقرير سليم واتهام.</para>
    /// </summary>
    [Fact]
    public void A_category_with_no_rows_on_the_other_side_was_simply_not_examined()
    {
        var d = One([Strong(ComponentType.Battery, "BATT-1")], []);

        Assert.Equal(ChangeKind.NotObserved, d.Kind);
        Assert.Equal("muted", ChangeKindText.Tone(d.Kind));
    }

    /// <summary>
    /// ⚠️ صف الغياب الصريح مش قطعة — دي ملاحظة «المكان ده فاضي».
    /// </summary>
    [Fact]
    public void An_explicit_absence_row_is_reported_as_not_observed()
    {
        var d = One([Absent(ComponentType.Storage)], []);

        Assert.Equal(ChangeKind.NotObserved, d.Kind);
        Assert.Contains("فاضي", d.Explanation);
    }

    /// <summary>
    /// 🔴 <b>الغياب الصريح على الناحية التانية <u>بيثبت</u>
    /// الغياب.</b>
    ///
    /// <para>القارئ كتب «مفيش هارد خالص»، فمجموعة الصفوف الموجودة
    /// فاضية و«اتقرأت بالكامل» بترجّع صح — والهارد اللي كان موجود
    /// بسيريـاله اتشال فعلاً.</para>
    /// </summary>
    [Fact]
    public void An_explicit_absence_on_the_other_side_does_prove_removal()
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(ComponentType.Storage, "SSD-1")],
            [Absent(ComponentType.Storage)]);

        Assert.Contains(diffs, d => d.Kind == ChangeKind.Removed);
    }

    /// <summary>
    /// 🔴 <b>الشبكة والشاشات عمرهم ما ياخدوا «اتشالت».</b>
    ///
    /// <para>القارئ بيستبعد منهم صفوف (كروت وهمية وUSB وشاشات
    /// خارجية)، فاختفاء صف ممكن يكون تغيّر في <b>الفلترة</b> أو في
    /// الدرايفر.</para>
    /// </summary>
    [Theory]
    [InlineData(ComponentType.Network)]
    [InlineData(ComponentType.Display)]
    public void Filtered_categories_never_produce_a_removal(int type)
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(type, "MAC-1"), Strong(type, "MAC-2", index: 1)],
            [Strong(type, "MAC-1")]);

        Assert.DoesNotContain(diffs, d => d.Kind == ChangeKind.Removed);
        Assert.Contains(diffs, d => d.Kind == ChangeKind.IdentityUncertain);
    }

    /// <summary>
    /// ⚠️ وصف ضعيف واحد على الناحية التانية كفاية إنه يمنع الاتهام
    /// — القطعة دي ممكن تكون هي نفسها بقراءة فاشلة.
    /// </summary>
    [Fact]
    public void One_weak_row_on_the_other_side_blocks_the_removal_verdict()
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(ComponentType.Storage, "SSD-1"),
             Strong(ComponentType.Storage, "SSD-2", index: 1)],
            [Strong(ComponentType.Storage, "SSD-1"),
             Weak(ComponentType.Storage, "FP-UNKNOWN", "Some Disk", index: 1)]);

        Assert.DoesNotContain(diffs, d => d.Kind == ChangeKind.Removed);
    }

    // =================================================================
    //  التحذير الكبير والعدّادات
    // =================================================================

    /// <summary>
    /// ⚠️ تغيّر مرساة النظام أو اللوحة الأم بيتعرض بوضوح — <b>من
    /// غير</b> ما نقول إنه جهاز تاني: تبديل بوردة أو صيانة وكيل
    /// بيغيّروا المعرّفات دي بشكل شرعي تماماً.
    /// </summary>
    [Theory]
    [InlineData(ComponentType.System)]
    [InlineData(ComponentType.Motherboard)]
    public void A_change_in_the_identity_anchors_raises_a_warning(int type)
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(type, "UUID-OLD")],
            [Strong(type, "UUID-NEW")]);

        string? warning = SnapshotComparison.MajorIdentityWarning(diffs);

        Assert.NotNull(warning);
        Assert.Contains(ComponentType.Arabic(type), warning);

        // 🔴 ومابيقولش «ده جهاز تاني».
        Assert.Contains("مش معناه تلقائياً", warning);
    }

    /// <summary>
    /// 🔴 <b>و«اتشالت» لوحدها كفاية للتحذير — والمسخ أثبت إن الفحص
    /// اللي فوق مش بيغطّيها.</b>
    ///
    /// <para>الفحص اللي فوق سيريالين مختلفين، فهو بيطلّع «اتشالت»
    /// <b>و</b>«اتضافت» مع بعض — فشيل أي واحدة منهم من مجموعة
    /// التحذير والتانية بتفضل شغّالة والفحص بيعدّي.</para>
    ///
    /// <para>هنا اللوحة الأم اختفت خالص (غياب صريح على الناحية
    /// التانية)، فالنتيجة «اتشالت» لوحدها — ولازم تحذّر.</para>
    /// </summary>
    [Fact]
    public void A_vanished_identity_anchor_warns_on_its_own()
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(ComponentType.Motherboard, "MB-1")],
            [Absent(ComponentType.Motherboard)]);

        /*
          ⚠️ وصف الغياب الصريح بيطلّع صفّه الخاص («المكان ده
          فاضي») جمب الحكم — فالفحص بيأكّد إن **«اتشالت» موجودة
          و«اتضافت» مش موجودة**، مش إن فيه صف واحد.
        */
        Assert.Contains(ChangeKind.Removed, diffs.Select(d => d.Kind));
        Assert.DoesNotContain(ChangeKind.Added, diffs.Select(d => d.Kind));
        Assert.DoesNotContain(ChangeKind.SerialChanged, diffs.Select(d => d.Kind));

        Assert.NotNull(SnapshotComparison.MajorIdentityWarning(diffs));
    }

    /// <summary>
    /// ⚠️ ومرساة <b>جديدة</b> ظهرت لوحدها كفاية كمان.
    /// </summary>
    [Fact]
    public void A_new_identity_anchor_warns_on_its_own()
    {
        var diffs = SnapshotComparison.Compare(
            [Absent(ComponentType.System)],
            [Strong(ComponentType.System, "UUID-NEW")]);

        Assert.Contains(ChangeKind.Added, diffs.Select(d => d.Kind));
        Assert.DoesNotContain(ChangeKind.Removed, diffs.Select(d => d.Kind));
        Assert.DoesNotContain(ChangeKind.SerialChanged, diffs.Select(d => d.Kind));

        Assert.NotNull(SnapshotComparison.MajorIdentityWarning(diffs));
    }

    [Fact]
    public void A_storage_change_alone_raises_no_identity_warning()
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(ComponentType.Storage, "SSD-OLD")],
            [Strong(ComponentType.Storage, "SSD-NEW")]);

        Assert.Contains(diffs, d => d.Kind == ChangeKind.Removed);
        Assert.Null(SnapshotComparison.MajorIdentityWarning(diffs));
    }

    /// <summary>
    /// 🔴 <b>«مش مؤكّدة» ليها خانة لوحدها — وكل الشرح اللي فوق
    /// بيعتمد على ده.</b>
    ///
    /// <para>ضمّها لـ«اتغيّرت» بيخلّي الرقم يشيل حالات إحنا صراحةً مش
    /// قادرين نثبتها.</para>
    /// </summary>
    [Fact]
    public void Uncertain_rows_are_counted_apart_from_changes_and_from_unchanged()
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(ComponentType.Storage, "SSD-1"),
             Weak(ComponentType.Gpu, "FP-A", "UHD 620")],
            [Strong(ComponentType.Storage, "SSD-1"),
             Weak(ComponentType.Gpu, "FP-B", "UHD 620")]);

        int uncertain = diffs.Count(d => d.Kind is ChangeKind.IdentityUncertain
                                              or ChangeKind.NotObserved);

        int changed = diffs.Count(d => d.Kind is ChangeKind.SerialChanged
                                            or ChangeKind.ModelChanged
                                            or ChangeKind.ConfigurationDifferent);

        Assert.Equal(1, diffs.Count(d => d.Kind == ChangeKind.Unchanged));
        Assert.Equal(2, uncertain);
        Assert.Equal(0, changed);
    }

    /// <summary>⚠️ الفئات بترجع مرتّبة بالرقم — الشاشة بتعتمد عليه.</summary>
    [Fact]
    public void Rows_come_back_ordered_by_component_type()
    {
        var diffs = SnapshotComparison.Compare(
            [Strong(ComponentType.Network, "MAC"), Strong(ComponentType.System, "UUID")],
            [Strong(ComponentType.Network, "MAC"), Strong(ComponentType.System, "UUID")]);

        Assert.Equal(
            [ComponentType.System, ComponentType.Network],
            diffs.Select(d => d.Type));
    }

    /// <summary>
    /// ⚠️ لقطتين فاضيتين = مفيش صفوف. ومفيش استثناء ولا صف
    /// «مفيش بيانات» — الشاشة هي اللي بتقول كده.
    /// </summary>
    [Fact]
    public void Two_empty_snapshots_produce_no_rows()
    {
        Assert.Empty(SnapshotComparison.Compare([], []));
        Assert.Null(SnapshotComparison.MajorIdentityWarning([]));
    }

    // =================================================================
    //  نصوص العرض
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفرق المؤكّد بس هو اللي بياخد أحمر.</b>
    /// </summary>
    [Theory]
    [InlineData(ChangeKind.Unchanged, "good")]
    [InlineData(ChangeKind.Added, "bad")]
    [InlineData(ChangeKind.Removed, "bad")]
    [InlineData(ChangeKind.SerialChanged, "bad")]
    [InlineData(ChangeKind.ModelChanged, "info")]
    [InlineData(ChangeKind.ConfigurationDifferent, "info")]
    [InlineData(ChangeKind.IdentityUncertain, "muted")]
    [InlineData(ChangeKind.NotObserved, "muted")]
    public void Only_a_proven_difference_is_shown_in_red(ChangeKind kind, string tone) =>
        Assert.Equal(tone, ChangeKindText.Tone(kind));

    /// <summary>
    /// ⚠️ كل قيمة ليها نص عربي — ومفيش ولا واحدة بترجّع اسمها
    /// الإنجليزي.
    /// </summary>
    [Fact]
    public void Every_change_kind_has_an_arabic_name()
    {
        foreach (var kind in Enum.GetValues<ChangeKind>())
        {
            string text = ChangeKindText.Arabic(kind);

            Assert.NotEqual(kind.ToString(), text);
            Assert.NotEmpty(text);
        }
    }

    /// <summary>
    /// 🔴 أرقام الفئات عقد — الراكة كاتبتها في صفوف الإنتاج.
    /// </summary>
    [Fact]
    public void Component_type_numbers_are_frozen()
    {
        Assert.Equal(0, ComponentType.System);
        Assert.Equal(1, ComponentType.Motherboard);
        Assert.Equal(2, ComponentType.Bios);
        Assert.Equal(3, ComponentType.Cpu);
        Assert.Equal(4, ComponentType.Memory);
        Assert.Equal(5, ComponentType.Storage);
        Assert.Equal(6, ComponentType.Battery);
        Assert.Equal(7, ComponentType.Display);
        Assert.Equal(8, ComponentType.Gpu);
        Assert.Equal(9, ComponentType.Network);
        Assert.Equal(10, ComponentType.Keyboard);
        Assert.Equal(11, ComponentType.Touchpad);
        Assert.Equal(12, ComponentType.Camera);
        Assert.Equal(13, ComponentType.Audio);
    }

    /// <summary>
    /// ⚠️ فئة من نسخة راكة أحدث بتبان برقمها بدل ما الصفحة توقع.
    /// </summary>
    [Fact]
    public void An_unknown_component_type_falls_back_to_its_number()
    {
        Assert.Equal("نوع 99", ComponentType.Arabic(99));

        // ⚠️ والقيم التقنية مابتتترجمش.
        Assert.Equal("BIOS", ComponentType.Arabic(ComponentType.Bios));
    }

    [Theory]
    [InlineData(1, "أ — سيريال حقيقي")]
    [InlineData(2, "ب — بصمة تحدّد الموديل")]
    [InlineData(3, "ج — مسار جهاز")]
    [InlineData(0, "مفيش")]
    [InlineData(99, "مفيش")]
    public void Confidence_has_one_set_of_names(int confidence, string text) =>
        Assert.Equal(text, IdentityConfidenceText.Arabic(confidence));

    // =================================================================
    //  نتايج المراحل
    // =================================================================

    /// <summary>
    /// 🔴 <b>الترتيب مش رقم الحالة.</b>
    ///
    /// <para>الترقيم المخزّن اعتباطي، فالمقارنة الرقمية المباشرة
    /// بتقول إن «مش موجود» (٤) أسوأ من «فيه مشكلة» (٢) — والشاشة
    /// بتقول «الجهاز باظ» وهو في الحقيقة سليم.</para>
    /// </summary>
    [Fact]
    public void Not_present_ranks_better_than_failed()
    {
        Assert.True(StepOutcome.Rank(4) > StepOutcome.Rank(2));

        // وشغال أحسن حاجة، وفيه مشكلة أسوأ حاجة.
        Assert.Equal(
            [1, 4, 0, 3, 5, 2],
            new[] { 0, 1, 2, 3, 4, 5 }.OrderByDescending(StepOutcome.Rank));
    }

    [Theory]
    [InlineData(2, 1, "Improved")]
    [InlineData(1, 2, "Regressed")]
    [InlineData(1, 1, "Same")]
    [InlineData(2, 4, "Improved")]
    public void Direction_is_read_from_the_rank_not_the_number(
        int left, int right, string expected) =>
        Assert.Equal(expected, StepOutcome.Direction(left, right));

    /// <summary>
    /// ⚠️ مرحلة موجودة في جهة واحدة بس — مرحلة اتضافت أو اتشالت من
    /// نسخة البرنامج، مش نتيجة اتغيّرت.
    /// </summary>
    [Fact]
    public void A_step_present_on_one_side_only_says_so()
    {
        Assert.Equal("OnlyRight", StepOutcome.Direction(null, 1));
        Assert.Equal("OnlyLeft", StepOutcome.Direction(1, null));
    }

    [Fact]
    public void An_unknown_step_status_still_has_a_name()
    {
        Assert.Equal("لم يُنفّذ", StepOutcome.Arabic(99));
        Assert.Equal(3, StepOutcome.Rank(99));
    }
}
