using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Features.Users;
using Codlek.Application.Features.Users.ActivateUser;
using Codlek.Application.Features.Users.GetUsers;
using Codlek.Application.Features.Users.SuspendUser;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// حسابات لوحة التحكم.
///
/// <para>⚠️ <b>الإنشاء وإعادة تعيين الباسورد مش هنا.</b> الاتنين
/// بيعتمدوا على <c>UserManager</c> اللي محتاج قاعدة بيانات حقيقية —
/// فحوصهم في <c>UserAccountIdentityTests</c>.</para>
/// </summary>
public class UserSliceTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid MyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class FakeUser(UserRole role) : ICurrentUser
    {
        public Guid Id => MyId;
        public Guid TenantId => Tenant;
        public string DisplayName => "كريم المدير";
        public string Code => "M001";
        public UserRole Role => role;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRepository : IUserAccountRepository
    {
        public readonly List<ApplicationUser> Rows = [];

        public Task<IReadOnlyList<ApplicationUser>> ListAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApplicationUser>>(
                Rows.Where(u => u.TenantId == tenantId)
                    .OrderBy(u => u.Role == UserRole.Technician ? 1 : 0)
                    .ThenBy(u => u.DisplayName)
                    .ToList());

        public Task<ApplicationUser?> FindAsync(
            Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(
                u => u.Id == id && u.TenantId == tenantId));

        public Task<bool> UsernameTakenAnywhereAsync(
            string normalizedUsername, CancellationToken ct = default) =>
            Task.FromResult(Rows.Any(u => u.NormalizedUserName == normalizedUsername));

        public Task<bool> CodeTakenAsync(
            Guid tenantId, string code, CancellationToken ct = default) =>
            Task.FromResult(Rows.Any(u => u.TenantId == tenantId && u.Code == code));

        public void Add(ApplicationUser user) => Rows.Add(user);
    }

    private sealed class FakeSessions : ILoginSessions
    {
        public readonly List<(Guid UserId, string Reason)> Ended = [];

        public Task<TokenPair> StartAsync(
            TokenSubject subject, CancellationToken ct = default) =>
            Task.FromResult(new TokenPair("a", "r", 900));

        public Task<Result<RefreshedSession>> RefreshAsync(
            string refreshToken, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> EndAllAsync(
            Guid userId, string reason, CancellationToken ct = default)
        {
            Ended.Add((userId, reason));
            return Task.FromResult(1);
        }
    }

    private sealed class FakeAudit : IAuditTrail
    {
        public readonly List<(string Action, string Summary)> Entries = [];

        public void Record(
            string action, string entityType, Guid? entityId,
            string entityCode, string summary) => Entries.Add((action, summary));

        /// <summary>⚠️ مسار الراكة — القطاع ده مابيستعملهوش.</summary>
        public void RecordForRack(
            RackAuditActor actor, string action, string entityType, Guid? entityId,
            string entityCode, string summary, string dataJson = "") =>
            Entries.Add((action, summary));
    }

    private sealed class FakeStanding : IAccountStanding
    {
        public List<Guid> Forgotten { get; } = [];

        public void Forget(Guid userId) => Forgotten.Add(userId);
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
        FakeSessions Sessions,
        FakeAudit Audit,
        FakeUnitOfWork UnitOfWork,
        GetUsersQueryHandler List,
        SuspendUserCommandHandler Suspend,
        ActivateUserCommandHandler Activate);

    private static Harness Build(UserRole myRole = UserRole.Owner)
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        var audit = new FakeAudit();
        var unitOfWork = new FakeUnitOfWork();
        var me = new FakeUser(myRole);
        var standing = new FakeStanding();

        return new Harness(
            repository, sessions, audit, unitOfWork,
            new GetUsersQueryHandler(repository, me),
            new SuspendUserCommandHandler(repository, sessions, audit, unitOfWork, me, standing),
            new ActivateUserCommandHandler(repository, audit, unitOfWork, me, standing));
    }

    private static ApplicationUser Row(
        UserRole role, string name = "أحمد", Guid? id = null, Guid? tenant = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = tenant ?? Tenant,
        UserName = name,
        NormalizedUserName = name,
        DisplayName = name,
        Code = "F001",
        Role = role,
        IsActive = true,
        CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    // =================================================================
    //  القايمة
    // =================================================================

    /// <summary>
    /// 🔴 <b><c>canManage</c> بيتحسب على السيرفر لكل صف.</b>
    ///
    /// <para>الواجهة بتقفل الأزرار بيه. لو الواجهة حسبته لوحدها،
    /// القاعدة بتبقى متكتوبة في مكانين بلغتين — وأول تعديل في واحدة
    /// منهم بيخلّي الصفحة تعرض زرار بيرجّع <c>403</c>، أو أسوأ تخفي
    /// زرار المستخدم من حقه يضغطه.</para>
    /// </summary>
    [Fact]
    public async Task The_list_marks_which_rows_i_can_manage()
    {
        var h = Build(UserRole.Manager);
        h.Repository.Rows.Add(Row(UserRole.Technician, "فني"));
        h.Repository.Rows.Add(Row(UserRole.Owner, "مالك"));

        var rows = (await h.List.Handle(new GetUsersQuery(), default)).Value!;

        Assert.True(rows.Single(r => r.Role == nameof(UserRole.Technician)).CanManage);
        Assert.False(rows.Single(r => r.Role == nameof(UserRole.Owner)).CanManage);
    }

    /// <summary>🔴 وصف المستخدم نفسه بيبقى <c>false</c> — حتى للمالك.</summary>
    [Fact]
    public async Task My_own_row_is_never_manageable()
    {
        var h = Build(UserRole.Owner);
        h.Repository.Rows.Add(Row(UserRole.Owner, "أنا", MyId));

        var rows = (await h.List.Handle(new GetUsersQuery(), default)).Value!;

        Assert.False(rows.Single().CanManage);
    }

    /// <summary>⚠️ والفنيين تحت — نفس ترتيب الصفحة القديمة.</summary>
    [Fact]
    public async Task Technicians_are_listed_last()
    {
        var h = Build();
        h.Repository.Rows.Add(Row(UserRole.Technician, "أ-فني"));
        h.Repository.Rows.Add(Row(UserRole.Manager, "ب-مدير"));

        var rows = (await h.List.Handle(new GetUsersQuery(), default)).Value!;

        Assert.Equal(nameof(UserRole.Manager), rows[0].Role);
        Assert.Equal(nameof(UserRole.Technician), rows[1].Role);
    }

    [Fact]
    public async Task Users_of_another_tenant_are_not_listed()
    {
        var h = Build();
        h.Repository.Rows.Add(Row(UserRole.Manager, "بره", tenant: Other));

        Assert.Empty((await h.List.Handle(new GetUsersQuery(), default)).Value!);
    }

    /// <summary>
    /// ⚠️ والدور بالعربي جايّ من المصدر الواحد.
    /// </summary>
    [Fact]
    public async Task The_arabic_role_text_comes_from_the_single_source()
    {
        var h = Build();
        h.Repository.Rows.Add(Row(UserRole.Manager));

        var rows = (await h.List.Handle(new GetUsersQuery(), default)).Value!;

        Assert.Equal("مدير المخزن", rows.Single().RoleText);
    }

    // =================================================================
    //  الإيقاف
    // =================================================================

    [Fact]
    public async Task Suspending_writes_the_reason_who_and_when()
    {
        var h = Build();
        var target = Row(UserRole.Technician);
        h.Repository.Rows.Add(target);

        var result = await h.Suspend.Handle(
            new SuspendUserCommand(target.Id, "  سرقة لابتوب  "), default);

        Assert.True(result.IsSuccess);
        Assert.False(target.IsActive);
        Assert.Equal("سرقة لابتوب", target.SuspendedReason);
        Assert.Equal("كريم المدير", target.SuspendedByName);
        Assert.NotNull(target.SuspendedAtUtc);
    }

    /// <summary>
    /// 🔴 <b>الإيقاف لازم يقطع الجلسة المفتوحة، مش يمنع دخول جديد
    /// وبس.</b>
    ///
    /// <para>السيناريو اللي الميزة موجودة عشانه هو لابتوب اتسرق أو
    /// موظف اتفصل — والاتنين التاب عندهم مفتوح. من غير ده هو بيفضل
    /// يعمل حسابات فنيين ويصدّر المخزن كله بعد الإيقاف بساعات.</para>
    /// </summary>
    [Fact]
    public async Task Suspending_bumps_the_credential_version_and_kills_sessions()
    {
        var h = Build();
        var target = Row(UserRole.Technician);
        target.CredentialVersion = 3;
        h.Repository.Rows.Add(target);

        await h.Suspend.Handle(new SuspendUserCommand(target.Id, "اتفصل"), default);

        Assert.Equal(4, target.CredentialVersion);
        Assert.Equal(target.Id, h.Sessions.Ended.Single().UserId);
    }

    /// <summary>
    /// 🔴 <b>السبب إجباري — والمستخدم بيشوفه في شاشة الدخول.</b>
    ///
    /// <para>من غيره بيقف قدام رسالة مقفولة مش عارف يكلّم مين، فبيروح
    /// يجرّب باسورد زميله — وده بالظبط اللي الإيقاف بيمنعه.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("اب")]
    public async Task Suspending_without_a_real_reason_is_rejected(string reason)
    {
        var h = Build();
        var target = Row(UserRole.Technician);
        h.Repository.Rows.Add(target);

        var result = await h.Suspend.Handle(
            new SuspendUserCommand(target.Id, reason), default);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.SuspendReasonRequired.Code, result.Error.Code);
        Assert.True(target.IsActive);
        Assert.Equal(0, h.UnitOfWork.Saves);
    }

    /// <summary>
    /// ⚠️ وسبب أطول من العمود بيترفض.
    ///
    /// <para>من غير الفحص ده الحساب مابيتوقفش والمدير بيشوف خطأ عام
    /// مش فاهم منه حاجة — SQL Server بيرمي «would be truncated»
    /// والاستثناء مش متمسك.</para>
    /// </summary>
    [Fact]
    public async Task A_reason_longer_than_the_column_is_rejected()
    {
        var h = Build();
        var target = Row(UserRole.Technician);
        h.Repository.Rows.Add(target);

        var result = await h.Suspend.Handle(
            new SuspendUserCommand(target.Id, new string('ا', 401)), default);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.SuspendReasonTooLong.Code, result.Error.Code);
    }

    /// <summary>⚠️ و٤٠٠ بالظبط بتعدّي.</summary>
    [Fact]
    public async Task A_reason_of_exactly_the_column_length_passes()
    {
        var h = Build();
        var target = Row(UserRole.Technician);
        h.Repository.Rows.Add(target);

        var result = await h.Suspend.Handle(
            new SuspendUserCommand(target.Id, new string('ا', 400)), default);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// 🔴 <b>المالك مايوقفش نفسه.</b>
    ///
    /// <para>من غير القاعدة دي، الشركة ممكن تفضل من غير ولا حساب مالك
    /// يقدر يفكّ الإيقاف.</para>
    /// </summary>
    [Fact]
    public async Task The_owner_cannot_suspend_themselves()
    {
        var h = Build(UserRole.Owner);
        var me = Row(UserRole.Owner, "أنا", MyId);
        h.Repository.Rows.Add(me);

        var result = await h.Suspend.Handle(new SuspendUserCommand(MyId, "أي سبب"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error.StatusCode);
        Assert.Contains("صفحة الحساب", result.Error.Description);
        Assert.True(me.IsActive);
    }

    /// <summary>🔴 ومدير المخزن مايوقفش المالك.</summary>
    [Fact]
    public async Task The_warehouse_manager_cannot_suspend_the_owner()
    {
        var h = Build(UserRole.Manager);
        var owner = Row(UserRole.Owner, "المالك");
        h.Repository.Rows.Add(owner);

        var result = await h.Suspend.Handle(
            new SuspendUserCommand(owner.Id, "محاولة"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error.StatusCode);
        Assert.Contains("المدير العام", result.Error.Description);
        Assert.True(owner.IsActive);
    }

    /// <summary>⚠️ ومدير الدور مايعملش أي إجراء — رغم إنه بيشوف القايمة.</summary>
    [Fact]
    public async Task The_floor_manager_cannot_suspend_anyone()
    {
        var h = Build(UserRole.FloorManager);
        var target = Row(UserRole.Technician);
        h.Repository.Rows.Add(target);

        var result = await h.Suspend.Handle(
            new SuspendUserCommand(target.Id, "محاولة"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error.StatusCode);
    }

    /// <summary>
    /// 🔴 <b>حساب شركة تانية = <c>404</c> مش <c>403</c>.</b>
    ///
    /// <para>«مش موجود» مابتأكّدش إن الحساب ده موجود عند حد تاني.
    /// و<c>403</c> كانت هتخلّي النقطة أداة عدّ لحسابات الشركات
    /// التانية.</para>
    /// </summary>
    [Fact]
    public async Task A_user_from_another_tenant_is_not_found()
    {
        var h = Build();
        var foreign = Row(UserRole.Technician, "بره", tenant: Other);
        h.Repository.Rows.Add(foreign);

        var result = await h.Suspend.Handle(
            new SuspendUserCommand(foreign.Id, "محاولة"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error.StatusCode);
    }

    [Fact]
    public async Task Suspending_is_audited_with_the_reason()
    {
        var h = Build();
        var target = Row(UserRole.Technician);
        h.Repository.Rows.Add(target);

        await h.Suspend.Handle(new SuspendUserCommand(target.Id, "اتفصل"), default);

        var entry = h.Audit.Entries.Single();
        Assert.Equal(AuditActions.WebUserSuspended, entry.Action);
        Assert.Contains("اتفصل", entry.Summary);
    }

    // =================================================================
    //  التفعيل
    // =================================================================

    /// <summary>
    /// 🔴 <b>التفعيل بيفضّي بيانات الإيقاف كلها.</b>
    ///
    /// <para>لو <c>SuspendedReason</c> فضل مكتوب، شاشة الدخول (اللي
    /// بتقرا العمود ده) بتفضل تعرض سبب إيقاف قديم لحساب شغّال —
    /// والمستخدم بيدخل وهو شايف رسالة بتقوله إنه موقوف.</para>
    /// </summary>
    [Fact]
    public async Task Activating_clears_every_suspension_field()
    {
        var h = Build();
        var target = Row(UserRole.Technician);
        target.IsActive = false;
        target.SuspendedReason = "سبب قديم";
        target.SuspendedByName = "مدير قديم";
        target.SuspendedAtUtc = DateTime.UtcNow.AddDays(-5);
        h.Repository.Rows.Add(target);

        var result = await h.Activate.Handle(new ActivateUserCommand(target.Id), default);

        Assert.True(result.IsSuccess);
        Assert.True(target.IsActive);
        Assert.Equal("", target.SuspendedReason);
        Assert.Equal("", target.SuspendedByName);
        Assert.Null(target.SuspendedAtUtc);
    }

    /// <summary>
    /// ⚠️ <b>والتفعيل مابيزوّدش <c>CredentialVersion</c> — زي القديم.</b>
    ///
    /// <para>التفعيل بيفتح الباب، مابيقفلش حاجة. وزيادة النسخة كانت
    /// هتطرد الحساب من أجهزته — وهو أصلاً مطرود من وقت الإيقاف.</para>
    /// </summary>
    [Fact]
    public async Task Activating_does_not_bump_the_credential_version()
    {
        var h = Build();
        var target = Row(UserRole.Technician);
        target.CredentialVersion = 7;
        target.IsActive = false;
        h.Repository.Rows.Add(target);

        await h.Activate.Handle(new ActivateUserCommand(target.Id), default);

        Assert.Equal(7, target.CredentialVersion);
        Assert.Empty(h.Sessions.Ended);
    }

    [Fact]
    public async Task Activating_someone_above_me_is_forbidden()
    {
        var h = Build(UserRole.Manager);
        var owner = Row(UserRole.Owner, "المالك");
        owner.IsActive = false;
        h.Repository.Rows.Add(owner);

        var result = await h.Activate.Handle(new ActivateUserCommand(owner.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error.StatusCode);
        Assert.False(owner.IsActive);
    }

    /// <summary>⚠️ وكل سبب فشل عنده كود لوحده.</summary>
    [Fact]
    public void Every_user_error_has_its_own_code()
    {
        string[] codes =
        [
            UserErrors.NotFound.Code,
            UserErrors.UnknownRole.Code,
            UserErrors.UsernameTooShort.Code,
            UserErrors.UsernameTooLong.Code,
            UserErrors.DisplayNameTooShort.Code,
            UserErrors.DisplayNameTooLong.Code,
            UserErrors.UsernameTaken.Code,
            UserErrors.SuspendReasonRequired.Code,
            UserErrors.SuspendReasonTooLong.Code,
            UserErrors.CannotCreateThatRole.Code,
            UserErrors.CodeExhausted.Code,
            UserErrors.Forbidden("x").Code,
            UserErrors.PasswordRejected("x").Code,
        ];

        Assert.Equal(codes.Length, codes.Distinct().Count());
    }
}
