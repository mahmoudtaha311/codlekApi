using Codlek.Core.Devices;
using Codlek.Core.Entities;
using Codlek.Core.Hardware;
using Codlek.Core.Sync;

namespace Codlek.Tests;

/// <summary>
/// القواعد النقية في الاستقبال — <b>الحتة اللي التحوير المقصود كشف
/// إنها مش مقاسة</b>.
///
/// <para>🔴 <b>والملف ده كله اتكتب بعد ٨ تحويرات نجت.</b> الفحوص
/// الأصلية كانت بتقيس الحاجة الصح بس بطريقة بتعدّي من تحتها: ثابت
/// بيتقارن بنفسه، حارسين بيغطّوا على بعض، وملخّص طويل كل كلماته
/// مكرّرة فالقص مابيحصلش. التحوير هو اللي قال، مش القراية.</para>
/// </summary>
public class IngestCoreRuleTests
{
    // =================================================================
    //  أرقام الحالة على السلك
    // =================================================================

    /// <summary>
    /// 🔴 <b>الأرقام دي على السلك — والفحص لازم يثبّتها
    /// <u>بالحرف</u>.</b>
    ///
    /// <para>الراكة بتبعت <c>status</c> كرقم. والفحص القديم كان
    /// بيستعمل <c>ReportIngestRules.StepPass</c> في الحمولة
    /// <b>وفي المقارنة</b> — فتغيير الثابت كان بيغيّر الطرفين
    /// والفحص يعدّي. تحوير <c>StepPass = 9</c> نجا فعلاً.</para>
    ///
    /// <para>⚠️ وتغييرهم معناه إن <b>كل عدّاد في كل تقرير قديم</b>
    /// يبقى غلط — والعدّادات دي بيتبني عليها تقييم الفني.</para>
    /// </summary>
    [Fact]
    public void The_step_status_numbers_are_frozen()
    {
        Assert.Equal(1, ReportIngestRules.StepPass);
        Assert.Equal(2, ReportIngestRules.StepFail);
        Assert.Equal(3, ReportIngestRules.StepSkip);
        Assert.Equal(4, ReportIngestRules.StepNotPresent);
        Assert.Equal(5, ReportIngestRules.StepError);
    }

    /// <summary>
    /// ⚠️ <b>و«ماتفحصتش» صفر — مش رقم سادس.</b> الراكة مابتبعتش رقم
    /// للمرحلة اللي محدّش لمسها؛ هي بتبعت <c>0</c>، والسيرفر بيعدّها.
    /// </summary>
    [Fact]
    public void Not_run_is_the_absence_of_a_status()
    {
        Assert.Equal(
            2,
            ReportIngestRules.NotRun(
            [
                (true, 0),
                (true, 0),
                (false, 0),
                (true, 1),
            ]));
    }

    // =================================================================
    //  كود المصنّع
    // =================================================================

    /// <summary>
    /// 🔴 <b>والشرطة السفلية شرط — مع رقم، وبطول معقول.</b>
    ///
    /// <para>من غير الشروط التلاتة، الفحص بياخد كلمات شرعية زي
    /// <c>G8</c> و<c>15ARH05</c> و<c>X1</c> — ودي أجزاء حقيقية من
    /// أسامي لابات، <b>ورفضها بيخرّب أسامي صح</b>.</para>
    ///
    /// <para>⚠️ والفحص القديم كان بيجرّب حالة واحدة
    /// (<c>103C_5336AN</c>) — وهي بتعدّي كل الشروط، فشيل أي شرط
    /// مانزلش النتيجة. تلات تحويرات نجت.</para>
    /// </summary>
    [Theory]
    // ✅ كود مصنّع حقيقي.
    [InlineData("103C_5336AN", true)]
    [InlineData("1028_0A1B", true)]

    // ❌ قصير — وده اللي بيحمي «G8».
    [InlineData("G8", false)]
    [InlineData("A_1", false)]

    // ❌ من غير شرطة سفلية — وده اللي بيحمي «15ARH05».
    [InlineData("15ARH05", false)]
    [InlineData("ELITEBOOK", false)]

    // ❌ من غير رقم.
    [InlineData("ABCD_EFGH", false)]

    // ❌ الشرطة في الأول أو الآخر.
    [InlineData("_5336AN", false)]
    [InlineData("103C5336_", false)]

    // ❌ طويل أوي.
    [InlineData("103C_5336AN103C_5336AN103C", false)]

    // ❌ فيه حروف مش مسموحة.
    [InlineData("103C_5336-AN", false)]
    [InlineData("103C 5336_AN", false)]
    public void An_oem_code_token_needs_all_three_conditions(string word, bool expected)
    {
        Assert.Equal(expected, DeviceNaming.IsOemCodeToken(word));
    }

    /// <summary>
    /// 🔴 <b>والكلمة الواحدة اللي كلها كود <u>برضه</u> مش اسم.</b>
    ///
    /// <para>ودي الحالة اللي تحوير «الكلمة الواحدة بتعدّي» نجا
    /// فيها: <c>IsOemCodeName</c> المفروض ترفض
    /// <c>103C_5336AN</c> لوحدها، مش بس
    /// <c>103C_5336AN HP EliteBook</c>.</para>
    /// </summary>
    [Theory]
    [InlineData("103C_5336AN HP EliteBook", true)]
    [InlineData("103C_5336AN", true)]
    [InlineData("HP EliteBook 835 G8", false)]
    [InlineData("Legion 5 15ARH05", false)]
    [InlineData("", false)]
    public void A_name_that_starts_with_a_code_is_not_a_name(string name, bool expected)
    {
        Assert.Equal(expected, DeviceNaming.IsOemCodeName(name));
    }

    /// <summary>
    /// 🔴 <b>حارس التنضيف = <c>StartsWithOemCode</c> بتاعة القديم بالحرف</b>
    /// — بيشترط مسافة، فالكلمة الواحدة اللي كلها كود <b>مابتتمسحش</b> (عكس
    /// <c>IsOemCodeName</c>). التنضيف بيمسح قيم موجودة على الإنتاج،
    /// وتوسيعه كان هيمسح حاجات القديم سابها.
    /// </summary>
    [Theory]
    [InlineData("103C_5336AN HP EliteBook", true)]
    [InlineData("  103C_5336AN HP EliteBook  ", true)]
    [InlineData("103C_5336AN", false)]
    [InlineData(" HP 103C_5336AN", false)]
    [InlineData("EliteBook 840 G8", false)]
    [InlineData("Legion 5 15ARH05", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void The_cleanup_guard_needs_a_code_followed_by_a_space_like_legacy(string? name, bool expected)
    {
        Assert.Equal(expected, DeviceNaming.StartsWithOemCode(name));
    }

    // =================================================================
    //  مصادر الاسم التجاري
    // =================================================================

    /// <summary>
    /// 🔴 <b>الأربع مصادر دي أسامي بتكتبها <u>الراكة</u> — والقايمة
    /// عقد.</b>
    ///
    /// <para>⚠️ وتحوير شال <c>SystemFamily</c> من القايمة
    /// <b>ونجا</b>، لأن فحوصي كانت بتستعمل التلاتة التانيين بس.
    /// وشيله كان هيكسّر <b>لينوفو</b>: عيلتهم اسم حقيقي
    /// (<c>Legion</c>)، مش كود.</para>
    /// </summary>
    [Theory]
    [InlineData("SMBIOS (Product Version)")]
    [InlineData("SystemFamily")]
    [InlineData("Model")]
    [InlineData("SKU")]
    public void Every_rack_written_source_is_trusted(string source)
    {
        Assert.True(CommercialModelEvidence.IsTrusted(source));
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("إدخال يدوي")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("SystemFamily2")]
    public void Anything_else_is_not_trusted(string? source)
    {
        Assert.False(CommercialModelEvidence.IsTrusted(source));
    }

    /// <summary>⚠️ والمقارنة بتتجاهل الحالة والمسافات على الطرفين.</summary>
    [Theory]
    [InlineData("  systemfamily  ")]
    [InlineData("SKU")]
    [InlineData("sku")]
    public void The_source_comparison_is_forgiving(string source)
    {
        Assert.True(CommercialModelEvidence.IsTrusted(source));
    }

    /// <summary>
    /// 🔴 <b>ولينوفو بتعدّي من <c>SystemFamily</c>، وHP لأ — والفرق
    /// في <u>القيمة</u> مش في المصدر.</b>
    /// </summary>
    [Fact]
    public void The_same_source_accepts_lenovo_and_refuses_hp()
    {
        Assert.True(CommercialModelEvidence.IsTrusted("SystemFamily"));

        Assert.True(CommercialModelEvidence.IsUsableName("Legion 5 15ARH05"));
        Assert.False(CommercialModelEvidence.IsUsableName("103C_5336AN HP EliteBook"));
    }

    /// <summary>⚠️ والاختيار بياخد أول صف <b>موثوق وصالح</b>.</summary>
    [Fact]
    public void The_pick_skips_untrusted_and_unusable_rows()
    {
        int index = CommercialModelEvidence.Pick(
            sources: ["Manual", "SystemFamily", "SKU"],
            names: ["اسم مشكوك", "103C_5336AN HP", "Legion 5"]);

        Assert.Equal(2, index);
    }

    [Fact]
    public void The_pick_gives_up_when_nothing_is_usable()
    {
        Assert.Equal(
            -1,
            CommercialModelEvidence.Pick(
                sources: ["Manual", "SystemFamily"],
                names: ["اسم", "103C_5336AN HP"]));
    }

    // =================================================================
    //  «قطعة اتغيّرت» — الحارسين لازم يتقاسوا كل واحد لوحده
    // =================================================================

    private static ReportSnapshotComponent Part(
        int type, string serial, bool present = true, int confidence = 1) =>
        new()
        {
            Type = type,
            ManufacturerSerial = serial,
            HardwareFingerprint = serial,
            IsPresent = present,
            IdentityConfidence = confidence,
        };

    /// <summary>
    /// 🔴 <b>اللقطة الناقصة عمرها ما تتهم — والفحص ده بيقيسها
    /// لوحدها.</b>
    ///
    /// <para>⚠️ والفحص القديم كان بيبعت ناحية <b>فاضية</b> مع العلم،
    /// فحارس «الفاضي» كان بيلقطها وحارس «الناقص» مابيتقاسش —
    /// والحارسين كانوا بيغطّوا على بعض. التحويرين الاتنين نجوا.
    /// هنا الناحيتين <b>مليانين</b>، فالعلم هو الحاجة الوحيدة اللي
    /// بتفرق.</para>
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void A_partial_snapshot_alone_stops_the_verdict(
        bool previousPartial, bool currentPartial)
    {
        var before = new[] { Part(ComponentType.Storage, "DISK-A") };

        var after = new[]
        {
            Part(ComponentType.Storage, "", present: false),
            Part(ComponentType.Memory, "RAM-1"),
        };

        Assert.False(
            PartChangeDetector.Compare(before, after, previousPartial, currentPartial)
                .Changed);

        // 🔴 وبنفس المدخلات من غير العلم — التحذير بينزل. وده اللي
        //    بيخلّي الفحص اللي فوق يقيس العلم فعلاً.
        Assert.True(PartChangeDetector.Compare(before, after, false, false).Changed);
    }

    /// <summary>
    /// ⚠️ <b>والناحية الفاضية مفيش فيها مقارنة — بس الحارس ده
    /// <u>زيادة فعلاً</u>.</b>
    ///
    /// <para>«مفيش لقطة على ناحية» معناها مفيش مقارنة، مش «اتشال كل
    /// حاجة» — والفحص ده بيثبّت السلوك ده من بره.</para>
    ///
    /// <para>🔴 <b>بس التحوير قال إن الحارس نفسه مالوش لزوم:</b>
    /// شيل الشرط من <c>PartChangeDetector</c> والفحص <b>يعدّي</b>،
    /// لأن <c>SnapshotComparison</c> بيقف لوحده (الفئة اللي مافيهاش
    /// صفوف في الناحية التانية بتدّي <c>NotObserved</c> مش
    /// <c>Removed</c>).</para>
    ///
    /// <para>⚠️ <b>وسايبينه بردو</b> — زي القديم: الحكم يبقى صريح
    /// هنا مش نتيجة جانبية لملف تاني. والفرق المهم إن حارس <b>اللقطة
    /// الناقصة</b> اللي فوقه <u>مش</u> زيادة: شيله بيخلّي لقطة ناقصة
    /// تطلّع <c>Removed</c> فعلاً — قسناها. فالتعليق اللي بيقول إن
    /// الاتنين «مجرد وضوح» غلط في واحد منهم.</para>
    /// </summary>
    [Fact]
    public void An_empty_side_alone_stops_the_verdict()
    {
        var full = new[] { Part(ComponentType.Storage, "DISK-A") };
        var none = Array.Empty<ReportSnapshotComponent>();

        Assert.False(PartChangeDetector.Compare(full, none, false, false).Changed);
        Assert.False(PartChangeDetector.Compare(none, full, false, false).Changed);
        Assert.False(PartChangeDetector.Compare(none, none, false, false).Changed);
    }

    /// <summary>
    /// 🔴 <b>واختلاف الموديل <u>مش</u> تبديل قطعة.</b>
    ///
    /// <para>المقارنة نفسها بتقول إنه «غالباً فرق في القراءة أو
    /// الفيرموير». والتحذير المبني على «مقدرناش نتأكد»
    /// <b>اتهام</b>.</para>
    ///
    /// <para>⚠️ وتحوير ضمّ <c>ModelChanged</c> لأنواع التبديل
    /// <b>ونجا</b> — لأن فحوصي كلها كانت على قطعة <b>اتشالت</b>.</para>
    /// </summary>
    [Fact]
    public void A_model_only_difference_is_not_a_part_change()
    {
        // نفس السيريال، موديل مختلف — هوية قوية على الطرفين.
        var before = new[]
        {
            new ReportSnapshotComponent
            {
                Type = ComponentType.Storage,
                ManufacturerSerial = "DISK-A",
                Model = "SK hynix BC501",
                IsPresent = true,
                IdentityConfidence = 1,
            },
        };

        var after = new[]
        {
            new ReportSnapshotComponent
            {
                Type = ComponentType.Storage,
                ManufacturerSerial = "DISK-A",
                Model = "SK hynix BC501 HFM256",
                IsPresent = true,
                IdentityConfidence = 1,
            },
        };

        var verdict = PartChangeDetector.Compare(before, after, false, false);

        Assert.False(verdict.Changed);
        Assert.Equal("", verdict.Summary);
    }

    /// <summary>
    /// ⚠️ <b>والملخّص بيتقص فعلاً — بأسامي <u>مختلفة</u>.</b>
    ///
    /// <para>والفحص القديم كان بيعمل ٤٠٠ فرق بأنواع من ٠ لـ٣٩٩ —
    /// وكلهم بيترجموا «قطعة»، و<c>Distinct</c> بتلمّهم في كلمة
    /// واحدة. فالنص كان بيطلع قصير والقص مابيحصلش. التحوير نجا.</para>
    /// </summary>
    [Fact]
    public void A_long_summary_is_clipped_with_an_ellipsis()
    {
        var kinds = Enum.GetValues<ChangeKind>();

        int[] types =
        [
            ComponentType.Cpu, ComponentType.Memory, ComponentType.Storage,
            ComponentType.Battery, ComponentType.Display, ComponentType.Gpu,
            ComponentType.Network, 99,
        ];

        var diffs = kinds
            .SelectMany(kind => types.Select(type => new ComponentDiff
            {
                Type = type,
                Kind = kind,
            }))
            .ToList();

        string text = PartChangeDetector.Describe(diffs);

        // 🔴 المدخلات دي بتطلّع نص أطول من الحد فعلاً.
        Assert.True(
            diffs.Count > 0,
            "الفحص محتاج فروقات — من غيرها مش بيقيس القص.");

        /*
          ⚠️ **٢٩٨ مش ٣٠٠ — والرقم ده من القديم بالحرف.**

          القص `text[..(MaxSummary - 3)] + "…"`، والـ`- 3` مفترضة
          علامة قص من تلات حروف (`...`) والمستعمل حرف واحد (`…`).
          فالناتج ٢٩٨: جوّه العمود بفارق حرفين.

          🔴 **ومابنصلّحهاش.** تعديلها لـ`- 1` بيغيّر النص
          المتخزّن لأي ملخّص طويل، فالصفوف القديمة والجديدة يبقوا
          شكلين — والمكسب حرفين.
        */
        Assert.Equal(PartChangeDetector.MaxSummary - 2, text.Length);

        Assert.True(
            text.Length <= PartChangeDetector.MaxSummary,
            "الملخّص عدّى طول العمود.");

        Assert.EndsWith("…", text, StringComparison.Ordinal);
    }

    /// <summary>⚠️ والملخّص القصير مابيتلمسش.</summary>
    [Fact]
    public void A_short_summary_keeps_its_shape()
    {
        string text = PartChangeDetector.Describe(
        [
            new ComponentDiff { Type = ComponentType.Storage, Kind = ChangeKind.Removed },
            new ComponentDiff { Type = ComponentType.Memory, Kind = ChangeKind.Added },
        ]);

        Assert.DoesNotContain("…", text);
        Assert.Contains("اتشال: الهارد", text);
        Assert.Contains("اتضاف: الرام", text);

        // ⚠️ والفاصل بين المجموعات ثابت — بيتعرض في أيقونة واحدة.
        Assert.Contains(" · ", text);
    }

    // =================================================================
    //  علم اللقطة الناقصة على الصف
    // =================================================================

    /// <summary>
    /// 🔴 <b>وعلم «اللقطة ناقصة» لازم يوصل الصف زي ما جه.</b>
    ///
    /// <para>العلم ده هو اللي بيمنع التحذير الكذّاب: لقطة اتجمعت من
    /// غير صلاحيات مسؤول غيابها <b>مايتقاسش عليه</b>. لو اتضيّع،
    /// الجهاز بياخد «قطعة اتغيّرت» على قراية فشلت — <b>اتهام على
    /// موظف</b>.</para>
    ///
    /// <para>⚠️ والفحص القديم كان بيبعت <c>IsPartial = false</c>
    /// ويتأكد إنها <c>false</c> — فتثبيتها على <c>false</c> في الكود
    /// عدّى من تحته. تحوير نجا.</para>
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_partial_flag_reaches_the_row_as_sent(bool partial)
    {
        var dto = new Application.Contracts.Sync.LaptopReportPayload
        {
            Id = Guid.NewGuid(),
            StartedAtUtc = new DateTime(2026, 10, 4, 9, 0, 0),
            Specs = new Application.Contracts.Sync.DeviceSpecsPayload(),
            Snapshot = new Application.Contracts.Sync.HardwareSnapshotPayload
            {
                CapturedAtUtc = new DateTime(2026, 10, 4, 8, 0, 0),
                IsPartial = partial,
                RanAsAdministrator = !partial,
            },
        };

        var row = new Report { Id = dto.Id, TenantId = Guid.NewGuid() };

        Application.Features.Rack.IngestReports.ReportRowWriter.Apply(
            row, dto, "{}", null, default, row.TenantId, null);

        Assert.Equal(partial, row.SnapshotIsPartial);
        Assert.Equal(!partial, row.SnapshotRanAsAdministrator);
    }

    /// <summary>
    /// ⚠️ <b>ومفيش لقطة = مش ناقصة.</b> «الراكة ماجمعتش لقطة»
    /// و«جمعت وطلعت ناقصة» حاجتين مختلفتين، والتانية بس هي اللي
    /// بتمنع المقارنة.
    /// </summary>
    [Fact]
    public void No_snapshot_is_not_a_partial_snapshot()
    {
        var dto = new Application.Contracts.Sync.LaptopReportPayload
        {
            Id = Guid.NewGuid(),
            StartedAtUtc = new DateTime(2026, 10, 4, 9, 0, 0),
            Specs = new Application.Contracts.Sync.DeviceSpecsPayload(),
        };

        var row = new Report { Id = dto.Id, TenantId = Guid.NewGuid() };

        Application.Features.Rack.IngestReports.ReportRowWriter.Apply(
            row, dto, "{}", null, default, row.TenantId, null);

        Assert.False(row.SnapshotIsPartial);
        Assert.Null(row.SnapshotCapturedAtUtc);
    }

    // =================================================================
    //  حالة المسح — الأحدث يكسب، والتعادل للصف
    // =================================================================

    private static readonly DateTime Early = new(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Late = new(2025, 1, 12, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 🔴 <b>المسح من الموقع أحدث من آخر قرار على الراكة ← الصف
    /// يفضل.</b> ودي الحالة اللي كانت بتضيّع مسح المدير.
    /// </summary>
    [Fact]
    public void A_newer_stored_decision_keeps_the_row()
    {
        // الصف اتمسح من الموقع متأخر، والراكة رجّعته بدري.
        Assert.False(ReportIngestRules.RackDeletionWins(Late, null, null, Early));

        // ⚠️ والوقت المتخزّن ممكن يبقى وقت استرجاع كمان.
        Assert.False(ReportIngestRules.RackDeletionWins(Early, Late, Early, null));
    }

    [Fact]
    public void A_newer_rack_decision_wins()
    {
        Assert.True(ReportIngestRules.RackDeletionWins(Early, null, Late, null));

        // ⚠️ الأحدث بين الاتنين هو اللي بيتقارن — مش وقت المسح لوحده.
        Assert.True(ReportIngestRules.RackDeletionWins(Early, null, Early, Late));
    }

    /// <summary>
    /// ⚠️ <b>الصف مالوش قرار ← الراكة تكسب حتى لو هي كمان مالهاش.</b>
    /// ودي حالة الإضافة: حالة الراكة بتتكتب زي الأول.
    /// </summary>
    [Fact]
    public void No_stored_decision_takes_the_rack_state()
    {
        Assert.True(ReportIngestRules.RackDeletionWins(null, null, Late, null));
        Assert.True(ReportIngestRules.RackDeletionWins(null, null, null, null));
    }

    /// <summary>
    /// 🔴 <b>الراكة مالهاش قرار ← مابتمسحش قرار الموقع.</b> «مش
    /// ممسوح» من غير وقت معناها «محدّش قرّر»، مش «اترجع».
    /// </summary>
    [Fact]
    public void No_rack_decision_never_overrides_a_stored_one()
    {
        Assert.False(ReportIngestRules.RackDeletionWins(Early, null, null, null));
        Assert.False(ReportIngestRules.RackDeletionWins(null, Early, null, null));
    }

    /// <summary>⚠️ <b>التعادل للصف</b> — نفس الوقت غالباً نفس القرار راجع.</summary>
    [Fact]
    public void Equal_times_keep_the_row()
    {
        Assert.False(ReportIngestRules.RackDeletionWins(Late, null, Late, null));
        Assert.False(ReportIngestRules.RackDeletionWins(Early, Late, null, Late));
    }

    [Fact]
    public void The_decision_time_is_the_later_of_delete_and_restore()
    {
        Assert.Null(ReportIngestRules.DeletionDecisionAt(null, null));
        Assert.Equal(Early, ReportIngestRules.DeletionDecisionAt(Early, null));
        Assert.Equal(Early, ReportIngestRules.DeletionDecisionAt(null, Early));
        Assert.Equal(Late, ReportIngestRules.DeletionDecisionAt(Early, Late));
        Assert.Equal(Late, ReportIngestRules.DeletionDecisionAt(Late, Early));
    }
}
