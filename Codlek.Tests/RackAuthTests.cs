using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Racks;
using Codlek.Infrastructure.Auth;

namespace Codlek.Tests;

/// <summary>
/// تحقق مفتاح محطة الفحص.
///
/// <para>🔴 <b>ودي البوابة الوحيدة لتسع نقط.</b> كل سطح الراكة
/// معتمد عليها، ومفيش فيه ولا نقطة بتاخد معرّف شركة من الطلب:
/// الشركة بتتقرا <b>من المحطة</b> بعد التحقق. يعني الوصول العابر
/// للشركات مش «ممنوع» — هو مش موجود كمسار أصلاً.</para>
///
/// <para>🔴 <b>والمحطة الموقوفة أو الملغية بتقع على أول خطوة</b>
/// قبل ما أي اسم أو شركة يتقرا. والملغي غالباً اتلغى عشان مفتاحه
/// اتسرق.</para>
/// </summary>
public class RackAuthTests
{
    /// <summary>
    /// ⚠️ بصمة مزيّفة — الفحص بيقيس <b>القرار</b> مش التشفير.
    /// والتشفير نفسه مفحوص في <c>LegacyPasswordTests</c>.
    /// </summary>
    private sealed class FakeKeys : IRackKeys
    {
        public int Verifications;

        public (string Key, string Hash, string Salt) Issue() =>
            ("KEY-" + Guid.NewGuid().ToString("N"), "hash", "salt");

        public bool Verify(string key, string storedHash, string storedSalt)
        {
            Verifications++;
            return storedHash == "hash:" + key;
        }
    }

    private sealed class FakeRacks : IRackRepository
    {
        public readonly List<Rack> Racks = [];

        /// <summary>⚠️ بيحفظ البادئة اللي اتبعت — الفحص بيقيسها.</summary>
        public string? LastPrefix;

        public Task<IReadOnlyList<Rack>> ActiveByKeyPrefixAsync(
            string prefix, CancellationToken ct = default)
        {
            LastPrefix = prefix;

            return Task.FromResult<IReadOnlyList<Rack>>(
                Racks.Where(r => r.Status == RackStatus.Active && r.KeyPrefix == prefix)
                    .ToList());
        }

        public Task<IReadOnlyList<Rack>> ListAsync(Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Rack>>(Racks);

        public Task<Rack?> FindAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Rack?>(null);

        public Task<IReadOnlyList<RackPairingCode>> PendingCodesAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RackPairingCode>>([]);

        public Task<RackPairingCode?> FindCodeAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<RackPairingCode?>(null);

        public void AddCode(RackPairingCode code) { }

        public Task<IReadOnlyList<RackPairingCode>> CodesByPrefixAsync(
            string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RackPairingCode>>([]);

        public Task<bool> ConsumeCodeAsync(
            Guid codeId, DateTime atUtc, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task LinkCodeToRackAsync(
            Guid codeId, Guid rackId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void Add(Rack rack) { }

        public Task<IReadOnlyList<Rack>> TwinsByInstallationAsync(
            Guid t, string installationId, Guid except, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Rack>>([]);


        public Task TouchAsync(
            Guid rackId, int reportsReceived, string? appVersion = null,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<string?> TenantNameAsync(Guid t, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public void RemoveCode(RackPairingCode code) { }
    }

    private static (FakeRacks Racks, FakeKeys Keys, RackAuthenticator Auth) Build()
    {
        var racks = new FakeRacks();
        var keys = new FakeKeys();

        return (racks, keys, new RackAuthenticator(racks, keys));
    }

    private static Rack NewRack(
        FakeRacks racks, string key, RackStatus status = RackStatus.Active)
    {
        var rack = new Rack
        {
            TenantId = Guid.NewGuid(),
            RackCode = "RK-01",
            Name = "محطة أحمد",
            KeyPrefix = RackKey.Prefix(key),
            ApiKeyHash = "hash:" + key,
            Salt = "salt",
            Status = status,
        };

        racks.Racks.Add(rack);
        return rack;
    }

    // =================================================================
    //  المفتاح الصح
    // =================================================================

    [Fact]
    public async Task A_correct_key_resolves_its_rack()
    {
        var (racks, _, auth) = Build();
        var rack = NewRack(racks, "RK1234567890ABCDEF");

        var found = await auth.AuthenticateAsync("RK1234567890ABCDEF");

        Assert.NotNull(found);
        Assert.Equal(rack.Id, found.Id);

        // 🔴 والشركة بتيجي من المحطة — مش من الطلب.
        Assert.Equal(rack.TenantId, found.TenantId);
    }

    /// <summary>
    /// ⚠️ <b>المساحات بتتشال.</b> الراكة بتقرا المفتاح من ملف
    /// إعدادات، والسطر بييجي فيه <c>\r\n</c>.
    /// </summary>
    [Theory]
    [InlineData("  RK1234567890ABCDEF  ")]
    [InlineData("RK1234567890ABCDEF\r\n")]
    [InlineData("\tRK1234567890ABCDEF")]
    public async Task Whitespace_around_the_key_is_ignored(string sent)
    {
        var (racks, _, auth) = Build();
        NewRack(racks, "RK1234567890ABCDEF");

        Assert.NotNull(await auth.AuthenticateAsync(sent));
    }

    // =================================================================
    //  الرفض
    // =================================================================

    /// <summary>
    /// 🔴 <b>ومفيش فرق في الرد بين الحالات.</b> مفتاح غلط، ومحطة
    /// موقوفة، ومحطة ملغية — كلهم <c>null</c> يعني <c>401</c>
    /// فاضية. اللي بيحاول مالوش يعرف إيه اللي ناقص.
    /// </summary>
    [Theory]
    [InlineData(RackStatus.Suspended)]
    [InlineData(RackStatus.Revoked)]
    [InlineData(RackStatus.PendingPairing)]
    public async Task A_rack_that_is_not_active_never_authenticates(RackStatus status)
    {
        var (racks, _, auth) = Build();
        NewRack(racks, "RK1234567890ABCDEF", status);

        // ⚠️ المفتاح **صح** — والرفض من الحالة.
        Assert.Null(await auth.AuthenticateAsync("RK1234567890ABCDEF"));
    }

    [Fact]
    public async Task A_wrong_key_does_not_authenticate()
    {
        var (racks, _, auth) = Build();
        NewRack(racks, "RK1234567890ABCDEF");

        Assert.Null(await auth.AuthenticateAsync("RK1234567890ZZZZZZ"));
    }

    /// <summary>
    /// 🔴 <b>والمفتاح القصير مابيوصلش القاعدة خالص.</b>
    ///
    /// <para>مفتاح من حرفين مش مفتاح، وتمريره للقاعدة معناه استعلام
    /// على كل طلب عابر — ومسار الراكة مفتوح على الإنترنت.</para>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1234567")]
    public async Task A_short_or_missing_key_never_reaches_the_database(string? key)
    {
        var (racks, keys, auth) = Build();
        NewRack(racks, "RK1234567890ABCDEF");

        Assert.Null(await auth.AuthenticateAsync(key));

        // 🔴 ولا استعلام ولا تحقق تشفيري.
        Assert.Null(racks.LastPrefix);
        Assert.Equal(0, keys.Verifications);
    }

    /// <summary>⚠️ وتمن حروف بالظبط بتعدّي — الحد شامل.</summary>
    [Fact]
    public async Task Exactly_eight_characters_is_long_enough_to_try()
    {
        var (racks, _, auth) = Build();

        await auth.AuthenticateAsync("12345678");

        Assert.Equal("12345678", racks.LastPrefix);
    }

    // =================================================================
    //  البادئة
    // =================================================================

    /// <summary>
    /// 🔴 <b>البحث بالبادئة، والتحقق على اللي فاضل.</b>
    ///
    /// <para>التحقق التشفيري غالي، فتشغيله على كل راكة في القاعدة
    /// مع كل طلب كان بيخلّي كل مزامنة تكلّف وقت معالج حقيقي.</para>
    /// </summary>
    [Fact]
    public async Task The_search_narrows_by_the_first_ten_characters()
    {
        var (racks, _, auth) = Build();

        await auth.AuthenticateAsync("ABCDEFGHIJKLMNOP");

        Assert.Equal("ABCDEFGHIJ", racks.LastPrefix);
        Assert.Equal(RackKey.PrefixLength, racks.LastPrefix!.Length);
    }

    /// <summary>
    /// 🔴 <b>وبادئة مش معروفة = صفر تحقق تشفيري.</b> ده الحاجز
    /// اللي بيمنع التخمين يكلّف السيرفر.
    /// </summary>
    [Fact]
    public async Task An_unknown_prefix_costs_no_verification()
    {
        var (racks, keys, auth) = Build();
        NewRack(racks, "RK1234567890ABCDEF");

        Assert.Null(await auth.AuthenticateAsync("ZZZZZZZZZZZZZZZZ"));
        Assert.Equal(0, keys.Verifications);
    }

    /// <summary>
    /// ⚠️ <b>وتصادم البادئة بيعدّي على كل المرشّحين.</b> البادئة
    /// بتقلّلهم لواحد عملياً، بس التصادم ممكن — فالحلقة لازم تكمل.
    /// </summary>
    [Fact]
    public async Task Two_racks_sharing_a_prefix_are_both_tried()
    {
        var (racks, keys, auth) = Build();

        NewRack(racks, "SAMEPREFIX-AAAA");
        var second = NewRack(racks, "SAMEPREFIX-BBBB");

        var found = await auth.AuthenticateAsync("SAMEPREFIX-BBBB");

        Assert.NotNull(found);
        Assert.Equal(second.Id, found.Id);

        // ⚠️ الاتنين اتجرّبوا.
        Assert.Equal(2, keys.Verifications);
    }

    /// <summary>
    /// ⚠️ <b>والموقوفة مش في المرشّحين خالص</b> — الفلتر في
    /// الاستعلام، فمفيش تحقق عليها.
    /// </summary>
    [Fact]
    public async Task A_suspended_rack_is_not_even_a_candidate()
    {
        var (racks, keys, auth) = Build();
        NewRack(racks, "RK1234567890ABCDEF", RackStatus.Suspended);

        Assert.Null(await auth.AuthenticateAsync("RK1234567890ABCDEF"));
        Assert.Equal(0, keys.Verifications);
    }

    // =================================================================
    //  قواعد الشكل
    // =================================================================

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("       ", false)]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData("  12345678  ", true)]
    public void Usable_measures_the_key_after_trimming(string? key, bool expected)
    {
        Assert.Equal(expected, RackKey.Usable(key));
    }

    [Theory]
    [InlineData("ABCDEFGHIJKLMNOP", "ABCDEFGHIJ")]
    [InlineData("  ABCDEFGHIJKLMNOP  ", "ABCDEFGHIJ")]
    [InlineData("SHORT", "SHORT")]
    [InlineData(null, "")]
    public void The_prefix_is_the_first_ten_characters(string? key, string expected)
    {
        Assert.Equal(expected, RackKey.Prefix(key));
    }

    /// <summary>
    /// ⚠️ <b>وطول البادئة لازم يطابق عمود القاعدة.</b> لو العمود
    /// أقصر، البادئة المتخزّنة بتتقص والمقارنة مابتلاقي حاجة أبداً.
    /// </summary>
    [Fact]
    public void The_prefix_length_fits_the_column()
    {
        var column = typeof(Rack).GetProperty(nameof(Rack.KeyPrefix))!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations
                .MaxLengthAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.MaxLengthAttribute>()
            .Single();

        Assert.True(RackKey.PrefixLength <= column.Length,
            $"البادئة {RackKey.PrefixLength} والعمود {column.Length}");
    }

    /// <summary>
    /// ⚠️ <b>والترويسة اسمها عقد.</b> الراكات في الميدان بتبعت
    /// <c>X-Api-Key</c> بالحرف — أي تغيير معناه إن كلهم بيقفوا.
    /// </summary>
    [Fact]
    public void The_header_name_is_frozen()
    {
        Assert.Equal("X-Api-Key", RackKey.Header);
    }
}
