using Codlek.Application.Features.Repairs;
using Codlek.Application.Features.Repairs.ApproveRepair;
using Codlek.Application.Features.Repairs.GetRepairs;
using Codlek.Application.Features.Repairs.RejectRepair;
using Codlek.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// قرار المحاسب — الموافقة والرفض.
///
/// <para>🔴 <b>الميزة دي موجودة عشان تراجع المدير.</b> فأي ثقب فيها
/// معناه إن المراجعة <b>شكل</b>: الأمر بيبدأ، والسجل بيقول إن
/// الموافقة اتخدت، والمحاسب مادوسش حاجة.</para>
/// </summary>
public class RepairDecisionTests
{
    private static (ApproveRepairCommandHandler Approve,
                    RejectRepairCommandHandler Reject,
                    FakeRepairRepository Repo,
                    FakeAuditTrail Audit,
                    FakeUnitOfWork Work,
                    FakeCurrentUser Me) Build()
    {
        var repo = new FakeRepairRepository();
        var workflow = new FakeWorkflowRecorder();
        var audit = new FakeAuditTrail();
        var work = new FakeUnitOfWork();
        var me = new FakeCurrentUser(UserRole.Accountant);

        var transitions = new RepairTransitions(
            repo, workflow, NullLogger<RepairTransitions>.Instance);

        return (
            new ApproveRepairCommandHandler(
                repo, transitions, audit, work, me,
                NullLogger<ApproveRepairCommandHandler>.Instance),
            new RejectRepairCommandHandler(
                repo, audit, work, me, NullLogger<RejectRepairCommandHandler>.Instance),
            repo, audit, work, me);
    }

    // =================================================================
    //  الموافقة
    // =================================================================

    [Fact]
    public async Task Approving_stamps_the_decision_and_saves_once()
    {
        var (approve, _, repo, audit, work, me) = Build();

        var device = RepairFixtures.Device(me.TenantId);
        var item = RepairFixtures.Item(me.TenantId, device.Id);

        repo.Devices.Add(device);
        repo.Items.Add(item);

        var result = await approve.Handle(
            new ApproveRepairCommand(item.Id, null, "  تمام  "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("اتوافق عليه.", result.Value.Message);

        Assert.Equal(RepairApproval.Approved, item.Approval);
        Assert.Equal(me.Id, item.ApprovedByUserId);
        Assert.Equal("كريم", item.ApprovedByName);
        Assert.NotNull(item.ApprovalDecidedAtUtc);

        // ⚠️ الملاحظة بتتشال مساحاتها — الشاشة بتعرضها زي ما هي.
        Assert.Equal("تمام", item.ApprovalNote);

        // 🔴 حفظة واحدة: الموافقة والإسناد والسجل.
        Assert.Equal(1, work.Saves);

        var line = Assert.Single(audit.Lines);
        Assert.Equal("repair.approved", line.Action);
        Assert.Equal("repair", line.EntityType);
        Assert.Equal(item.PublicCode, line.Code);
    }

    /// <summary>
    /// 🔴 <b>محاسبين على شاشتين بيدوسوا في نفس اللحظة.</b>
    ///
    /// <para>من غير إعادة التأكد من «لسه معلّق»، التاني بيدهس قرار
    /// الأول ويكتب اسمه مكانه — والسجل بيقول إن الموافقة اتخدت
    /// مرتين من ناس مختلفة.</para>
    /// </summary>
    [Theory]
    [InlineData(RepairApproval.Approved)]
    [InlineData(RepairApproval.Rejected)]
    public async Task A_decided_order_cannot_be_decided_again(RepairApproval already)
    {
        var (approve, reject, repo, audit, work, me) = Build();

        var device = RepairFixtures.Device(me.TenantId);

        var a = RepairFixtures.Item(me.TenantId, device.Id, approval: already);
        var b = RepairFixtures.Item(me.TenantId, device.Id, approval: already);
        a.ApprovedByName = "محاسب تاني";
        b.ApprovedByName = "محاسب تاني";

        repo.Devices.Add(device);
        repo.Items.Add(a);
        repo.Items.Add(b);

        var onApprove = await approve.Handle(new ApproveRepairCommand(a.Id, null, null), default);
        var onReject = await reject.Handle(new RejectRepairCommand(b.Id, "سبب كافي"), default);

        Assert.True(onApprove.IsFailure);
        Assert.True(onReject.IsFailure);
        Assert.Equal("repair.already_decided", onApprove.Error.Code);
        Assert.Equal("repair.already_decided", onReject.Error.Code);

        // اسم المحاسب الأصلي مااتدهسش.
        Assert.Equal("محاسب تاني", a.ApprovedByName);
        Assert.Equal("محاسب تاني", b.ApprovedByName);

        // ولا سطر سجل ولا حفظة.
        Assert.Empty(audit.Lines);
        Assert.Equal(0, work.Saves);
    }

    /// <summary>
    /// 🔴 <b>تغيير الفني بيحصل الأول — عشان الفشل يسيب الأمر
    /// معلّق.</b>
    ///
    /// <para>لو الموافقة اتكتبت قبل الإسناد وفشل الإسناد، الأمر بيبقى
    /// <b>موافَق عليه ومسنود غلط</b> — والفني اللي مش من حقه بيبدأ
    /// شغل بسلطة موافقة حقيقية.</para>
    /// </summary>
    [Fact]
    public async Task A_failed_technician_change_leaves_the_order_pending()
    {
        var (approve, _, repo, audit, work, me) = Build();

        var device = RepairFixtures.Device(me.TenantId);
        var item = RepairFixtures.Item(me.TenantId, device.Id);

        repo.Devices.Add(device);
        repo.Items.Add(item);

        // فني موقوف — الإسناد بيترفض.
        var suspended = RepairFixtures.Technician(me.TenantId, isActive: false);
        repo.Technicians.Add(suspended);

        var result = await approve.Handle(
            new ApproveRepairCommand(item.Id, suspended.Id, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("repair.technician_suspended", result.Error.Code);

        // 🔴 الأمر فضل معلّق — مش «موافَق عليه ومسنود غلط».
        Assert.Equal(RepairApproval.Pending, item.Approval);
        Assert.Null(item.AssignedTechnicianId);
        Assert.Empty(audit.Lines);
        Assert.Equal(0, work.Saves);
    }

    /// <summary>
    /// 🔴 <b>المحاسب بيعدّي قيد الماركة — والتعدية بتتسجّل على
    /// الصف.</b>
    ///
    /// <para>تعدية مابتتعرضش معناها إن القاعدة مالهاش أي معنى.</para>
    /// </summary>
    [Fact]
    public async Task Approving_with_an_out_of_brand_technician_records_the_override()
    {
        var (approve, _, repo, _, _, me) = Build();

        var device = RepairFixtures.Device(me.TenantId, manufacturer: "HP");
        var item = RepairFixtures.Item(me.TenantId, device.Id);
        var tech = RepairFixtures.Technician(me.TenantId);

        repo.Devices.Add(device);
        repo.Items.Add(item);
        repo.Technicians.Add(tech);

        var dell = Guid.NewGuid();
        repo.Rules.Add(new(dell, "Dell", []));
        repo.TechBrands[tech.Id] = [dell];

        var result = await approve.Handle(
            new ApproveRepairCommand(item.Id, tech.Id, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(tech.Id, item.AssignedTechnicianId);
        Assert.True(item.BrandOverride);
    }

    /// <summary>
    /// ⚠️ <b>و<c>StartedWithoutApproval</c> مابيتفضّاش.</b>
    ///
    /// <para>العلم ده <b>تاريخ</b> مش حالة: هو بيقول إن راكة اشتغلت
    /// على الأمر وهو لسه معلّق. والموافقة بعدين مابتمسحش الحقيقة
    /// دي — اللي بيقرا السجل محتاج يعرف إن الشغل اتعمل قبل
    /// القرار.</para>
    /// </summary>
    [Fact]
    public async Task Approval_does_not_erase_the_started_without_approval_flag()
    {
        var (approve, _, repo, _, _, me) = Build();

        var device = RepairFixtures.Device(me.TenantId);
        var item = RepairFixtures.Item(me.TenantId, device.Id, RepairStatus.InProgress);
        item.StartedWithoutApproval = true;

        repo.Devices.Add(device);
        repo.Items.Add(item);

        await approve.Handle(new ApproveRepairCommand(item.Id, null, null), default);

        Assert.True(item.StartedWithoutApproval);
    }

    // =================================================================
    //  الرفض
    // =================================================================

    [Fact]
    public async Task Rejecting_stamps_the_reason_and_puts_it_in_the_trail()
    {
        var (_, reject, repo, audit, work, me) = Build();

        var device = RepairFixtures.Device(me.TenantId);
        var item = RepairFixtures.Item(me.TenantId, device.Id);

        repo.Devices.Add(device);
        repo.Items.Add(item);

        var result = await reject.Handle(
            new RejectRepairCommand(item.Id, "  اللاب ده مش بيستاهل  "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("اترفض.", result.Value.Message);

        Assert.Equal(RepairApproval.Rejected, item.Approval);
        Assert.Equal("اللاب ده مش بيستاهل", item.ApprovalNote);
        Assert.Equal(me.Id, item.ApprovedByUserId);
        Assert.Equal(1, work.Saves);

        var line = Assert.Single(audit.Lines);
        Assert.Equal("repair.rejected", line.Action);

        // ⚠️ والسبب جوّه نص السجل كمان — المدير بيشوفه في صفحة
        // الإجراءات، والفني بيشوف `ApprovalNote`.
        Assert.Contains("اللاب ده مش بيستاهل", line.Summary);
    }

    /// <summary>
    /// 🔴 <b>فحص السبب <u>قبل</u> البحث عن الصف — والترتيب ده
    /// حمّال.</b>
    ///
    /// <para>رفض من غير سبب على معرّف مش موجود بيرجّع <c>400</c>
    /// «اكتب سبب الرفض» مش <c>404</c>. والرسالة بتقول للمحاسب يعمل
    /// إيه؛ والمعرّف الغلط مش مشكلته.</para>
    ///
    /// <para>⚠️ وعشان كده الفحص ده <b>ماينفعش</b> يروح للمتحقّق
    /// كمان: المتحقّق بيرد بشكل <c>ValidationProblem</c> مختلف عن
    /// <c>ToProblem</c>.</para>
    /// </summary>
    [Fact]
    public async Task A_reasonless_reject_on_an_unknown_id_complains_about_the_reason()
    {
        var (_, reject, _, _, work, _) = Build();

        var result = await reject.Handle(new RejectRepairCommand(Guid.NewGuid(), null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("repair.reject_note_required", result.Error.Code);
        Assert.Equal(400, result.Error.StatusCode);
        Assert.Equal(0, work.Saves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("لأ")]
    public async Task The_rejection_reason_is_mandatory_and_has_a_minimum(string? note)
    {
        var (_, reject, repo, _, _, me) = Build();

        var device = RepairFixtures.Device(me.TenantId);
        var item = RepairFixtures.Item(me.TenantId, device.Id);

        repo.Devices.Add(device);
        repo.Items.Add(item);

        var result = await reject.Handle(new RejectRepairCommand(item.Id, note), default);

        Assert.True(result.IsFailure);
        Assert.Equal("repair.reject_note_required", result.Error.Code);

        // الأمر فضل معلّق — المحاسب لسه مادوسش حاجة فعلاً.
        Assert.Equal(RepairApproval.Pending, item.Approval);
    }

    /// <summary>⚠️ والرفض مابيلمسش الفني خالص.</summary>
    [Fact]
    public async Task Rejecting_never_changes_the_technician()
    {
        var (_, reject, repo, _, _, me) = Build();

        var device = RepairFixtures.Device(me.TenantId);
        var tech = RepairFixtures.Technician(me.TenantId);
        var item = RepairFixtures.Item(me.TenantId, device.Id, technicianId: tech.Id);

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(item);

        await reject.Handle(new RejectRepairCommand(item.Id, "مش موافق"), default);

        Assert.Equal(tech.Id, item.AssignedTechnicianId);
    }

    [Fact]
    public async Task A_missing_order_is_a_not_found_with_an_arabic_message()
    {
        var (approve, reject, _, _, _, _) = Build();

        var onApprove = await approve.Handle(
            new ApproveRepairCommand(Guid.NewGuid(), null, null), default);

        var onReject = await reject.Handle(
            new RejectRepairCommand(Guid.NewGuid(), "سبب كافي"), default);

        foreach (var error in new[] { onApprove.Error, onReject.Error })
        {
            Assert.Equal("repair.not_found", error.Code);
            Assert.Equal(404, error.StatusCode);

            // 🔴 قرار صاحب الشغل: رسالة عربي موحّدة بدل رد فاضي.
            Assert.Equal("أمر الصيانة مش موجود.", error.Description);
        }
    }

    // =================================================================
    //  فلاتر القايمة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفلتر المش مفهوم بيتجاهل — مابيرفضش.</b>
    ///
    /// <para>الداش بورد بتحفظ الفلاتر في الرابط، فرابط محفوظ فيه
    /// حالة قديمة كان بيفضّي الشاشة والمدير مش عارف ليه.</para>
    /// </summary>
    [Fact]
    public async Task An_unparseable_filter_is_dropped_not_rejected()
    {
        var repo = new FakeRepairRepository();
        var me = new FakeCurrentUser();
        var handler = new GetRepairsQueryHandler(repo, me);

        var result = await handler.Handle(
            new GetRepairsQuery(
                Search: null, Status: "حالة مالهاش وجود", Approval: "كلام",
                Technician: null, From: null, To: null, Sort: null,
                Page: null, PageSize: null), default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repo.LastFilter);
        Assert.Null(repo.LastFilter.Status);
        Assert.Null(repo.LastFilter.Approval);
    }

    /// <summary>
    /// ⚠️ وبالاسم مش بالرقم — الداش بورد بتبعت
    /// <c>status=InProgress</c>.
    /// </summary>
    [Fact]
    public async Task Status_and_approval_parse_by_name_ignoring_case()
    {
        var repo = new FakeRepairRepository();
        var handler = new GetRepairsQueryHandler(repo, new FakeCurrentUser());

        await handler.Handle(
            new GetRepairsQuery(null, "inprogress", "PENDING", null, null, null,
                null, null, null), default);

        Assert.Equal(RepairStatus.InProgress, repo.LastFilter!.Status);
        Assert.Equal(RepairApproval.Pending, repo.LastFilter.Approval);
    }

    /// <summary>
    /// 🔴 التصفيح بيتظبّط في المدى ومابيرفضش — و<c>Skip</c> عمره ما
    /// بيبقى سالب.
    /// </summary>
    [Theory]
    [InlineData(-5, -1, 1, 40)]
    [InlineData(0, 0, 1, 40)]
    [InlineData(3, 5000, 3, 200)]
    public async Task Paging_is_clamped_never_refused(
        int page, int pageSize, int expectedPage, int expectedSize)
    {
        var repo = new FakeRepairRepository();
        var handler = new GetRepairsQueryHandler(repo, new FakeCurrentUser());

        var result = await handler.Handle(
            new GetRepairsQuery(null, null, null, null, null, null, null, page, pageSize),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPage, repo.LastFilter!.Page);
        Assert.Equal(expectedSize, repo.LastFilter.PageSize);
        Assert.Equal(expectedPage, result.Value.Page);
        Assert.Equal(expectedSize, result.Value.PageSize);
    }

    /// <summary>
    /// 🔴 <b>العدّاد بيوصل للواجهة ومابيتحسبش من الصفوف
    /// المعروضة.</b>
    ///
    /// <para>الصفوف هنا فاضية والعدّاد ٣ — ولازم يوصل ٣.</para>
    /// </summary>
    [Fact]
    public async Task Awaiting_approval_reaches_the_response_even_with_no_rows()
    {
        var repo = new FakeRepairRepository { Awaiting = 3, ListTotal = 0 };
        var handler = new GetRepairsQueryHandler(repo, new FakeCurrentUser());

        var result = await handler.Handle(
            new GetRepairsQuery(null, null, null, null, null, null, null, null, null), default);

        Assert.Empty(result.Value.Items);
        Assert.Equal(3, result.Value.AwaitingApproval);
    }

    /// <summary>
    /// ⚠️ «oldest» بأي حالة أحرف بتقلب الترتيب؛ وأي حاجة تانية =
    /// الأحدث.
    /// </summary>
    [Theory]
    [InlineData("oldest", true)]
    [InlineData("OLDEST", true)]
    [InlineData("newest", false)]
    [InlineData(null, false)]
    [InlineData("كلام", false)]
    public async Task Only_oldest_flips_the_order(string? sort, bool expected)
    {
        var repo = new FakeRepairRepository();
        var handler = new GetRepairsQueryHandler(repo, new FakeCurrentUser());

        await handler.Handle(
            new GetRepairsQuery(null, null, null, null, null, null, sort, null, null), default);

        Assert.Equal(expected, repo.LastFilter!.Oldest);
    }

    /// <summary>
    /// 🔴 <b>نص البحث بيتوحّد قبل ما يوصل للاستعلام، والكود
    /// لأ.</b>
    ///
    /// <para>التوحيد لازم يحصل في المعالج: لو اتحصل جوّه
    /// <c>Where</c>، EF مابيعرفش يترجمه وبيرمي على قاعدة حقيقية —
    /// نفس ثقب <c>LoginName.Normalize</c>.</para>
    /// </summary>
    [Fact]
    public async Task Search_arrives_as_both_a_raw_code_and_a_normalized_pattern()
    {
        var repo = new FakeRepairRepository();
        var handler = new GetRepairsQueryHandler(repo, new FakeCurrentUser());

        await handler.Handle(
            new GetRepairsQuery("  البطاريه  ", null, null, null, null, null,
                null, null, null), default);

        // الكود خام (بعد شيل المساحات) للمقارنة المضبوطة.
        Assert.Equal("البطاريه", repo.LastFilter!.ExactCode);

        // والنمط موحّد ومحطوط بين `%`.
        Assert.StartsWith("%", repo.LastFilter.SearchPattern);
        Assert.EndsWith("%", repo.LastFilter.SearchPattern);
        Assert.Contains(Codlek.Core.Text.ArabicText.Normalize("البطاريه"),
            repo.LastFilter.SearchPattern);
    }

    [Fact]
    public async Task An_empty_search_sets_no_pattern_at_all()
    {
        var repo = new FakeRepairRepository();
        var handler = new GetRepairsQueryHandler(repo, new FakeCurrentUser());

        await handler.Handle(
            new GetRepairsQuery("   ", null, null, null, null, null, null, null, null), default);

        Assert.Null(repo.LastFilter!.SearchPattern);
        Assert.Null(repo.LastFilter.ExactCode);
    }
}
