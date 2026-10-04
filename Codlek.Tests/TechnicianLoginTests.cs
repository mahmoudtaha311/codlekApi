using Codlek.Application.Features.Rack.TechnicianChangePassword;
using Codlek.Application.Features.Rack.TechnicianLogin;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Technicians;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// دخول الفني من محطة فحص، وتغيير باسورده.
///
/// <para>🔴 <b>والترتيب هو الأمان نفسه مش تنظيم:</b> القفل ←
/// الباسورد ← الإيقاف. كل ترتيب غير ده بيسرّب معلومة: إيقاف قبل
/// الباسورد بيقول مين موجود، وقفل بعد الباسورد بيدّي الإجابة قبل
/// القفل.</para>
///
/// <para>🔴 <b>والقفل <c>429</c> مش <c>401</c>.</b> الراكة بتحسب
/// <c>401</c>/<c>403</c> «رفض قاطع» وبتوقف عندهم؛ أما <c>429</c>
/// بتحسبها «السيرفر تعبان» وبتسمح بالدخول من النسخة المحفوظة. يعني
/// <c>401</c> على القفل بتطفّي ورشة نتها قاطع.</para>
/// </summary>
public class TechnicianLoginTests
{
    private const string SyncRack = "RACK-007";

    private sealed class FakePasswords : ITechnicianPasswords
    {
        public int Created;

        public (string Hash, string Salt) Create(string password)
        {
            Created++;
            return ("hash:" + password, "salt:" + Created);
        }

        public bool Verify(string password, string storedHash, string storedSalt) =>
            storedHash == "hash:" + password;
    }

    private sealed class FakeLogins : ITechnicianLoginRepository
    {
        public readonly List<Technician> Technicians = [];
        public readonly List<TechnicianLoginAttempt> Attempts = [];

        /// <summary>⚠️ عدّاد الاستعلامات — الفحص بيقيس إن القفل بيسبق البحث.</summary>
        public int Lookups;
        public int FailureCounts;

        public Task<int> RecentFailuresAsync(
            Guid rackId, string normalizedUsername, DateTime sinceUtc,
            CancellationToken ct = default)
        {
            FailureCounts++;

            return Task.FromResult(Attempts.Count(
                a => a.RackId == rackId
                  && a.AttemptedUsername == normalizedUsername
                  && !a.Success
                  && a.AtUtc >= sinceUtc));
        }

        public Task<Technician?> FindByLoginKeyAsync(
            Guid tenantId, string normalizedUsername, CancellationToken ct = default)
        {
            Lookups++;

            return Task.FromResult(Technicians.FirstOrDefault(
                t => t.TenantId == tenantId
                  && t.NormalizedUsername == normalizedUsername));
        }

        public void AddAttempt(TechnicianLoginAttempt attempt) => Attempts.Add(attempt);
    }

    private sealed record Harness(
        FakeLogins Logins,
        FakePasswords Passwords,
        FakeUnitOfWork Work,
        TechnicianLoginCommandHandler Login,
        TechnicianChangePasswordCommandHandler Change,
        Guid Tenant,
        Guid RackId);

    private static Harness Build()
    {
        var logins = new FakeLogins();
        var passwords = new FakePasswords();
        var work = new FakeUnitOfWork();

        return new Harness(
            logins, passwords, work,
            new TechnicianLoginCommandHandler(logins, passwords, work),
            new TechnicianChangePasswordCommandHandler(
                logins, passwords, work,
                NullLogger<TechnicianChangePasswordCommandHandler>.Instance),
            Guid.NewGuid(),
            Guid.NewGuid());
    }

    private static Technician NewTech(
        Harness h, string username = "ahmed", string password = "1234",
        bool isActive = true, bool canTest = true, bool canRepair = false,
        bool mustChange = false, int version = 3, Guid? tenant = null)
    {
        var tech = new Technician
        {
            TenantId = tenant ?? h.Tenant,
            Code = "100200",
            DisplayName = "أحمد فني",
            Username = username,
            NormalizedUsername = username.ToLowerInvariant(),
            PasswordHash = "hash:" + password,

            // ⚠️ ملح مختلف عن اللي المزيّف بيولّده عن قصد — كان
            //    بيتصادم معاه، ففحص «الملح بيتجدّد» كان بيقع وهو صح.
            Salt = "oldsalt",
            IsActive = isActive,
            CanTest = canTest,
            CanRepair = canRepair,
            MustChangePassword = mustChange,
            CredentialVersion = version,
        };

        h.Logins.Technicians.Add(tech);
        return tech;
    }

    private static TechnicianLoginCommand Login(
        Harness h, string? username = "ahmed", string? password = "1234", int days = 7) =>
        new(h.Tenant, h.RackId, SyncRack, username, password, days);

    private static TechnicianChangePasswordCommand Change(
        Harness h, string? username = "ahmed", string? current = "1234",
        string? next = "5678", string? confirm = "5678") =>
        new(h.Tenant, h.RackId, SyncRack, username, current, next, confirm);

    private static void Fail(Harness h, string key, int count, DateTime? atUtc = null)
    {
        for (int i = 0; i < count; i++)
            h.Logins.Attempts.Add(new TechnicianLoginAttempt
            {
                TenantId = h.Tenant,
                RackId = h.RackId,
                AttemptedUsername = key,
                Success = false,
                AtUtc = atUtc ?? DateTime.UtcNow,
            });
    }

    // =================================================================
    //  أكواد الحالة — العقد اللي الراكة بتتفرّع عليه
    // =================================================================

    /// <summary>
    /// 🔴 <b>القفل <c>429</c> — مش <c>401</c> ولا <c>403</c>.</b>
    ///
    /// <para>الراكة بتحسب <c>401</c>/<c>403</c> «رفض قاطع» وبتوقف
    /// عندهم خالص. أما <c>429</c> بتحسبها «السيرفر تعبان» وبتسمح
    /// بالدخول من النسخة المحفوظة.</para>
    /// </summary>
    [Fact]
    public void A_throttle_is_never_a_hard_rejection()
    {
        int status = TechnicianAuthCodes.LoginStatus(TechnicianAuthCodes.TooManyAttempts);

        Assert.Equal(429, status);
        Assert.NotEqual(401, status);
        Assert.NotEqual(403, status);
    }

    [Theory]
    [InlineData(TechnicianAuthCodes.Suspended, 403)]
    [InlineData(TechnicianAuthCodes.TooManyAttempts, 429)]
    [InlineData(TechnicianAuthCodes.InvalidCredentials, 401)]
    [InlineData("SomethingNew", 401)]
    public void The_login_status_map_is_frozen(string code, int status)
    {
        Assert.Equal(status, TechnicianAuthCodes.LoginStatus(code));
    }

    /// <summary>
    /// 🔴 <b>وسياسة الباسورد الجديد <c>400</c> مش <c>401</c>.</b>
    ///
    /// <para>لو رجعت <c>401</c>، شاشة الراكة بتعرض «الباسورد الحالي
    /// غلط» على باسورد حالي <b>صح</b> — وده بالظبط العطل اللي النقطة
    /// دي اتعملت عشانه.</para>
    /// </summary>
    [Theory]
    [InlineData(TechnicianAuthCodes.Suspended, 403)]
    [InlineData(TechnicianAuthCodes.TooManyAttempts, 429)]
    [InlineData(TechnicianAuthCodes.InvalidCredentials, 401)]
    [InlineData(TechnicianAuthCodes.WeakPassword, 400)]
    [InlineData(TechnicianAuthCodes.Mismatch, 400)]
    [InlineData(TechnicianAuthCodes.SameAsCurrent, 400)]
    public void The_change_status_map_is_frozen(string code, int status)
    {
        Assert.Equal(status, TechnicianAuthCodes.ChangeStatus(code));
    }

    /// <summary>
    /// ⚠️ <b>والخريطتين مختلفتين فعلاً</b> — عشان الفحص اللي فوق
    /// مايبقاش بيقارن حاجة بنفسها.
    /// </summary>
    [Fact]
    public void The_two_maps_disagree_on_a_weak_password()
    {
        Assert.NotEqual(
            TechnicianAuthCodes.LoginStatus(TechnicianAuthCodes.WeakPassword),
            TechnicianAuthCodes.ChangeStatus(TechnicianAuthCodes.WeakPassword));
    }

    // =================================================================
    //  القواعد النقية
    // =================================================================

    [Theory]
    [InlineData(0, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(99, true)]
    public void The_lockout_starts_at_ten(int failures, bool locked)
    {
        Assert.Equal(locked, TechnicianLoginRules.Throttled(failures));
    }

    [Fact]
    public void The_window_is_five_minutes()
    {
        var now = new DateTime(2026, 10, 4, 9, 30, 0, DateTimeKind.Utc);

        Assert.Equal(
            new DateTime(2026, 10, 4, 9, 25, 0, DateTimeKind.Utc),
            TechnicianLoginRules.WindowStart(now));
    }

    /// <summary>
    /// ⚠️ <b>والمهلة السالبة بتتصفّر.</b> إعداد بالغلط بسالب كان
    /// بيدّي مهلة <b>في الماضي</b> — يعني كل الفنيين مابيعرفوش
    /// يدخلوا أوفلاين والسبب مش باين في أي لوج.
    /// </summary>
    [Theory]
    [InlineData(7, 7)]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(-9999, 0)]
    public void A_negative_offline_window_clamps_to_zero(int days, int expected)
    {
        var now = new DateTime(2026, 10, 4, 9, 30, 0, DateTimeKind.Utc);

        Assert.Equal(
            now.AddDays(expected), TechnicianLoginRules.OfflineUntil(now, days));
    }

    /// <summary>
    /// 🔴 <b>وسبب الإيقاف بيوصل لشاشة المحطة عن قصد.</b> من غيره
    /// الفني بيقف قدام رسالة مقفولة ومش عارف يكلّم مين، فبيروح يجرّب
    /// حساب زميله — وده بالظبط اللي الإيقاف بيمنعه.
    /// </summary>
    [Fact]
    public void The_suspension_message_carries_the_reason_and_who()
    {
        string message = TechnicianLoginRules.SuspendedMessage("غياب", "كريم");

        Assert.Contains("غياب", message);
        Assert.Contains("كريم", message);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void A_blank_reason_leaves_no_dangling_label(string? reason, string? who)
    {
        string message = TechnicianLoginRules.SuspendedMessage(reason, who);

        Assert.DoesNotContain("السبب:", message);
        Assert.DoesNotContain("أوقفه:", message);
    }

    // =================================================================
    //  الدخول — النجاح
    // =================================================================

    [Fact]
    public async Task A_correct_password_returns_the_whole_offline_payload()
    {
        var h = Build();
        var tech = NewTech(h, canTest: true, canRepair: true, mustChange: true, version: 5);

        var result = await h.Login.Handle(Login(h), default);

        Assert.True(result.IsSuccess);

        var reply = result.Value;

        Assert.Equal(tech.Id, reply.TechnicianId);
        Assert.Equal("100200", reply.TechnicianCode);
        Assert.Equal("أحمد فني", reply.DisplayName);
        Assert.Equal(5, reply.CredentialVersion);
        Assert.True(reply.MustChangePassword);
        Assert.True(reply.CanTest);
        Assert.True(reply.CanRepair);
        Assert.Equal(SyncRack, reply.RackCode);

        // 🔴 والمهلة من وقت الدخول — سبعة أيام.
        Assert.NotNull(reply.OfflineValidUntilUtc);
        Assert.Equal(
            7, Math.Round((reply.OfflineValidUntilUtc!.Value - reply.ServerTimeUtc).TotalDays));
    }

    /// <summary>
    /// 🔴 <b>قدرات الفني بتنزل زي ما هي — <u>مش مفتوحة</u>.</b>
    ///
    /// <para>الراكة بتشتغل أوفلاين وبتقفل شاشة الصيانة بالقيم دي من
    /// غير ما تسأل السيرفر. فلو نزلت <c>true</c> دايماً، الشاشة
    /// بتتفتح لأي فني والسيرفر يرفض الشغل <b>بعد</b> ما يتعمل —
    /// والشغل ده مفيش في التطبيق أي مسار بيرجّعه.</para>
    ///
    /// <para>⚠️ <b>والفحص ده اتكتب بعد تحوير نجا.</b> الفحص الأصلي
    /// كان بيدّي الفني القدرتين <c>true</c> ويتأكد إنهم
    /// <c>true</c> — فتثبيتهم على <c>true</c> في الكود عدّى من
    /// تحته.</para>
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task The_capabilities_travel_down_exactly_as_stored(
        bool canTest, bool canRepair)
    {
        var h = Build();
        NewTech(h, canTest: canTest, canRepair: canRepair);

        var result = await h.Login.Handle(Login(h), default);

        Assert.Equal(canTest, result.Value.CanTest);
        Assert.Equal(canRepair, result.Value.CanRepair);
    }

    /// <summary>
    /// 🔴 <b>ومفيش بصمة ولا ملح في الرد — ولا في أي خانة.</b>
    /// </summary>
    [Fact]
    public async Task No_hash_or_salt_ever_reaches_the_wire()
    {
        var h = Build();
        var tech = NewTech(h);

        var result = await h.Login.Handle(Login(h), default);

        foreach (var property in result.Value.GetType().GetProperties())
        {
            if (property.GetValue(result.Value) is not string value) continue;

            Assert.NotEqual(tech.PasswordHash, value);
            Assert.NotEqual(tech.Salt, value);
            Assert.DoesNotContain("1234", value);
        }
    }

    /// <summary>
    /// 🔴 <b>والصف الناجح ده مش للسجل — هو دليل تصريح.</b> السيرفر
    /// بيستعمله وقت رفع الشغل عشان يعرف إن الفني كان مصرّح له
    /// <b>وقتها</b>. من غيره، سحب صلاحية بيمسح شغل حصل فعلاً وهو
    /// مصرّح به.
    /// </summary>
    [Fact]
    public async Task A_successful_login_writes_the_authorisation_row()
    {
        var h = Build();
        var tech = NewTech(h);

        await h.Login.Handle(Login(h), default);

        var row = Assert.Single(h.Logins.Attempts);

        Assert.True(row.Success);
        Assert.Equal(tech.Id, row.TechnicianId);
        Assert.Equal(h.RackId, row.RackId);
        Assert.Equal(h.Tenant, row.TenantId);
        Assert.Equal("ahmed", row.AttemptedUsername);
        Assert.NotNull(tech.LastSuccessfulLoginUtc);

        // ⚠️ حفظة واحدة — الصف وتاريخ الدخول مع بعض.
        Assert.Equal(1, h.Work.Saves);
    }

    /// <summary>
    /// ⚠️ <b>والاسم بيتطبّع.</b> الفني بيكتب اسمه بمسافة أو بحرف
    /// كبير، والراكة بتبعته زي ما اتكتب.
    /// </summary>
    [Theory]
    [InlineData("ahmed")]
    [InlineData("AHMED")]
    [InlineData("  Ahmed  ")]
    public async Task The_username_is_normalised(string typed)
    {
        var h = Build();
        NewTech(h);

        var result = await h.Login.Handle(Login(h, username: typed), default);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// 🔴 <b>وفني شركة تانية مش موجود — خالص.</b> لو الشركة جات من
    /// الطلب، أي مفتاح محطة مسروق كان بيفتح فنيي كل الشركات.
    /// </summary>
    [Fact]
    public async Task A_technician_in_another_workshop_does_not_exist_here()
    {
        var h = Build();
        NewTech(h, tenant: Guid.NewGuid());

        var result = await h.Login.Handle(Login(h), default);

        Assert.True(result.IsFailure);
        Assert.Equal(TechnicianAuthCodes.InvalidCredentials, result.Error.Code);
    }

    // =================================================================
    //  الدخول — الرفض
    // =================================================================

    /// <summary>
    /// ⚠️ <b>نفس الكود ونفس الرسالة للاسم المش موجود وللباسورد
    /// الغلط.</b> التفرقة بينهم بتقول للي بيجرّب أسماء مين موجود.
    /// </summary>
    [Fact]
    public async Task A_wrong_password_and_an_unknown_name_read_identically()
    {
        var h = Build();
        NewTech(h);

        var wrong = await h.Login.Handle(Login(h, password: "9999"), default);
        var unknown = await h.Login.Handle(Login(h, username: "salma"), default);

        Assert.Equal(wrong.Error.Code, unknown.Error.Code);
        Assert.Equal(wrong.Error.Description, unknown.Error.Description);
        Assert.Equal(401, wrong.Error.StatusCode);
    }

    /// <summary>
    /// ⚠️ <b>والاسم المش موجود بيتسجّل بردو.</b> محاولات كتير على
    /// أسماء مش موجودة من نفس المحطة دي إشارة تخمين — والإشارة بتضيع
    /// لو سجّلنا اللي لقينا لهم حساب بس.
    /// </summary>
    [Fact]
    public async Task An_unknown_name_is_still_recorded()
    {
        var h = Build();

        await h.Login.Handle(Login(h, username: "salma"), default);

        var row = Assert.Single(h.Logins.Attempts);

        Assert.Equal("salma", row.AttemptedUsername);
        Assert.Null(row.TechnicianId);
        Assert.False(row.Success);
    }

    /// <summary>
    /// ⚠️ <b>والاسم الطويل مابيوصلش العمود أطول منه.</b>
    ///
    /// <para>اللي بيقص هو <c>LoginName.Normalize</c> — مش المعالج.
    /// (كان فيه قص تاني في المعالج، وتحوير شالّه <b>نجا</b> لأنه
    /// مكانش بيعمل حاجة؛ اتشال.)</para>
    /// </summary>
    [Fact]
    public async Task A_very_long_name_never_exceeds_the_column()
    {
        var h = Build();

        await h.Login.Handle(Login(h, username: new string('a', 500)), default);

        Assert.True(
            Assert.Single(h.Logins.Attempts).AttemptedUsername.Length
                <= TechnicianLoginRules.MaxAttemptedUsername);
    }

    /// <summary>
    /// 🔴 <b>والحارس الحقيقي من الانحراف هو ده.</b>
    ///
    /// <para>لو حد وسّع <c>LoginName.MaxLength</c> من غير هجرة على
    /// العمود، الحفظ بيقع بخطأ من القاعدة على <b>كل</b> محاولة دخول
    /// — يعني الورشة كلها بتقف، والسبب سطر في ملف تاني خالص.</para>
    /// </summary>
    [Fact]
    public void A_login_key_can_never_overflow_the_column()
    {
        Assert.True(
            Codlek.Core.Text.LoginName.MaxLength
                <= TechnicianLoginRules.MaxAttemptedUsername,
            "LoginName.MaxLength بقى أطول من عمود AttemptedUsername — "
            + "لازم هجرة على العمود الأول.");
    }

    /// <summary>
    /// 🔴 <b>الإيقاف بيتفحص <u>بعد</u> الباسورد.</b>
    ///
    /// <para>موقوف + باسورد غلط = «بيانات غلط»، مش «موقوف». لو
    /// عكسنا، اللي بيجرّب أسماء كان هيعرف مين موجود ومين موقوف من
    /// غير ما يعرف ولا باسورد واحد.</para>
    /// </summary>
    [Fact]
    public async Task A_suspended_account_with_a_wrong_password_says_wrong_password()
    {
        var h = Build();
        NewTech(h, isActive: false);

        var result = await h.Login.Handle(Login(h, password: "9999"), default);

        Assert.Equal(TechnicianAuthCodes.InvalidCredentials, result.Error.Code);
        Assert.Equal(401, result.Error.StatusCode);
    }

    /// <summary>
    /// ⚠️ <b>والموقوف بيشوف السبب بعد ما يثبت إنه هو.</b>
    /// </summary>
    [Fact]
    public async Task A_suspended_account_with_the_right_password_says_why()
    {
        var h = Build();
        var tech = NewTech(h, isActive: false);

        tech.SuspendedReason = "غياب أسبوع";
        tech.SuspendedByName = "كريم";

        var result = await h.Login.Handle(Login(h), default);

        Assert.True(result.IsFailure);
        Assert.Equal(TechnicianAuthCodes.Suspended, result.Error.Code);
        Assert.Equal(403, result.Error.StatusCode);
        Assert.Contains("غياب أسبوع", result.Error.Description);
        Assert.Contains("كريم", result.Error.Description);

        Assert.Equal(
            TechnicianLoginRules.ReasonSuspended,
            Assert.Single(h.Logins.Attempts).Reason);
    }

    // =================================================================
    //  القفل
    // =================================================================

    [Fact]
    public async Task Ten_failures_in_the_window_lock_the_name_on_this_rack()
    {
        var h = Build();
        NewTech(h);

        Fail(h, "ahmed", TechnicianLoginRules.MaxAttemptsPerWindow);

        var result = await h.Login.Handle(Login(h), default);

        Assert.True(result.IsFailure);
        Assert.Equal(TechnicianAuthCodes.TooManyAttempts, result.Error.Code);
        Assert.Equal(429, result.Error.StatusCode);
    }

    /// <summary>
    /// 🔴 <b>والقفل بيسبق البحث — ولا استعلام واحد.</b>
    ///
    /// <para>مش تحسين: استعلام بالاسم على اسم مقفول بيخلّي زمن الرد
    /// يفرّق بين «اسم موجود» و«اسم مش موجود» — يعني العدّاد بيقفل
    /// التخمين بالباسورد وبيسيب التخمين بالاسم مفتوح.</para>
    /// </summary>
    [Fact]
    public async Task A_locked_name_is_never_looked_up()
    {
        var h = Build();
        NewTech(h);

        Fail(h, "ahmed", TechnicianLoginRules.MaxAttemptsPerWindow);

        await h.Login.Handle(Login(h), default);

        Assert.Equal(0, h.Logins.Lookups);
    }

    /// <summary>
    /// ⚠️ <b>والمحاولة المقفولة بتتسجّل كفاشلة بردو — فالقفل بيمدّ
    /// نفسه.</b> اللي بيطرطق على الشاشة مايستفيدش من الطرطقة.
    /// </summary>
    [Fact]
    public async Task A_locked_attempt_extends_the_lockout()
    {
        var h = Build();
        NewTech(h);

        Fail(h, "ahmed", TechnicianLoginRules.MaxAttemptsPerWindow);

        await h.Login.Handle(Login(h), default);

        Assert.Equal(11, h.Logins.Attempts.Count);
        Assert.Equal(
            TechnicianLoginRules.ReasonTooMany, h.Logins.Attempts[^1].Reason);
    }

    /// <summary>
    /// ⚠️ <b>والقفل مربوط بالمحطة دي.</b> فني مقفول على راكة الاستقبال
    /// لازم يقدر يدخل على راكة الصيانة — الورشة مابتقفش عشان حد
    /// كتب غلط على بنش واحد.
    /// </summary>
    [Fact]
    public async Task The_lockout_does_not_follow_the_name_to_another_rack()
    {
        var h = Build();
        NewTech(h);

        Fail(h, "ahmed", TechnicianLoginRules.MaxAttemptsPerWindow);

        var elsewhere = new TechnicianLoginCommand(
            h.Tenant, Guid.NewGuid(), "RACK-008", "ahmed", "1234", 7);

        Assert.True((await h.Login.Handle(elsewhere, default)).IsSuccess);
    }

    /// <summary>⚠️ والمحاولات القديمة خارج النافذة مابتعدّش.</summary>
    [Fact]
    public async Task Failures_older_than_the_window_are_forgotten()
    {
        var h = Build();
        NewTech(h);

        Fail(h, "ahmed", 20, atUtc: DateTime.UtcNow.AddHours(-1));

        Assert.True((await h.Login.Handle(Login(h), default)).IsSuccess);
    }

    /// <summary>
    /// ⚠️ <b>والاسم الفاضي مابيعملش استعلام عدّ خالص.</b> مفتاح فاضي
    /// بيعدّ كل المحاولات اللي اسمها فاضي من كل الناس — قفل جماعي
    /// على حاجة مش مرتبطة بحد.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_name_is_not_counted(string? username)
    {
        var h = Build();
        NewTech(h);

        var result = await h.Login.Handle(Login(h, username: username), default);

        Assert.Equal(0, h.Logins.FailureCounts);
        Assert.True(result.IsFailure);
        Assert.Equal(TechnicianAuthCodes.InvalidCredentials, result.Error.Code);
    }

    // =================================================================
    //  تغيير الباسورد — الترتيب
    // =================================================================

    /// <summary>
    /// 🔴 <b>القفل قبل كل حاجة — حتى قبل الباسورد الحالي.</b> النقطة
    /// دي عرّافة باسوردات زي الدخول؛ لو العدّاد اتفحص بعد التحقق،
    /// المهاجم بياخد إجابته الأول والقفل بييجي بعد ما الضرر حصل.
    /// </summary>
    [Fact]
    public void Throttling_beats_everything_else()
    {
        var decision = TechnicianPasswordChangeRules.Decide(
            throttled: true, found: true, currentOk: true, isActive: true,
            next: "5678", confirm: "5678", sameAsCurrent: false);

        Assert.False(decision.Ok);
        Assert.Equal(TechnicianAuthCodes.TooManyAttempts, decision.Code);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void A_missing_name_and_a_wrong_current_read_identically(
        bool found, bool currentOk)
    {
        var decision = TechnicianPasswordChangeRules.Decide(
            false, found, currentOk, true, "5678", "5678", false);

        Assert.Equal(TechnicianAuthCodes.InvalidCredentials, decision.Code);
        Assert.Equal("الباسورد الحالي غلط", decision.Message);
    }

    /// <summary>
    /// ⚠️ <b>والإيقاف بعد التحقق</b> — موقوف بباسورد حالي غلط بياخد
    /// «الحالي غلط».
    /// </summary>
    [Fact]
    public void Suspension_is_decided_after_the_current_password()
    {
        var wrong = TechnicianPasswordChangeRules.Decide(
            false, true, currentOk: false, isActive: false, "5678", "5678", false);

        var right = TechnicianPasswordChangeRules.Decide(
            false, true, currentOk: true, isActive: false, "5678", "5678", false);

        Assert.Equal(TechnicianAuthCodes.InvalidCredentials, wrong.Code);
        Assert.Equal(TechnicianAuthCodes.Suspended, right.Code);
    }

    [Fact]
    public void A_short_new_password_is_refused_before_the_confirmation()
    {
        var decision = TechnicianPasswordChangeRules.Decide(
            false, true, true, true, "12", confirm: "99", sameAsCurrent: false);

        Assert.Equal(TechnicianAuthCodes.WeakPassword, decision.Code);
    }

    /// <summary>
    /// ⚠️ <b>و«زي القديم» بعد التأكيد مش قبله.</b> لو جه قبله، الفني
    /// اللي كتب الجديد غلط في خانة التأكيد كان بيشوف «زي القديم»
    /// ويستغرب.
    /// </summary>
    [Fact]
    public void A_mismatch_is_refused_before_same_as_current()
    {
        var decision = TechnicianPasswordChangeRules.Decide(
            false, true, true, true, "5678", confirm: "9999", sameAsCurrent: true);

        Assert.Equal(TechnicianAuthCodes.Mismatch, decision.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    public void The_new_password_has_a_floor(string? next)
    {
        var decision = TechnicianPasswordChangeRules.Decide(
            false, true, true, true, next, next, false);

        Assert.Equal(TechnicianAuthCodes.WeakPassword, decision.Code);
        Assert.Contains(
            TechnicianPasswordChangeRules.MinLength.ToString(), decision.Message);
    }

    /// <summary>
    /// 🔴 <b>الباسورد الحالي الغلط بس هو اللي بيتعدّ.</b> لو كل رفض
    /// اتعدّ، الفني اللي كتب باسورد جديد قصير تلات مرات بيتقفل عليه
    /// <b>الدخول</b> — عقوبة على غلطة إملائية.
    /// </summary>
    [Theory]
    [InlineData(TechnicianAuthCodes.InvalidCredentials, true)]
    [InlineData(TechnicianAuthCodes.WeakPassword, false)]
    [InlineData(TechnicianAuthCodes.Mismatch, false)]
    [InlineData(TechnicianAuthCodes.SameAsCurrent, false)]
    [InlineData(TechnicianAuthCodes.Suspended, false)]
    [InlineData(TechnicianAuthCodes.TooManyAttempts, false)]
    [InlineData(TechnicianAuthCodes.Ok, false)]
    public void Only_a_wrong_current_password_spends_login_credit(
        string code, bool counts)
    {
        Assert.Equal(
            counts, TechnicianPasswordChangeRules.CountsAsFailedAttempt(code));
    }

    // =================================================================
    //  تغيير الباسورد — المعالج
    // =================================================================

    [Fact]
    public async Task A_successful_change_rotates_the_hash_and_the_version()
    {
        var h = Build();
        var tech = NewTech(h, version: 3, mustChange: true);

        string oldHash = tech.PasswordHash;
        string oldSalt = tech.Salt;

        var result = await h.Change.Handle(Change(h), default);

        Assert.True(result.IsSuccess);

        Assert.NotEqual(oldHash, tech.PasswordHash);
        Assert.NotEqual(oldSalt, tech.Salt);
        Assert.Equal(4, tech.CredentialVersion);

        // 🔴 ودي الحاجة اللي الميزة كلها اتعملت عشانها.
        Assert.False(tech.MustChangePassword);

        Assert.Equal(4, result.Value.CredentialVersion);
        Assert.False(result.Value.MustChangePassword);
        Assert.Equal(1, h.Work.Saves);
    }

    /// <summary>
    /// 🔴 <b>ومفيش صف محاولة على التغيير الناجح — لا ناجح ولا
    /// فاشل.</b>
    ///
    /// <para>الصف الناجح دليل تصريح أوفلاين، والفاشل بيصرف من رصيد
    /// دخول الفني. وتغيير باسورد ناجح مش واحد ولا التاني — ولو
    /// كتبناه ناجح، كنا بنمدّ تصريح مالوش أساس.</para>
    /// </summary>
    [Fact]
    public async Task A_successful_change_writes_no_attempt_row()
    {
        var h = Build();
        NewTech(h);

        await h.Change.Handle(Change(h), default);

        Assert.Empty(h.Logins.Attempts);
    }

    /// <summary>
    /// 🔴 <b>ومفيش مهلة أوفلاين في الرد خالص.</b> تغيير الباسورد مش
    /// دخول — تمديد المهلة من غير صف نجاح كان بيخلّي الفني يشتغل
    /// أسبوع والسيرفر يرفض شغله كله وقت الرفع.
    /// </summary>
    [Fact]
    public void The_change_reply_has_no_offline_window()
    {
        var names = typeof(Application.Contracts.Rack.TechnicianPasswordChanged)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("OfflineValidUntilUtc", names);

        // ⚠️ وحراسة: رد الدخول **فيه** الحقل — عشان الفحص ده
        //    مايبقاش بيقيس حاجة مفيش حد عندها الحقل أصلاً.
        Assert.Contains(
            "OfflineValidUntilUtc",
            typeof(Application.Contracts.Rack.TechnicianLoggedIn)
                .GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task A_wrong_current_password_spends_a_login_attempt()
    {
        var h = Build();
        var tech = NewTech(h);

        var result = await h.Change.Handle(Change(h, current: "9999"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error.StatusCode);

        var row = Assert.Single(h.Logins.Attempts);

        Assert.False(row.Success);
        Assert.Equal(tech.Id, row.TechnicianId);
        Assert.Equal(
            TechnicianLoginRules.ReasonChangeWrongCurrent, row.Reason);
    }

    /// <summary>
    /// 🔴 <b>والباسورد الجديد القصير مابيصرفش من رصيد الدخول.</b>
    /// </summary>
    [Theory]
    [InlineData("12", "12", 400)]
    [InlineData("5678", "9999", 400)]
    [InlineData("1234", "1234", 400)]
    public async Task A_policy_refusal_never_spends_login_credit(
        string next, string confirm, int status)
    {
        var h = Build();
        NewTech(h);

        var result = await h.Change.Handle(
            Change(h, next: next, confirm: confirm), default);

        Assert.True(result.IsFailure);
        Assert.Equal(status, result.Error.StatusCode);

        Assert.Empty(h.Logins.Attempts);
        Assert.Equal(0, h.Work.Saves);
    }

    /// <summary>
    /// ⚠️ <b>و«الجديد زي القديم» بيتفحص بالبصمة.</b> النص القديم
    /// عمره ما اتخزّن، فالمقارنة الوحيدة الممكنة هي تحقق البصمة.
    /// </summary>
    [Fact]
    public async Task Reusing_the_current_password_is_caught_by_the_hash()
    {
        var h = Build();
        NewTech(h, password: "1234");

        var result = await h.Change.Handle(
            Change(h, next: "1234", confirm: "1234"), default);

        Assert.Equal(TechnicianAuthCodes.SameAsCurrent, result.Error.Code);
    }

    [Fact]
    public async Task A_locked_name_is_never_looked_up_on_the_change_route_either()
    {
        var h = Build();
        NewTech(h);

        Fail(h, "ahmed", TechnicianLoginRules.MaxAttemptsPerWindow);

        var result = await h.Change.Handle(Change(h), default);

        Assert.Equal(429, result.Error.StatusCode);
        Assert.Equal(0, h.Logins.Lookups);
        Assert.Equal(0, h.Work.Saves);
    }

    /// <summary>
    /// ⚠️ <b>ونفس العدّاد بالظبط — مش عدّاد خاص.</b> دلو منفصل معناه
    /// إن اللي بيخمّن عنده <b>ضعف</b> المحاولات: عشرة على الدخول
    /// وعشرة على التغيير.
    /// </summary>
    [Fact]
    public async Task The_change_route_shares_the_login_counter()
    {
        var h = Build();
        NewTech(h);

        // تسع محاولات غلط على مسار **التغيير**…
        for (int i = 0; i < 9; i++)
            await h.Change.Handle(Change(h, current: "9999"), default);

        // …والعاشرة على مسار **الدخول** بتكمّل العشرة.
        await h.Login.Handle(Login(h, password: "9999"), default);

        // 🔴 فالدخول الصح بقى مقفول.
        var result = await h.Login.Handle(Login(h), default);

        Assert.Equal(TechnicianAuthCodes.TooManyAttempts, result.Error.Code);
    }

    [Fact]
    public async Task A_suspended_account_cannot_change_its_password()
    {
        var h = Build();
        NewTech(h, isActive: false);

        var result = await h.Change.Handle(Change(h), default);

        Assert.Equal(TechnicianAuthCodes.Suspended, result.Error.Code);
        Assert.Equal(403, result.Error.StatusCode);
        Assert.Empty(h.Logins.Attempts);
    }
}
