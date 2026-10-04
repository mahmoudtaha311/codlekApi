using Codlek.Application.Features.Account.GetAccount;
using Codlek.Application.Features.Account.UpdateProfile;
using Codlek.Application.Features.Account;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// صفحة «حسابي».
///
/// <para>🔴 <b>أهم حاجة بتتفحص هنا: إن المستخدم بييجي من التوكن
/// وبس.</b> الأوامر نفسها مافيهاش <c>userId</c> ولا
/// <c>tenantId</c> — فمفيش طريقة حد يبعت حساب تاني.</para>
/// </summary>
public class AccountSliceTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid Mine = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class FakeUser : ICurrentUser
    {
        public Guid Id => Mine;
        public Guid TenantId => Tenant;
        public string DisplayName => "كريم";
        public string Code => "M001";
        public UserRole Role => UserRole.Manager;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRepository : IAccountRepository
    {
        public readonly List<ApplicationUser> Rows = [];
        public string TenantName = "ورشة كودلك";

        public Task<ApplicationUser?> FindAsync(
            Guid tenantId, Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(
                u => u.Id == userId && u.TenantId == tenantId));

        public Task<string> TenantNameAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(TenantName);
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

    private static ApplicationUser Me() => new()
    {
        Id = Mine,
        TenantId = Tenant,
        UserName = "kareem",
        DisplayName = "كريم المدير",
        Code = "M001",
        Role = UserRole.Manager,
        CredentialVersion = 3,
        IsActive = true,
        MustChangePassword = false,
        CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static (FakeRepository Repo, FakeUnitOfWork Uow,
                    GetAccountQueryHandler Get, UpdateProfileCommandHandler Update) Build()
    {
        var repo = new FakeRepository();
        var uow = new FakeUnitOfWork();
        var me = new FakeUser();

        return (repo, uow,
            new GetAccountQueryHandler(repo, me),
            new UpdateProfileCommandHandler(repo, uow, me));
    }

    // =================================================================
    //  قراية الحساب
    // =================================================================

    [Fact]
    public async Task Reading_my_account_returns_my_row()
    {
        var (repo, _, get, _) = Build();
        repo.Rows.Add(Me());

        var result = await get.Handle(new GetAccountQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("kareem", result.Value!.Username);
        Assert.Equal("كريم المدير", result.Value.DisplayName);
        Assert.Equal("ورشة كودلك", result.Value.TenantName);
    }

    /// <summary>
    /// 🔴 <b>الدور بالعربي بييجي من <see cref="UserRoleText"/>، والنص
    /// ده بيتعرض في الشاشة.</b>
    ///
    /// <para>⚠️ وكان مكتوب في مكانين واختلفوا: «مدير المخزن» مقابل
    /// «مدير».</para>
    /// </summary>
    [Fact]
    public async Task The_arabic_role_name_comes_from_the_single_source()
    {
        var (repo, _, get, _) = Build();
        repo.Rows.Add(Me());

        var result = await get.Handle(new GetAccountQuery(), default);

        Assert.Equal("Manager", result.Value!.Role);
        Assert.Equal("مدير المخزن", result.Value.RoleText);
    }

    /// <summary>
    /// 🔴 <b>الصف مش موجود = <c>401</c> مش <c>404</c>.</b>
    ///
    /// <para>التوكن سليم والحساب اتمسح أو اتنقل لشركة تانية. ودي
    /// <b>جلسة مابقتش صالحة</b>، مش «مورد مش موجود». والفرق بيبان في
    /// الواجهة: <c>401</c> بتودّي على شاشة الدخول، و<c>404</c> بتعرض
    /// «مش موجود» لواحد قاعد في النظام.</para>
    /// </summary>
    [Fact]
    public async Task A_missing_row_is_an_expired_session_not_a_missing_resource()
    {
        var (_, _, get, _) = Build();

        var result = await get.Handle(new GetAccountQuery(), default);

        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error.StatusCode);
    }

    /// <summary>
    /// 🔴 <b>حساب شركة تانية مابيترجعش — حتى بنفس المعرّف.</b>
    ///
    /// <para>المعرّف فريد عالمياً فالشرط على الشركة شكله زيادة. بس لو
    /// حساب اتنقل لشركة تانية بعد ما التوكن اتعمل، القراية من غير
    /// الشرط بتفتح بيانات شركة التوكن مش شركة الصف.</para>
    /// </summary>
    [Fact]
    public async Task A_row_that_moved_to_another_tenant_is_not_returned()
    {
        var (repo, _, get, _) = Build();
        var moved = Me();
        moved.TenantId = Other;
        repo.Rows.Add(moved);

        var result = await get.Handle(new GetAccountQuery(), default);

        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error.StatusCode);
    }

    // =================================================================
    //  تعديل الاسم
    // =================================================================

    [Fact]
    public async Task Updating_my_name_saves_it()
    {
        var (repo, uow, _, update) = Build();
        repo.Rows.Add(Me());

        var result = await update.Handle(new UpdateProfileCommand("  كريم طه  "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("كريم طه", result.Value!.DisplayName);
        Assert.Equal("كريم طه", repo.Rows.Single().DisplayName);
        Assert.Equal(1, uow.Saves);
    }

    /// <summary>
    /// 🔴 <b>ولا حاجة تانية بتتغيّر — دا الفحص اللي بيمنع ترقية
    /// الصلاحيات.</b>
    ///
    /// <para>الأمر مافيهوش دور ولا شركة ولا كود ولا نسخة اعتماد، بس
    /// الفحص ده بيثبّت إن الكود كمان مش بيلمسهم. لأن الأمر ممكن يتزاد
    /// له حقل بعدين، والمعالج ينقله من غير ما حد ياخد باله.</para>
    /// </summary>
    [Fact]
    public async Task Updating_my_name_touches_nothing_else()
    {
        var (repo, _, _, update) = Build();
        repo.Rows.Add(Me());
        var before = Me();

        await update.Handle(new UpdateProfileCommand("اسم جديد"), default);

        var after = repo.Rows.Single();
        Assert.Equal(before.Role, after.Role);
        Assert.Equal(before.TenantId, after.TenantId);
        Assert.Equal(before.Code, after.Code);
        Assert.Equal(before.CredentialVersion, after.CredentialVersion);
        Assert.Equal(before.IsActive, after.IsActive);
        Assert.Equal(before.UserName, after.UserName);
        Assert.Equal(before.MustChangePassword, after.MustChangePassword);
    }

    /// <summary>⚠️ وجلسة مابقتش صالحة مابتحفظش حاجة.</summary>
    [Fact]
    public async Task Updating_with_no_row_saves_nothing()
    {
        var (_, uow, _, update) = Build();

        var result = await update.Handle(new UpdateProfileCommand("اسم"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(0, uow.Saves);
    }

    // =================================================================
    //  التحقق
    // =================================================================

    /// <summary>
    /// ⚠️ الاسم الفاضي بيترفض — <b>بخلاف تعديل القسم</b>.
    ///
    /// <para>الفرق مقصود: القسم بيتبعت منه الحقل اللي اتغيّر بس،
    /// فالفاضي معناه «ماتغيّرش». والاسم هنا هو <b>كل</b> اللي الطلب
    /// بيبعته — فالفاضي معناه «فضّي اسمي»، وده غلط.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_display_name_is_rejected(string name)
    {
        var failures = new UpdateProfileCommandValidator()
            .Validate(new UpdateProfileCommand(name));

        Assert.False(failures.IsValid);
        Assert.Contains("فاضي", failures.Errors[0].ErrorMessage);
    }

    [Fact]
    public void A_name_longer_than_the_column_is_rejected()
    {
        var failures = new UpdateProfileCommandValidator()
            .Validate(new UpdateProfileCommand(new string('ا', 121)));

        Assert.False(failures.IsValid);
    }

    /// <summary>⚠️ و١٢٠ بالظبط بتعدّي — دا طول العمود في القاعدة.</summary>
    [Fact]
    public void A_name_of_exactly_the_column_length_passes()
    {
        var failures = new UpdateProfileCommandValidator()
            .Validate(new UpdateProfileCommand(new string('ا', 120)));

        Assert.True(failures.IsValid);
    }

    /// <summary>
    /// 🔴 <b>أسباب الفشل كلها ليها أكواد مختلفة.</b>
    ///
    /// <para>الواجهة بتتفرّع على الكود مش على الرسالة. ولو اتنين
    /// اشتركوا في كود، الواجهة مش هتعرف تفرّق — مثلاً تودّي على شاشة
    /// الدخول في حالة «التأكيد مش مطابق».</para>
    /// </summary>
    [Fact]
    public void Every_account_error_has_its_own_code()
    {
        string[] codes =
        [
            AccountErrors.SessionNoLongerValid.Code,
            AccountErrors.CurrentPasswordWrong.Code,
            AccountErrors.ConfirmationMismatch.Code,
            AccountErrors.NewPasswordRejected("أي سبب").Code,
        ];

        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    /// <summary>
    /// ⚠️ و«الحالية غلط» <c>400</c> مش <c>401</c>.
    ///
    /// <para><c>401</c> كانت هتخلّي الواجهة تطرد المستخدم على شاشة
    /// الدخول — وهو داخل فعلاً، غلط في خانة واحدة بس.</para>
    /// </summary>
    [Fact]
    public void A_wrong_current_password_does_not_log_the_user_out()
    {
        Assert.Equal(400, AccountErrors.CurrentPasswordWrong.StatusCode);
        Assert.Equal(401, AccountErrors.SessionNoLongerValid.StatusCode);
    }
}
