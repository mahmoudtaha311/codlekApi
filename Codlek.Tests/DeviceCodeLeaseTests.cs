using Codlek.Application.Features.Rack.LeaseDeviceCodes;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// تأجير بلوكات أكواد الأجهزة.
///
/// <para>🔴 <b>والبلوك ده هو اللي بيخلّي الراكة تشتغل أوفلاين.</b>
/// الفني بيفحص لاب جديد وهو مقطوع عن النت، واللاب محتاج كود فوراً
/// عشان الليبل يتطبع.</para>
///
/// <para>🔴 <b>والقاعدة اللي لازم تفضل:</b> البلوك القديم بيتقفل
/// <u>من غير ما يرجع للمساحة</u>. الراكة ممكن تكون طبعت ليبل برقم
/// منه وهي أوفلاين، فإعادة الرقم للمساحة معناها <b>استيكرين بنفس
/// الكود على لابين مختلفين</b>.</para>
/// </summary>
public class DeviceCodeLeaseTests
{
    private sealed class FakeLeases : IDeviceCodeLeaseRepository
    {
        public readonly List<DeviceCodeLease> Leases = [];

        public Task<IReadOnlyList<DeviceCodeLease>> OpenForRackAsync(
            Guid t, Guid rack, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceCodeLease>>(
                Leases.Where(l => l.TenantId == t
                               && l.RackId == rack
                               && l.Status == DeviceCodeLeaseStatus.Open)
                    .ToList());

        public Task<DeviceCodeLease?> ContainingAsync(
            Guid t, Guid rack, int number, CancellationToken ct = default) =>
            Task.FromResult(Leases.FirstOrDefault(
                l => l.TenantId == t && l.RackId == rack
                  && l.FromNumber <= number && l.ToNumber >= number));

        public void Add(DeviceCodeLease lease) => Leases.Add(lease);
    }

    /// <summary>
    /// ⚠️ عدّاد مزيّف بيحجز فعلاً — الفحص بيقيس إن المدى متصل
    /// ومابيتداخلش.
    /// </summary>
    private sealed class FakeCounters : ITenantCounters
    {
        public int Next = 1;

        /// <summary>⚠️ بيحفظ كل حجز — الفحص بيقيس العدد والحجم.</summary>
        public readonly List<int> Reservations = [];

        public Task<int> NextAsync(Guid t, string name, CancellationToken ct = default) =>
            Task.FromResult(Next++);

        public Task<int> ReserveAsync(
            Guid t, string name, int count, CancellationToken ct = default)
        {
            Reservations.Add(count);

            int start = Next;
            Next += count;

            return Task.FromResult(start);
        }
    }

    private sealed record Harness(
        FakeLeases Leases,
        FakeCounters Counters,
        FakeUnitOfWork Work,
        LeaseDeviceCodesCommandHandler Handler,
        Guid Tenant,
        Guid Rack);

    private static Harness Build()
    {
        var leases = new FakeLeases();
        var counters = new FakeCounters();
        var work = new FakeUnitOfWork();

        return new Harness(
            leases, counters, work,
            new LeaseDeviceCodesCommandHandler(
                leases, counters, work,
                NullLogger<LeaseDeviceCodesCommandHandler>.Instance),
            Guid.NewGuid(),
            Guid.NewGuid());
    }

    // =================================================================
    //  الحجم
    // =================================================================

    /// <summary>
    /// ⚠️ <b>القيمة المش مفهومة بتاخد الافتراضي، مش
    /// <c>400</c>.</b> الراكة في الميدان ومش المفروض تقف عشان
    /// معامل.
    /// </summary>
    [Theory]
    [InlineData(null, 500)]
    [InlineData(0, 500)]
    [InlineData(-5, 500)]
    [InlineData(1, 1)]
    [InlineData(250, 250)]
    [InlineData(2000, 2000)]
    [InlineData(2001, 2000)]
    [InlineData(99999, 2000)]
    public void The_block_size_is_clamped_not_refused(int? requested, int expected)
    {
        Assert.Equal(expected, DeviceCodeBlock.Size(requested));
    }

    [Fact]
    public async Task A_lease_with_no_size_takes_the_default_block()
    {
        var h = Build();

        var result = await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, null, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(DeviceCodeBlock.DefaultSize, result.Value.Size);
        Assert.Equal(1, result.Value.FromNumber);
        Assert.Equal(500, result.Value.ToNumber);
    }

    /// <summary>
    /// ⚠️ والحجم في الرد محسوب من المدى — مش من اللي اتطلب، لأن
    /// اللي اتطلب ممكن يكون اتقص.
    /// </summary>
    [Fact]
    public async Task The_reported_size_comes_from_the_range()
    {
        var h = Build();

        var result = await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 99999, null), default);

        Assert.Equal(DeviceCodeBlock.MaxSize, result.Value.Size);
        Assert.Equal(
            result.Value.ToNumber - result.Value.FromNumber + 1, result.Value.Size);
    }

    // =================================================================
    //  الشكل
    // =================================================================

    /// <summary>
    /// 🔴 <b>تمن خانات بأصفار على الشمال — والشكل ده مجمّد.</b>
    ///
    /// <para>الليبل المطبوع على اللاب شايله، ومسح الباركود بيطابقه
    /// <b>مطابقة تامة</b>. أي تغيير في الحشو معناه إن كل استيكر في
    /// الورشة مابقاش بيطابق.</para>
    /// </summary>
    [Theory]
    [InlineData(1, "LP-00000001")]
    [InlineData(500, "LP-00000500")]
    [InlineData(18425, "LP-00018425")]
    [InlineData(99999999, "LP-99999999")]
    public void The_code_format_is_eight_padded_digits(int number, string expected)
    {
        Assert.Equal(expected, DeviceCodeBlock.Format(number));
    }

    /// <summary>
    /// 🔴 <b>والشكل ده نفسه اللي ماسح الباركود بيقبله.</b>
    ///
    /// <para>والفحص ده بيربط الاتنين: كود مؤجّر لازم يعدّي من
    /// <c>DeviceScan.Parse</c> — ولو الحشو اتغيّر في مكان واحد،
    /// الفحص ده بيوقّع.</para>
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(18425)]
    [InlineData(99999999)]
    public void A_leased_code_is_readable_by_the_scanner(int number)
    {
        string code = DeviceCodeBlock.Format(number);

        Assert.Equal(code, DeviceScan.Parse(code));
    }

    [Fact]
    public async Task The_response_carries_both_the_numbers_and_the_codes()
    {
        var h = Build();

        var result = await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 10, null), default);

        Assert.Equal("LP-00000001", result.Value.FromCode);
        Assert.Equal("LP-00000010", result.Value.ToCode);
        Assert.Equal(1, result.Value.FromNumber);
        Assert.Equal(10, result.Value.ToNumber);
    }

    // =================================================================
    //  الحجز
    // =================================================================

    /// <summary>
    /// 🔴 <b>حجز واحد بحجم البلوك — مش حلقة.</b>
    ///
    /// <para>خمسمية نداء معناه خمسمية رحلة للقاعدة، والأهم إن راكة
    /// تانية بتقدر تتخلّل بينهم فالمدى بيطلع <b>مقطّع</b> — والراكة
    /// محتاجة مدى متصل عشان توزّعه أوفلاين.</para>
    /// </summary>
    [Fact]
    public async Task One_reservation_covers_the_whole_block()
    {
        var h = Build();

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 500, null), default);

        int count = Assert.Single(h.Counters.Reservations);

        Assert.Equal(500, count);
    }

    /// <summary>
    /// 🔴 <b>وراكتين بياخدوا مديين مختلفين ومتصلين — مفيش
    /// تداخل.</b>
    /// </summary>
    [Fact]
    public async Task Two_racks_get_separate_ranges()
    {
        var h = Build();
        var other = Guid.NewGuid();

        var first = await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        var second = await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, other, 100, null), default);

        Assert.Equal(1, first.Value.FromNumber);
        Assert.Equal(100, first.Value.ToNumber);

        Assert.Equal(101, second.Value.FromNumber);
        Assert.Equal(200, second.Value.ToNumber);

        // 🔴 مفيش رقم في المديين.
        Assert.True(second.Value.FromNumber > first.Value.ToNumber);
    }

    /// <summary>
    /// ⚠️ <b>و«لسه ماستهلكتش» = أقل من أول رقم بواحد.</b> الصفر كان
    /// بيتقري «استهلكت لحد الرقم صفر» على بلوك بيبدأ من ألف.
    /// </summary>
    [Fact]
    public async Task A_new_block_starts_with_nothing_consumed()
    {
        var h = Build();
        h.Counters.Next = 1000;

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        var lease = Assert.Single(h.Leases.Leases);

        Assert.Equal(999, lease.ConsumedThrough);
        Assert.Equal(1000, lease.FromNumber);
    }

    // =================================================================
    //  قفل البلوك القديم
    // =================================================================

    /// <summary>
    /// 🔴 <b>البلوك القديم بيتقفل — بلوك واحد مفتوح في المرة.</b>
    ///
    /// <para>الراكة بتوزّع من اللي معاها، وبلوكين مفتوحين معناهم
    /// إنها ممكن توزّع من القديم بعد ما اتقفل في دماغها.</para>
    /// </summary>
    [Fact]
    public async Task Taking_a_new_block_closes_the_old_one()
    {
        var h = Build();

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        Assert.Equal(2, h.Leases.Leases.Count);

        var old = h.Leases.Leases[0];
        var fresh = h.Leases.Leases[1];

        Assert.Equal(DeviceCodeLeaseStatus.Exhausted, old.Status);
        Assert.NotNull(old.ClosedAtUtc);
        Assert.Equal("اتقفل عشان بلوك جديد اتاخد", old.ClosedReason);

        Assert.Equal(DeviceCodeLeaseStatus.Open, fresh.Status);
    }

    /// <summary>
    /// 🔴 <b>والأرقام المتستخدمتش <u>مابترجعش</u> للمساحة.</b>
    ///
    /// <para>الراكة ممكن تكون طبعت ليبل برقم من البلوك القديم وهي
    /// أوفلاين، فإعادة الرقم للمساحة معناها <b>استيكرين بنفس الكود
    /// على لابين مختلفين</b> — وده عطل مالوش علاج بعد الطباعة.</para>
    /// </summary>
    [Fact]
    public async Task Unused_numbers_in_a_closed_block_stay_a_gap_forever()
    {
        var h = Build();

        // بلوك من ١ لـ١٠٠، واستهلكت لحد ١٠ وبس.
        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        var second = await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, 10), default);

        // 🔴 البلوك الجديد بيبدأ من ١٠١ — مش من ١١.
        Assert.Equal(101, second.Value.FromNumber);
    }

    // =================================================================
    //  «استهلكت لحد»
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الراكة بتقول لحد فين استهلكت، وبيتسجّل قبل
    /// القفل.</b> الرقم ده هو اللي بيجاوب «الفجوة دي من فين» بعد
    /// شهور.
    /// </summary>
    [Fact]
    public async Task The_consumed_mark_is_recorded_on_the_old_block()
    {
        var h = Build();

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, 42), default);

        Assert.Equal(42, h.Leases.Leases[0].ConsumedThrough);
    }

    /// <summary>
    /// 🔴 <b>والرقم بيزيد وبس.</b>
    ///
    /// <para>رد قديم وصل متأخر (الراكة بعتت مرتين والشبكة قلبت
    /// الترتيب) مش هيرجّع العدّاد لورا — والرجوع معناه إن أرقام
    /// اتستخدمت تتحسب فاضية وتتوزّع تاني.</para>
    /// </summary>
    [Fact]
    public async Task A_late_lower_mark_never_moves_the_counter_back()
    {
        var h = Build();

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        h.Leases.Leases[0].ConsumedThrough = 50;

        // رد قديم بيقول ١٠ — المفروض يتجاهل.
        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, 10), default);

        Assert.Equal(50, h.Leases.Leases[0].ConsumedThrough);
    }

    /// <summary>
    /// ⚠️ <b>والعلامة مابتزيدش عن آخر البلوك.</b> رقم من برّه المدى
    /// معناه إن الراكة استهلكت من بلوك تاني — والقص بيمنع القيمة
    /// المستحيلة.
    /// </summary>
    [Fact]
    public async Task The_mark_never_exceeds_the_block_end()
    {
        var h = Build();

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, 100), default);

        Assert.Equal(100, h.Leases.Leases[0].ConsumedThrough);
    }

    /// <summary>
    /// ⚠️ ورقم مش في أي بلوك بيتجاهل — مش بيرمي.
    /// </summary>
    [Fact]
    public async Task A_mark_outside_every_block_is_ignored()
    {
        var h = Build();

        var result = await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, 9_999_999), default);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_missing_or_zero_mark_is_skipped(int? mark)
    {
        var h = Build();

        var result = await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, mark), default);

        Assert.True(result.IsSuccess);
    }

    // =================================================================
    //  الحفظ
    // =================================================================

    /// <summary>
    /// 🔴 <b>حفظة واحدة للقفل والبلوك الجديد والعلامة.</b>
    ///
    /// <para>انهيار بينهم كان بيسيب الراكة من غير بلوك مفتوح
    /// خالص — يعني مش قادرة تدّي كود لأي لاب جديد.</para>
    /// </summary>
    [Fact]
    public async Task Everything_lands_in_one_save()
    {
        var h = Build();

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, 50), default);

        Assert.Equal(2, h.Work.Saves);
    }

    /// <summary>
    /// 🔴 <b>وبلوك راكة تانية مابيتقفلش.</b> القفل مقصور على
    /// المحطة اللي بتطلب.
    /// </summary>
    [Fact]
    public async Task Another_racks_block_is_left_open()
    {
        var h = Build();
        var other = Guid.NewGuid();

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, other, 100, null), default);

        await h.Handler.Handle(
            new LeaseDeviceCodesCommand(h.Tenant, h.Rack, 100, null), default);

        var theirs = h.Leases.Leases.Single(l => l.RackId == other);

        Assert.Equal(DeviceCodeLeaseStatus.Open, theirs.Status);
    }
}
