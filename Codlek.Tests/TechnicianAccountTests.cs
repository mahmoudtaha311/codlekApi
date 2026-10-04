using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Features.TechnicianAccounts.ActivateTechnician;
using Codlek.Application.Features.TechnicianAccounts.CreateTechnician;
using Codlek.Application.Features.TechnicianAccounts.GetAccounts;
using Codlek.Application.Features.TechnicianAccounts.ResetPassword;
using Codlek.Application.Features.TechnicianAccounts.SetBrands;
using Codlek.Application.Features.TechnicianAccounts.SuspendTechnician;
using Codlek.Application.Features.TechnicianAccounts.UpdateTechnician;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// حسابات الفنيين.
///
/// <para>🔴 <b>تلات قواعد لازم تتنفّذ مع بعض في كل مرة:</b> الاسم
/// فريد <b>جوّه الشركة</b>، والكود ثابت بعد الإنشاء، ونسخة البيانات
/// بتزيد مع كل تغيير باسورد أو إيقاف.</para>
///
/// <para>🔴 <b>والنسيان هنا مش خطأ ظاهر:</b> نسخة محفوظة على راكة
/// أوفلاين بتفضل صالحة بعد ما المدير يعتقد إنه ألغاها.</para>
/// </summary>
public class TechnicianAccountTests
{
    /// <summary>بصمة مزيّفة — الفحوص بتقيس القرار مش التشفير.</summary>
    private sealed class FakePasswords : ITechnicianPasswords
    {
        public int Calls;

        public (string Hash, string Salt) Create(string password)
        {
            Calls++;
            return ("hash:" + password, "salt:" + Calls);
        }

        public bool Verify(string password, string storedHash, string storedSalt) =>
            storedHash == "hash:" + password;
    }

    private sealed class FakeAccountRepository : ITechnicianAccountRepository
    {
        public readonly List<Technician> Technicians = [];
        public readonly List<TechnicianBrand> Links = [];
        public readonly HashSet<Guid> KnownBrands = [];
        public readonly Dictionary<Guid, string> BrandNames = [];

        /// <summary>
        /// ⚠️ أكواد بتتحجز عشان نقيس حلقة توليد الكود.
        /// </summary>
        public readonly HashSet<string> ReservedCodes = [];

        public Task<IReadOnlyList<Technician>> ListAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Technician>>(
                Technicians.Where(x => x.TenantId == t)
                    .OrderBy(x => x.DisplayName)
                    .ToList());

        public Task<Technician?> FindAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Technicians.FirstOrDefault(x => x.Id == id && x.TenantId == t));

        public Task<bool> UsernameTakenAsync(
            Guid t, string normalized, CancellationToken ct = default) =>
            Task.FromResult(Technicians.Any(x =>
                x.TenantId == t && x.NormalizedUsername == normalized));

        public Task<bool> CodeTakenAsync(Guid t, string code, CancellationToken ct = default) =>
            Task.FromResult(
                ReservedCodes.Contains(code)
                || Technicians.Any(x => x.TenantId == t && x.Code == code));

        public void Add(Technician technician) => Technicians.Add(technician);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> BrandLinksAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(
                Links.Where(l => l.TenantId == t)
                    .GroupBy(l => l.TechnicianId)
                    .ToDictionary(
                        g => g.Key,
                        g => (IReadOnlyList<Guid>)g.Select(l => l.BrandId).ToList()));

        public Task<IReadOnlyList<TechnicianBrand>> BrandLinksForAsync(
            Guid t, Guid technicianId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TechnicianBrand>>(
                Links.Where(l => l.TenantId == t && l.TechnicianId == technicianId).ToList());

        public Task<int> CountKnownBrandsAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult(ids.Count(KnownBrands.Contains));

        public Task<IReadOnlyList<string>> BrandNamesAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(
                ids.Where(BrandNames.ContainsKey)
                    .Select(id => BrandNames[id])
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToList());

        public void RemoveBrandLinks(IEnumerable<TechnicianBrand> links)
        {
            foreach (var link in links.ToList()) Links.Remove(link);
        }

        public void AddBrandLink(TechnicianBrand link) => Links.Add(link);
    }

    private sealed record Harness(
        FakeAccountRepository Repo,
        FakePasswords Passwords,
        FakeAuditTrail Audit,
        FakeUnitOfWork Work,
        FakeCurrentUser Me,
        CreateTechnicianCommandHandler Create,
        ResetTechnicianPasswordCommandHandler Reset,
        SuspendTechnicianCommandHandler Suspend,
        ActivateTechnicianCommandHandler Activate,
        UpdateTechnicianCommandHandler Update,
        SetTechnicianBrandsCommandHandler Brands,
        GetTechnicianAccountsQueryHandler List);

    private static Harness Build()
    {
        var repo = new FakeAccountRepository();
        var passwords = new FakePasswords();
        var audit = new FakeAuditTrail();
        var work = new FakeUnitOfWork();
        var me = new FakeCurrentUser(UserRole.Manager);

        return new Harness(
            repo, passwords, audit, work, me,
            new CreateTechnicianCommandHandler(
                repo, passwords, audit, work, me,
                NullLogger<CreateTechnicianCommandHandler>.Instance),
            new ResetTechnicianPasswordCommandHandler(
                repo, passwords, audit, work, me,
                NullLogger<ResetTechnicianPasswordCommandHandler>.Instance),
            new SuspendTechnicianCommandHandler(repo, audit, work, me),
            new ActivateTechnicianCommandHandler(repo, audit, work, me),
            new UpdateTechnicianCommandHandler(repo, audit, work, me),
            new SetTechnicianBrandsCommandHandler(repo, audit, work, me),
            new GetTechnicianAccountsQueryHandler(repo, me));
    }

    private static Technician Existing(
        Harness h, string name = "محمود", string username = "mahmoud",
        bool isActive = true, int credentialVersion = 1,
        TechnicianSpecialty specialty = TechnicianSpecialty.None,
        bool canTest = true, bool canRepair = false)
    {
        var row = new Technician
        {
            TenantId = h.Me.TenantId,
            Code = "T001",
            DisplayName = name,
            Username = username,
            NormalizedUsername = LoginName.Normalize(username),
            PasswordHash = "old",
            Salt = "old",
            IsActive = isActive,
            CredentialVersion = credentialVersion,
            Specialty = specialty,
            CanTest = canTest,
            CanRepair = canRepair,
        };

        h.Repo.Technicians.Add(row);
        return row;
    }

    // =================================================================
    //  الإنشاء
    // =================================================================

    [Fact]
    public async Task Creating_an_account_returns_the_initial_password_once()
    {
        var h = Build();

        var result = await h.Create.Handle(
            new CreateTechnicianCommand("محمود الفني", "mahmoud", "1234", 4, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("1234", result.Value.Password);

        var created = Assert.Single(h.Repo.Technicians);

        Assert.Equal("محمود الفني", created.DisplayName);
        Assert.Equal("mahmoud", created.Username);
        Assert.Equal(LoginName.Normalize("mahmoud"), created.NormalizedUsername);
        Assert.Equal(TechnicianSpecialty.Screens, created.Specialty);
        Assert.True(created.IsActive);

        // ⚠️ الباسورد ده المدير حطّه، فالفني لازم يغيّره أول دخول.
        Assert.True(created.MustChangePassword);

        // ⚠️ والبصمة من الخدمة، مش من المعالج.
        Assert.Equal("hash:1234", created.PasswordHash);

        Assert.Equal(1, h.Work.Saves);

        var line = Assert.Single(h.Audit.Lines);
        Assert.Equal("technician.created", line.Action);
        Assert.Equal("Technician", line.EntityType);

        // 🔴 والباسورد الأولي عمره ما بيدخل السجل.
        Assert.DoesNotContain("1234", line.Summary);
    }

    /// <summary>
    /// ⚠️ والكود بيتولّد من السيرفر — مش من الطلب، ومش مكرّر.
    /// </summary>
    [Fact]
    public async Task The_code_is_generated_and_never_collides()
    {
        var h = Build();

        // ⚠️ نحجز كود موجود عشان الحلقة تدوّر على غيره.
        Existing(h);
        h.Repo.ReservedCodes.Add("T001");

        var result = await h.Create.Handle(
            new CreateTechnicianCommand("كريم", "karim", "1234", null, null), default);

        Assert.True(result.IsSuccess);

        var created = h.Repo.Technicians.Single(t => t.DisplayName == "كريم");

        Assert.NotEqual("T001", created.Code);
        Assert.Equal(6, created.Code.Length);
    }

    /// <summary>
    /// 🔴 <b>الاسم فريد <u>جوّه الشركة</u>، مش عالمياً — وده عكس
    /// مستخدمي اللوحة بالظبط.</b>
    ///
    /// <para>الفني بيدخل من راكة متحققة بمفتاحها، والشركة معروفة
    /// <b>من الراكة</b> قبل ما الاسم يتقرا — فشركتين ينفع يبقى
    /// عندهم «ahmed».</para>
    /// </summary>
    [Fact]
    public async Task The_username_is_unique_inside_the_tenant_only()
    {
        var h = Build();

        Existing(h, username: "ahmed");

        var sameTenant = await h.Create.Handle(
            new CreateTechnicianCommand("أحمد تاني", "AHMED", "1234", null, null), default);

        Assert.True(sameTenant.IsFailure);
        Assert.Equal("technician.username_taken", sameTenant.Error.Code);
        Assert.Contains("في الشركة دي", sameTenant.Error.Description);

        // ⚠️ وشركة تانية بنفس الاسم بتعدّي.
        h.Me.TenantId = Guid.NewGuid();

        var otherTenant = await h.Create.Handle(
            new CreateTechnicianCommand("أحمد تالت", "ahmed", "1234", null, null), default);

        Assert.True(otherTenant.IsSuccess);
    }

    [Theory]
    [InlineData("م", "mahmoud", "1234", "technician.name_too_short")]
    [InlineData("محمود", "ma", "1234", "technician.username_too_short")]
    [InlineData("محمود", "mahmoud", "123", "technician.password_too_short")]
    public async Task The_minimum_lengths_are_enforced(
        string name, string username, string password, string code)
    {
        var h = Build();

        var result = await h.Create.Handle(
            new CreateTechnicianCommand(name, username, password, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);

        // ⚠️ ومفيش حفظ ولا سطر سجل على محاولة وقعت.
        Assert.Empty(h.Repo.Technicians);
        Assert.Equal(0, h.Work.Saves);
        Assert.Empty(h.Audit.Lines);
    }

    /// <summary>
    /// 🔴 <b>التخصص لازم يكون من القايمة — نفس ثقب
    /// <c>(UserRole)99</c>.</b>
    /// </summary>
    [Fact]
    public async Task An_unknown_specialty_is_refused_not_stored()
    {
        var h = Build();

        var result = await h.Create.Handle(
            new CreateTechnicianCommand("محمود", "mahmoud", "1234", 99, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("technician.unknown_specialty", result.Error.Code);
        Assert.Empty(h.Repo.Technicians);
    }

    // =================================================================
    //  نسخة البيانات — أهم رقم في الملف
    // =================================================================

    /// <summary>
    /// 🔴 <b>الزيادة هي اللي بتوصل للراكات الأوفلاين.</b>
    ///
    /// <para>النسخة المحفوظة على الراكة شايلة الرقم القديم؛ وأول ما
    /// الراكة تتصل وتلاقي رقم أحدث بتمسح نسختها. ومن غير الزيادة،
    /// تغيير الباسورد على اللوحة مكانش هيمنع الدخول بالقديم على
    /// راكة أوفلاين.</para>
    /// </summary>
    [Fact]
    public async Task Resetting_the_password_bumps_the_credential_version()
    {
        var h = Build();
        var tech = Existing(h, credentialVersion: 3);

        var result = await h.Reset.Handle(
            new ResetTechnicianPasswordCommand(tech.Id, "5678"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("5678", result.Value.Password);

        Assert.Equal("hash:5678", tech.PasswordHash);
        Assert.Equal(4, tech.CredentialVersion);
        Assert.True(tech.MustChangePassword);

        Assert.Equal(1, h.Work.Saves);
        Assert.Equal("technician.password_reset", Assert.Single(h.Audit.Lines).Action);
    }

    /// <summary>
    /// 🔴 <b>والإيقاف بيزوّد الرقم كمان:</b> لازم يلغي النسخ
    /// المحفوظة، مش بس يمنع الدخول الجديد.
    /// </summary>
    [Fact]
    public async Task Suspending_bumps_the_version_and_records_who_and_why()
    {
        var h = Build();
        var tech = Existing(h, credentialVersion: 2);

        var result = await h.Suspend.Handle(
            new SuspendTechnicianCommand(tech.Id, "  غياب متكرر  "), default);

        Assert.True(result.IsSuccess);

        Assert.False(tech.IsActive);
        Assert.Equal("غياب متكرر", tech.SuspendedReason);
        Assert.Equal("كريم", tech.SuspendedByName);
        Assert.NotNull(tech.SuspendedAtUtc);
        Assert.Equal(3, tech.CredentialVersion);

        // ⚠️ والسبب في السجل كمان — اللي بيراجع بيشوفه.
        Assert.Contains("غياب متكرر", Assert.Single(h.Audit.Lines).Summary);
    }

    /// <summary>
    /// 🔴 <b>وسبب الإيقاف إجباري — الفني بيشوفه على الراكة.</b>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("لا")]
    public async Task Suspending_without_a_reason_is_refused(string? reason)
    {
        var h = Build();
        var tech = Existing(h);

        var result = await h.Suspend.Handle(
            new SuspendTechnicianCommand(tech.Id, reason), default);

        Assert.True(result.IsFailure);
        Assert.Equal("technician.suspend_reason_required", result.Error.Code);
        Assert.True(tech.IsActive);
        Assert.Equal(1, tech.CredentialVersion);
    }

    /// <summary>
    /// 🔴 <b>والتشغيل <u>مابيزوّدش</u> الرقم عن قصد.</b>
    ///
    /// <para>التشغيل مش إلغاء صلاحية — وزيادة الرقم كانت هتمسح نسخ
    /// <b>صالحة</b> على الراكات من غير سبب، فالفني يرجع للخدمة
    /// ويلاقي نفسه مطرود من كل محطة.</para>
    /// </summary>
    [Fact]
    public async Task Activating_does_not_bump_the_version()
    {
        var h = Build();

        var tech = Existing(h, isActive: false, credentialVersion: 5);
        tech.SuspendedReason = "غياب";
        tech.SuspendedByName = "المدير";
        tech.SuspendedAtUtc = DateTime.UtcNow;

        var result = await h.Activate.Handle(new ActivateTechnicianCommand(tech.Id), default);

        Assert.True(result.IsSuccess);
        Assert.True(tech.IsActive);

        // 🔴 نفس الرقم.
        Assert.Equal(5, tech.CredentialVersion);

        // ⚠️ وبيانات الإيقاف اتفضّت.
        Assert.Equal("", tech.SuspendedReason);
        Assert.Equal("", tech.SuspendedByName);
        Assert.Null(tech.SuspendedAtUtc);
    }

    // =================================================================
    //  التعديل — «ماتلمسش» مقابل «شيل»
    // =================================================================

    /// <summary>
    /// 🔴 <b>«غير محدد» = صفر، فالحقل المش مبعوت كان بيرجّع كل فني
    /// اتعدّل لـ«غير محدد» في صمت.</b>
    /// </summary>
    [Fact]
    public async Task An_unsent_specialty_is_left_alone()
    {
        var h = Build();
        var tech = Existing(h, specialty: TechnicianSpecialty.Boards);

        var result = await h.Update.Handle(
            new UpdateTechnicianCommand(tech.Id, "محمود الكبير", null, null, null, null),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal("محمود الكبير", tech.DisplayName);

        // 🔴 التخصص زي ما هو.
        Assert.Equal(TechnicianSpecialty.Boards, tech.Specialty);
    }

    /// <summary>
    /// 🔴 <b>تلات حالات في حقل القسم:</b> «ماتلمسش» · «شيل» ·
    /// «حطّه».
    /// </summary>
    [Fact]
    public async Task The_department_field_has_three_meanings()
    {
        var h = Build();
        var tech = Existing(h);

        var department = Guid.NewGuid();

        // «حطّه»
        await h.Update.Handle(
            new UpdateTechnicianCommand(tech.Id, "محمود", null, department, null, null),
            default);

        Assert.Equal(department, tech.DepartmentId);

        // «ماتلمسش»
        await h.Update.Handle(
            new UpdateTechnicianCommand(tech.Id, "محمود", null, null, null, null), default);

        Assert.Equal(department, tech.DepartmentId);

        // «شيل» — بالقيمة الحارسة.
        await h.Update.Handle(
            new UpdateTechnicianCommand(tech.Id, "محمود", null, Guid.Empty, null, null),
            default);

        Assert.Null(tech.DepartmentId);
    }

    /// <summary>
    /// 🔴 <b>الصلاحيات <c>null</c> = «ماتلمسش».</b>
    ///
    /// <para>ولو كانت <c>bool</c> عادية، أي نداء قديم من واجهة مش
    /// عارفة الحقول دي كان هيوصل <c>false</c> وينزع صلاحية الفحص من
    /// كل فني اتعدّل اسمه.</para>
    /// </summary>
    [Fact]
    public async Task Unsent_capabilities_are_left_alone()
    {
        var h = Build();
        var tech = Existing(h, canTest: true, canRepair: true);

        await h.Update.Handle(
            new UpdateTechnicianCommand(tech.Id, "محمود", null, null, null, null), default);

        Assert.True(tech.CanTest);
        Assert.True(tech.CanRepair);

        // ⚠️ ومفيش وقت تغيير صلاحية — مفيش تغيير حصل.
        Assert.Null(tech.CapabilityChangedAtUtc);
    }

    /// <summary>
    /// 🔴 <b>ووقت تغيير الصلاحية هو دليل الشغل الأوفلاين.</b>
    ///
    /// <para>الراكة بتشتغل من غير نت، فممكن فني يعمل صيانة وهو مصرّح
    /// له وبعدين الصلاحية تتسحب قبل ما الشغل يترفع. والوقت ده هو
    /// اللي بيخلّي السيرفر يفرّق بين «كان مصرّح له وقتها» و«مكانش
    /// مصرّح له خالص».</para>
    /// </summary>
    [Fact]
    public async Task A_capability_change_stamps_the_time_and_writes_its_own_audit_line()
    {
        var h = Build();
        var tech = Existing(h, canTest: true, canRepair: false);

        var result = await h.Update.Handle(
            new UpdateTechnicianCommand(tech.Id, "محمود", null, null, false, true), default);

        Assert.True(result.IsSuccess);
        Assert.False(tech.CanTest);
        Assert.True(tech.CanRepair);
        Assert.NotNull(tech.CapabilityChangedAtUtc);

        // 🔴 سطرين: التعديل، والصلاحيات.
        Assert.Equal(2, h.Audit.Lines.Count);
        Assert.Contains(h.Audit.Lines, l => l.Action == "technician.updated");
        Assert.Contains(h.Audit.Lines, l => l.Action == "technician.capability_changed");
    }

    /// <summary>
    /// ⚠️ <b>وتعديل اسم مالوش يسيب سطر «اتغيّرت الصلاحيات»</b> —
    /// اللي بيراجع بيدوّر على التغييرات الحقيقية.
    /// </summary>
    [Fact]
    public async Task A_name_only_edit_writes_one_audit_line()
    {
        var h = Build();
        var tech = Existing(h, canTest: true, canRepair: false);

        // ⚠️ نفس القيم المبعوتة — مفيش تغيير فعلي.
        await h.Update.Handle(
            new UpdateTechnicianCommand(tech.Id, "محمود الجديد", null, null, true, false),
            default);

        Assert.Equal("technician.updated", Assert.Single(h.Audit.Lines).Action);
        Assert.Null(tech.CapabilityChangedAtUtc);
    }

    [Fact]
    public async Task A_missing_technician_is_a_not_found_everywhere()
    {
        var h = Build();
        var ghost = Guid.NewGuid();

        var reset = await h.Reset.Handle(
            new ResetTechnicianPasswordCommand(ghost, "1234"), default);

        var suspend = await h.Suspend.Handle(
            new SuspendTechnicianCommand(ghost, "سبب كافي"), default);

        var activate = await h.Activate.Handle(new ActivateTechnicianCommand(ghost), default);

        var update = await h.Update.Handle(
            new UpdateTechnicianCommand(ghost, "محمود", null, null, null, null), default);

        var brands = await h.Brands.Handle(
            new SetTechnicianBrandsCommand(ghost, []), default);

        foreach (var error in new[]
                 {
                     reset.Error, suspend.Error, activate.Error, update.Error, brands.Error,
                 })
        {
            Assert.Equal("technician.not_found", error.Code);
            Assert.Equal(404, error.StatusCode);
        }

        Assert.Equal(0, h.Work.Saves);
    }

    // =================================================================
    //  الماركات
    // =================================================================

    [Fact]
    public async Task Setting_the_brands_replaces_the_whole_set()
    {
        var h = Build();
        var tech = Existing(h);

        var hp = Guid.NewGuid();
        var dell = Guid.NewGuid();
        var lenovo = Guid.NewGuid();

        foreach (var (id, name) in new[] { (hp, "HP"), (dell, "Dell"), (lenovo, "Lenovo") })
        {
            h.Repo.KnownBrands.Add(id);
            h.Repo.BrandNames[id] = name;
        }

        h.Repo.Links.Add(new TechnicianBrand
        { TenantId = h.Me.TenantId, TechnicianId = tech.Id, BrandId = lenovo });

        var result = await h.Brands.Handle(
            new SetTechnicianBrandsCommand(tech.Id, [hp, dell]), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("اتحفظت الماركات", result.Value.Message);

        // ⚠️ الاستبدال كامل — Lenovo مش موجودة.
        Assert.Equal([hp, dell], h.Repo.Links.Select(l => l.BrandId));

        // 🔴 والقيد ده صلاحية، فوقت تغييرها بيتحدّث.
        Assert.NotNull(tech.CapabilityChangedAtUtc);

        // ⚠️ والسجل بيقول الأسماء مش المعرّفات.
        string summary = Assert.Single(h.Audit.Lines).Summary;

        Assert.Contains("Dell", summary);
        Assert.Contains("HP", summary);
    }

    /// <summary>
    /// 🔴 <b>والقايمة الفاضية معناها «مفيش قيد» بقرار صريح.</b>
    /// </summary>
    [Fact]
    public async Task An_empty_list_means_no_restriction_and_says_so()
    {
        var h = Build();
        var tech = Existing(h);

        var hp = Guid.NewGuid();
        h.Repo.KnownBrands.Add(hp);
        h.Repo.BrandNames[hp] = "HP";

        h.Repo.Links.Add(new TechnicianBrand
        { TenantId = h.Me.TenantId, TechnicianId = tech.Id, BrandId = hp });

        var result = await h.Brands.Handle(
            new SetTechnicianBrandsCommand(tech.Id, []), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(h.Repo.Links);
        Assert.Empty(result.Value.BrandIds);

        Assert.Contains("مفيش قيد", Assert.Single(h.Audit.Lines).Summary);
    }

    /// <summary>
    /// ⚠️ <b>والمقارنة بالمجموعة — الترتيب مالوش معنى.</b>
    ///
    /// <para>المدير بيدوس حفظ من غير ما يعدّل، والرد لازم يقوله إنه
    /// مااتغيّرش حاجة. ومن غير الفحص ده، كل دوسة بتمسح الصفوف
    /// وتكتبها تاني وتسيب سطر سجل وتحدّث وقت تغيير الصلاحية —
    /// واللي بيراجع بيشوف تغييرات مااتعملتش.</para>
    /// </summary>
    [Fact]
    public async Task Saving_the_same_set_in_a_different_order_changes_nothing()
    {
        var h = Build();
        var tech = Existing(h);

        var hp = Guid.NewGuid();
        var dell = Guid.NewGuid();

        foreach (var id in new[] { hp, dell })
        {
            h.Repo.KnownBrands.Add(id);
            h.Repo.BrandNames[id] = id == hp ? "HP" : "Dell";
        }

        h.Repo.Links.Add(new TechnicianBrand
        { TenantId = h.Me.TenantId, TechnicianId = tech.Id, BrandId = hp });

        h.Repo.Links.Add(new TechnicianBrand
        { TenantId = h.Me.TenantId, TechnicianId = tech.Id, BrandId = dell });

        var result = await h.Brands.Handle(
            new SetTechnicianBrandsCommand(tech.Id, [dell, hp]), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("مفيش تغيير", result.Value.Message);

        Assert.Equal(0, h.Work.Saves);
        Assert.Empty(h.Audit.Lines);
        Assert.Null(tech.CapabilityChangedAtUtc);
    }

    /// <summary>
    /// ⚠️ <b>والماركة المش موجودة بترفض الطلب كله.</b>
    ///
    /// <para>ربط فني بماركة مش في القايمة بيخلّي القيد يتصرّف بشكل
    /// مش متوقّع: الماركة مش في قواعد التوحيد، فأي لاب عمره ما
    /// يطابقها — والفني بيبان مقيّد وهو عملياً ممنوع من كل حاجة.</para>
    /// </summary>
    [Fact]
    public async Task An_unknown_brand_refuses_the_whole_request()
    {
        var h = Build();
        var tech = Existing(h);

        var hp = Guid.NewGuid();
        h.Repo.KnownBrands.Add(hp);

        var result = await h.Brands.Handle(
            new SetTechnicianBrandsCommand(tech.Id, [hp, Guid.NewGuid()]), default);

        Assert.True(result.IsFailure);
        Assert.Equal("technician.unknown_brand", result.Error.Code);

        // ⚠️ ومفيش ولا ربط اتكتب.
        Assert.Empty(h.Repo.Links);
    }

    /// <summary>⚠️ والمكرّر بيتلم قبل أي حاجة.</summary>
    [Fact]
    public async Task Duplicate_brand_ids_are_collapsed()
    {
        var h = Build();
        var tech = Existing(h);

        var hp = Guid.NewGuid();
        h.Repo.KnownBrands.Add(hp);
        h.Repo.BrandNames[hp] = "HP";

        var result = await h.Brands.Handle(
            new SetTechnicianBrandsCommand(tech.Id, [hp, hp, hp]), default);

        Assert.True(result.IsSuccess);
        Assert.Single(h.Repo.Links);
        Assert.Single(result.Value.BrandIds);
    }

    // =================================================================
    //  القايمة
    // =================================================================

    /// <summary>
    /// 🔴 <b>القايمة الفاضية مش <c>null</c>.</b>
    ///
    /// <para>الفني اللي مالوش ربط لازم يرجع <c>[]</c> — عشان الواجهة
    /// تفرّق بين «محمّلة ومفيش قيد» و«المسار ده مابيحمّلش
    /// الربط».</para>
    /// </summary>
    [Fact]
    public async Task Every_row_carries_a_brand_list_even_when_empty()
    {
        var h = Build();

        var withBrands = Existing(h, name: "أ", username: "a");
        var withNone = new Technician
        {
            TenantId = h.Me.TenantId, Code = "T002", DisplayName = "ب",
            Username = "b", NormalizedUsername = "b",
        };
        h.Repo.Technicians.Add(withNone);

        var hp = Guid.NewGuid();

        h.Repo.Links.Add(new TechnicianBrand
        { TenantId = h.Me.TenantId, TechnicianId = withBrands.Id, BrandId = hp });

        var result = await h.List.Handle(new GetTechnicianAccountsQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        Assert.All(result.Value, a => Assert.NotNull(a.BrandIds));

        Assert.Equal([hp], result.Value.Single(a => a.DisplayName == "أ").BrandIds);
        Assert.Empty(result.Value.Single(a => a.DisplayName == "ب").BrandIds!);
    }

    /// <summary>
    /// 🔴 <b>ومفيش بصمة ولا ملح في العقد خالص.</b>
    /// </summary>
    [Fact]
    public void The_account_contract_carries_no_secret()
    {
        var fields = typeof(TechnicianAccount)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.Name!)
            .ToList();

        Assert.DoesNotContain("PasswordHash", fields);
        Assert.DoesNotContain("Salt", fields);
        Assert.DoesNotContain("Password", fields);

        // ⚠️ ونسخة البيانات **موجودة** — الواجهة بتعرضها عشان المدير
        // يعرف إن الطرد وصل.
        Assert.Contains("CredentialVersion", fields);
    }
}
