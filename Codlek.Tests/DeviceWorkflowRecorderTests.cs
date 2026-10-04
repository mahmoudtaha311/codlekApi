using Codlek.Application.Contracts.Workflow;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// مسجّل حركة الجهاز — على قاعدة حقيقية.
///
/// <para>🔴 <b>الحاجة اللي بتتفحص هنا كانت سبب عطل إنتاج:</b> حركة
/// اترفضت لجهاز <b>وصل فعلاً</b> بس بمعرّف تاني. والفحص الأول تحت هو
/// اللي بيمنع رجوعه.</para>
/// </summary>
public class DeviceWorkflowRecorderTests(WorkflowDbFixture fixture)
    : IClassFixture<WorkflowDbFixture>
{
    private static DeviceWorkflowRecorder Recorder(AppDbContext db) =>
        new(db,
            new DeviceReference(db, NullLogger<DeviceReference>.Instance),
            NullLogger<DeviceWorkflowRecorder>.Instance);

    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Device NewDevice(
        AppDbContext db, Guid tenantId,
        DeviceLifecycleStatus status = DeviceLifecycleStatus.Active,
        DeviceOperationalStage stage = DeviceOperationalStage.Received,
        Guid? mergedInto = null, Guid? holder = null, Guid? location = null)
    {
        var row = new Device
        {
            TenantId = tenantId,
            PublicCode = "LP-" + Guid.NewGuid().ToString("N")[..6],
            LastKnownManufacturer = "HP",
            LastKnownModel = "20L5",
            Status = status,
            MergedIntoDeviceId = mergedInto,
            OperationalStage = stage,
            CurrentHolderTechnicianId = holder,
            CurrentLocationId = location,
            LastSeenAtUtc = DateTime.UtcNow,
        };
        db.Devices.Add(row);
        return row;
    }

    private static Technician NewTechnician(AppDbContext db, Guid tenantId)
    {
        // ⚠️ الاسم لازم يبقى فريد: فيه فهرس فريد مفلتر على
        // (TenantId, NormalizedUsername) — واتنين باسم فاضي بيتصادموا.
        string username = "t" + Guid.NewGuid().ToString("N")[..10];

        var row = new Technician
        {
            TenantId = tenantId,
            DisplayName = "أحمد",
            Username = username,
            NormalizedUsername = username,
            Code = "F" + Guid.NewGuid().ToString("N")[..5],
            IsActive = true,
            CanRepair = true,
        };
        db.Technicians.Add(row);
        return row;
    }

    private static WorkflowMove Move(
        Guid deviceId,
        DeviceWorkflowEventType type = DeviceWorkflowEventType.StageChanged,
        DeviceOperationalStage? toStage = null,
        Guid? toTechnician = null,
        Guid? toLocation = null,
        bool clearsHolder = false,
        Guid? eventId = null,
        DateTime? occurredAt = null) => new()
    {
        DeviceId = deviceId,
        EventType = type,
        Actor = WorkflowActor.System("فحص"),
        ToStage = toStage,
        ToTechnicianId = toTechnician,
        ToLocationId = toLocation,
        ClearsHolder = clearsHolder,
        EventId = eventId,
        OccurredAtUtc = occurredAt,
    };

    // =================================================================
    //  الترجمة للكانوني — العطل اللي حصل فعلاً
    // =================================================================

    /// <summary>
    /// 🔴 <b>حركة بمعرّف جهاز مدموج بتتسجّل على الكانوني.</b>
    ///
    /// <para>الراكة بتبعت معرّفها المحلي، واللاب متسجّل على السيرفر
    /// بمعرّف تاني. والبحث المباشر كان بيرفض — والفني شاف «الجهاز مش
    /// موجود» على لاب هو ماسكه في إيده.</para>
    ///
    /// <para>⚠️ <b>وأنا كنت متوقّع إن الحركة تترفض — وده كان غلط.</b>
    /// المشي بيعدّي <b>خلف</b> الجهاز المدموج للكانوني، فالفحص بيتسجّل
    /// على الصح. وفحص «المدموج بيترفض» تحت بيغطّي الحالة التانية:
    /// صف مدموج <b>من غير</b> جهاز كانوني — بيانات ناقصة، والمشي
    /// بيوقف عنده.</para>
    /// </summary>
    [Fact]
    public async Task A_move_on_a_merged_id_lands_on_the_canonical_device()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var canonical = NewDevice(db, tenant);
        await db.SaveChangesAsync();

        var merged = NewDevice(
            db, tenant, DeviceLifecycleStatus.Merged, mergedInto: canonical.Id);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(
            tenant, Move(merged.Id, toStage: DeviceOperationalStage.UnderRepair));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(canonical.Id, result.Event!.DeviceId);
    }

    /// <summary>
    /// 🔴 <b>وصف مدموج من غير جهاز كانوني بيترفض.</b>
    ///
    /// <para>دي بيانات ناقصة: الحالة <c>Merged</c> و
    /// <c>MergedIntoDeviceId</c> فاضي. والمشي بيوقف عنده بدل ما
    /// يتبع <c>Guid.Empty</c> — والمسجّل بيرفض عشان السؤال «اللاب ده
    /// فين» مايبقاش ليه إجابتين.</para>
    /// </summary>
    [Fact]
    public async Task A_merged_row_with_no_canonical_target_is_refused()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var orphan = NewDevice(db, tenant, DeviceLifecycleStatus.Merged, mergedInto: null);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(tenant, Move(orphan.Id));

        Assert.False(result.Ok);
        Assert.Contains("مدموج", result.Error!);
    }

    /// <summary>
    /// 🔴 <b>وحركة باسم مستعار بتعدّي وبتتسجّل على الكانوني.</b>
    ///
    /// <para>ودي الحالة اللي العطل كان فيها بالظبط: المعرّف مش في
    /// <c>Devices</c> خالص، هو في <c>DeviceAliases</c>.</para>
    /// </summary>
    [Fact]
    public async Task A_move_on_an_alias_id_is_recorded_on_the_canonical_device()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var canonical = NewDevice(db, tenant);
        await db.SaveChangesAsync();

        Guid aliasId = Guid.NewGuid();
        db.DeviceAliases.Add(new DeviceAlias
        {
            TenantId = tenant,
            AliasDeviceId = aliasId,
            CanonicalDeviceId = canonical.Id,
            SourceRackId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(
            tenant, Move(aliasId, toStage: DeviceOperationalStage.UnderRepair));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(canonical.Id, result.Event!.DeviceId);
    }

    [Fact]
    public async Task A_move_on_an_unknown_id_is_refused()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(tenant, Move(Guid.NewGuid()));

        Assert.False(result.Ok);
        Assert.Equal("الجهاز مش موجود.", result.Error);
    }

    /// <summary>⚠️ وجهاز شركة تانية = مش موجود.</summary>
    [Fact]
    public async Task A_device_from_another_tenant_is_not_found()
    {
        await using var db = fixture.Create();
        Guid mine = NewTenant(db), theirs = NewTenant(db);
        var theirDevice = NewDevice(db, theirs);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(mine, Move(theirDevice.Id));

        Assert.False(result.Ok);
    }

    // =================================================================
    //  منع التكرار
    // =================================================================

    /// <summary>
    /// 🔴 <b>إعادة رفع نفس الحركة بترجع بنجاح ومعاها الصف القديم.</b>
    ///
    /// <para>الراكة بتعيد الرفع بعد انقطاع، والرفض ساعتها بيخلّيها
    /// تعيد <b>للأبد</b>. ومن غير منع التكرار، كل إعادة محاولة بتكتب
    /// سطر تاني في سجل الحركة.</para>
    /// </summary>
    [Fact]
    public async Task Re_uploading_the_same_move_is_idempotent()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var device = NewDevice(db, tenant);
        await db.SaveChangesAsync();

        Guid eventId = Guid.NewGuid();
        var recorder = Recorder(db);

        var first = await recorder.RecordAsync(
            tenant, Move(device.Id, toStage: DeviceOperationalStage.Testing, eventId: eventId));
        await db.SaveChangesAsync();

        var second = await recorder.RecordAsync(
            tenant, Move(device.Id, toStage: DeviceOperationalStage.Ready, eventId: eventId));

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal(first.Event!.Id, second.Event!.Id);

        Assert.Equal(1, await db.DeviceWorkflowEvents.CountAsync(e => e.TenantId == tenant));
    }

    /// <summary>
    /// ⚠️ <b>والمرحلة مابتتغيّرش في الإعادة.</b>
    ///
    /// <para>الحركة التانية طلبت <c>Ready</c> واتجاهلت — فالجهاز لازم
    /// يفضل على <c>Testing</c>. ولو الكاش اتحدّث، إعادة رفع قديمة
    /// بترجّع الجهاز لمرحلة فات عليها يومين.</para>
    /// </summary>
    [Fact]
    public async Task A_duplicate_move_does_not_move_the_device_again()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var device = NewDevice(db, tenant);
        await db.SaveChangesAsync();

        Guid eventId = Guid.NewGuid();
        var recorder = Recorder(db);

        await recorder.RecordAsync(
            tenant, Move(device.Id, toStage: DeviceOperationalStage.Testing, eventId: eventId));
        await db.SaveChangesAsync();

        await recorder.RecordAsync(
            tenant, Move(device.Id, toStage: DeviceOperationalStage.Ready, eventId: eventId));
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var reread = await db.Devices.SingleAsync(d => d.Id == device.Id);

        Assert.Equal(DeviceOperationalStage.Testing, reread.OperationalStage);
    }

    // =================================================================
    //  المراجع
    // =================================================================

    [Fact]
    public async Task An_unknown_location_is_refused()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var device = NewDevice(db, tenant);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(
            tenant, Move(device.Id, toLocation: Guid.NewGuid()));

        Assert.False(result.Ok);
        Assert.Equal("الموقع مش موجود.", result.Error);
    }

    [Fact]
    public async Task An_unknown_technician_is_refused()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var device = NewDevice(db, tenant);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(
            tenant, Move(device.Id, toTechnician: Guid.NewGuid()));

        Assert.False(result.Ok);
        Assert.Equal("الفني مش موجود.", result.Error);
    }

    // =================================================================
    //  القرارات لكل حقل
    // =================================================================

    /// <summary>
    /// 🔴 <b>حركة حيازة بس بتسجّل المرحلة <i>الحالية</i>، مش
    /// <c>Unknown</c>.</b>
    ///
    /// <para>ولو سجّلت صفر، سجل الحركة بيقول إن اللاب رجع لـ«مرحلة
    /// غير معروفة» في كل تسليم — والتاريخ بيبقى مالوش معنى.</para>
    /// </summary>
    [Fact]
    public async Task A_custody_only_move_records_the_current_stage()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var device = NewDevice(db, tenant, stage: DeviceOperationalStage.UnderRepair);
        var tech = NewTechnician(db, tenant);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(
            tenant,
            Move(device.Id, DeviceWorkflowEventType.CustodyHandoff, toTechnician: tech.Id));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(DeviceOperationalStage.UnderRepair, result.Event!.ToStage);
        Assert.Equal(DeviceOperationalStage.UnderRepair, result.Event.FromStage);
    }

    /// <summary>
    /// 🔴 <b>حركة مرحلة بس بتحمل الحائز معاها.</b>
    ///
    /// <para>ولو فضّته، كل تغيير مرحلة كان بيشيل اللاب من إيد الفني
    /// اللي شغّال عليه — والسؤال «مع مين» بيرجع فاضي في نص
    /// الصيانة.</para>
    /// </summary>
    [Fact]
    public async Task A_stage_only_move_carries_the_holder_forward()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var tech = NewTechnician(db, tenant);
        await db.SaveChangesAsync();
        var device = NewDevice(db, tenant, holder: tech.Id);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(
            tenant, Move(device.Id, toStage: DeviceOperationalStage.Ready));
        await db.SaveChangesAsync();

        Assert.Equal(tech.Id, result.Event!.ToTechnicianId);

        db.ChangeTracker.Clear();
        var reread = await db.Devices.SingleAsync(d => d.Id == device.Id);
        Assert.Equal(tech.Id, reread.CurrentHolderTechnicianId);
    }

    /// <summary>
    /// 🔴 <b><c>ClearsHolder</c> بتسبق <c>ToTechnicianId</c>.</b>
    ///
    /// <para>«اللاب رجع الرف» حركة حقيقية مختلفة عن «اللاب انتقل لفني
    /// تاني» — ولو الاتنين مبعوتين، الرجوع للرف هو اللي بيكسب.</para>
    /// </summary>
    [Fact]
    public async Task Clearing_the_holder_beats_an_explicit_technician()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var holder = NewTechnician(db, tenant);
        var other = NewTechnician(db, tenant);
        await db.SaveChangesAsync();
        var device = NewDevice(db, tenant, holder: holder.Id);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(
            tenant, Move(device.Id, toTechnician: other.Id, clearsHolder: true));
        await db.SaveChangesAsync();

        Assert.Null(result.Event!.ToTechnicianId);
        Assert.Equal(holder.Id, result.Event.FromTechnicianId);

        db.ChangeTracker.Clear();
        var reread = await db.Devices.SingleAsync(d => d.Id == device.Id);
        Assert.Null(reread.CurrentHolderTechnicianId);
    }

    /// <summary>
    /// ⚠️ <b>وحركة من غير حائز ومن غير تفضية مابتلمسش الحائز.</b>
    ///
    /// <para>حركة رف لرف — واللاب لسه مع نفس الفني.</para>
    /// </summary>
    [Fact]
    public async Task A_shelf_to_shelf_move_does_not_clear_the_holder()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var tech = NewTechnician(db, tenant);
        await db.SaveChangesAsync();
        var device = NewDevice(db, tenant, holder: tech.Id);
        await db.SaveChangesAsync();

        await Recorder(db).RecordAsync(
            tenant, Move(device.Id, DeviceWorkflowEventType.LocationMoved));
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var reread = await db.Devices.SingleAsync(d => d.Id == device.Id);

        Assert.Equal(tech.Id, reread.CurrentHolderTechnicianId);
    }

    /// <summary>
    /// ⚠️ <b>وحركة لنفس المرحلة مابتلمسش <c>StageChangedAtUtc</c>.</b>
    ///
    /// <para>ولو لمستها، «بقاله كام ساعة في المرحلة دي» بيتصفّر مع كل
    /// حركة حيازة — والطابور بيقول إن كل حاجة جديدة.</para>
    /// </summary>
    [Fact]
    public async Task A_same_stage_move_leaves_the_stage_timestamp_alone()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var device = NewDevice(db, tenant, stage: DeviceOperationalStage.UnderRepair);
        var stamped = DateTime.UtcNow.AddDays(-3);
        device.StageChangedAtUtc = stamped;
        await db.SaveChangesAsync();

        await Recorder(db).RecordAsync(
            tenant, Move(device.Id, toStage: DeviceOperationalStage.UnderRepair));
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var reread = await db.Devices.SingleAsync(d => d.Id == device.Id);

        Assert.Equal(stamped, reread.StageChangedAtUtc!.Value, TimeSpan.FromSeconds(1));
    }

    // =================================================================
    //  الوقت
    // =================================================================

    /// <summary>
    /// 🔴 <b>وقت الحركة ووقت التسجيل حاجتين مختلفتين.</b>
    ///
    /// <para>راكة اشتغلت أوفلاين يومين وبعدين رفعت: «اتعمل إمتى»
    /// سؤال مختلف عن «وصلنا إمتى». والمرحلة بتتوقّت بوقت
    /// <b>الحركة</b> — عشان «بقاله كام ساعة» يبقى صح.</para>
    /// </summary>
    [Fact]
    public async Task An_offline_move_keeps_both_timestamps()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var device = NewDevice(db, tenant);
        await db.SaveChangesAsync();

        var twoDaysAgo = DateTime.UtcNow.AddDays(-2);

        var result = await Recorder(db).RecordAsync(
            tenant,
            Move(device.Id, toStage: DeviceOperationalStage.Ready, occurredAt: twoDaysAgo));
        await db.SaveChangesAsync();

        Assert.Equal(twoDaysAgo, result.Event!.OccurredAtUtc, TimeSpan.FromSeconds(1));
        Assert.True(result.Event.RecordedAtUtc > twoDaysAgo.AddHours(1));

        db.ChangeTracker.Clear();
        var reread = await db.Devices.SingleAsync(d => d.Id == device.Id);

        // ⚠️ المرحلة اتوقّتت بوقت الحركة مش بوقت التسجيل
        Assert.Equal(twoDaysAgo, reread.StageChangedAtUtc!.Value, TimeSpan.FromSeconds(1));
    }

    /// <summary>⚠️ والقص بيتطبّق على الأسماء والسبب.</summary>
    [Fact]
    public async Task Long_names_and_reasons_are_clipped()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var device = NewDevice(db, tenant);
        await db.SaveChangesAsync();

        var result = await Recorder(db).RecordAsync(tenant, new WorkflowMove
        {
            DeviceId = device.Id,
            EventType = DeviceWorkflowEventType.StageChanged,
            Actor = WorkflowActor.System(new string('ا', 300)),
            Reason = new string('ب', 600),
        });

        Assert.True(result.Ok, result.Error);
        Assert.Equal(120, result.Event!.ActorName.Length);
        Assert.Equal(400, result.Event.Reason.Length);
    }
}
