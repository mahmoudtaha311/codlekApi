using Codlek.Application.Features.Rack.IngestReports;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// ترجمة معرّف الجهاز للكانوني <b>الحيّ</b>.
///
/// <para>🔴 <b>والملف ده موجود عشان حادثة إنتاج.</b> راكة جديدة
/// بتشوف لاب لأول مرة بتولّد له معرّف محلي. السيرفر بيطابق مراسيه
/// ويلاقيه جهاز موجود بمعرّف تاني، فبيعمل اسم مستعار وبيرجّع
/// «اتقبل» — وده الصح. بس الدمج <b>مابيمسحش</b> صف الجهاز المكرر:
/// بيسيبه شاهد قبر. فمعرّف مدموج بيتلاقى في جدول الأجهزة وبيرجع
/// <b>شاهد القبر</b>، والنتيجة شغل بيتربط بجهاز ميّت.</para>
///
/// <para>⚠️ <b>والدمج مابيعيدش توجيه الأسامي المستعارة القديمة</b> —
/// «أ ← ب» اللي اتعمل قبل «ب ← ج» بيفضل بيشاور على ب، وب بقى شاهد
/// قبر. عشان كده التتبّع بيلفّ.</para>
/// </summary>
public class DeviceReferenceTests
{
    private sealed class FakeDevices : IDeviceReferenceRepository
    {
        public readonly Dictionary<Guid, DeviceMergeState> States = [];
        public readonly Dictionary<Guid, Guid> Aliases = [];

        /// <summary>⚠️ عدّاد اللفّات — الفحص بيقيس إن الشايع لفّة واحدة.</summary>
        public int Rounds;

        /// <summary>
        /// ⚠️ <b>المعرّفات اللي اتسأل عنها في جدول الأسامي
        /// المستعارة.</b>
        ///
        /// <para>🔴 والعدّاد ده اتضاف بعد تحوير نجا: الاستعلام
        /// المفروض يسأل على <b>اللي مالوش صف جهاز بس</b>. سؤاله على
        /// الكل بيدّي نفس الإجابة (فرع الصف بيكسب) — فالفحص لازم
        /// يقيس <b>اللي اتسأل عنه</b> مش بس النتيجة.</para>
        /// </summary>
        public readonly List<Guid> AliasProbes = [];

        public Task<IReadOnlyDictionary<Guid, DeviceMergeState>> DeviceStatesAsync(
            Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        {
            Rounds++;

            return Task.FromResult<IReadOnlyDictionary<Guid, DeviceMergeState>>(
                States.Where(p => ids.Contains(p.Key))
                    .ToDictionary(p => p.Key, p => p.Value));
        }

        public Task<IReadOnlyDictionary<Guid, Guid>> AliasTargetsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> aliasIds,
            CancellationToken ct = default)
        {
            AliasProbes.AddRange(aliasIds);

            return Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(
                Aliases.Where(p => aliasIds.Contains(p.Key))
                    .ToDictionary(p => p.Key, p => p.Value));
        }

        public void Alive(Guid id) =>
            States[id] = new DeviceMergeState { Status = DeviceLifecycleStatus.Active };

        public void Tombstone(Guid id, Guid into) =>
            States[id] = new DeviceMergeState
            {
                Status = DeviceLifecycleStatus.Merged,
                MergedIntoDeviceId = into,
            };
    }

    private static (DeviceReferenceResolver Resolver, FakeDevices Repo) Build()
    {
        var repo = new FakeDevices();
        return (new DeviceReferenceResolver(repo), repo);
    }

    private static readonly Guid Tenant = Guid.NewGuid();

    // =================================================================
    //  الحالة الشايعة
    // =================================================================

    [Fact]
    public async Task A_live_device_resolves_to_itself_in_one_round()
    {
        var (resolver, repo) = Build();
        var device = Guid.NewGuid();

        repo.Alive(device);

        Assert.Equal(device, await resolver.ResolveAsync(Tenant, device));

        // ⚠️ لفّة واحدة — الشايع مالوش لازمة رحلة تانية.
        Assert.Equal(1, repo.Rounds);
    }

    [Fact]
    public async Task An_id_the_server_never_saw_resolves_to_nothing()
    {
        var (resolver, _) = Build();

        Assert.Null(await resolver.ResolveAsync(Tenant, Guid.NewGuid()));
    }

    [Fact]
    public async Task An_empty_id_is_never_asked_about()
    {
        var (resolver, repo) = Build();

        Assert.Null(await resolver.ResolveAsync(Tenant, Guid.Empty));
        Assert.Equal(0, repo.Rounds);
    }

    // =================================================================
    //  الاسم المستعار
    // =================================================================

    [Fact]
    public async Task An_alias_resolves_to_its_canonical()
    {
        var (resolver, repo) = Build();

        var local = Guid.NewGuid();
        var canonical = Guid.NewGuid();

        repo.Aliases[local] = canonical;
        repo.Alive(canonical);

        Assert.Equal(canonical, await resolver.ResolveAsync(Tenant, local));
    }

    /// <summary>
    /// 🔴 <b>وشاهد القبر مش نهاية الطريق.</b>
    ///
    /// <para>«موجود» معناها <b>حيّ</b> مش «ليه صف» — والصف اللي
    /// <c>Merged</c> لازم يوصّلنا للي بعده.</para>
    /// </summary>
    [Fact]
    public async Task A_tombstone_keeps_going_to_the_living_device()
    {
        var (resolver, repo) = Build();

        var dead = Guid.NewGuid();
        var alive = Guid.NewGuid();

        repo.Tombstone(dead, alive);
        repo.Alive(alive);

        Assert.Equal(alive, await resolver.ResolveAsync(Tenant, dead));
    }

    /// <summary>
    /// 🔴 <b>والسلسلة الكاملة: اسم مستعار ← شاهد قبر ← حيّ.</b>
    ///
    /// <para>ودي الحالة اللي كانت بتقع: «أ ← ب» اتعمل، وبعدين ب اتدمج
    /// في ج. الاسم المستعار لسه بيشاور على ب، وب شاهد قبر — فلولا
    /// اللفّة، الشغل كان بيتربط <b>بجهاز ميّت</b>.</para>
    /// </summary>
    [Fact]
    public async Task An_alias_onto_a_tombstone_still_finds_the_living_device()
    {
        var (resolver, repo) = Build();

        var local = Guid.NewGuid();
        var merged = Guid.NewGuid();
        var canonical = Guid.NewGuid();

        repo.Aliases[local] = merged;
        repo.Tombstone(merged, canonical);
        repo.Alive(canonical);

        Assert.Equal(canonical, await resolver.ResolveAsync(Tenant, local));
    }

    /// <summary>⚠️ وسلسلة دمج من تلات خطوات.</summary>
    [Fact]
    public async Task A_three_hop_merge_chain_resolves()
    {
        var (resolver, repo) = Build();

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();

        repo.Tombstone(a, b);
        repo.Tombstone(b, c);
        repo.Tombstone(c, d);
        repo.Alive(d);

        Assert.Equal(d, await resolver.ResolveAsync(Tenant, a));
    }

    // =================================================================
    //  البيانات البايظة
    // =================================================================

    /// <summary>
    /// 🔴 <b>والحلقة المقفولة بترجع «مش معروف» — مش بتعلّق
    /// الطلب.</b>
    ///
    /// <para>السقف موجود عشان بيانات بايظة <b>متعلّقش الطلب
    /// للأبد</b>.</para>
    /// </summary>
    [Fact]
    public async Task A_closed_loop_gives_up_instead_of_spinning()
    {
        var (resolver, repo) = Build();

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        repo.Tombstone(a, b);
        repo.Tombstone(b, a);

        Assert.Null(await resolver.ResolveAsync(Tenant, a));

        /*
          🔴 **ووقف بدري — مش لما السقف يخلص.**

          السقف لوحده بيمنع اللفّ للأبد، فكشف الحلقة **زيادة** من
          ناحية النتيجة. بس هو اللي بيوفّر الرحلات: من غيره، كل
          سلسلة بايظة بتاكل عشر رحلات على القاعدة بدل اتنين.

          ⚠️ وتحوير شال الكشف **ونجا** من فحص كان بيقول
          `<= MaxHops` — وده شرط بيتحقق في الحالتين.
        */
        Assert.True(
            repo.Rounds <= 3,
            $"كشف الحلقة مابيوفّرش رحلات: {repo.Rounds} لفّة.");
    }

    /// <summary>
    /// ⚠️ <b>والسلسلة الأطول من السقف بترجع «مش معروف».</b>
    /// </summary>
    [Fact]
    public async Task A_chain_longer_than_the_cap_gives_up()
    {
        var (resolver, repo) = Build();

        var ids = Enumerable.Range(0, DeviceMergeChain.MaxHops + 3)
            .Select(_ => Guid.NewGuid())
            .ToList();

        for (int i = 0; i < ids.Count - 1; i++) repo.Tombstone(ids[i], ids[i + 1]);

        repo.Alive(ids[^1]);

        Assert.Null(await resolver.ResolveAsync(Tenant, ids[0]));
    }

    /// <summary>
    /// ⚠️ <b>وشاهد قبر بيشاور على لا حاجة بيرجع «مش معروف».</b>
    /// </summary>
    [Fact]
    public async Task A_tombstone_pointing_nowhere_is_unknown()
    {
        var (resolver, repo) = Build();

        var dead = Guid.NewGuid();
        var gone = Guid.NewGuid();

        repo.Tombstone(dead, gone);

        Assert.Null(await resolver.ResolveAsync(Tenant, dead));
    }

    /// <summary>
    /// ⚠️ <b>و<c>Merged</c> من غير معرّف الهدف مش شاهد قبر</b> —
    /// بيانات ناقصة، فالصف نفسه هو الإجابة بدل ما نلف على فاضي.
    /// </summary>
    [Fact]
    public async Task Merged_without_a_target_is_not_a_tombstone()
    {
        var (resolver, repo) = Build();

        var device = Guid.NewGuid();

        repo.States[device] = new DeviceMergeState
        {
            Status = DeviceLifecycleStatus.Merged,
            MergedIntoDeviceId = null,
        };

        Assert.Equal(device, await resolver.ResolveAsync(Tenant, device));
    }

    // =================================================================
    //  بالجملة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>واللفّة بتحلّ طبقة لكل المعرّفات مع بعض</b> — مش معرّف
    /// معرّف.
    /// </summary>
    [Fact]
    public async Task A_mixed_batch_resolves_in_as_few_rounds_as_the_longest_chain()
    {
        var (resolver, repo) = Build();

        var alive = Guid.NewGuid();
        var local = Guid.NewGuid();
        var canonical = Guid.NewGuid();
        var dead = Guid.NewGuid();
        var unknown = Guid.NewGuid();

        repo.Alive(alive);
        repo.Aliases[local] = canonical;
        repo.Alive(canonical);
        repo.Tombstone(dead, canonical);

        var map = await resolver.ResolveManyAsync(
            Tenant, [alive, local, dead, unknown]);

        Assert.Equal(alive, map[alive]);
        Assert.Equal(canonical, map[local]);
        Assert.Equal(canonical, map[dead]);

        // 🔴 والمجهول **مابيظهرش في الخريطة خالص** — «السيرفر عمره
        //    ما شافه» مش «شافه وماعرفش يترجمه».
        Assert.False(map.ContainsKey(unknown));

        // ⚠️ أطول سلسلة خطوتين، فلفّتين كفاية.
        Assert.True(repo.Rounds <= 3, $"لفّات كتير: {repo.Rounds}");
    }

    /// <summary>⚠️ والمعرّف الفاضي والمتكرر بيتشالوا قبل أي استعلام.</summary>
    [Fact]
    public async Task Empty_and_duplicate_ids_are_dropped_first()
    {
        var (resolver, repo) = Build();

        var device = Guid.NewGuid();
        repo.Alive(device);

        var map = await resolver.ResolveManyAsync(
            Tenant, [device, device, Guid.Empty, device]);

        Assert.Single(map);
        Assert.Equal(1, repo.Rounds);
    }

    [Fact]
    public async Task An_empty_request_asks_nothing()
    {
        var (resolver, repo) = Build();

        Assert.Empty(await resolver.ResolveManyAsync(Tenant, []));
        Assert.Equal(0, repo.Rounds);
    }

    /// <summary>
    /// ⚠️ <b>وحلقة في سلسلة واحدة مابتوقّفش الباقي.</b> كل معرّف
    /// بيتتبّع سلسلته لوحده.
    /// </summary>
    [Fact]
    public async Task A_broken_chain_does_not_poison_the_batch()
    {
        var (resolver, repo) = Build();

        var loopA = Guid.NewGuid();
        var loopB = Guid.NewGuid();
        var good = Guid.NewGuid();

        repo.Tombstone(loopA, loopB);
        repo.Tombstone(loopB, loopA);
        repo.Alive(good);

        var map = await resolver.ResolveManyAsync(Tenant, [loopA, good]);

        Assert.Equal(good, map[good]);
        Assert.False(map.ContainsKey(loopA));
    }

    /// <summary>
    /// 🔴 <b>وجدول الأسامي المستعارة بيتسأل على اللي <u>مالوش صف
    /// جهاز</u> بس.</b>
    ///
    /// <para>الجهاز الحيّ خلص — سؤاله في جدول تاني رحلة على الفاضي.
    /// وده بيشتغل على <b>كل صف في كل دفعة</b>، فالرحلة الزيادة مش
    /// تفصيلة.</para>
    ///
    /// <para>⚠️ وتحوير خلّى الاستعلام يسأل على الكل <b>ونجا</b>، لأن
    /// الإجابة بتفضل نفسها (فرع الصف بيكسب). فالفحص بيقيس
    /// <b>اللي اتسأل عنه</b>.</para>
    /// </summary>
    [Fact]
    public async Task Only_ids_without_a_device_row_are_looked_up_as_aliases()
    {
        var (resolver, repo) = Build();

        var alive = Guid.NewGuid();
        var local = Guid.NewGuid();
        var canonical = Guid.NewGuid();

        repo.Alive(alive);
        repo.Aliases[local] = canonical;
        repo.Alive(canonical);

        await resolver.ResolveManyAsync(Tenant, [alive, local]);

        Assert.Contains(local, repo.AliasProbes);
        Assert.DoesNotContain(alive, repo.AliasProbes);
    }

    /// <summary>
    /// ⚠️ <b>وكله أجهزة حيّة = ولا سؤال واحد على الأسامي
    /// المستعارة.</b>
    /// </summary>
    [Fact]
    public async Task A_batch_of_live_devices_never_touches_the_alias_table()
    {
        var (resolver, repo) = Build();

        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids) repo.Alive(id);

        await resolver.ResolveManyAsync(Tenant, ids);

        Assert.Empty(repo.AliasProbes);
        Assert.Equal(1, repo.Rounds);
    }
}
