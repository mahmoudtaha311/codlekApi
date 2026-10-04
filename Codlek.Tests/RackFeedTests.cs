using System.Globalization;
using Codlek.Application.Features.Rack.Feeds;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Repairs;
using Codlek.Core.Sync;

namespace Codlek.Tests;

/// <summary>
/// التغذيات النازلة — <b>السيرفر بيكلّم الراكة</b>.
///
/// <para>🔴 <b>وقبل كده المزامنة كانت رفع بس.</b> أمر الصيانة
/// بيتفتح على راكة الفحص وبيتسند لفني صيانة بيشتغل على راكة تانية —
/// وأمره ماكانش بيوصله <b>أبداً</b>. الأمر بيترفع للسيرفر ويقعد
/// هناك، والمدير شايفه على الموقع، والفني قدام راكته مش شايف
/// حاجة.</para>
/// </summary>
public class RackFeedTests
{
    private sealed class FakeFeeds : IRackFeedRepository
    {
        public readonly List<RosterFeedRow> Roster = [];
        public readonly List<ContainerFeedRow> Containers = [];
        public readonly List<AssignedRepairFeedRow> Assigned = [];

        /// <summary>⚠️ اللي المستودع اتنده بيه — الفحص بيقيس التمرير.</summary>
        public DateTime? SawSince;
        public int SawTake;
        public bool SinceWasPassed;

        public Task<IReadOnlyList<RosterFeedRow>> RepairRosterAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RosterFeedRow>>(Roster);

        public Task<IReadOnlyList<ContainerFeedRow>> ContainersAsync(
            Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ContainerFeedRow>>(Containers);

        public Task<IReadOnlyList<AssignedRepairFeedRow>> AssignedRepairsAsync(
            Guid tenantId, DateTime? sinceUtc, int take, CancellationToken ct = default)
        {
            SawSince = sinceUtc;
            SawTake = take;
            SinceWasPassed = true;

            return Task.FromResult<IReadOnlyList<AssignedRepairFeedRow>>(
                Assigned.Take(take).ToList());
        }
    }

    private static AssignedRepairFeedRow Row(string code, DateTime at) =>
        new()
        {
            Id = Guid.NewGuid(),
            PublicCode = code,
            DeviceCode = "LP-00000001",
            DeviceName = "لاب",
            DeviceManufacturer = "Dell Inc.",
            UpdatedAtUtc = at,
        };

    // =================================================================
    //  قراية العلامة — الحتة اللي بتطفّي الأسطول
    // =================================================================

    /// <summary>
    /// 🔴 <b><c>RoundtripKind</c> لوحده — ممنوع يتخلط مع
    /// <c>AdjustToUniversal</c>. والفحص ده بيقيس السبب بنفسه.</b>
    ///
    /// <para>الاتنين مع بعض بيرموا <c>ArgumentException</c>
    /// <b>قبل</b> أي تحليل، و<c>TryParse</c> مابتلمّهاش — يعني
    /// النقطة بتاخد <c>500</c>. والراكة بتعتبر أي رد غير 2xx
    /// «أجّل»، جوّه <c>catch { }</c>، فمابتقدّمش علامتها
    /// ومابتكتبش لوج ومابتسحبش تاني — <b>للأبد</b>. والعطل بيبان
    /// كأنه انقطاع في الأسطول كله مع تنصيب جديد نضيف.</para>
    ///
    /// <para>⚠️ والفخ عايش في المستودع ده فعلاً: مساعد في برنامج
    /// الراكة على بُعد مية سطر بيستعمل التركيبة الممنوعة (<b>وهي
    /// قانونية هناك</b> لأنها مع <c>AssumeUniversal</c> مش
    /// <c>RoundtripKind</c>). فنسخ-ولزق من هناك لهنا = الأسطول
    /// بيطفى.</para>
    /// </summary>
    [Fact]
    public void Roundtrip_combined_with_adjust_throws_before_it_parses()
    {
        Assert.Throws<ArgumentException>(() =>
            DateTime.TryParse(
                "2026-10-04T09:30:00Z", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AdjustToUniversal,
                out _));

        // ⚠️ واللي إحنا بنستعمله مابيرميش.
        Assert.True(DateTime.TryParse(
            "2026-10-04T09:30:00Z", CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out _));
    }

    /// <summary>⚠️ وقت بـ<c>Z</c> بيعدّي زي ما هو.</summary>
    [Fact]
    public void A_z_suffixed_mark_stays_exactly_as_sent()
    {
        var parsed = SinceCursor.Parse("2026-10-04T09:30:00.123Z");

        Assert.Equal(
            new DateTime(2026, 10, 4, 9, 30, 0, 123, DateTimeKind.Utc), parsed);

        Assert.Equal(DateTimeKind.Utc, parsed!.Value.Kind);
    }

    /// <summary>
    /// 🔴 <b>والذراع المهم هو <c>Local</c> — هو الوحيد اللي بيغيّر
    /// القيمة.</b>
    ///
    /// <para>وقت بـ<c>+02:00</c> بيتقرا بتوقيت السيرفر، ومقارنته
    /// بعمود UTC من غير تحويل بتزحلق النافذة بفرق التوقيت: زحلقة
    /// لورا = الراكة بتسحب كل حاجة من تاني كل دورة؛ زحلقة لقدام =
    /// أوامر حقيقية بتقع في الفجوة و<b>بتتفوّت للأبد</b>.</para>
    /// </summary>
    [Fact]
    public void An_offset_mark_is_converted_not_relabelled()
    {
        var parsed = SinceCursor.Parse("2026-10-04T11:30:00+02:00");

        Assert.Equal(
            new DateTime(2026, 10, 4, 9, 30, 0, DateTimeKind.Utc), parsed);

        Assert.Equal(DateTimeKind.Utc, parsed!.Value.Kind);
    }

    /// <summary>
    /// ⚠️ <b>ووقت من غير منطقة بيتفسّر UTC — مش توقيت
    /// السيرفر.</b> ده <b>واجهة آلة</b>، فالافتراض بيبقى اتفاق
    /// السلك مش ساعة المضيف.
    /// </summary>
    [Fact]
    public void A_zone_less_mark_is_read_as_utc()
    {
        var parsed = SinceCursor.Parse("2026-10-04T09:30:00");

        Assert.Equal(DateTimeKind.Utc, parsed!.Value.Kind);

        // ⚠️ والقيمة نفسها ماتغيّرتش — `SpecifyKind` بتغيّر اللافتة بس.
        Assert.Equal(new DateTime(2026, 10, 4, 9, 30, 0), parsed.Value);
    }

    /// <summary>
    /// 🔴 <b>وتاريخ مش مفهوم بيرجع <c>null</c> مش خطأ.</b>
    ///
    /// <para>يعني الراكة بتاخد <c>200</c> بأول صفحة — نفس اللي
    /// بتاخده لو مابعتتش علامة خالص. ورفضه بـ<c>400</c> كان بيوقّف
    /// السحب على الراكة اللي علامتها اتخربت، وهي بالظبط اللي محتاجة
    /// تسحب من الأول.</para>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("مش تاريخ")]
    [InlineData("2026-13-45")]
    [InlineData("yesterday")]
    [InlineData("0")]
    public void A_mark_that_will_not_parse_becomes_no_mark(string? raw)
    {
        Assert.Null(SinceCursor.Parse(raw));
    }

    [Fact]
    public void The_page_is_two_hundred_and_we_ask_for_one_more()
    {
        Assert.Equal(200, SinceCursor.PageSize);
        Assert.Equal(201, SinceCursor.Need);

        Assert.False(SinceCursor.HasMore(200));
        Assert.True(SinceCursor.HasMore(201));
    }

    // =================================================================
    //  قايمة فنيي الصيانة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>القدرتين ثابتين <c>true</c> — والاستعلام هو اللي
    /// بيفلتر.</b> الراكة بتخزّن القيمة زي ما جت، فلو يوم ما
    /// التغذية رجّعت فنيين بعلامة، الراكة القديمة هتحترمها من غير
    /// تحديث.
    /// </summary>
    [Fact]
    public async Task The_roster_declares_every_row_able_and_active()
    {
        var feeds = new FakeFeeds();

        feeds.Roster.Add(new RosterFeedRow
        {
            Id = Guid.NewGuid(), Code = "100200", DisplayName = "أحمد",
        });

        var result = await new RepairRosterQueryHandler(feeds)
            .Handle(new RepairRosterQuery(Guid.NewGuid()), default);

        var row = Assert.Single(result.Value.Technicians);

        Assert.Equal("أحمد", row.DisplayName);
        Assert.Equal("100200", row.Code);
        Assert.True(row.CanRepair);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task An_empty_roster_is_an_empty_list_not_a_null()
    {
        var result = await new RepairRosterQueryHandler(new FakeFeeds())
            .Handle(new RepairRosterQuery(Guid.NewGuid()), default);

        Assert.NotNull(result.Value.Technicians);
        Assert.Empty(result.Value.Technicians);
    }

    // =================================================================
    //  الحاويات
    // =================================================================

    [Fact]
    public async Task Containers_carry_their_device_count()
    {
        var feeds = new FakeFeeds();

        feeds.Containers.Add(new ContainerFeedRow
        {
            Id = Guid.NewGuid(), Code = "C-1", Name = "شحنة يناير", DeviceCount = 7,
        });

        var result = await new RackContainersQueryHandler(feeds)
            .Handle(new RackContainersQuery(Guid.NewGuid()), default);

        var row = Assert.Single(result.Value.Containers);

        Assert.Equal("C-1", row.Code);
        Assert.Equal(7, row.DeviceCount);
    }

    // =================================================================
    //  الأوامر المسنودة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>بنطلب صف زيادة عشان نعرف لو فيه كمان</b> — من غير
    /// استعلام عدّ تاني.
    /// </summary>
    [Fact]
    public async Task The_query_asks_for_one_row_past_the_page()
    {
        var feeds = new FakeFeeds();

        await new AssignedRepairsQueryHandler(feeds)
            .Handle(new AssignedRepairsQuery(Guid.NewGuid(), null), default);

        Assert.Equal(SinceCursor.Need, feeds.SawTake);
    }

    [Fact]
    public async Task The_extra_row_sets_has_more_and_never_ships()
    {
        var feeds = new FakeFeeds();
        var at = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < SinceCursor.Need; i++)
            feeds.Assigned.Add(Row("RP-" + i, at.AddSeconds(i)));

        var result = await new AssignedRepairsQueryHandler(feeds)
            .Handle(new AssignedRepairsQuery(Guid.NewGuid(), null), default);

        Assert.True(result.Value.HasMore);
        Assert.Equal(SinceCursor.PageSize, result.Value.Items.Count);

        // ⚠️ والعلامة من آخر صف **في الصفحة** مش من الصف الزيادة —
        //    وإلا الصف الزيادة بيتخطّى في السحبة الجاية.
        Assert.Equal(
            at.AddSeconds(SinceCursor.PageSize - 1), result.Value.HighWaterUtc);
    }

    [Fact]
    public async Task A_full_page_with_nothing_behind_it_says_so()
    {
        var feeds = new FakeFeeds();
        var at = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < SinceCursor.PageSize; i++)
            feeds.Assigned.Add(Row("RP-" + i, at.AddSeconds(i)));

        var result = await new AssignedRepairsQueryHandler(feeds)
            .Handle(new AssignedRepairsQuery(Guid.NewGuid(), null), default);

        Assert.False(result.Value.HasMore);
        Assert.Equal(SinceCursor.PageSize, result.Value.Items.Count);
    }

    /// <summary>
    /// 🔴 <b>العلامة من <u>آخر صف</u> — مش من ساعة السيرفر.</b>
    ///
    /// <para>لو أخدناها من الساعة، صف اتكتب في نفس الجزء من الثانية
    /// بعد ما بنينا الرد كان هيتفوّت <b>للأبد</b> — والراكة مش هتعرف
    /// إنها فوّتته.</para>
    /// </summary>
    [Fact]
    public async Task The_mark_comes_from_the_last_row_not_the_clock()
    {
        var feeds = new FakeFeeds();

        var last = new DateTime(2026, 10, 4, 9, 5, 0, DateTimeKind.Utc);

        feeds.Assigned.Add(Row("RP-1", new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc)));
        feeds.Assigned.Add(Row("RP-2", last));

        var result = await new AssignedRepairsQueryHandler(feeds)
            .Handle(new AssignedRepairsQuery(Guid.NewGuid(), null), default);

        Assert.Equal(last, result.Value.HighWaterUtc);

        // 🔴 وساعة السيرفر **أحدث** — فالفحص بيفرّق بين الاتنين فعلاً.
        Assert.True(result.Value.ServerTimeUtc > last);
    }

    /// <summary>
    /// 🔴 <b>وعلى صفحة فاضية بترجع العلامة اللي جات — مش ساعة
    /// السيرفر.</b>
    ///
    /// <para>ساعة السيرفر هنا كانت بتدّي لراكة جديدة علامة
    /// ماكسبتهاش وتخلّيها <b>تتخطّى تاريخها كله</b>.</para>
    /// </summary>
    [Fact]
    public async Task An_empty_page_echoes_the_mark_it_was_given()
    {
        var feeds = new FakeFeeds();

        var sent = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

        var result = await new AssignedRepairsQueryHandler(feeds)
            .Handle(new AssignedRepairsQuery(
                Guid.NewGuid(), "2026-10-01T08:00:00.000Z"), default);

        Assert.Empty(result.Value.Items);
        Assert.Equal(sent, result.Value.HighWaterUtc);
    }

    /// <summary>
    /// 🔴 <b>وراكة جديدة على ورشة فاضية بتاخد <c>null</c>.</b>
    ///
    /// <para>والراكة بترفض تحفظ علامة من <c>null</c> — وده الصح:
    /// علامة ماكسبتهاش معناها إنها بتتخطّى شغل ماشافتهوش.</para>
    /// </summary>
    [Fact]
    public async Task A_brand_new_rack_on_an_empty_workshop_gets_no_mark()
    {
        var result = await new AssignedRepairsQueryHandler(new FakeFeeds())
            .Handle(new AssignedRepairsQuery(Guid.NewGuid(), null), default);

        Assert.Empty(result.Value.Items);
        Assert.Null(result.Value.HighWaterUtc);
        Assert.False(result.Value.HasMore);
    }

    /// <summary>
    /// ⚠️ <b>والعلامة البايظة بتوصل المستودع <c>null</c> — يعني
    /// الراكة بتسحب من الأول بـ<c>200</c>.</b>
    /// </summary>
    [Fact]
    public async Task A_broken_mark_becomes_a_full_first_page()
    {
        var feeds = new FakeFeeds();

        var at = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);
        feeds.Assigned.Add(Row("RP-1", at));

        var result = await new AssignedRepairsQueryHandler(feeds)
            .Handle(new AssignedRepairsQuery(Guid.NewGuid(), "مش تاريخ"), default);

        Assert.True(feeds.SinceWasPassed);
        Assert.Null(feeds.SawSince);

        Assert.Single(result.Value.Items);
        Assert.Equal(at, result.Value.HighWaterUtc);
    }

    /// <summary>⚠️ والعلامة السليمة بتوصل المستودع متحوّلة UTC.</summary>
    [Fact]
    public async Task A_good_mark_reaches_the_query_as_utc()
    {
        var feeds = new FakeFeeds();

        await new AssignedRepairsQueryHandler(feeds)
            .Handle(new AssignedRepairsQuery(
                Guid.NewGuid(), "2026-10-04T11:30:00+02:00"), default);

        Assert.Equal(
            new DateTime(2026, 10, 4, 9, 30, 0, DateTimeKind.Utc), feeds.SawSince);

        Assert.Equal(DateTimeKind.Utc, feeds.SawSince!.Value.Kind);
    }

    /// <summary>
    /// 🔴 <b>والسيرفر بيقول صريح إنه بيطبّق الموافقة ولا لأ.</b>
    ///
    /// <para>راكة جديدة على سيرفر قديم هتلاقي الخانة ناقصة،
    /// و«ناقصة» معناها «مش بيطبّق» — مش «كله معلّق». والعلم على
    /// المظروف مش على الصف، لأن دفعة فاضية مافيهاش صفوف.</para>
    /// </summary>
    [Fact]
    public async Task The_envelope_declares_whether_approval_is_enforced()
    {
        var result = await new AssignedRepairsQueryHandler(new FakeFeeds())
            .Handle(new AssignedRepairsQuery(Guid.NewGuid(), null), default);

        Assert.Equal(RepairPolicy.ApprovalEnforced, result.Value.ApprovalEnforced);

        // ⚠️ وحراسة: القيمة دي `true` في الإعداد الحالي — عشان الفحص
        //    مايبقاش بيقارن `false` بـ`false`.
        Assert.True(result.Value.ApprovalEnforced);
    }

    /// <summary>⚠️ وكل حقل في الصف بيعدّي زي ما هو.</summary>
    [Fact]
    public async Task Every_field_on_the_row_travels_down_unchanged()
    {
        var feeds = new FakeFeeds();

        var at = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);
        var tech = Guid.NewGuid();
        var decided = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

        feeds.Assigned.Add(new AssignedRepairFeedRow
        {
            Id = Guid.NewGuid(),
            PublicCode = "RP-000123",
            DeviceId = Guid.NewGuid(),
            DeviceCode = "LP-00000042",
            DeviceName = "ThinkPad T14",
            DeviceManufacturer = "Hewlett-Packard",
            Status = 2,
            AssignedTechnicianId = tech,
            AssignedTechnicianName = "سامي",
            OpenedByName = "كريم",
            OpenedAtUtc = at.AddHours(-2),
            FaultSummary = "الشاشة بتطفى",
            Approval = 1,
            ApprovalNote = "موافق — القطعة موجودة",
            ApprovedByName = "المحاسب",
            ApprovalDecidedAtUtc = decided,
            UpdatedAtUtc = at,
        });

        var result = await new AssignedRepairsQueryHandler(feeds)
            .Handle(new AssignedRepairsQuery(Guid.NewGuid(), null), default);

        var row = Assert.Single(result.Value.Items);

        Assert.Equal("RP-000123", row.PublicCode);
        Assert.Equal("LP-00000042", row.DeviceCode);
        Assert.Equal("ThinkPad T14", row.DeviceName);
        Assert.Equal("Hewlett-Packard", row.DeviceManufacturer);
        Assert.Equal(2, row.Status);
        Assert.Equal(tech, row.AssignedTechnicianId);
        Assert.Equal("سامي", row.AssignedTechnicianName);
        Assert.Equal("كريم", row.OpenedByName);
        Assert.Equal("الشاشة بتطفى", row.FaultSummary);
        Assert.Equal(1, row.Approval);
        Assert.Equal("موافق — القطعة موجودة", row.ApprovalNote);
        Assert.Equal("المحاسب", row.ApprovedByName);
        Assert.Equal(decided, row.ApprovalDecidedAtUtc);
        Assert.Equal(at, row.UpdatedAtUtc);
    }

    /// <summary>
    /// 🔴 <b>ومفيش بصمة ولا ملح ولا اسم دخول في أي صف من التغذيات
    /// دي.</b>
    ///
    /// <para>صف الفني عقد «مين ينفع يتسند» مش عقد دخول — والصف
    /// الضيّق بيخلّي التسريب <b>مستحيل</b> مش «ممنوع».</para>
    /// </summary>
    [Fact]
    public void No_feed_row_has_a_field_that_could_hold_a_credential()
    {
        Type[] rows =
        [
            typeof(RosterFeedRow),
            typeof(ContainerFeedRow),
            typeof(AssignedRepairFeedRow),
            typeof(Application.Contracts.Rack.RepairTechnicianRow),
            typeof(Application.Contracts.Rack.ContainerRow),
            typeof(Application.Contracts.Rack.AssignedRepairRow),
        ];

        foreach (var type in rows)
        {
            foreach (var property in type.GetProperties())
            {
                string name = property.Name.ToLowerInvariant();

                Assert.DoesNotContain("hash", name);
                Assert.DoesNotContain("salt", name);
                Assert.DoesNotContain("password", name);
                Assert.DoesNotContain("username", name);
                Assert.DoesNotContain("apikey", name);
            }
        }
    }

    /// <summary>
    /// ⚠️ <b>وأسماء التغذيات اللي السيرفر بيعلنها هي نفسها اللي
    /// النقط دي بتخدمها.</b>
    ///
    /// <para>الراكة بتسأل <c>capabilities</c> الأول، ولو الاسم
    /// المعلن مش موجود كنقطة، بتفضل تسأل عن حاجة مش بتتخدم —
    /// ساكتة.</para>
    /// </summary>
    [Fact]
    public void The_three_announced_feeds_are_the_three_we_serve()
    {
        Assert.Equal(3, DownstreamFeeds.All.Count);

        Assert.Contains(DownstreamFeeds.RepairRoster, DownstreamFeeds.All);
        Assert.Contains(DownstreamFeeds.RepairAssigned, DownstreamFeeds.All);
        Assert.Contains(DownstreamFeeds.Containers, DownstreamFeeds.All);
    }
}
