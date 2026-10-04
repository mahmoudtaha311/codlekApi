using Codlek.Application.Abstractions;
using Codlek.Application.Features.Rack.RegisterRack;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Racks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// تسجيل محطة فحص.
///
/// <para>🔴 <b>ودي النقطة الوحيدة في سطح الراكة اللي بتقبل كتابة
/// من غير مفتاح</b> — لأن المحطة الجديدة مالهاش مفتاح لسه. واللي
/// بيحميها هو كود التفعيل: قصير العمر، بيتستهلك مرة واحدة،
/// وبيتقفل بعد خمس محاولات غلط.</para>
///
/// <para>🔴 <b>وأربع قواعد هنا مالهاش رجعة لو اتكسرت:</b> المفتاح
/// بيرجع مرة واحدة · الكود بيتستهلك مرة واحدة بالظبط · المحاولة
/// الغلط بتتعدّ على كل أكواد نفس البادئة · ورابط المزامنة من
/// الإعدادات مش من الطلب.</para>
/// </summary>
public class RackRegisterTests
{
    private const string SyncUrl = "https://codlek.runasp.net/api/sync/reports";

    private sealed class FakeKeys : IRackKeys
    {
        public int Issued;

        public (string Key, string Hash, string Salt) Issue()
        {
            Issued++;
            string key = "RKKEY00000" + Issued.ToString("0000");

            return (key, "hash:" + key, "salt");
        }

        public bool Verify(string key, string hash, string salt) => hash == "hash:" + key;
    }

    private sealed class FakeCodes : IActivationCodeHasher
    {
        public (string Hash, string Salt) Create(string code) => ("hash:" + code, "salt");

        public bool Verify(string code, string hash, string salt) => hash == "hash:" + code;
    }

    private sealed class FakeCounters : ITenantCounters
    {
        public int Next = 1;

        public Task<int> NextAsync(Guid t, string name, CancellationToken ct = default) =>
            Task.FromResult(Next++);

        public Task<int> ReserveAsync(
            Guid t, string name, int count, CancellationToken ct = default)
        {
            int start = Next;
            Next += count;
            return Task.FromResult(start);
        }
    }

    private sealed class FakeRacks : IRackRepository
    {
        public readonly List<Core.Entities.Rack> Racks = [];
        public readonly List<RackPairingCode> Codes = [];
        public readonly Dictionary<Guid, string> Tenants = [];

        /// <summary>⚠️ بيحفظ هل الاستهلاك نجح — الفحص بيقيس السباق.</summary>
        public bool ConsumeSucceeds = true;
        public int ConsumeCalls;
        public readonly List<(Guid Code, Guid Rack)> Links = [];

        public Task<IReadOnlyList<RackPairingCode>> CodesByPrefixAsync(
            string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RackPairingCode>>(
                Codes.Where(c => c.CodePrefix == prefix && c.ConsumedAtUtc == null).ToList());

        public Task<bool> ConsumeCodeAsync(
            Guid codeId, DateTime atUtc, CancellationToken ct = default)
        {
            ConsumeCalls++;

            if (!ConsumeSucceeds) return Task.FromResult(false);

            var row = Codes.FirstOrDefault(c => c.Id == codeId && c.ConsumedAtUtc == null);

            if (row is null) return Task.FromResult(false);

            row.ConsumedAtUtc = atUtc;
            return Task.FromResult(true);
        }

        public Task LinkCodeToRackAsync(
            Guid codeId, Guid rackId, CancellationToken ct = default)
        {
            Links.Add((codeId, rackId));
            return Task.CompletedTask;
        }

        public void Add(Core.Entities.Rack rack) => Racks.Add(rack);

        public Task<IReadOnlyList<Core.Entities.Rack>> TwinsByInstallationAsync(
            Guid t, string installationId, Guid except, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Core.Entities.Rack>>(
                Racks.Where(r => r.TenantId == t
                              && r.InstallationId == installationId
                              && r.Id != except
                              && r.Status != RackStatus.Revoked)
                    .ToList());

        public Task<string?> TenantNameAsync(Guid t, CancellationToken ct = default) =>
            Task.FromResult(Tenants.GetValueOrDefault(t));

        public Task<IReadOnlyList<Core.Entities.Rack>> ActiveByKeyPrefixAsync(
            string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Core.Entities.Rack>>([]);

        public Task<IReadOnlyList<Core.Entities.Rack>> ListAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Core.Entities.Rack>>(Racks);

        public Task<Core.Entities.Rack?> FindAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Core.Entities.Rack?>(null);

        public Task<IReadOnlyList<RackPairingCode>> PendingCodesAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RackPairingCode>>([]);

        public Task<RackPairingCode?> FindCodeAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<RackPairingCode?>(null);

        public void AddCode(RackPairingCode code) => Codes.Add(code);

        public void RemoveCode(RackPairingCode code) => Codes.Remove(code);
    }

    private sealed record Harness(
        FakeRacks Racks,
        FakeKeys Keys,
        FakeCounters Counters,
        FakeAuditTrail Audit,
        FakeUnitOfWork Work,
        RegisterRackCommandHandler Handler,
        Guid Tenant);

    private static Harness Build()
    {
        var racks = new FakeRacks();
        var keys = new FakeKeys();
        var counters = new FakeCounters();
        var audit = new FakeAuditTrail();
        var work = new FakeUnitOfWork();
        var tenant = Guid.NewGuid();

        racks.Tenants[tenant] = "ورشة كودلك";

        return new Harness(
            racks, keys, counters, audit, work,
            new RegisterRackCommandHandler(
                racks, keys, new FakeCodes(), counters, audit, work,
                NullLogger<RegisterRackCommandHandler>.Instance),
            tenant);
    }

    private static RackPairingCode NewCode(
        Harness h, string code, Guid? tenant = null,
        DateTime? expiresAtUtc = null, int failedAttempts = 0,
        string intendedName = "محطة أحمد", string intendedLocation = "الدور الأول")
    {
        var row = new RackPairingCode
        {
            TenantId = tenant ?? h.Tenant,
            CodeHash = "hash:" + code,
            Salt = "salt",
            CodePrefix = PairingCode.Prefix(code),
            IntendedName = intendedName,
            IntendedLocation = intendedLocation,
            CreatedByName = "المالك",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(10),
            FailedAttempts = failedAttempts,
        };

        h.Racks.Codes.Add(row);
        return row;
    }

    private static RegisterRackCommand Register(
        string? code, string? name = "محطة البنش", string install = "disk-001") =>
        new(code, name, install, "LAPTOP-01", "1.4.0", "10.0.0.5", SyncUrl);

    // =================================================================
    //  النجاح
    // =================================================================

    [Fact]
    public async Task A_valid_code_registers_the_rack_and_returns_its_key_once()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX");

        var result = await h.Handler.Handle(Register("4F7K-92QX"), default);

        Assert.True(result.IsSuccess);

        var rack = Assert.Single(h.Racks.Racks);

        Assert.Equal("RACK-001", result.Value.RackCode);
        Assert.Equal("ورشة كودلك", result.Value.TenantName);
        Assert.Equal(h.Tenant, result.Value.TenantId);

        // 🔴 المفتاح في الرد، وبصمته بس في الصف.
        Assert.NotEmpty(result.Value.ApiKey);
        Assert.Equal("hash:" + result.Value.ApiKey, rack.ApiKeyHash);
        Assert.Equal(RackKey.Prefix(result.Value.ApiKey), rack.KeyPrefix);

        // ⚠️ والصف مافيهوش المفتاح خام في أي خانة.
        foreach (var property in typeof(Core.Entities.Rack).GetProperties())
        {
            if (property.GetValue(rack) is string value && value.Length > 0)
                Assert.NotEqual(result.Value.ApiKey, value);
        }

        Assert.Equal(RackStatus.Active, rack.Status);
        Assert.NotNull(rack.RegisteredAtUtc);
        Assert.NotNull(rack.KeyIssuedAtUtc);
    }

    /// <summary>
    /// 🔴 <b>رابط المزامنة من الإعدادات، مش من الطلب.</b>
    ///
    /// <para>النسخة الأولى في القديم كانت بتبنيه من
    /// <c>Request.Host</c> — يعني اللي بيسجّل كان بيحدّد بترويسة
    /// فين الراكة هترفع شغلها بعد كده، والراكة بتخزّن العنوان
    /// وتفضل عليه <b>شهور</b>.</para>
    /// </summary>
    [Fact]
    public async Task The_sync_url_comes_from_configuration()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX");

        var result = await h.Handler.Handle(Register("4F7K-92QX"), default);

        Assert.Equal(SyncUrl, result.Value.SyncUrl);
    }

    /// <summary>
    /// ⚠️ <b>اسم الراكة: اللي بعتته يكسب، وبعده نيّة المدير، وبعده
    /// اسم عام.</b> الفني قدام البنش عارف هو فين، والمدير كتب
    /// نيّته من أسبوع.
    /// </summary>
    [Theory]
    [InlineData("محطة البنش", "محطة أحمد", "محطة البنش")]
    [InlineData("", "محطة أحمد", "محطة أحمد")]
    [InlineData(null, "محطة أحمد", "محطة أحمد")]
    [InlineData("   ", "محطة أحمد", "محطة أحمد")]
    [InlineData(null, "", "راكة")]
    [InlineData("", "   ", "راكة")]
    public void The_name_falls_back_in_order(string? sent, string? intended, string expected)
    {
        Assert.Equal(expected, RackRegistration.Name(sent, intended));
    }

    /// <summary>
    /// ⚠️ <b>والمكان من نيّة المدير</b> — الراكة مابتعرفش هي في
    /// أنهي دور.
    /// </summary>
    [Fact]
    public async Task The_location_comes_from_the_code()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX", intendedLocation: "الدور التالت");

        await h.Handler.Handle(Register("4F7K-92QX"), default);

        Assert.Equal("الدور التالت", Assert.Single(h.Racks.Racks).Location);
    }

    /// <summary>
    /// 🔴 <b>الكود بيتربط بالمحطة — وده الدليل الوحيد على إنها
    /// اتسجّلت بأنهي إذن.</b>
    /// </summary>
    [Fact]
    public async Task The_code_is_linked_to_the_rack_it_created()
    {
        var h = Build();
        var code = NewCode(h, "4F7K-92QX");

        await h.Handler.Handle(Register("4F7K-92QX"), default);

        var rack = Assert.Single(h.Racks.Racks);
        var link = Assert.Single(h.Racks.Links);

        Assert.Equal(code.Id, link.Code);
        Assert.Equal(rack.Id, link.Rack);
        Assert.NotNull(code.ConsumedAtUtc);
    }

    // =================================================================
    //  الكود الغلط
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الكود القصير مابيوصلش القاعدة خالص.</b>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC")]
    [InlineData("4F7K-92")]
    public async Task A_short_code_is_refused_before_any_query(string? code)
    {
        var h = Build();
        NewCode(h, "4F7K-92QX");

        var result = await h.Handler.Handle(Register(code), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackRegisterErrors.InvalidCode, result.Error);
        Assert.Empty(h.Racks.Racks);
        Assert.Equal(0, h.Racks.ConsumeCalls);
    }

    /// <summary>
    /// ⚠️ <b>والكود بيتكبّر قبل المقارنة</b> — الفني بيكتبه من
    /// الشاشة والكيبورد ممكن يكون على حروف صغيرة.
    /// </summary>
    [Fact]
    public async Task A_lowercase_code_still_matches()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX");

        var result = await h.Handler.Handle(Register("4f7k-92qx"), default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task An_unknown_code_is_a_404()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX");

        var result = await h.Handler.Handle(Register("ZZZZ-ZZZZ"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error.StatusCode);
        Assert.Equal("InvalidCode", result.Error.Code);
    }

    // =================================================================
    //  عدّاد المحاولات — القاعدة المقصودة
    // =================================================================

    /// <summary>
    /// 🔴 <b>المحاولة الغلط بتتعدّ على <u>كل</u> أكواد نفس
    /// البادئة.</b>
    ///
    /// <para>وده اللي بيخلّي التخمين غير مجدي بدل ما يبقى مجرد
    /// إبطاء. والزيادة الجماعية دي <b>مقصودة ومش حاجة
    /// تتصلّح</b>.</para>
    /// </summary>
    [Fact]
    public async Task A_wrong_attempt_counts_against_every_code_sharing_the_prefix()
    {
        var h = Build();

        var mine = NewCode(h, "4F7K-AAAA");
        var other = NewCode(h, "4F7K-BBBB", tenant: Guid.NewGuid());

        // كود ببادئة ٤F7K بس مش بتاع ولا واحد منهم.
        await h.Handler.Handle(Register("4F7K-ZZZZ"), default);

        Assert.Equal(1, mine.FailedAttempts);
        Assert.Equal(1, other.FailedAttempts);
    }

    /// <summary>
    /// 🔴 <b>والزيادة عابرة للشركات — وده بالظبط اللي القاعدة
    /// بتقوله.</b>
    ///
    /// <para>خمس محاولات غلط على كود شركة أ بتقفل كود شركة ب لو
    /// البادئة واحدة. والتصادم نادر (٣١ أس ٤ = ٩٢٣٥٢١ بادئة)،
    /// والمكسب إن التخمين غير مجدي.</para>
    /// </summary>
    [Fact]
    public async Task Five_wrong_attempts_lock_out_a_correct_code_in_another_tenant()
    {
        var h = Build();

        var victim = NewCode(h, "4F7K-BBBB", tenant: Guid.NewGuid());

        for (int i = 0; i < RackRegistration.MaxFailedAttempts; i++)
            await h.Handler.Handle(Register("4F7K-ZZZZ"), default);

        Assert.Equal(5, victim.FailedAttempts);

        // 🔴 والكود **الصح** بقى بيترفض.
        var result = await h.Handler.Handle(Register("4F7K-BBBB"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackRegisterErrors.InvalidCode, result.Error);
    }

    /// <summary>
    /// ⚠️ <b>والكود المنتهي بيتعدّ عليه كمان</b> — هو في المرشّحين.
    /// </summary>
    [Fact]
    public async Task An_expired_code_in_the_prefix_is_also_incremented()
    {
        var h = Build();

        var expired = NewCode(
            h, "4F7K-AAAA", expiresAtUtc: DateTime.UtcNow.AddMinutes(-30));

        await h.Handler.Handle(Register("4F7K-ZZZZ"), default);

        Assert.Equal(1, expired.FailedAttempts);
    }

    /// <summary>
    /// 🔴 <b>والمقفول مابيتطابقش — فالرد عليه «غلط» مش
    /// «مقفول».</b> اللي بيحاول مايعرفش إنه لقى الكود الصح
    /// وقفله.
    /// </summary>
    [Fact]
    public async Task A_locked_out_code_reads_as_merely_wrong()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX", failedAttempts: RackRegistration.MaxFailedAttempts);

        var result = await h.Handler.Handle(Register("4F7K-92QX"), default);

        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCode", result.Error.Code);
        Assert.DoesNotContain("مقفول", result.Error.Description);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(99, true)]
    public void Lockout_starts_at_five(int attempts, bool locked)
    {
        Assert.Equal(locked, RackRegistration.LockedOut(attempts));
    }

    // =================================================================
    //  الانتهاء والسباق
    // =================================================================

    /// <summary>
    /// ⚠️ <b>والانتهاء رد مختلف — <c>410</c> مش <c>404</c>.</b>
    /// «جرّب كود تاني» و«الكود ده خلص» تعليمتين مختلفتين للفني.
    /// </summary>
    [Fact]
    public async Task An_expired_code_says_so()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX", expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var result = await h.Handler.Handle(Register("4F7K-92QX"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(410, result.Error.StatusCode);
        Assert.Equal("CodeExpired", result.Error.Code);

        // ⚠️ ومفيش استهلاك — الكود فضل زي ما هو.
        Assert.Equal(0, h.Racks.ConsumeCalls);
    }

    /// <summary>
    /// 🔴 <b>راكتين بنفس الكود: واحدة بس تكسب.</b>
    ///
    /// <para>الاستهلاك تحديث مشروط، واللي خسر السباق بياخد
    /// <c>409</c> — ومابيكمّلش.</para>
    /// </summary>
    [Fact]
    public async Task Losing_the_consume_race_gives_a_conflict_and_creates_nothing()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX");

        h.Racks.ConsumeSucceeds = false;

        var result = await h.Handler.Handle(Register("4F7K-92QX"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error.StatusCode);
        Assert.Equal("CodeUsed", result.Error.Code);

        // 🔴 ولا محطة ولا مفتاح ولا سطر سجل.
        Assert.Empty(h.Racks.Racks);
        Assert.Equal(0, h.Keys.Issued);
        Assert.Empty(h.Audit.RackLines);
    }

    /// <summary>
    /// ⚠️ <b>والشركة المش موجودة <c>423</c>.</b> الكود اتعمل لشركة
    /// اتمسحت — واللي بيسجّل مالوش يد فيها.
    /// </summary>
    [Fact]
    public async Task A_code_for_a_deleted_tenant_is_locked_not_wrong()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX", tenant: Guid.NewGuid());

        var result = await h.Handler.Handle(Register("4F7K-92QX"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(423, result.Error.StatusCode);
        Assert.Equal("TenantMissing", result.Error.Code);
        Assert.Empty(h.Racks.Racks);
    }

    /// <summary>⚠️ وكل كود حالة مختلف عن التاني.</summary>
    [Fact]
    public void Every_failure_has_its_own_status_and_code()
    {
        Error[] all =
        [
            RackRegisterErrors.InvalidCode,
            RackRegisterErrors.CodeExpired,
            RackRegisterErrors.CodeUsed,
            RackRegisterErrors.TenantMissing,
        ];

        Assert.Equal(all.Length, all.Select(e => e.StatusCode).Distinct().Count());
        Assert.Equal(all.Length, all.Select(e => e.Code).Distinct().Count());
    }

    // =================================================================
    //  السجل
    // =================================================================

    /// <summary>
    /// 🔴 <b>السطر فاعله محطة، والشركة من الكود مش من توكن.</b>
    ///
    /// <para>طلب التسجيل مالوش توكن خالص —
    /// <c>ICurrentUser.TenantId</c> بترمي لو اتندهت عليه.</para>
    /// </summary>
    [Fact]
    public async Task The_audit_line_has_a_rack_actor_and_the_tenant_from_the_code()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX");

        await h.Handler.Handle(Register("4F7K-92QX"), default);

        var paired = h.Audit.RackLines.Single(
            l => l.Action == AuditActions.RackPaired);

        Assert.Equal(h.Tenant, paired.Actor.TenantId);
        Assert.Equal("10.0.0.5", paired.Actor.Ip);
        Assert.Equal("Rack", paired.EntityType);
        Assert.Equal("RACK-001", paired.Code);
        Assert.Contains("المالك", paired.Summary);

        // ⚠️ وتفاصيل الجهاز في السجل — هي اللي بتخلّي اشتباه
        //    الاستنساخ قابل للمراجعة بعد أسبوع.
        Assert.Contains("disk-001", paired.DataJson);
        Assert.Contains("1.4.0", paired.DataJson);
    }

    /// <summary>
    /// 🔴 <b>والمفتاح عمره ما يدخل السجل.</b>
    /// </summary>
    [Fact]
    public async Task The_api_key_never_reaches_the_audit_trail()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX");

        var result = await h.Handler.Handle(Register("4F7K-92QX"), default);

        foreach (var line in h.Audit.RackLines)
        {
            Assert.DoesNotContain(result.Value.ApiKey, line.Summary);
            Assert.DoesNotContain(result.Value.ApiKey, line.DataJson);
        }
    }

    /// <summary>
    /// ⚠️ <b>ولا الكود نفسه.</b> واحد بيقرا السجل ماينفعش يطلع منه
    /// بكود يسجّل بيه محطة.
    /// </summary>
    [Fact]
    public async Task The_pairing_code_never_reaches_the_audit_trail()
    {
        var h = Build();
        NewCode(h, "4F7K-92QX");

        await h.Handler.Handle(Register("4F7K-92QX"), default);

        foreach (var line in h.Audit.RackLines)
        {
            Assert.DoesNotContain("4F7K-92QX", line.Summary);
            Assert.DoesNotContain("4F7K-92QX", line.DataJson);
        }
    }

    // =================================================================
    //  اشتباه الاستنساخ
    // =================================================================

    /// <summary>
    /// 🔴 <b>نفس هوية القرص مرتين = اشتباه استنساخ — <u>تنبيه مش
    /// منع</u>.</b>
    ///
    /// <para>الرفض القاطع بيقفل راكة شرعية يوم ما فني ينقل الهارد
    /// لبنش تاني — ومحدّش هيربط الحدثين.</para>
    /// </summary>
    [Fact]
    public async Task A_duplicate_installation_id_is_flagged_not_blocked()
    {
        var h = Build();
        NewCode(h, "4F7K-AAAA");
        NewCode(h, "4F7K-BBBB");

        var first = await h.Handler.Handle(
            Register("4F7K-AAAA", install: "same-disk"), default);

        var second = await h.Handler.Handle(
            Register("4F7K-BBBB", install: "same-disk"), default);

        // 🔴 الاتنين اتسجّلوا.
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, h.Racks.Racks.Count);

        // ⚠️ وسطر اشتباه واحد — على التانية.
        var suspect = h.Audit.RackLines.Single(
            l => l.Action == AuditActions.RackCloneSuspected);

        Assert.Equal("النظام", suspect.Actor.Name);
        Assert.Contains("اتستنسخت", suspect.Summary);
    }

    /// <summary>
    /// ⚠️ <b>والفاعل «النظام» مش المحطة.</b> المحطة مااعملتش حاجة
    /// غلط — اللي لاحظ هو السيرفر، وكتابة المحطة كفاعل كانت بتتقري
    /// اتهام.
    /// </summary>
    [Fact]
    public async Task The_clone_warning_is_attributed_to_the_system()
    {
        var h = Build();
        NewCode(h, "4F7K-AAAA");
        NewCode(h, "4F7K-BBBB");

        await h.Handler.Handle(Register("4F7K-AAAA", install: "same-disk"), default);
        await h.Handler.Handle(Register("4F7K-BBBB", install: "same-disk"), default);

        var suspect = h.Audit.RackLines.Single(
            l => l.Action == AuditActions.RackCloneSuspected);

        Assert.NotEqual("محطة البنش", suspect.Actor.Name);
        Assert.Equal("", suspect.Actor.Ip);
    }

    /// <summary>⚠️ وهوية قرص فاضية مابتولّدش اشتباه.</summary>
    [Fact]
    public async Task An_empty_installation_id_never_triggers_a_warning()
    {
        var h = Build();
        NewCode(h, "4F7K-AAAA");
        NewCode(h, "4F7K-BBBB");

        await h.Handler.Handle(Register("4F7K-AAAA", install: ""), default);
        await h.Handler.Handle(Register("4F7K-BBBB", install: ""), default);

        Assert.DoesNotContain(h.Audit.RackLines,
            l => l.Action == AuditActions.RackCloneSuspected);
    }

    /// <summary>
    /// ⚠️ <b>والملغية مستبعدة من البحث:</b> محطة اتلغت وهوية قرصها
    /// اتسجّلت تاني ده تسجيل جديد مشروع مش استنساخ.
    /// </summary>
    [Fact]
    public async Task A_revoked_twin_does_not_trigger_a_warning()
    {
        var h = Build();
        NewCode(h, "4F7K-AAAA");
        NewCode(h, "4F7K-BBBB");

        await h.Handler.Handle(Register("4F7K-AAAA", install: "same-disk"), default);

        h.Racks.Racks[0].Status = RackStatus.Revoked;

        await h.Handler.Handle(Register("4F7K-BBBB", install: "same-disk"), default);

        Assert.DoesNotContain(h.Audit.RackLines,
            l => l.Action == AuditActions.RackCloneSuspected);
    }

    // =================================================================
    //  كود المحطة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>تلات خانات بأصفار.</b> رقم من غير حشو كان بيخلّي
    /// الترتيب الأبجدي يحط <c>RACK-10</c> قبل <c>RACK-2</c>.
    /// </summary>
    [Theory]
    [InlineData(1, "RACK-001")]
    [InlineData(2, "RACK-002")]
    [InlineData(10, "RACK-010")]
    [InlineData(999, "RACK-999")]
    [InlineData(1000, "RACK-1000")]
    public void The_rack_code_is_three_padded_digits(int number, string expected)
    {
        Assert.Equal(expected, RackRegistration.Code(number));
    }

    [Fact]
    public async Task Each_registration_takes_the_next_number()
    {
        var h = Build();
        NewCode(h, "4F7K-AAAA");
        NewCode(h, "4F7K-BBBB");

        var first = await h.Handler.Handle(Register("4F7K-AAAA"), default);
        var second = await h.Handler.Handle(Register("4F7K-BBBB"), default);

        Assert.Equal("RACK-001", first.Value.RackCode);
        Assert.Equal("RACK-002", second.Value.RackCode);
    }
}
