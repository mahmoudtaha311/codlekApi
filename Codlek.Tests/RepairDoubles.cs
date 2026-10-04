using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Abstractions;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;

namespace Codlek.Tests;

/// <summary>
/// بدائل الصيانة للفحوص.
///
/// <para>⚠️ <b>البدائل دي بتثبت القرار، مش الاستعلام.</b> ترتيب
/// الفحوص وفضّ التكرار وأنهي حاجة بتتكتب في السجل — ده اللي
/// بتقيسه. وقواعد الاستعلام (الترشيح بالشركة، الترتيب، المدى)
/// محتاجة قاعدة حقيقية — راجع
/// <see cref="RepairListRepositoryTests"/>.</para>
/// </summary>
public sealed class FakeRepairRepository : IRepairRepository
{
    public readonly List<RepairWorkItem> Items = [];
    public readonly List<Technician> Technicians = [];
    public readonly List<Device> Devices = [];
    public readonly List<BrandRule> Rules = [];
    public readonly Dictionary<Guid, IReadOnlyCollection<Guid>> TechBrands = [];
    public readonly List<RepairWorkItem> Added = [];
    public readonly HashSet<Guid> Reports = [];

    public Task<RepairWorkItem?> FindAsync(Guid t, Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(w => w.Id == id && w.TenantId == t));

    public Task<RepairWorkItem?> FindWithPartsAsync(
        Guid t, Guid id, CancellationToken ct = default) => FindAsync(t, id, ct);

    public void Add(RepairWorkItem item)
    {
        Added.Add(item);
        Items.Add(item);
    }

    public Task<Device?> FindDeviceAsync(Guid t, Guid d, CancellationToken ct = default) =>
        Task.FromResult(Devices.FirstOrDefault(x => x.Id == d && x.TenantId == t));

    public Task<bool> ReportExistsAsync(Guid t, Guid r, CancellationToken ct = default) =>
        Task.FromResult(Reports.Contains(r));

    public Task<Technician?> FindTechnicianAsync(
        Guid t, Guid id, CancellationToken ct = default) =>
        Task.FromResult(Technicians.FirstOrDefault(x => x.Id == id && x.TenantId == t));

    public Task<IReadOnlyList<Technician>> AssignableTechniciansAsync(
        Guid t, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Technician>>(
            Technicians.Where(x => x.TenantId == t && x.IsActive && x.CanRepair).ToList());

    public Task<IReadOnlyCollection<Guid>> TechnicianBrandsAsync(
        Guid t, Guid technicianId, CancellationToken ct = default) =>
        Task.FromResult(
            TechBrands.TryGetValue(technicianId, out var v) ? v : (IReadOnlyCollection<Guid>)[]);

    public Task<IReadOnlyList<BrandRule>> BrandRulesAsync(
        Guid t, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BrandRule>>(Rules);

    public Task<string> DeviceManufacturerAsync(
        Guid t, Guid d, CancellationToken ct = default) =>
        Task.FromResult(
            Devices.FirstOrDefault(x => x.Id == d)?.LastKnownManufacturer ?? "");

    public Task<IReadOnlyList<RepairWorkItem>> PendingAsync(
        Guid t, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RepairWorkItem>>(
            Items.Where(w => w.TenantId == t && w.Approval == RepairApproval.Pending).ToList());

    /// <summary>
    /// ⚠️ <b>الفلتر اللي وصل، محفوظ.</b> فحوص المعالج بتقيس إنه
    /// <b>بنى</b> الفلتر صح — القايمة نفسها شغل المستودع الحقيقي.
    /// </summary>
    public RepairListFilter? LastFilter;

    public List<RepairListRow> ListRows = [];
    public int ListTotal;
    public int Awaiting;

    public Task<(IReadOnlyList<RepairListRow> Rows, int TotalItems)> ListAsync(
        Guid t, RepairListFilter f, CancellationToken ct = default)
    {
        LastFilter = f;
        return Task.FromResult<(IReadOnlyList<RepairListRow>, int)>((ListRows, ListTotal));
    }

    /// <summary>⚠️ بيحفظ الفلتر كمان — فحص «الملف زي الشاشة» بيقارنهم.</summary>
    public Task<IReadOnlyList<RepairListRow>> ExportAsync(
        Guid t, RepairListFilter f, int cap, CancellationToken ct = default)
    {
        LastFilter = f;
        LastExportCap = cap;
        return Task.FromResult<IReadOnlyList<RepairListRow>>(ListRows.Take(cap).ToList());
    }

    public int LastExportCap;

    public Task<int> CountAwaitingApprovalAsync(Guid t, CancellationToken ct = default) =>
        Task.FromResult(Awaiting);

    public Task<IReadOnlyList<RepairListRow>> ListForDeviceAsync(
        Guid t, Guid d, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<RepairWorkItem?> FindDetailAsync(
        Guid t, Guid id, CancellationToken ct = default) => FindAsync(t, id, ct);

    public Task<IReadOnlyList<RepairWorkflowFacts>> ListWorkflowAsync(
        Guid t, Guid w, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RepairWorkflowFacts>>([]);

    public Task<IReadOnlyDictionary<Guid, string>> TechnicianNamesAsync(
        Guid t, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            Technicians.Where(x => x.TenantId == t).ToDictionary(x => x.Id, x => x.DisplayName));

    public Task<bool> DeviceExistsAsync(Guid t, Guid d, CancellationToken ct = default) =>
        Task.FromResult(Devices.Any(x => x.Id == d && x.TenantId == t));

    public Task<RepairDeviceFacts?> DeviceFactsAsync(
        Guid t, Guid d, CancellationToken ct = default)
    {
        var device = Devices.FirstOrDefault(x => x.Id == d && x.TenantId == t);

        return Task.FromResult(device is null
            ? null
            : new RepairDeviceFacts(
                device.PublicCode,
                device.LastKnownManufacturer,
                device.CommercialModelName ?? "",
                device.LastKnownModel));
    }
}

/// <summary>
/// مسجّل حركات بيحفظ اللي اتبعتله.
///
/// <para>⚠️ <c>Refuse</c> بتخلّيه يرفض — عشان نقيس إن فتح الأمر
/// بيكمّل والحركة بتتجاهل، وده سلوك منقول من القديم.</para>
/// </summary>
public sealed class FakeWorkflowRecorder : IDeviceWorkflowRecorder
{
    public readonly List<WorkflowMove> Moves = [];
    public bool Refuse;

    /// <summary>
    /// ⚠️ <b>نص الرفض</b> — مسار المزامنة بيفرّق بين «الجهاز مش موجود»
    /// (رفض مؤقت) وأي سبب تاني (نهائي) <b>بالنص</b>، فالفحص محتاج
    /// يتحكّم فيه.
    /// </summary>
    public string RefuseWith = "مرفوض";

    /// <summary>
    /// ⚠️ <b>بترفض الحركة رقم كده وبس</b> — عشان نقيس «الكل أو ولا
    /// واحد»: الدفعة لازم ترجّع صفر، واللي نجح قبلها يفضل في
    /// الذاكرة ومحدش يحفظه.
    /// </summary>
    public int? RefuseAt;

    public async Task<BatchMoveResult> RecordManyAsync(
        Guid tenantId, IReadOnlyList<Guid> deviceIds,
        Func<Guid, WorkflowMove> build, CancellationToken ct = default)
    {
        var wanted = deviceIds.Distinct().ToList();

        if (wanted.Count == 0) return BatchMoveResult.Fail("مفيش أجهزة متحدّدة.");

        for (int i = 0; i < wanted.Count; i++)
        {
            if (RefuseAt == i) return BatchMoveResult.Fail("الحركة دي مرفوضة");

            var result = await RecordAsync(tenantId, build(wanted[i]), ct);

            if (!result.Ok) return BatchMoveResult.Fail(result.Error!);
        }

        return BatchMoveResult.Recorded(wanted.Count);
    }

    public Task<MoveResult> RecordAsync(
        Guid tenantId, WorkflowMove move, CancellationToken ct = default)
    {
        Moves.Add(move);

        return Task.FromResult(Refuse
            ? MoveResult.Fail(RefuseWith)
            : MoveResult.Recorded(new DeviceWorkflowEvent
            {
                TenantId = tenantId,
                DeviceId = move.DeviceId,
                EventType = move.EventType,
            }));
    }
}

/// <summary>سجل مراجعة بيحفظ السطور.</summary>
public sealed class FakeAuditTrail : IAuditTrail
{
    public readonly List<(string Action, string EntityType, Guid? Id, string Code, string Summary)>
        Lines = [];

    public void Record(
        string action, string entityType, Guid? entityId, string entityCode, string summary) =>
        Lines.Add((action, entityType, entityId, entityCode, summary));

    /// <summary>
    /// ⚠️ <b>سطور الراكة في قايمة لوحدها.</b> الفاعل مختلف (محطة مش
    /// مستخدم) والشركة جايّة من المحطة — فخلطهم في قايمة واحدة كان
    /// بيخلّي الفحص مايقدرش يقيس إن الشركة الصح اتكتبت.
    /// </summary>
    public readonly List<(RackAuditActor Actor, string Action, string EntityType,
        Guid? Id, string Code, string Summary, string DataJson)> RackLines = [];

    public void RecordForRack(
        RackAuditActor actor, string action, string entityType, Guid? entityId,
        string entityCode, string summary, string dataJson = "") =>
        RackLines.Add((actor, action, entityType, entityId, entityCode, summary, dataJson));
}

/// <summary>
/// وحدة عمل بتعدّ الحفظات.
///
/// <para>🔴 <b>العدّاد ده مهم:</b> كل إجراء لازم يحفظ <b>مرة
/// واحدة</b> — الانتقال والحركة والسجل مع بعض. حفظتين معناها إن
/// انهيار بينهم بيسيب نص الشغل.</para>
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        Saves++;
        return Task.FromResult(1);
    }

    public Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work, CancellationToken ct = default) => work(ct);
}

/// <summary>مستخدم حالي بدور قابل للتغيير.</summary>
public sealed class FakeCurrentUser(UserRole role = UserRole.Manager) : ICurrentUser
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.NewGuid();
    public string DisplayName => "كريم";
    public string Code => "U001";
    public UserRole Role { get; set; } = role;
    public bool IsAuthenticated => true;
}

/// <summary>بنّاؤو كيانات الفحوص.</summary>
public static class RepairFixtures
{
    public static Technician Technician(
        Guid tenantId, string name = "محمود",
        bool isActive = true, bool canRepair = true) => new()
        {
            TenantId = tenantId,
            DisplayName = name,
            Code = "T" + Guid.NewGuid().ToString("N")[..4],
            Username = Guid.NewGuid().ToString("N")[..8],
            NormalizedUsername = Guid.NewGuid().ToString("N")[..8],
            IsActive = isActive,
            CanRepair = canRepair,
        };

    public static Device Device(
        Guid tenantId, string manufacturer = "HP",
        DeviceLifecycleStatus status = DeviceLifecycleStatus.Active) => new()
        {
            TenantId = tenantId,
            PublicCode = "D-" + Guid.NewGuid().ToString("N")[..4],
            LastKnownManufacturer = manufacturer,
            LastKnownModel = "6470b",
            Status = status,
            LastSeenAtUtc = DateTime.UtcNow,
        };

    public static RepairWorkItem Item(
        Guid tenantId, Guid deviceId,
        RepairStatus status = RepairStatus.New,
        RepairApproval approval = RepairApproval.Pending,
        Guid? technicianId = null) => new()
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            PublicCode = "RP-" + Guid.NewGuid().ToString("N")[..6],
            Status = status,
            Approval = approval,
            AssignedTechnicianId = technicianId,
            OpenedByName = "كريم",
            OpenedByActorType = "User",
            FaultSummary = "مابيفتحش",
            SearchText = ArabicText.Combine("مابيفتحش"),
        };
}
