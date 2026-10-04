using Codlek.Application.Abstractions;
using Codlek.Application.Features.Racks;
using Codlek.Application.Features.Racks.CreateActivationCode;
using Codlek.Application.Features.Racks.DeleteActivationCode;
using Codlek.Application.Features.Racks.GetActivationCodes;
using Codlek.Application.Features.Racks.GetStations;
using Codlek.Application.Features.Racks.RevokeStation;
using Codlek.Application.Features.Racks.SetStationStatus;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Racks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// إدارة محطات الفحص.
///
/// <para>🔴 <b>أربع قواعد مالهاش رجعة لو اتكسرت:</b> كود التفعيل
/// الكامل بيرجع مرة واحدة وبعدها بصمته بس، والمفتاح وبصمته عمرهم
/// ما يخرجوا في قايمة، والملغية نهائياً مابترجعش، والكود اللي
/// اتفعّلت بيه محطة مابيتمسحش.</para>
///
/// <para>⚠️ والتالتة والرابعة <b>مش موجودين في الكيان</b> — مفيش
/// حقل بيمنعهم. هما شرطين مكتوبين في معالج، فمفيش حاجة غير الفحوص
/// دي بتمسكهم.</para>
/// </summary>
public class RackSliceTests
{
    /// <summary>بصمة مزيّفة — الفحوص بتقيس القرار مش التشفير.</summary>
    private sealed class FakeCodeHasher : IActivationCodeHasher
    {
        public readonly List<string> Hashed = [];

        public (string Hash, string Salt) Create(string code)
        {
            Hashed.Add(code);
            return ("hash:" + code, "salt:" + Hashed.Count);
        }
    }

    private sealed class FakeRackRepository : IRackRepository
    {
        public readonly List<Rack> Racks = [];
        public readonly List<RackPairingCode> Codes = [];
        public readonly List<RackPairingCode> Removed = [];

        public Task<IReadOnlyList<Rack>> ListAsync(Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Rack>>(
                Racks.Where(r => r.TenantId == t)
                    .OrderBy(r => r.RackCode, StringComparer.Ordinal)
                    .ThenBy(r => r.Id)
                    .ToList());

        public Task<Rack?> FindAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Racks.FirstOrDefault(r => r.Id == id && r.TenantId == t));

        public Task<IReadOnlyList<RackPairingCode>> PendingCodesAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RackPairingCode>>(
                Codes.Where(c => c.TenantId == t && c.ConsumedAtUtc == null)
                    .OrderByDescending(c => c.CreatedAtUtc)
                    .ThenBy(c => c.Id)
                    .ToList());

        public Task<RackPairingCode?> FindCodeAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Codes.FirstOrDefault(c => c.Id == id && c.TenantId == t));

        public void AddCode(RackPairingCode code) => Codes.Add(code);

        public void RemoveCode(RackPairingCode code)
        {
            Codes.Remove(code);
            Removed.Add(code);
        }
    }

    private sealed record Harness(
        FakeRackRepository Repo,
        FakeCodeHasher Hasher,
        FakeAuditTrail Audit,
        FakeUnitOfWork Work,
        FakeCurrentUser Me,
        GetStationsQueryHandler Stations,
        GetActivationCodesQueryHandler CodeList,
        CreateActivationCodeCommandHandler CreateCode,
        DeleteActivationCodeCommandHandler DeleteCode,
        SetStationStatusCommandHandler Status,
        RevokeStationCommandHandler Revoke);

    private static Harness Build()
    {
        var repo = new FakeRackRepository();
        var hasher = new FakeCodeHasher();
        var audit = new FakeAuditTrail();
        var work = new FakeUnitOfWork();
        var me = new FakeCurrentUser(UserRole.Owner);

        return new Harness(
            repo, hasher, audit, work, me,
            new GetStationsQueryHandler(repo, me),
            new GetActivationCodesQueryHandler(repo, me),
            new CreateActivationCodeCommandHandler(
                repo, hasher, audit, work, me,
                NullLogger<CreateActivationCodeCommandHandler>.Instance),
            new DeleteActivationCodeCommandHandler(repo, audit, work, me),
            new SetStationStatusCommandHandler(repo, audit, work, me),
            new RevokeStationCommandHandler(repo, audit, work, me));
    }

    private static Rack Station(
        Harness h, string code = "RACK-001", string name = "محطة أحمد",
        RackStatus status = RackStatus.Active)
    {
        var row = new Rack
        {
            TenantId = h.Me.TenantId,
            RackCode = code,
            Name = name,
            Location = "الدور الأول",
            Status = status,
            ApiKeyHash = "secret-hash",
            Salt = "secret-salt",
            KeyPrefix = "RK12345678",
            AppVersion = "1.4.0",
            ReportsReceived = 12,
        };

        h.Repo.Racks.Add(row);
        return row;
    }

    private static RackPairingCode Code(
        Harness h, string prefix = "4F7K", string name = "محطة جديدة",
        DateTime? expiresAtUtc = null, DateTime? consumedAtUtc = null,
        int failedAttempts = 0)
    {
        var row = new RackPairingCode
        {
            TenantId = h.Me.TenantId,
            CodeHash = "hash",
            Salt = "salt",
            CodePrefix = prefix,
            IntendedName = name,
            IntendedLocation = "الورشة",
            CreatedByName = "كريم",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(10),
            ConsumedAtUtc = consumedAtUtc,
            FailedAttempts = failedAttempts,
        };

        h.Repo.Codes.Add(row);
        return row;
    }

    // =================================================================
    //  كود التفعيل — الإنشاء
    // =================================================================

    [Fact]
    public async Task Creating_a_code_returns_the_full_code_once_and_stores_only_its_hash()
    {
        var h = Build();

        var result = await h.CreateCode.Handle(
            new CreateActivationCodeCommand("محطة أحمد", "الدور التاني"), default);

        Assert.True(result.IsSuccess);

        string issued = result.Value.Code;
        var row = Assert.Single(h.Repo.Codes);

        // ⚠️ البصمة اتعملت من الكود المرجّع — مش من نص تاني.
        Assert.Equal("hash:" + issued, row.CodeHash);
        Assert.Equal(issued, Assert.Single(h.Hasher.Hashed));
        Assert.Equal(PairingCode.Prefix(issued), row.CodePrefix);

        /*
          🔴 **ومفيش حقل في الكيان شايل الكود خام.**

          ⚠️ والفحص بالانعكاس مش على قيمة. «البصمة مابتكشفش الكود»
          خاصية بتاعة `PasswordHasher` نفسه — وفحوصه في
          `LegacyPasswordTests` (نفس الكلمة بتدّي بصمتين مختلفتين،
          والتحقق بيعدّي). والمصفّف **هنا** مزيّف وبيحشر الكود جوّه
          البصمة عن قصد عشان نقيس إنه اتنده بالكود الصح، فمينفعش
          يتقاس بيه حاجة عن إخفاء الكود. اللي ينفع يتقاس هو الشكل:
          عمود اتضاف «للتسهيل» وبيخزّن الكود زي ما هو.
        */
        foreach (var property in typeof(RackPairingCode).GetProperties())
        {
            if (property.GetValue(row) is string value && value.Length > 0)
                Assert.NotEqual(issued, value);
        }

        Assert.Equal("محطة أحمد", row.IntendedName);
        Assert.Equal("الدور التاني", row.IntendedLocation);
        Assert.Equal(h.Me.Id, row.CreatedByUserId);
        Assert.Equal(h.Me.DisplayName, row.CreatedByName);
        Assert.Null(row.ConsumedAtUtc);
    }

    /// <summary>
    /// 🔴 واحد بيقرا السجل ماينفعش يطلع منه بكود يفعّل بيه محطة.
    /// </summary>
    [Fact]
    public async Task The_code_itself_never_reaches_the_audit_line()
    {
        var h = Build();

        var result = await h.CreateCode.Handle(
            new CreateActivationCodeCommand("محطة أحمد", null), default);

        string issued = result.Value.Code;
        var line = Assert.Single(h.Audit.Lines);

        Assert.Equal(AuditActions.RackCodeCreated, line.Action);
        Assert.DoesNotContain(issued, line.Summary);
        Assert.DoesNotContain(issued, line.Code);

        // ⚠️ ولا البادئة كمان — المسح هو اللي بيكتبها.
        Assert.DoesNotContain(PairingCode.Prefix(issued), line.Summary);
        Assert.Equal("", line.Code);

        Assert.Contains("محطة أحمد", line.Summary);
    }

    [Fact]
    public async Task Creating_a_code_saves_the_row_and_the_audit_line_together()
    {
        var h = Build();

        await h.CreateCode.Handle(new CreateActivationCodeCommand("محطة أحمد", null), default);

        /*
          🔴 **حفظة واحدة.**

          القديم كان بيحفظ الصف جوّه الخدمة وبعدين يحفظ السجل تاني —
          وقوع القاعدة بين الاتنين كان بيدّي كود صالح مالوش سطر في
          السجل.
        */
        Assert.Equal(1, h.Work.Saves);
    }

    [Fact]
    public async Task The_expiry_comes_from_the_lifetime_constant()
    {
        var h = Build();
        var before = DateTime.UtcNow;

        var result = await h.CreateCode.Handle(
            new CreateActivationCodeCommand("محطة أحمد", null), default);

        var expected = before.Add(PairingCode.Lifetime);

        Assert.True(result.Value.ExpiresAtUtc >= expected.AddSeconds(-5));
        Assert.True(result.Value.ExpiresAtUtc <= expected.AddSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("م")]
    public async Task A_station_name_shorter_than_the_minimum_is_refused(string? name)
    {
        var h = Build();

        var result = await h.CreateCode.Handle(
            new CreateActivationCodeCommand(name, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackErrors.NameTooShort, result.Error);

        // ⚠️ ولا صف ولا سطر سجل ولا حفظة.
        Assert.Empty(h.Repo.Codes);
        Assert.Empty(h.Audit.Lines);
        Assert.Equal(0, h.Work.Saves);
    }

    /// <summary>
    /// ⚠️ المسافات بتتشال — «  محطة أحمد  » اسمها «محطة أحمد»، مش
    /// اسم بمسافات بيتعرض جنب كل فحص.
    /// </summary>
    [Fact]
    public async Task The_name_and_location_are_trimmed()
    {
        var h = Build();

        await h.CreateCode.Handle(
            new CreateActivationCodeCommand("  محطة أحمد  ", "  الورشة  "), default);

        var row = Assert.Single(h.Repo.Codes);

        Assert.Equal("محطة أحمد", row.IntendedName);
        Assert.Equal("الورشة", row.IntendedLocation);
    }

    [Fact]
    public async Task A_missing_location_becomes_an_empty_string_not_null()
    {
        var h = Build();

        await h.CreateCode.Handle(new CreateActivationCodeCommand("محطة أحمد", null), default);

        // ⚠️ العمود مش بيقبل null، والعقد بيرجّع string مش string?.
        Assert.Equal("", Assert.Single(h.Repo.Codes).IntendedLocation);
    }

    // =================================================================
    //  كود التفعيل — القايمة
    // =================================================================

    [Fact]
    public async Task The_code_list_never_carries_the_hash_or_the_salt()
    {
        var h = Build();
        Code(h, prefix: "4F7K");

        var result = await h.CodeList.Handle(new GetActivationCodesQuery(), default);
        var row = Assert.Single(result.Value);

        Assert.Equal("4F7K", row.Prefix);

        /*
          🔴 **البصمة والملح مش في العقد خالص.**

          الفحص بالانعكاس مش على القيمة: حقل اسمه `hash` في عقد
          بيترجع للوحة هو المشكلة نفسها، حتى لو فاضي النهاردة.
        */
        var names = typeof(Codlek.Application.Contracts.Racks.ActivationCodeRow)
            .GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();

        Assert.DoesNotContain("codehash", names);
        Assert.DoesNotContain("hash", names);
        Assert.DoesNotContain("salt", names);
        Assert.DoesNotContain("code", names);
    }

    [Fact]
    public async Task An_expired_code_is_flagged_expired_and_a_live_one_is_not()
    {
        var h = Build();
        Code(h, prefix: "LIVE", expiresAtUtc: DateTime.UtcNow.AddMinutes(5));
        Code(h, prefix: "DEAD", expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var result = await h.CodeList.Handle(new GetActivationCodesQuery(), default);

        Assert.False(result.Value.Single(r => r.Prefix == "LIVE").IsExpired);
        Assert.True(result.Value.Single(r => r.Prefix == "DEAD").IsExpired);
    }

    /// <summary>
    /// 🔴 الكود اللي وقت انتهاءه <b>هو</b> اللحظة دي يبقى منتهي —
    /// نفس الحساب اللي المسح بيعتمد عليه.
    /// </summary>
    [Fact]
    public void A_code_expiring_exactly_now_counts_as_expired()
    {
        var now = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(RackPolicy.IsExpired(now, now));
        Assert.Equal(CodeDeletion.Allowed, RackPolicy.Deletion(null, now, now));
    }

    [Fact]
    public async Task A_consumed_code_is_not_in_the_pending_list()
    {
        var h = Build();
        Code(h, prefix: "USED", consumedAtUtc: DateTime.UtcNow.AddMinutes(-2));
        Code(h, prefix: "OPEN");

        var result = await h.CodeList.Handle(new GetActivationCodesQuery(), default);

        Assert.Equal("OPEN", Assert.Single(result.Value).Prefix);
    }

    [Fact]
    public async Task Codes_from_another_workshop_are_not_listed()
    {
        var h = Build();
        Code(h, prefix: "MINE");

        h.Repo.Codes.Add(new RackPairingCode
        {
            TenantId = Guid.NewGuid(),
            CodePrefix = "THRS",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        });

        var result = await h.CodeList.Handle(new GetActivationCodesQuery(), default);

        Assert.Equal("MINE", Assert.Single(result.Value).Prefix);
    }

    /// <summary>
    /// ⚠️ عدّاد المحاولات الغلط بيتعرض — هو اللي بيقول للمالك إن
    /// الفني بيكتب الكود غلط، مش إن فيه حد بيخمّن.
    /// </summary>
    [Fact]
    public async Task The_failed_attempt_counter_is_visible()
    {
        var h = Build();
        Code(h, failedAttempts: 3);

        var result = await h.CodeList.Handle(new GetActivationCodesQuery(), default);

        Assert.Equal(3, Assert.Single(result.Value).FailedAttempts);
    }

    // =================================================================
    //  كود التفعيل — المسح
    // =================================================================

    [Fact]
    public async Task An_expired_unused_code_can_be_deleted()
    {
        var h = Build();
        var row = Code(h, prefix: "DEAD", expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var result = await h.DeleteCode.Handle(
            new DeleteActivationCodeCommand(row.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("اتمسح", result.Value.Message);
        Assert.Empty(h.Repo.Codes);
        Assert.Equal(1, h.Work.Saves);

        // 🔴 والبادئة في السجل — هي اللي بتربط المسح بالإنشاء.
        var line = Assert.Single(h.Audit.Lines);

        Assert.Equal(AuditActions.RackCodeDeleted, line.Action);
        Assert.Equal("DEAD", line.Code);
        Assert.Contains("محطة جديدة", line.Summary);
    }

    /// <summary>
    /// 🔴 الكود المستهلك هو الدليل الوحيد على إن المحطة اتسجّلت
    /// بأنهي إذن ومن مين.
    /// </summary>
    [Fact]
    public async Task A_consumed_code_cannot_be_deleted_even_after_it_expires()
    {
        var h = Build();

        var row = Code(h,
            expiresAtUtc: DateTime.UtcNow.AddMinutes(-30),
            consumedAtUtc: DateTime.UtcNow.AddMinutes(-25));

        var result = await h.DeleteCode.Handle(
            new DeleteActivationCodeCommand(row.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackErrors.CodeAlreadyUsed, result.Error);

        Assert.Single(h.Repo.Codes);
        Assert.Empty(h.Repo.Removed);
        Assert.Empty(h.Audit.Lines);
        Assert.Equal(0, h.Work.Saves);
    }

    /// <summary>
    /// ⚠️ ممكن يكون فيه حد ماسكه دلوقتي عشان يفعّل بيه محطة.
    /// </summary>
    [Fact]
    public async Task A_code_that_is_still_valid_cannot_be_deleted()
    {
        var h = Build();
        var row = Code(h, expiresAtUtc: DateTime.UtcNow.AddMinutes(10));

        var result = await h.DeleteCode.Handle(
            new DeleteActivationCodeCommand(row.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackErrors.CodeStillValid, result.Error);
        Assert.Single(h.Repo.Codes);
    }

    /// <summary>
    /// 🔴 <b>والرسالتين مختلفتين عن قصد.</b> «مابيتمسحش» لوحدها
    /// بتخلّي المالك يحاول تاني — واحد خلاص واللي تاني استنى شوية.
    /// </summary>
    [Fact]
    public void The_two_refusals_do_not_share_a_message()
    {
        Assert.NotEqual(RackErrors.CodeAlreadyUsed.Code, RackErrors.CodeStillValid.Code);

        Assert.NotEqual(
            RackErrors.CodeAlreadyUsed.Description, RackErrors.CodeStillValid.Description);
    }

    [Fact]
    public async Task Deleting_a_code_from_another_workshop_reads_as_not_found()
    {
        var h = Build();

        var other = new RackPairingCode
        {
            TenantId = Guid.NewGuid(),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-10),
        };

        h.Repo.Codes.Add(other);

        var result = await h.DeleteCode.Handle(
            new DeleteActivationCodeCommand(other.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackErrors.CodeNotFound, result.Error);
        Assert.Single(h.Repo.Codes);
    }

    [Fact]
    public async Task Deleting_a_code_that_does_not_exist_is_a_404()
    {
        var h = Build();

        var result = await h.DeleteCode.Handle(
            new DeleteActivationCodeCommand(Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error.StatusCode);
    }

    // =================================================================
    //  قايمة المحطات
    // =================================================================

    /// <summary>
    /// 🔴 <b>المفتاح وبصمته وبادئته مش في العقد خالص.</b> قايمة
    /// بسيطة بتبقى باب على مفاتيح المحطات كلها.
    /// </summary>
    [Fact]
    public void The_station_contract_has_no_key_field()
    {
        var names = typeof(Codlek.Application.Contracts.Racks.StationListItem)
            .GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();

        Assert.DoesNotContain("apikeyhash", names);
        Assert.DoesNotContain("salt", names);
        Assert.DoesNotContain("keyprefix", names);
        Assert.DoesNotContain("installationid", names);

        foreach (string name in names)
            Assert.DoesNotContain("key", name);
    }

    [Fact]
    public async Task A_station_carries_both_the_english_status_and_the_arabic_one()
    {
        var h = Build();
        Station(h, status: RackStatus.Suspended);

        var result = await h.Stations.Handle(new GetStationsQuery(), default);
        var row = Assert.Single(result.Value);

        // ⚠️ اللوحة بتلوّن من ده.
        Assert.Equal("Suspended", row.Status);

        // ⚠️ والمستخدم بيقرا ده.
        Assert.Equal("موقوفة", row.StatusText);
    }

    /// <summary>
    /// 🔴 الحالة المجهولة بترجع عربي كمان — المحطة اللي لسه
    /// ماتفعّلتش كانت بتظهر «PendingPairing» جنب أسماء عربية.
    /// </summary>
    [Fact]
    public async Task A_station_awaiting_pairing_reads_in_arabic()
    {
        var h = Build();
        Station(h, status: RackStatus.PendingPairing);

        var result = await h.Stations.Handle(new GetStationsQuery(), default);

        Assert.Equal("مستنية التفعيل", Assert.Single(result.Value).StatusText);
    }

    [Fact]
    public async Task Stations_from_another_workshop_are_not_listed()
    {
        var h = Build();
        Station(h, code: "RACK-001");

        h.Repo.Racks.Add(new Rack { TenantId = Guid.NewGuid(), RackCode = "RACK-999" });

        var result = await h.Stations.Handle(new GetStationsQuery(), default);

        Assert.Equal("RACK-001", Assert.Single(result.Value).RackCode);
    }

    // =================================================================
    //  الإيقاف والرجوع
    // =================================================================

    [Fact]
    public async Task Suspending_a_station_writes_the_suspended_action()
    {
        var h = Build();
        var rack = Station(h);

        var result = await h.Status.Handle(
            new SetStationStatusCommand(rack.Id, Resume: false), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(RackStatus.Suspended, rack.Status);
        Assert.Equal(1, h.Work.Saves);

        var line = Assert.Single(h.Audit.Lines);

        Assert.Equal(AuditActions.RackSuspended, line.Action);
        Assert.Equal("RACK-001", line.Code);

        // ⚠️ والرسالة بتقول العاقبة مش الحالة.
        Assert.Contains("مش هتقدر ترفع تاني", result.Value.Message);
    }

    [Fact]
    public async Task Resuming_a_suspended_station_writes_the_resumed_action()
    {
        var h = Build();
        var rack = Station(h, status: RackStatus.Suspended);

        var result = await h.Status.Handle(
            new SetStationStatusCommand(rack.Id, Resume: true), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(RackStatus.Active, rack.Status);
        Assert.Equal(AuditActions.RackResumed, Assert.Single(h.Audit.Lines).Action);
    }

    /// <summary>
    /// 🔴 <b>الملغية نهائياً مابترجعش.</b> الرجوع معناه إن مفتاح
    /// اتلغى يبقى صالح تاني — واللي اتلغى غالباً اتلغى عشان اتسرق.
    /// </summary>
    [Fact]
    public async Task A_revoked_station_cannot_be_brought_back()
    {
        var h = Build();
        var rack = Station(h, status: RackStatus.Revoked);

        var result = await h.Status.Handle(
            new SetStationStatusCommand(rack.Id, Resume: true), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackErrors.Revoked, result.Error);

        // 🔴 والحالة مااتغيّرتش، ولا فيه سطر سجل، ولا حفظة.
        Assert.Equal(RackStatus.Revoked, rack.Status);
        Assert.Empty(h.Audit.Lines);
        Assert.Equal(0, h.Work.Saves);
    }

    /// <summary>
    /// ⚠️ ولا بتتوقف كمان — الرفض على <b>أي</b> تغيير حالة، مش على
    /// الرجوع بس.
    /// </summary>
    [Fact]
    public async Task A_revoked_station_cannot_even_be_suspended()
    {
        var h = Build();
        var rack = Station(h, status: RackStatus.Revoked);

        var result = await h.Status.Handle(
            new SetStationStatusCommand(rack.Id, Resume: false), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackErrors.Revoked, result.Error);
        Assert.Equal(RackStatus.Revoked, rack.Status);
    }

    /// <summary>⚠️ والرسالة بتقول البديل: كود تفعيل جديد.</summary>
    [Fact]
    public void The_revoked_refusal_names_the_way_out()
    {
        Assert.Contains("كود تفعيل جديد", RackErrors.Revoked.Description);
    }

    [Fact]
    public async Task Changing_the_status_of_a_station_in_another_workshop_is_a_404()
    {
        var h = Build();
        var other = new Rack { TenantId = Guid.NewGuid(), RackCode = "RACK-999" };

        h.Repo.Racks.Add(other);

        var result = await h.Status.Handle(
            new SetStationStatusCommand(other.Id, Resume: false), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackErrors.NotFound, result.Error);
        Assert.Equal(RackStatus.PendingPairing, other.Status);
    }

    // =================================================================
    //  الإلغاء النهائي
    // =================================================================

    [Fact]
    public async Task Revoking_a_station_stamps_the_time_and_the_reason()
    {
        var h = Build();
        var rack = Station(h);
        var before = DateTime.UtcNow;

        var result = await h.Revoke.Handle(
            new RevokeStationCommand(rack.Id, "الراكة اتسرقت من الورشة"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(RackStatus.Revoked, rack.Status);
        Assert.Equal("الراكة اتسرقت من الورشة", rack.RevokedReason);
        Assert.NotNull(rack.RevokedAtUtc);
        Assert.True(rack.RevokedAtUtc >= before.AddSeconds(-5));
        Assert.Equal(1, h.Work.Saves);

        var line = Assert.Single(h.Audit.Lines);

        Assert.Equal(AuditActions.RackRevoked, line.Action);
        Assert.Equal("RACK-001", line.Code);

        // 🔴 والسبب في نص السجل — هو الحاجة اللي بتفضل تشرح ليه.
        Assert.Contains("الراكة اتسرقت من الورشة", line.Summary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("لأ")]
    public async Task Revoking_without_a_real_reason_is_refused(string? reason)
    {
        var h = Build();
        var rack = Station(h);

        var result = await h.Revoke.Handle(new RevokeStationCommand(rack.Id, reason), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackErrors.RevokeReasonRequired, result.Error);

        // 🔴 والمحطة مالمستش.
        Assert.Equal(RackStatus.Active, rack.Status);
        Assert.Null(rack.RevokedAtUtc);
        Assert.Empty(h.Audit.Lines);
        Assert.Equal(0, h.Work.Saves);
    }

    /// <summary>
    /// 🔴 <b>فحص السبب قبل قراية القاعدة.</b> لو جا بعدها، كان فيه
    /// نداء بيقرا صف ويرجّع نفس الرفض — شغل زيادة على نفس الرد.
    /// </summary>
    [Fact]
    public async Task The_reason_is_checked_before_the_station_is_even_read()
    {
        var h = Build();

        var result = await h.Revoke.Handle(
            new RevokeStationCommand(Guid.NewGuid(), "لأ"), default);

        Assert.True(result.IsFailure);

        // ⚠️ المحطة مش موجودة أصلاً، ومع ذلك الرد «اكتب سبب» مش «مش موجودة».
        Assert.Equal(RackErrors.RevokeReasonRequired, result.Error);
    }

    [Fact]
    public async Task The_reason_is_trimmed_before_it_is_measured()
    {
        var h = Build();
        var rack = Station(h);

        var result = await h.Revoke.Handle(
            new RevokeStationCommand(rack.Id, "   اتسرقت   "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("اتسرقت", rack.RevokedReason);
    }

    [Fact]
    public async Task Revoking_a_station_in_another_workshop_is_a_404()
    {
        var h = Build();
        var other = new Rack { TenantId = Guid.NewGuid(), RackCode = "RACK-999" };

        h.Repo.Racks.Add(other);

        var result = await h.Revoke.Handle(
            new RevokeStationCommand(other.Id, "سبب كفاية"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(RackErrors.NotFound, result.Error);
        Assert.Equal(RackStatus.PendingPairing, other.Status);
    }

    /// <summary>
    /// ⚠️ <b>الإلغاء مرتين بيعدّي</b> — زي القديم. مابيأذيش، وبيكتب
    /// سبب أوضح وسطر سجل تاني.
    /// </summary>
    [Fact]
    public async Task Revoking_an_already_revoked_station_records_the_newer_reason()
    {
        var h = Build();
        var rack = Station(h, status: RackStatus.Revoked);
        rack.RevokedReason = "سبب قديم";

        var result = await h.Revoke.Handle(
            new RevokeStationCommand(rack.Id, "طلع إنها اتستنسخت"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("طلع إنها اتستنسخت", rack.RevokedReason);
        Assert.Single(h.Audit.Lines);
    }

    // =================================================================
    //  أبجدية الكود
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفني بيقرا الكود من الشاشة ويكتبه بإيده.</b>
    /// <c>0</c>/<c>O</c> و<c>1</c>/<c>I</c>/<c>L</c> بيتلخبطوا،
    /// والكود الملخبط بيتحسب محاولة غلط — وخمس محاولات بتقفله.
    /// </summary>
    [Fact]
    public void The_alphabet_has_no_confusable_characters()
    {
        foreach (char c in "01OIL")
            Assert.DoesNotContain(c, PairingCode.Alphabet);
    }

    [Fact]
    public void A_generated_code_only_uses_the_alphabet()
    {
        for (int i = 0; i < 200; i++)
        {
            string code = PairingCode.New();

            Assert.Equal(9, code.Length);
            Assert.Equal('-', code[4]);

            foreach (char c in code.Replace("-", ""))
                Assert.Contains(c, PairingCode.Alphabet);
        }
    }

    [Fact]
    public void The_prefix_is_the_first_four_characters()
    {
        string code = PairingCode.New();

        Assert.Equal(code[..4], PairingCode.Prefix(code));
        Assert.Equal(PairingCode.PrefixLength, PairingCode.Prefix(code).Length);
    }

    /// <summary>
    /// ⚠️ <b>والشرطة مش جوّه البادئة.</b> لو كانت، البحث وقت
    /// التسجيل كان هيدوّر على ٣ حروف بس — ودي ٢٩٧٩١ احتمال بدل
    /// ٩٢٣٥٢١.
    /// </summary>
    [Fact]
    public void The_prefix_stops_before_the_dash()
    {
        Assert.DoesNotContain("-", PairingCode.Prefix(PairingCode.New()));
    }

    /// <summary>
    /// ⚠️ نص أقصر من البادئة بيرجع زي ما هو — مش استثناء. الدالة
    /// دي بتتنده على مدخلات من بره في مرحلة التسجيل.
    /// </summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("AB", "AB")]
    [InlineData("ABCD", "ABCD")]
    [InlineData("ABCDE", "ABCD")]
    public void A_short_code_yields_itself_as_the_prefix(string code, string expected)
    {
        Assert.Equal(expected, PairingCode.Prefix(code));
    }

    /// <summary>
    /// ⚠️ <b>عمر الكود قصير عن قصد.</b> الكود بيتحوّل لمفتاح محطة
    /// دايم، فاللي بيفضل صالح أسبوع بيبقى مفتاح احتياطي ساكت لأي حد
    /// شافه على الشاشة.
    /// </summary>
    [Fact]
    public void The_code_lifetime_is_minutes_not_days()
    {
        Assert.True(PairingCode.Lifetime <= TimeSpan.FromHours(1));
        Assert.True(PairingCode.Lifetime >= TimeSpan.FromMinutes(5));
    }
}
