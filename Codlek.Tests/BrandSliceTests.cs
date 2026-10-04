using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Brands;
using Codlek.Application.Features.Brands;
using Codlek.Application.Features.Brands.AddAlias;
using Codlek.Application.Features.Brands.CreateBrand;
using Codlek.Application.Features.Brands.GetUnknownBrands;
using Codlek.Application.Features.Brands.RemoveAlias;
using Codlek.Application.Features.Brands.UpdateBrand;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;

namespace Codlek.Tests;

/// <summary>
/// ماركات اللابات وأسماءها البديلة.
///
/// <para>🔴 <b>القايمة دي شرط لقاعدة «الفني ده لماركات معيّنة».</b>
/// فأي خلل في حلّ الماركة بيبان كـ«الفني مش عارف يفتح أمر صيانة من
/// حقه» — أو أسوأ، «فني فتح أمر مش من حقه».</para>
/// </summary>
public class BrandSliceTests
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

    private sealed class FakeRepository : IBrandRepository
    {
        public readonly List<LaptopBrand> Brands = [];
        public readonly List<LaptopBrandAlias> Aliases = [];
        public readonly Dictionary<Guid, int> TechnicianCounts = [];
        public readonly List<(string Name, int Count)> Manufacturers = [];

        public Task<IReadOnlyList<BrandRow>> ListAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BrandRow>>(
                Brands.Where(b => b.TenantId == tenantId)
                    .OrderBy(b => b.SortOrder).ThenBy(b => b.Name)
                    .Select(b => new BrandRow(
                        b.Id, b.Name, b.IsActive, b.SortOrder,
                        Aliases.Where(a => a.BrandId == b.Id)
                               .OrderBy(a => a.RawValue).Select(a => a.RawValue).ToList(),
                        TechnicianCounts.GetValueOrDefault(b.Id)))
                    .ToList());

        public Task<LaptopBrand?> FindWithAliasesAsync(
            Guid tenantId, Guid id, CancellationToken ct = default) => FindAsync(tenantId, id, ct);

        public Task<LaptopBrand?> FindAsync(
            Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Brands.FirstOrDefault(b => b.Id == id && b.TenantId == tenantId));

        public Task<bool> NameTakenAsync(
            Guid tenantId, string normalizedName, Guid? exceptId = null,
            CancellationToken ct = default) =>
            Task.FromResult(Brands.Any(
                b => b.TenantId == tenantId && b.NormalizedName == normalizedName
                     && (exceptId == null || b.Id != exceptId)));

        public Task<LaptopBrand?> FindByNormalizedNameAsync(
            Guid tenantId, string normalizedName, Guid? exceptId = null,
            CancellationToken ct = default) =>
            Task.FromResult(Brands.FirstOrDefault(
                b => b.TenantId == tenantId && b.NormalizedName == normalizedName
                     && (exceptId == null || b.Id != exceptId)));

        public Task<(Guid BrandId, string BrandName)?> FindAliasOwnerAsync(
            Guid tenantId, string normalizedValue, CancellationToken ct = default)
        {
            var alias = Aliases.FirstOrDefault(
                a => a.TenantId == tenantId && a.NormalizedValue == normalizedValue);

            if (alias is null) return Task.FromResult<(Guid, string)?>(null);

            string name = Brands.First(b => b.Id == alias.BrandId).Name;
            return Task.FromResult<(Guid, string)?>((alias.BrandId, name));
        }

        public Task<LaptopBrandAlias?> FindAliasAsync(
            Guid tenantId, Guid brandId, string normalizedValue,
            CancellationToken ct = default) =>
            Task.FromResult(Aliases.FirstOrDefault(
                a => a.TenantId == tenantId && a.BrandId == brandId
                     && a.NormalizedValue == normalizedValue));

        public void Add(LaptopBrand brand) => Brands.Add(brand);

        public void AddAlias(LaptopBrandAlias alias) => Aliases.Add(alias);

        public void RemoveAlias(LaptopBrandAlias alias) => Aliases.Remove(alias);

        public Task<int> CountTechniciansAsync(Guid brandId, CancellationToken ct = default) =>
            Task.FromResult(TechnicianCounts.GetValueOrDefault(brandId));

        public Task<IReadOnlyList<string>> AliasValuesAsync(
            Guid brandId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(
                Aliases.Where(a => a.BrandId == brandId)
                       .OrderBy(a => a.RawValue).Select(a => a.RawValue).ToList());

        public Task<IReadOnlyList<BrandRule>> RulesAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BrandRule>>(
                Brands.Where(b => b.TenantId == tenantId)
                    .Select(b => new BrandRule(
                        b.Id, b.Name,
                        Aliases.Where(a => a.BrandId == b.Id).Select(a => a.RawValue).ToList()))
                    .ToList());

        public Task<IReadOnlyList<(string Name, int Count)>> RawManufacturersAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<(string, int)>>(Manufacturers);
    }

    private sealed class FakeAudit : IAuditTrail
    {
        public readonly List<string> Actions = [];

        public void Record(
            string action, string entityType, Guid? entityId,
            string entityCode, string summary) => Actions.Add(action);

        /// <summary>⚠️ مسار الراكة — القطاع ده مابيستعملهوش.</summary>
        public void RecordForRack(
            RackAuditActor actor, string action, string entityType, Guid? entityId,
            string entityCode, string summary, string dataJson = "") =>
            Actions.Add(action);
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
            Func<CancellationToken, Task<T>> work, CancellationToken ct = default) => work(ct);
    }

    private sealed record Harness(
        FakeRepository Repository,
        FakeAudit Audit,
        FakeUnitOfWork UnitOfWork,
        CreateBrandCommandHandler Create,
        UpdateBrandCommandHandler Update,
        AddAliasCommandHandler AddAlias,
        RemoveAliasCommandHandler RemoveAlias,
        GetUnknownBrandsQueryHandler Unknown);

    private static Harness Build()
    {
        var repository = new FakeRepository();
        var audit = new FakeAudit();
        var unitOfWork = new FakeUnitOfWork();
        var me = new FakeUser();

        return new Harness(
            repository, audit, unitOfWork,
            new CreateBrandCommandHandler(repository, audit, unitOfWork, me),
            new UpdateBrandCommandHandler(repository, audit, unitOfWork, me),
            new AddAliasCommandHandler(repository, audit, unitOfWork, me),
            new RemoveAliasCommandHandler(repository, audit, unitOfWork, me),
            new GetUnknownBrandsQueryHandler(repository, me));
    }

    private static async Task<(Harness H, Guid Id)> WithBrand(string name = "HP")
    {
        var h = Build();
        var created = await h.Create.Handle(new CreateBrandCommand(name, 0), default);
        return (h, created.Value!.Id);
    }

    // =================================================================
    //  الإنشاء
    // =================================================================

    [Fact]
    public async Task Creating_a_brand_stores_the_name_and_its_key()
    {
        var h = Build();

        var result = await h.Create.Handle(new CreateBrandCommand("  HP  ", 2), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("HP", result.Value!.Name);
        Assert.Equal(BrandToken.Normalize("HP"), h.Repository.Brands.Single().NormalizedName);
        Assert.Equal(Tenant, h.Repository.Brands.Single().TenantId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_brand_name_is_rejected(string name)
    {
        var h = Build();

        var result = await h.Create.Handle(new CreateBrandCommand(name, 0), default);

        Assert.True(result.IsFailure);
        Assert.Equal(BrandErrors.NameRequired.Code, result.Error.Code);
        Assert.Equal(0, h.UnitOfWork.Saves);
    }

    /// <summary>
    /// 🔴 <b>التكرار بيتقاس على الشكل المطبَّع مش الخام.</b>
    ///
    /// <para>«hp» و«HP » نفس الماركة. ولو اتسجّلوا مرتين، حل اللاب
    /// بيبقى معتمد على ترتيب الصفوف.</para>
    /// </summary>
    [Theory]
    [InlineData("hp")]
    [InlineData("HP ")]
    [InlineData(" Hp")]
    public async Task The_same_brand_written_differently_is_a_duplicate(string second)
    {
        var (h, _) = await WithBrand("HP");

        var again = await h.Create.Handle(new CreateBrandCommand(second, 0), default);

        Assert.True(again.IsFailure);
        Assert.Single(h.Repository.Brands);
    }

    [Fact]
    public async Task The_same_brand_in_another_tenant_is_fine()
    {
        var h = Build();
        h.Repository.Brands.Add(new LaptopBrand
        {
            TenantId = Other, Name = "HP", NormalizedName = BrandToken.Normalize("HP"),
        });

        Assert.True((await h.Create.Handle(new CreateBrandCommand("HP", 0), default)).IsSuccess);
    }

    [Fact]
    public async Task Creating_a_brand_is_audited()
    {
        var (h, _) = await WithBrand();

        Assert.Equal(AuditActions.BrandCreated, h.Audit.Actions.Single());
    }

    // =================================================================
    //  التعديل
    // =================================================================

    [Fact]
    public async Task Renaming_a_brand_updates_its_key_too()
    {
        var (h, id) = await WithBrand("HP");

        var result = await h.Update.Handle(
            new UpdateBrandCommand(id, "Hewlett-Packard", null, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hewlett-Packard", result.Value!.Name);
        Assert.Equal(
            BrandToken.Normalize("Hewlett-Packard"),
            h.Repository.Brands.Single().NormalizedName);
    }

    /// <summary>
    /// 🔴 <b>ده فرق عن القديم — وهو إصلاح.</b>
    ///
    /// <para>القديم كان بيكتب الاسم على طول من غير أي فحص. فماركتين
    /// بنفس الاسم المطبَّع كان ممكن يتعملوا بالتعديل: تعمل «HP»،
    /// وتعدّل «Dell» وتسمّيها «hp». وساعتها حل اللاب بيبقى معتمد على
    /// ترتيب الصفوف.</para>
    ///
    /// <para>⚠️ والفرق آمن: الطلب اللي كان بينجح <b>غلط</b> بقى
    /// بيترفض برسالة مفهومة. مفيش طلب سليم بيترفض.</para>
    /// </summary>
    [Fact]
    public async Task Renaming_a_brand_onto_another_brands_name_is_rejected()
    {
        var (h, _) = await WithBrand("HP");
        var dell = await h.Create.Handle(new CreateBrandCommand("Dell", 0), default);

        var result = await h.Update.Handle(
            new UpdateBrandCommand(dell.Value!.Id, "hp", null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(BrandErrors.NameTaken("hp").Code, result.Error.Code);
        Assert.Equal("Dell", h.Repository.Brands.Single(b => b.Id == dell.Value.Id).Name);
    }

    /// <summary>⚠️ وإعادة تسمية الماركة لاسمها هي بتعدّي.</summary>
    [Fact]
    public async Task Renaming_a_brand_to_its_own_name_is_allowed()
    {
        var (h, id) = await WithBrand("HP");

        var result = await h.Update.Handle(new UpdateBrandCommand(id, "HP", 5, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.SortOrder);
    }

    [Fact]
    public async Task An_empty_name_on_update_leaves_the_old_one()
    {
        var (h, id) = await WithBrand("HP");

        var result = await h.Update.Handle(new UpdateBrandCommand(id, "  ", 3, null), default);

        Assert.Equal("HP", result.Value!.Name);
        Assert.Equal(3, result.Value.SortOrder);
    }

    [Fact]
    public async Task A_brand_from_another_tenant_is_not_found()
    {
        var h = Build();
        var foreign = new LaptopBrand { TenantId = Other, Name = "X", NormalizedName = "x" };
        h.Repository.Brands.Add(foreign);

        var result = await h.Update.Handle(
            new UpdateBrandCommand(foreign.Id, "محاولة", null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error.StatusCode);
    }

    // =================================================================
    //  الأسماء البديلة
    // =================================================================

    [Fact]
    public async Task Adding_an_alias_stores_both_forms()
    {
        var (h, id) = await WithBrand("HP");

        var result = await h.AddAlias.Handle(
            new AddAliasCommand(id, "  Hewlett-Packard  "), default);

        Assert.True(result.IsSuccess);
        var alias = h.Repository.Aliases.Single();
        Assert.Equal("Hewlett-Packard", alias.RawValue);
        Assert.Equal(BrandToken.Normalize("Hewlett-Packard"), alias.NormalizedValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_alias_is_rejected(string value)
    {
        var (h, id) = await WithBrand();

        var result = await h.AddAlias.Handle(new AddAliasCommand(id, value), default);

        Assert.True(result.IsFailure);
        Assert.Equal(BrandErrors.AliasRequired.Code, result.Error.Code);
    }

    /// <summary>
    /// 🔴 <b>الاسم البديل فريد على مستوى الشركة كلها، مش جوّه
    /// الماركة.</b>
    ///
    /// <para>لو ماركتين ادّعوا «HPQ»، حل اللاب بيبقى معتمد على ترتيب
    /// الصفوف — ونفس اللاب يتحل لماركة مختلفة بعد أي تغيير في
    /// الترتيب.</para>
    /// </summary>
    [Fact]
    public async Task An_alias_already_used_by_another_brand_is_rejected()
    {
        var (h, hp) = await WithBrand("HP");
        var dell = await h.Create.Handle(new CreateBrandCommand("Dell", 0), default);
        await h.AddAlias.Handle(new AddAliasCommand(hp, "HPQ"), default);

        var result = await h.AddAlias.Handle(
            new AddAliasCommand(dell.Value!.Id, "hpq"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(BrandErrors.AliasTakenBy("x", "y").Code, result.Error.Code);

        // ⚠️ والرسالة بتقول الماركة اللي ماسكة الاسم بالاسم.
        Assert.Contains("HP", result.Error.Description);
        Assert.Single(h.Repository.Aliases);
    }

    /// <summary>
    /// ⚠️ والاسم اللي هو نفسه اسم ماركة تانية بيترفض — <b>غير كده
    /// الاسم بيبقى ليه معنيين</b>.
    /// </summary>
    [Fact]
    public async Task An_alias_that_is_another_brands_name_is_rejected()
    {
        var (h, hp) = await WithBrand("HP");
        await h.Create.Handle(new CreateBrandCommand("Dell", 0), default);

        var result = await h.AddAlias.Handle(new AddAliasCommand(hp, "dell"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(BrandErrors.AliasIsAnotherBrandName("x", "y").Code, result.Error.Code);
        Assert.Empty(h.Repository.Aliases);
    }

    /// <summary>
    /// ⚠️ <b>بس اسم الماركة نفسها كاسم بديل ليها بيعدّي.</b>
    ///
    /// <para>مالوش لازمة بس مش غلط — والمنع كان هيحتاج رسالة تشرح
    /// حاجة محدش بيسأل عنها.</para>
    /// </summary>
    [Fact]
    public async Task A_brands_own_name_as_its_own_alias_is_allowed()
    {
        var (h, hp) = await WithBrand("HP");

        Assert.True((await h.AddAlias.Handle(new AddAliasCommand(hp, "HP"), default)).IsSuccess);
    }

    [Fact]
    public async Task Adding_an_alias_to_an_unknown_brand_is_not_found()
    {
        var h = Build();

        var result = await h.AddAlias.Handle(new AddAliasCommand(Guid.NewGuid(), "X"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error.StatusCode);
    }

    /// <summary>
    /// 🔴 والشيل بأي شكل مكتوب — البحث على المطبَّع.
    ///
    /// <para>⚠️ <b>بس تطبيع الماركات بيحافظ على فصل الكلمات</b> —
    /// الشرطة بتبقى مسافة مش بتتشال. يعني <c>HEWLETTPACKARD</c>
    /// (ملزوقة) <b>مش</b> نفس الشكل.</para>
    ///
    /// <para>🔴 <b>وده مختلف عن <c>ContainerCode.Normalize</c> عن
    /// قصد.</b> رمز الحاوية بيتشال منه كل حاجة مش حرف ولا رقم، لأنه
    /// رمز مش اسم. واسم الماركة كلمات، ودمجها كان هيخلّي ماركة قصيرة
    /// تطابق حاجات مالهاش علاقة.</para>
    /// </summary>
    [Theory]
    [InlineData("Hewlett-Packard")]
    [InlineData("hewlett packard")]
    [InlineData("  HEWLETT   PACKARD  ")]
    [InlineData("Hewlett_Packard")]
    public async Task Removing_an_alias_works_however_it_is_typed(string typed)
    {
        var (h, id) = await WithBrand("HP");
        await h.AddAlias.Handle(new AddAliasCommand(id, "Hewlett-Packard"), default);

        var result = await h.RemoveAlias.Handle(new RemoveAliasCommand(id, typed), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(h.Repository.Aliases);
    }

    [Fact]
    public async Task Removing_an_alias_that_does_not_exist_is_not_found()
    {
        var (h, id) = await WithBrand();

        var result = await h.RemoveAlias.Handle(new RemoveAliasCommand(id, "مش موجود"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error.StatusCode);
    }

    /// <summary>
    /// ⚠️ <b>والسجل بيشيل الاسم الخام مش المطبَّع.</b>
    ///
    /// <para>اللي بيقرا السجل عايز يشوف «Hewlett-Packard» مش
    /// «hewlettpackard».</para>
    /// </summary>
    [Fact]
    public async Task Removing_an_alias_is_audited()
    {
        var (h, id) = await WithBrand("HP");
        await h.AddAlias.Handle(new AddAliasCommand(id, "Hewlett-Packard"), default);

        await h.RemoveAlias.Handle(new RemoveAliasCommand(id, "hewlett packard"), default);

        Assert.Equal(AuditActions.BrandAliasRemoved, h.Audit.Actions.Last());
    }

    // =================================================================
    //  الماركات المش معروفة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الشاشة اللي بتخلّي «المش معروفة بتعدّي» قرار آمن.</b>
    ///
    /// <para>من غيرها، لاب بماركة جديدة بيعدّي من القيد في صمت للأبد
    /// — وصاحب الشغل مش هيعرف إن فيه ماركة ناقصة إلا بالصدفة.</para>
    /// </summary>
    [Fact]
    public async Task Unknown_lists_manufacturers_with_no_matching_brand()
    {
        var (h, _) = await WithBrand("HP");
        h.Repository.Manufacturers.AddRange([("HP", 5), ("Acer", 3), ("Toshiba", 9)]);

        var result = await h.Unknown.Handle(new GetUnknownBrandsQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value!, x => x.Name == "HP");
        Assert.Contains(result.Value!, x => x.Name == "Acer");
        Assert.Contains(result.Value!, x => x.Name == "Toshiba");
    }

    /// <summary>
    /// ⚠️ <b>واسم بديل معروف بيخرج من القايمة.</b>
    ///
    /// <para>ودي الحاجة اللي الشاشة موجودة عشانها: صاحب الشغل بيضيف
    /// «Hewlett-Packard» كاسم بديل، والسطر بيختفي.</para>
    /// </summary>
    [Fact]
    public async Task A_resolved_alias_disappears_from_unknown()
    {
        var (h, id) = await WithBrand("HP");
        h.Repository.Manufacturers.Add(("Hewlett-Packard", 4));

        var before = await h.Unknown.Handle(new GetUnknownBrandsQuery(), default);
        Assert.Contains(before.Value!, x => x.Name == "Hewlett-Packard");

        await h.AddAlias.Handle(new AddAliasCommand(id, "Hewlett-Packard"), default);

        var after = await h.Unknown.Handle(new GetUnknownBrandsQuery(), default);
        Assert.DoesNotContain(after.Value!, x => x.Name == "Hewlett-Packard");
    }

    /// <summary>⚠️ والترتيب بالعدد — صاحب الشغل يبدأ بالأكتر.</summary>
    [Fact]
    public async Task Unknown_is_ordered_by_device_count()
    {
        var h = Build();
        h.Repository.Manufacturers.AddRange([("Acer", 3), ("Toshiba", 9), ("MSI", 5)]);

        var result = await h.Unknown.Handle(new GetUnknownBrandsQuery(), default);

        Assert.Equal(["Toshiba", "MSI", "Acer"], result.Value!.Select(x => x.Name));
    }

    /// <summary>
    /// 🔴 <b>تطبيع الماركات وتطبيع رمز الحاوية مش واحد — وده
    /// مقصود.</b>
    ///
    /// <para>⚠️ الفحص ده موجود عشان حد ميروحش يوحّدهم «عشان يبقوا
    /// متسقين».</para>
    /// </summary>
    [Fact]
    public void Brand_and_container_normalisation_are_deliberately_different()
    {
        Assert.Equal("HEWLETT PACKARD", BrandToken.Normalize("Hewlett-Packard"));
        Assert.Equal("sh202401", Codlek.Core.Text.ContainerCode.Normalize("SH-2024/01"));
    }
}
