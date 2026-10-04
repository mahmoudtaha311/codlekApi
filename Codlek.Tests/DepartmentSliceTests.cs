using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Departments;
using Codlek.Application.Features.Departments.CreateDepartment;
using Codlek.Application.Features.Departments.UpdateDepartment;
using Codlek.Application.Features.Departments;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// أول قطاع كامل — <b>والقواعد اللي جواه هي اللي بتتكرر ٨٤ مرة</b>.
///
/// <para>⚠️ الفحوص دي مابتلمسش قاعدة: المستودع بديل في الذاكرة.
/// السؤال هنا «القرار صح؟» مش «الاستعلام شغّال؟» — والتاني مكانه
/// فحوص القاعدة.</para>
/// </summary>
public class DepartmentSliceTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("99999999-9999-9999-9999-999999999999");

    // =================================================================
    //  البدائل
    // =================================================================

    private sealed class FakeUser : ICurrentUser
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid TenantId { get; init; } = Tenant;
        public string DisplayName => "كريم المدير";
        public string Code => "M001";
        public UserRole Role => UserRole.Manager;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRepository : IDepartmentRepository
    {
        public readonly List<Department> Rows = [];
        public readonly Dictionary<Guid, int> TechnicianCounts = [];

        public Task<IReadOnlyList<DepartmentRow>> ListAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DepartmentRow>>(
                Rows.Where(d => d.TenantId == tenantId)
                    .OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
                    .Select(d => new DepartmentRow(
                        d.Id, d.Code, d.Name, d.IsActive, d.SortOrder,
                        TechnicianCounts.GetValueOrDefault(d.Id)))
                    .ToList());

        public Task<Department?> FindAsync(
            Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(
                d => d.Id == id && d.TenantId == tenantId));

        public Task<bool> NameTakenAsync(
            Guid tenantId, string name, Guid? exceptId = null,
            CancellationToken ct = default) =>
            Task.FromResult(Rows.Any(
                d => d.TenantId == tenantId && d.Name == name
                     && (exceptId == null || d.Id != exceptId)));

        public void Add(Department department) => Rows.Add(department);

        public Task<int> CountTechniciansAsync(
            Guid departmentId, CancellationToken ct = default) =>
            Task.FromResult(TechnicianCounts.GetValueOrDefault(departmentId));
    }

    private sealed class FakeAudit : IAuditTrail
    {
        public readonly List<(string Action, string Summary)> Entries = [];

        public void Record(
            string action, string entityType, Guid? entityId,
            string entityCode, string summary) =>
            Entries.Add((action, summary));

        /// <summary>⚠️ مسار الراكة — القطاع ده مابيستعملهوش.</summary>
        public void RecordForRack(
            RackAuditActor actor, string action, string entityType, Guid? entityId,
            string entityCode, string summary, string dataJson = "") =>
            Entries.Add((action, summary));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public async Task<bool> TrySaveChangesAsync(CancellationToken ct = default)
        {
            await SaveChangesAsync(ct);
            return true;
        }

        public int Saves;

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            Saves++;
            return Task.FromResult(1);
        }

        public Task<T> InTransactionAsync<T>(
            Func<CancellationToken, Task<T>> work, CancellationToken ct = default) =>
            work(ct);
    }

    private sealed record Harness(
        FakeRepository Repository,
        FakeAudit Audit,
        FakeUnitOfWork UnitOfWork,
        CreateDepartmentCommandHandler Create,
        UpdateDepartmentCommandHandler Update);

    private static Harness Build()
    {
        var repository = new FakeRepository();
        var audit = new FakeAudit();
        var unitOfWork = new FakeUnitOfWork();
        var me = new FakeUser();

        return new Harness(
            repository, audit, unitOfWork,
            new CreateDepartmentCommandHandler(repository, audit, unitOfWork, me),
            new UpdateDepartmentCommandHandler(repository, audit, unitOfWork, me));
    }

    // =================================================================
    //  الإنشاء
    // =================================================================

    [Fact]
    public async Task Creating_a_department_returns_its_row()
    {
        var h = Build();

        var result = await h.Create.Handle(
            new CreateDepartmentCommand("قسم الصيانة", "REP", 2), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("قسم الصيانة", result.Value!.Name);
        Assert.Equal("REP", result.Value.Code);
        Assert.Equal(2, result.Value.SortOrder);
        Assert.True(result.Value.IsActive);
        Assert.Equal(0, result.Value.TechnicianCount);
    }

    /// <summary>
    /// 🔴 <b>الشركة بتتحطّ من التوكن، مش من الطلب.</b>
    ///
    /// <para>الأمر نفسه مافيهوش <c>TenantId</c> خالص — فمفيش طريقة
    /// حد يبعت شركة تانية.</para>
    /// </summary>
    [Fact]
    public async Task A_new_department_belongs_to_the_signed_in_tenant()
    {
        var h = Build();

        await h.Create.Handle(new CreateDepartmentCommand("الفحص", "", 0), default);

        Assert.Equal(Tenant, h.Repository.Rows.Single().TenantId);
    }

    /// <summary>⚠️ المسافات الزايدة بتتشال — «‎ الفحص ‎» و«الفحص» نفس القسم.</summary>
    [Fact]
    public async Task The_name_and_code_are_trimmed()
    {
        var h = Build();

        var result = await h.Create.Handle(
            new CreateDepartmentCommand("  الفحص  ", "  TST  ", 0), default);

        Assert.Equal("الفحص", result.Value!.Name);
        Assert.Equal("TST", result.Value.Code);
    }

    /// <summary>
    /// 🔴 الاسم المكرر بيترفض.
    ///
    /// <para>قسمين بنفس الاسم معناهم إن المدير بيختار من قايمة فيها
    /// سطرين متطابقين — وبعدين الأرقام بتتقسم بينهم من غير ما حد ياخد
    /// باله.</para>
    /// </summary>
    [Fact]
    public async Task A_duplicate_name_is_rejected()
    {
        var h = Build();
        await h.Create.Handle(new CreateDepartmentCommand("الفحص", "", 0), default);

        var again = await h.Create.Handle(
            new CreateDepartmentCommand("الفحص", "", 0), default);

        Assert.True(again.IsFailure);
        Assert.Equal("department.name_taken", again.Error.Code);
        Assert.Single(h.Repository.Rows);
    }

    /// <summary>⚠️ والرفض مابيحفظش حاجة — مفيش صف ناقص بيفضل.</summary>
    [Fact]
    public async Task A_rejected_create_saves_nothing()
    {
        var h = Build();
        await h.Create.Handle(new CreateDepartmentCommand("الفحص", "", 0), default);
        int savesBefore = h.UnitOfWork.Saves;

        await h.Create.Handle(new CreateDepartmentCommand("الفحص", "", 0), default);

        Assert.Equal(savesBefore, h.UnitOfWork.Saves);
    }

    /// <summary>
    /// ⚠️ <b>نفس الاسم في شركة تانية مسموح.</b>
    ///
    /// <para>لأن الفرادة جوّه الشركة. ولو كانت عامة، شركة جديدة مش
    /// هتعرف تسمّي قسمها «الصيانة» لأن شركة تانية سمّته كده.</para>
    /// </summary>
    [Fact]
    public async Task The_same_name_in_another_tenant_is_fine()
    {
        var h = Build();
        h.Repository.Rows.Add(new Department { TenantId = Other, Name = "الفحص" });

        var result = await h.Create.Handle(
            new CreateDepartmentCommand("الفحص", "", 0), default);

        Assert.True(result.IsSuccess);
    }

    /// <summary>🔴 والإنشاء بيتسجّل في سجل الإجراءات.</summary>
    [Fact]
    public async Task Creating_a_department_is_audited()
    {
        var h = Build();

        await h.Create.Handle(new CreateDepartmentCommand("الفحص", "", 0), default);

        var entry = Assert.Single(h.Audit.Entries);
        Assert.Equal(AuditActions.DepartmentCreated, entry.Action);
        Assert.Contains("الفحص", entry.Summary);
    }

    // =================================================================
    //  التعديل
    // =================================================================

    private static async Task<(Harness H, Guid Id)> WithOneDepartment()
    {
        var h = Build();
        var created = await h.Create.Handle(
            new CreateDepartmentCommand("الفحص", "TST", 1), default);
        return (h, created.Value!.Id);
    }

    [Fact]
    public async Task Updating_changes_only_what_was_sent()
    {
        var (h, id) = await WithOneDepartment();

        var result = await h.Update.Handle(
            new UpdateDepartmentCommand(id, null, null, 7, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("الفحص", result.Value!.Name);   // ماتغيّرش
        Assert.Equal("TST", result.Value.Code);      // ماتغيّرش
        Assert.Equal(7, result.Value.SortOrder);     // اتغيّر
        Assert.True(result.Value.IsActive);          // ماتغيّرش
    }

    /// <summary>
    /// 🔴 <b>الاسم الفاضي معناه «ماتغيّرش» — مش «فضّيه».</b>
    ///
    /// <para>نفس سلوك المشروع القديم بالحرف. الواجهة بتبعت الحقل اللي
    /// اتغيّر بس، فنص فاضي من حقل مابعتوش كان بيمسح اسم القسم.</para>
    /// </summary>
    [Fact]
    public async Task An_empty_name_leaves_the_old_one_alone()
    {
        var (h, id) = await WithOneDepartment();

        var result = await h.Update.Handle(
            new UpdateDepartmentCommand(id, "   ", null, null, null), default);

        Assert.Equal("الفحص", result.Value!.Name);
    }

    /// <summary>
    /// 🔴 <b>التحقق من التكرار لازم يستني القسم ده نفسه.</b>
    ///
    /// <para>من غير كده، أي حفظ من غير تغيير الاسم بيترفض بـ«الاسم
    /// موجود خلاص» — والاسم الموجود هو اسمه هو. يعني المدير مش هيعرف
    /// يغيّر ترتيب قسم خالص.</para>
    /// </summary>
    [Fact]
    public async Task Saving_a_department_under_its_own_name_is_allowed()
    {
        var (h, id) = await WithOneDepartment();

        var result = await h.Update.Handle(
            new UpdateDepartmentCommand(id, "الفحص", null, 3, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.SortOrder);
    }

    /// <summary>⚠️ وأخد اسم قسم تاني بيترفض.</summary>
    [Fact]
    public async Task Taking_another_departments_name_is_rejected()
    {
        var (h, id) = await WithOneDepartment();
        await h.Create.Handle(new CreateDepartmentCommand("الصيانة", "", 0), default);

        var result = await h.Update.Handle(
            new UpdateDepartmentCommand(id, "الصيانة", null, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("department.name_taken", result.Error.Code);
    }

    [Fact]
    public async Task Deactivating_a_department_works()
    {
        var (h, id) = await WithOneDepartment();

        var result = await h.Update.Handle(
            new UpdateDepartmentCommand(id, null, null, null, false), default);

        Assert.False(result.Value!.IsActive);
    }

    /// <summary>
    /// 🔴 <b>قسم شركة تانية = مش موجود، مش «ممنوع».</b>
    ///
    /// <para>⚠️ والفرق مهم: <c>403</c> بيقول «موجود ومش من حقك»، وده
    /// بيأكّد للي بيجرّب إن المعرّف ده حقيقي. <c>404</c> مابيقولش حاجة.</para>
    /// </summary>
    [Fact]
    public async Task A_department_from_another_tenant_is_not_found()
    {
        var h = Build();
        var foreign = new Department { TenantId = Other, Name = "قسم غريب" };
        h.Repository.Rows.Add(foreign);

        var result = await h.Update.Handle(
            new UpdateDepartmentCommand(foreign.Id, "محاولة", null, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error.StatusCode);
    }

    [Fact]
    public async Task A_missing_department_is_not_found()
    {
        var h = Build();

        var result = await h.Update.Handle(
            new UpdateDepartmentCommand(Guid.NewGuid(), "أي حاجة", null, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(DepartmentErrors.NotFound.Code, result.Error.Code);
    }

    /// <summary>⚠️ وقسم مش موجود مابيتسجّلش ومابيتحفظش.</summary>
    [Fact]
    public async Task A_failed_update_neither_saves_nor_audits()
    {
        var h = Build();

        await h.Update.Handle(
            new UpdateDepartmentCommand(Guid.NewGuid(), "أي حاجة", null, null, null), default);

        Assert.Equal(0, h.UnitOfWork.Saves);
        Assert.Empty(h.Audit.Entries);
    }

    [Fact]
    public async Task Updating_a_department_is_audited()
    {
        var (h, id) = await WithOneDepartment();

        await h.Update.Handle(
            new UpdateDepartmentCommand(id, null, null, 5, null), default);

        Assert.Equal(AuditActions.DepartmentUpdated, h.Audit.Entries.Last().Action);
    }
}
