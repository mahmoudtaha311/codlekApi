using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Features.Repairs.GetAssignableTechnicians;
using Codlek.Application.Features.Repairs.GetPendingRepairs;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;

namespace Codlek.Tests;

/// <summary>
/// طابور قرار المحاسب وقايمة الفنيين.
///
/// <para>🔴 <b>الصف ده فيه معلومتين المحاسب مايقدرش يقرّر من
/// غيرهم:</b> هل الماركة مش معروفة (فالقيد ماتطبّقش)، وهل الفني
/// المتسند برّه ماركات اللاب. وهو الوحيد اللي بيقدر يعدّي القاعدة.</para>
/// </summary>
public class PendingRepairQueueTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Hp = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Dell = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private sealed class FakeUser : ICurrentUser
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid TenantId => Tenant;
        public string DisplayName => "محاسب";
        public string Code => "A001";
        public UserRole Role => UserRole.Accountant;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRepository : IRepairRepository
    {
        public readonly List<RepairWorkItem> Pending = [];
        public readonly List<Technician> Technicians = [];
        public readonly Dictionary<Guid, IReadOnlyCollection<Guid>> TechBrands = [];
        public readonly List<BrandRule> Rules = [];

        /// <summary>⚠️ عدّاد عشان نقيس عدد الاستعلامات — راجع الفحص تحت.</summary>
        public int TechnicianBrandCalls;

        public Task<IReadOnlyList<RepairWorkItem>> PendingAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepairWorkItem>>(
                Pending.Where(w => w.TenantId == tenantId)
                       .OrderBy(w => w.OpenedAtUtc).ToList());

        public Task<IReadOnlyList<BrandRule>> BrandRulesAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BrandRule>>(Rules);

        public Task<IReadOnlyCollection<Guid>> TechnicianBrandsAsync(
            Guid tenantId, Guid technicianId, CancellationToken ct = default)
        {
            TechnicianBrandCalls++;
            return Task.FromResult(
                TechBrands.TryGetValue(technicianId, out var v) ? v : (IReadOnlyCollection<Guid>)[]);
        }

        public Task<IReadOnlyList<Technician>> AssignableTechniciansAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Technician>>(
                Technicians.Where(t => t.TenantId == tenantId && t.IsActive && t.CanRepair)
                           .OrderBy(t => t.DisplayName).ToList());

        // --- اللي الفحوص دي مابتستعملهوش ---
        public Task<RepairWorkItem?> FindAsync(Guid t, Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RepairWorkItem?> FindWithPartsAsync(
            Guid t, Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public void Add(RepairWorkItem item) => throw new NotSupportedException();

        public Task<Device?> FindDeviceAsync(Guid t, Guid d, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> ReportExistsAsync(Guid t, Guid r, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Technician?> FindTechnicianAsync(
            Guid t, Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<string> DeviceManufacturerAsync(
            Guid t, Guid d, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<(IReadOnlyList<RepairListRow> Rows, int TotalItems)> ListAsync(
            Guid t, RepairListFilter f, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepairListRow>> ExportAsync(
            Guid t, RepairListFilter f, int cap, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> CountAwaitingApprovalAsync(Guid t, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepairListRow>> ListForDeviceAsync(
            Guid t, Guid d, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<RepairWorkItem?> FindDetailAsync(
            Guid t, Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<RepairWorkflowFacts>> ListWorkflowAsync(
            Guid t, Guid w, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> TechnicianNamesAsync(
            Guid t, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<bool> DeviceExistsAsync(Guid t, Guid d, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RepairDeviceFacts?> DeviceFactsAsync(
            Guid t, Guid d, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static (FakeRepository Repo,
                    GetPendingRepairsQueryHandler Queue,
                    GetAssignableTechniciansQueryHandler Techs) Build()
    {
        var repo = new FakeRepository();
        repo.Rules.AddRange([
            new(Hp, "HP", ["Hewlett-Packard"]),
            new(Dell, "Dell", []),
        ]);
        var me = new FakeUser();

        return (repo,
            new GetPendingRepairsQueryHandler(repo, me),
            new GetAssignableTechniciansQueryHandler(repo, me));
    }

    private static RepairWorkItem Item(
        string manufacturer, Guid? technicianId = null, string technicianName = "",
        int minutesAgo = 0, bool startedWithoutApproval = false,
        string? commercialModel = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Tenant,
        PublicCode = "RP-1",
        OpenedByName = "الفني",
        OpenedAtUtc = DateTime.UtcNow.AddMinutes(-minutesAgo),
        FaultSummary = "مش بيقلّع",
        AssignedTechnicianId = technicianId,
        StartedWithoutApproval = startedWithoutApproval,
        Approval = RepairApproval.Pending,
        Device = new Device
        {
            TenantId = Tenant,
            PublicCode = "LP-1",
            LastKnownManufacturer = manufacturer,
            LastKnownModel = "20L5",
            CommercialModelName = commercialModel,
        },
        AssignedTechnician = technicianId is null
            ? null
            : new Technician { Id = technicianId.Value, DisplayName = technicianName },
    };

    // =================================================================
    //  الطابور
    // =================================================================

    [Fact]
    public async Task An_empty_queue_returns_an_empty_list()
    {
        var (_, queue, _) = Build();

        var result = await queue.Handle(new GetPendingRepairsQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    /// <summary>⚠️ الأقدم الأول — دي طابور شغل مش قايمة أخبار.</summary>
    [Fact]
    public async Task The_oldest_order_comes_first()
    {
        var (repo, queue, _) = Build();
        var recent = Item("HP", minutesAgo: 5);
        recent.PublicCode = "RP-NEW";
        var old = Item("HP", minutesAgo: 90);
        old.PublicCode = "RP-OLD";
        repo.Pending.AddRange([recent, old]);

        var rows = (await queue.Handle(new GetPendingRepairsQuery(), default)).Value!;

        Assert.Equal("RP-OLD", rows[0].PublicCode);
        Assert.Equal("RP-NEW", rows[1].PublicCode);
    }

    /// <summary>
    /// 🔴 <b>الماركة المش معروفة بتتعلّم للمحاسب.</b>
    ///
    /// <para>القيد بيعدّي عليها، وصاحب الشغل لازم يشوفها عشان يضيفها
    /// للقايمة — بدل ما تفضل مخبّية.</para>
    /// </summary>
    [Fact]
    public async Task An_unknown_brand_is_flagged_not_hidden()
    {
        var (repo, queue, _) = Build();
        repo.Pending.Add(Item("Toshiba"));

        var row = (await queue.Handle(new GetPendingRepairsQuery(), default)).Value!.Single();

        Assert.True(row.BrandUnknown);
        Assert.Equal("", row.Brand);
    }

    [Fact]
    public async Task A_known_brand_is_named()
    {
        var (repo, queue, _) = Build();
        repo.Pending.Add(Item("Hewlett-Packard"));

        var row = (await queue.Handle(new GetPendingRepairsQuery(), default)).Value!.Single();

        Assert.False(row.BrandUnknown);
        Assert.Equal("HP", row.Brand);
    }

    /// <summary>
    /// 🔴 <b>«الفني برّه ماركاته» بيتحسب على السيرفر.</b>
    ///
    /// <para>المحاسب هو الوحيد اللي بيقدر يعدّي القاعدة، فلازم يشوفها
    /// <b>قبل</b> ما يوافق. ولو الواجهة حسبتها لوحدها، القاعدة بتبقى
    /// مكتوبة في مكانين بلغتين.</para>
    /// </summary>
    [Fact]
    public async Task A_technician_outside_the_brand_is_flagged()
    {
        var (repo, queue, _) = Build();
        var tech = Guid.NewGuid();
        repo.TechBrands[tech] = [Hp];
        repo.Pending.Add(Item("Dell", tech, "أحمد"));

        var row = (await queue.Handle(new GetPendingRepairsQuery(), default)).Value!.Single();

        Assert.True(row.TechnicianOutsideBrand);
        Assert.Equal("أحمد", row.TechnicianName);
    }

    [Fact]
    public async Task A_technician_inside_the_brand_is_not_flagged()
    {
        var (repo, queue, _) = Build();
        var tech = Guid.NewGuid();
        repo.TechBrands[tech] = [Hp];
        repo.Pending.Add(Item("HP", tech, "أحمد"));

        var row = (await queue.Handle(new GetPendingRepairsQuery(), default)).Value!.Single();

        Assert.False(row.TechnicianOutsideBrand);
    }

    /// <summary>
    /// ⚠️ <b>وفني من غير قيد عمره ما يبان «برّه».</b>
    ///
    /// <para>القايمة الفاضية معناها «مفيش قيد» — ولو اتفهمت غلط، كل
    /// صف في الطابور بيطلع بتحذير كاذب والمحاسب بيبطّل يبص عليه.</para>
    /// </summary>
    [Fact]
    public async Task A_technician_with_no_brands_is_never_flagged()
    {
        var (repo, queue, _) = Build();
        var tech = Guid.NewGuid();
        repo.Pending.Add(Item("Dell", tech, "أحمد"));

        var row = (await queue.Handle(new GetPendingRepairsQuery(), default)).Value!.Single();

        Assert.False(row.TechnicianOutsideBrand);
    }

    /// <summary>⚠️ وأمر من غير فني متسند مابيتعلّمش.</summary>
    [Fact]
    public async Task An_unassigned_order_is_not_flagged()
    {
        var (repo, queue, _) = Build();
        repo.Pending.Add(Item("Dell"));

        var row = (await queue.Handle(new GetPendingRepairsQuery(), default)).Value!.Single();

        Assert.False(row.TechnicianOutsideBrand);
        Assert.Null(row.AssignedTechnicianId);
        Assert.Equal("", row.TechnicianName);
    }

    /// <summary>
    /// 🔴 <b>«اتبدأ من غير موافقة» بيعدّي للمحاسب.</b>
    ///
    /// <para>المحاسب لسه بيقرّر، بس اللاب خلاص اتفك — ورفض ساعتها قرار
    /// مختلف تماماً عن رفض لاب لسه ماحدش لمسه.</para>
    /// </summary>
    [Fact]
    public async Task Work_started_without_approval_reaches_the_accountant()
    {
        var (repo, queue, _) = Build();
        repo.Pending.Add(Item("HP", startedWithoutApproval: true));

        var row = (await queue.Handle(new GetPendingRepairsQuery(), default)).Value!.Single();

        Assert.True(row.StartedWithoutApproval);
    }

    /// <summary>⚠️ والاسم التجاري بيسبق الموديل الخام.</summary>
    [Fact]
    public async Task The_commercial_model_wins_in_the_device_name()
    {
        var (repo, queue, _) = Build();
        repo.Pending.Add(Item("HP", commercialModel: "EliteBook 840"));

        var row = (await queue.Handle(new GetPendingRepairsQuery(), default)).Value!.Single();

        Assert.Equal("HP EliteBook 840", row.DeviceName);
    }

    /// <summary>
    /// ⚠️ <b>ماركات الفني بتتقرا مرة واحدة لكل فني مميّز.</b>
    ///
    /// <para>القديم كان بيقراها جوّه اللفّة — استعلام لكل صف. والطابور
    /// عادةً فيه نفس الفنيين مكرّرين، فده بيحوّل ١٠٠ استعلام لخمسة.
    /// <b>والسلوك واحد بالحرف.</b></para>
    /// </summary>
    [Fact]
    public async Task Technician_brands_are_read_once_per_distinct_technician()
    {
        var (repo, queue, _) = Build();
        var tech = Guid.NewGuid();
        repo.TechBrands[tech] = [Hp];

        for (int i = 0; i < 5; i++) repo.Pending.Add(Item("HP", tech, "أحمد"));

        await queue.Handle(new GetPendingRepairsQuery(), default);

        Assert.Equal(1, repo.TechnicianBrandCalls);
    }

    // =================================================================
    //  قايمة الفنيين
    // =================================================================

    private static Technician Tech(
        string name, bool active = true, bool canRepair = true,
        TechnicianSpecialty specialty = TechnicianSpecialty.None) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Tenant,
        DisplayName = name,
        Code = "F001",
        IsActive = active,
        CanRepair = canRepair,
        Specialty = specialty,
    };

    /// <summary>
    /// 🔴 <b>نشط <u>و</u> يقدر يصلّح.</b>
    ///
    /// <para>الموقوف مايظهرش حتى لو قدرته شغّالة — إسناد شغل لحد مش
    /// قادر يدخل معناه أمر بيقعد.</para>
    /// </summary>
    [Fact]
    public async Task Only_active_technicians_who_can_repair_are_listed()
    {
        var (repo, _, techs) = Build();
        repo.Technicians.AddRange([
            Tech("شغّال"),
            Tech("موقوف", active: false),
            Tech("مش بيصلّح", canRepair: false),
            Tech("موقوف ومش بيصلّح", active: false, canRepair: false),
        ]);

        var rows = (await techs.Handle(new GetAssignableTechniciansQuery(), default)).Value!;

        Assert.Single(rows);
        Assert.Equal("شغّال", rows[0].DisplayName);
    }

    /// <summary>
    /// ⚠️ <b>والقايمة الفاضية بترجع فاضية بنجاح — مش خطأ.</b>
    ///
    /// <para>الواجهة محتاجة تعرف <b>صفر</b> بوضوح عشان تعرض «لا يوجد
    /// فني صيانة مفعّل حاليًا» بدل منسدلة فاضية.</para>
    /// </summary>
    [Fact]
    public async Task No_assignable_technicians_is_a_successful_empty_list()
    {
        var (_, _, techs) = Build();

        var result = await techs.Handle(new GetAssignableTechniciansQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    /// <summary>
    /// 🔴 التخصص بيخرج <b>رقم ونص</b> مع بعض.
    ///
    /// <para>لو الواجهة ترجمت لوحدها، أول تخصص جديد بيظهر بالإنجليزي
    /// في شاشة وبالعربي في التانية.</para>
    /// </summary>
    [Fact]
    public async Task The_specialty_comes_back_as_both_number_and_text()
    {
        var (repo, _, techs) = Build();
        repo.Technicians.Add(Tech("أحمد", specialty: TechnicianSpecialty.Boards));

        var row = (await techs.Handle(new GetAssignableTechniciansQuery(), default))
            .Value!.Single();

        Assert.Equal((int)TechnicianSpecialty.Boards, row.Specialty);
        Assert.Equal("بوردات", row.SpecialtyText);
    }

    /// <summary>🔴 ونصوص التخصص كلها مجمّدة — بتتعرض في تلات أماكن.</summary>
    [Theory]
    [InlineData(TechnicianSpecialty.Testing, "فحص")]
    [InlineData(TechnicianSpecialty.Boards, "بوردات")]
    [InlineData(TechnicianSpecialty.Batteries, "بطاريات")]
    [InlineData(TechnicianSpecialty.Screens, "شاشات")]
    [InlineData(TechnicianSpecialty.Refinishing, "تجديد")]
    [InlineData(TechnicianSpecialty.Grading, "تصنيف")]
    [InlineData(TechnicianSpecialty.None, "غير محدد")]
    public void The_specialty_text_is_frozen(TechnicianSpecialty specialty, string expected) =>
        Assert.Equal(expected, TechnicianSpecialtyText.Arabic(specialty));
}
