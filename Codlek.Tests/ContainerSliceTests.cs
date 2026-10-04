using Codlek.Application.Contracts.Containers;
using Codlek.Application.Features.Containers.CreateContainer;
using Codlek.Application.Features.Containers.GetContainer;
using Codlek.Application.Features.Containers.UpdateContainer;
using Codlek.Application.Features.Containers;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;

namespace Codlek.Tests;

/// <summary>حاويات الاستيراد.</summary>
public class ContainerSliceTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private sealed class FakeUser : ICurrentUser
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid TenantId => Tenant;
        public string DisplayName => "كريم المدير";
        public string Code => "M001";
        public UserRole Role => UserRole.Manager;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRepository : IContainerRepository
    {
        public readonly List<ImportContainer> Rows = [];
        public readonly Dictionary<Guid, int> DeviceCounts = [];

        public Task<IReadOnlyList<ContainerListItem>> ListAsync(
            Guid tenantId, string? search, CancellationToken ct = default)
        {
            var rows = Rows.Where(c => c.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string key = ContainerCode.Normalize(search);
                if (key.Length > 0) rows = rows.Where(c => c.NormalizedCode.Contains(key));
            }

            return Task.FromResult<IReadOnlyList<ContainerListItem>>(
                rows.OrderBy(c => c.SortOrder).ThenByDescending(c => c.CreatedAtUtc)
                    .Select(c => new ContainerListItem(
                        c.Id, c.Code, c.Name, c.IsActive,
                        DeviceCounts.GetValueOrDefault(c.Id),
                        c.CreatedByName, c.CreatedAtUtc))
                    .ToList());
        }

        public Task<ImportContainer?> FindAsync(
            Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(c => c.Id == id && c.TenantId == tenantId));

        public Task<bool> CodeTakenAsync(
            Guid tenantId, string normalizedCode, CancellationToken ct = default) =>
            Task.FromResult(Rows.Any(
                c => c.TenantId == tenantId && c.NormalizedCode == normalizedCode));

        public void Add(ImportContainer container) => Rows.Add(container);

        public Task<int> CountDevicesAsync(
            Guid tenantId, Guid containerId, CancellationToken ct = default) =>
            Task.FromResult(DeviceCounts.GetValueOrDefault(containerId));

        public Task<IReadOnlyList<ContainerDeviceItem>> DevicesAsync(
            Guid tenantId, Guid containerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ContainerDeviceItem>>([]);
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
        FakeUnitOfWork UnitOfWork,
        CreateContainerCommandHandler Create,
        UpdateContainerCommandHandler Update,
        GetContainerQueryHandler Get);

    private static Harness Build()
    {
        var repository = new FakeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var me = new FakeUser();

        return new Harness(
            repository, unitOfWork,
            new CreateContainerCommandHandler(repository, unitOfWork, me),
            new UpdateContainerCommandHandler(repository, unitOfWork, me),
            new GetContainerQueryHandler(repository, me));
    }

    // =================================================================
    //  الإنشاء
    // =================================================================

    [Fact]
    public async Task Creating_a_container_stores_the_code_and_its_key()
    {
        var h = Build();

        var result = await h.Create.Handle(
            new CreateContainerCommand("SH-2024/01", "شحنة يناير", 1), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("SH-2024/01", result.Value!.Code);

        var row = h.Repository.Rows.Single();
        Assert.Equal("sh202401", row.NormalizedCode);
        Assert.Equal(Tenant, row.TenantId);
    }

    /// <summary>
    /// ⚠️ <b>اسم اللي عملها لقطة، مش ربط.</b>
    ///
    /// <para>لو المدير اتشال بعدين، الحاوية لازم تفضل بتقول مين
    /// عملها.</para>
    /// </summary>
    [Fact]
    public async Task The_creator_name_is_a_snapshot()
    {
        var h = Build();

        await h.Create.Handle(new CreateContainerCommand("SH1", "", 0), default);

        Assert.Equal("كريم المدير", h.Repository.Rows.Single().CreatedByName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("S")]
    public async Task A_code_shorter_than_two_characters_is_rejected(string code)
    {
        var h = Build();

        var result = await h.Create.Handle(new CreateContainerCommand(code, "", 0), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContainerErrors.CodeMissing.Code, result.Error.Code);
        Assert.Empty(h.Repository.Rows);
    }

    /// <summary>
    /// 🔴 <b>«مش صالح» كود مختلف عن «اكتب رمز».</b>
    ///
    /// <para><c>---</c> طوله كفاية بس مفيهوش ولا حرف ولا رقم. ولو
    /// رجّعنا «اكتب رمز الحاوية»، المستخدم بيبص على الخانة ويلاقيها
    /// مليانة ومايفهمش إيه اللي ناقص.</para>
    /// </summary>
    [Theory]
    [InlineData("---")]
    [InlineData("///")]
    [InlineData("...")]
    public async Task A_code_with_no_letters_or_digits_is_invalid_not_missing(string junk)
    {
        var h = Build();

        var result = await h.Create.Handle(new CreateContainerCommand(junk, "", 0), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContainerErrors.CodeInvalid.Code, result.Error.Code);
    }

    /// <summary>
    /// 🔴 <b>الرمز المكرر بيترفض — حتى بشكل مكتوب مختلف.</b>
    ///
    /// <para>ودي الحاجة اللي التطبيع موجود عشانها: <c>SH-2024/01</c>
    /// و<c>sh 2024 01</c> نفس الشحنة، وتسجيلهم مرتين بيفرّق لابات
    /// الشحنة على صفّين.</para>
    /// </summary>
    [Fact]
    public async Task The_same_code_written_differently_is_still_a_duplicate()
    {
        var h = Build();
        await h.Create.Handle(new CreateContainerCommand("SH-2024/01", "", 0), default);

        var again = await h.Create.Handle(
            new CreateContainerCommand("sh 2024 01", "", 0), default);

        Assert.True(again.IsFailure);
        Assert.Equal(ContainerErrors.CodeTaken("x").Code, again.Error.Code);
        Assert.Single(h.Repository.Rows);
    }

    [Fact]
    public async Task The_same_code_in_another_tenant_is_fine()
    {
        var h = Build();
        h.Repository.Rows.Add(new ImportContainer
        {
            TenantId = Other, Code = "SH-2024/01", NormalizedCode = "sh202401",
        });

        var result = await h.Create.Handle(
            new CreateContainerCommand("SH-2024/01", "", 0), default);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// ⚠️ <b>القص قبل التطبيع، زي القديم بالحرف.</b>
    ///
    /// <para>التطبيع بيشيل الشرطات، فرمز ٥٠ حرف بشرطات ممكن يطلع
    /// مطبَّعه ٣٠. ولو طبّعنا الأول وقصّينا بعدين، الرمز المخزّن كان
    /// هيبقى أطول من اللي القديم بيخزّنه لنفس الإدخال.</para>
    /// </summary>
    [Fact]
    public async Task A_long_code_is_truncated_before_normalising()
    {
        var h = Build();
        string long50 = new('A', 50);

        var result = await h.Create.Handle(
            new CreateContainerCommand(long50, "", 0), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(40, result.Value!.Code.Length);
        Assert.Equal(40, h.Repository.Rows.Single().NormalizedCode.Length);
    }

    [Fact]
    public async Task A_long_name_is_truncated_to_the_column_width()
    {
        var h = Build();

        await h.Create.Handle(
            new CreateContainerCommand("SH1", new string('ا', 200), 0), default);

        Assert.Equal(120, h.Repository.Rows.Single().Name.Length);
    }

    [Fact]
    public async Task A_rejected_create_saves_nothing()
    {
        var h = Build();

        await h.Create.Handle(new CreateContainerCommand("-", "", 0), default);

        Assert.Equal(0, h.UnitOfWork.Saves);
    }

    // =================================================================
    //  التعديل
    // =================================================================

    private static async Task<(Harness H, Guid Id)> WithOneContainer()
    {
        var h = Build();
        var created = await h.Create.Handle(
            new CreateContainerCommand("SH-2024/01", "شحنة يناير", 1), default);
        return (h, created.Value!.Id);
    }

    /// <summary>
    /// 🔴 <b>الرمز مابيتغيّرش من التعديل — الأمر مافيهوش <c>Code</c>
    /// أصلاً.</b>
    ///
    /// <para>الرمز مطبوع على الشحنة وموجود على لابات اتفحصت خلاص؛
    /// تغييره بيخلّي اللي ماسك ورقة الاستيراد مايلاقيش حاجة.</para>
    /// </summary>
    [Fact]
    public async Task Updating_never_changes_the_code()
    {
        var (h, id) = await WithOneContainer();

        var result = await h.Update.Handle(
            new UpdateContainerCommand(id, "اسم جديد", 5, false), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("SH-2024/01", result.Value!.Code);
        Assert.Equal("sh202401", h.Repository.Rows.Single().NormalizedCode);
    }

    /// <summary>
    /// ⚠️ <b>الاسم الفاضي بيمسح الاسم — بخلاف الأقسام.</b>
    ///
    /// <para>الفرق مقصود وهو سلوك القديم: اسم القسم هو هويته، أما
    /// اسم الحاوية فوصف اختياري (الرمز هو المفتاح). فتفضيته حاجة
    /// مشروعة: «شلت الوصف الغلط».</para>
    /// </summary>
    [Fact]
    public async Task An_empty_name_clears_the_description()
    {
        var (h, id) = await WithOneContainer();

        var result = await h.Update.Handle(
            new UpdateContainerCommand(id, "   ", null, null), default);

        Assert.Equal("", result.Value!.Name);
    }

    /// <summary>
    /// ⚠️ والترتيب والتفعيل بيتغيّروا لو اتبعتوا بس.
    ///
    /// <para>🔴 <b>والفحص على الصف نفسه، مش على الرد.</b> العقد
    /// مافيهوش <c>SortOrder</c> — هو بيرجّع اللي الواجهة بتعرضه بس.
    /// فلو فحصنا الرد، ماكناش هنفحص حاجة خالص.</para>
    /// </summary>
    [Fact]
    public async Task A_missing_flag_leaves_the_old_value_alone()
    {
        var (h, id) = await WithOneContainer();

        var result = await h.Update.Handle(
            new UpdateContainerCommand(id, "اسم", null, null), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsActive);

        var row = h.Repository.Rows.Single();
        Assert.Equal(1, row.SortOrder);      // كان ١ وقت الإنشاء
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task Deactivating_a_container_works()
    {
        var (h, id) = await WithOneContainer();

        var result = await h.Update.Handle(
            new UpdateContainerCommand(id, null, null, false), default);

        Assert.False(result.Value!.IsActive);
    }

    [Fact]
    public async Task A_container_from_another_tenant_is_not_found()
    {
        var h = Build();
        var foreign = new ImportContainer { TenantId = Other, Code = "X", NormalizedCode = "x" };
        h.Repository.Rows.Add(foreign);

        var result = await h.Update.Handle(
            new UpdateContainerCommand(foreign.Id, "محاولة", null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error.StatusCode);
    }

    [Fact]
    public async Task An_unknown_container_is_not_found()
    {
        var h = Build();

        var result = await h.Get.Handle(new GetContainerQuery(Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContainerErrors.NotFound.Code, result.Error.Code);
    }

    // =================================================================
    //  البحث
    // =================================================================

    /// <summary>
    /// 🔴 البحث بأي شكل مكتوب بيلاقي الحاوية.
    /// </summary>
    [Theory]
    [InlineData("sh-2024-01")]
    [InlineData("SH202401")]
    [InlineData("2024")]
    public async Task Search_finds_the_container_however_it_is_typed(string typed)
    {
        var (h, _) = await WithOneContainer();

        var rows = await h.Repository.ListAsync(Tenant, typed);

        Assert.Single(rows);
    }

    /// <summary>
    /// ⚠️ وبحث مفيهوش ولا حرف ولا رقم بيتجاهل — <b>مش بيرجّع
    /// فاضي</b>.
    ///
    /// <para>لو رجّع فاضي، المستخدم اللي كتب شرطة بالغلط بيلاقي
    /// القايمة فضيت ويفتكر إن مفيش حاويات.</para>
    /// </summary>
    [Fact]
    public async Task A_junk_search_is_ignored_rather_than_returning_nothing()
    {
        var (h, _) = await WithOneContainer();

        Assert.Single(await h.Repository.ListAsync(Tenant, "---"));
    }
}
