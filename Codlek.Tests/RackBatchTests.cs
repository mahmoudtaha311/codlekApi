using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Codlek.Application.Contracts.Sync;
using Codlek.Application.Contracts.Wire;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Racks;
using Codlek.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Codlek.Tests;

/// <summary>
/// سيرفر حقيقي على <b>قاعدة فحص لوحدها</b> — ومعاه محطة متسجّلة بمفتاح.
///
/// <para>🔴 <b>القاعدة بتتبدّل في الحاوية نفسها — مش في الإعدادات.</b>
/// نص الاتصال في التطوير جايّ من أسرار المستخدم، وأي قيمة بنحطّها
/// في الإعدادات من هنا بتتدهس بيها — يعني الفحوص كانت هتكتب على
/// قاعدة التطوير. والسيرفر مابيقبلش يقوم لو القاعدة اللي متوصّل بيها
/// مش قاعدة الفحص.</para>
/// </summary>
public sealed class RackBatchServer : WebApplicationFactory<Program>
{
    private sealed class Db : SqlServerDbFixture
    {
        protected override string DatabaseName => "codlek_rack_batch_test";
    }

    private readonly Db _db = new();

    public AppDbContext CreateDb() => _db.Create();

    public Guid TenantId { get; private set; }
    public Guid RackId { get; private set; }
    public string Key { get; private set; } = "";

    /// <summary>
    /// ⚠️ <b>بيتنفّذ جوّه الطلب قبل كل «احفظ لو تقدر»</b> — رقم النداء
    /// في الطلب. ده اللي بيمثّل طلب منافس كتب قبلنا.
    /// </summary>
    public Func<int, Task>? BeforeTrySave { get; set; }

    /// <summary>نتيجة كل «احفظ لو تقدر» بالترتيب — عشان الفحص يثبت إن السباق حصل فعلاً.</summary>
    public List<bool> TrySaveResults { get; } = [];

    public RackBatchServer()
    {
        // ⚠️ الإقلاع نفسه هنا — والتحقق من القاعدة قبل أي زرع.
        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string expected = new SqlConnectionStringParts(_db.ConnectionString).Database;
        string actual = db.Database.GetDbConnection().Database;

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"السيرفر متوصّل بـ{actual} مش بقاعدة الفحص {expected} — وقفنا قبل أي كتابة.");

        var tenant = new Tenant { Name = "ورشة الدفعات" };
        db.Tenants.Add(tenant);

        var (key, hash, salt) = scope.ServiceProvider.GetRequiredService<IRackKeys>().Issue();

        var rack = new Rack
        {
            TenantId = tenant.Id,
            RackCode = "R-BATCH",
            Name = "بنش الدفعات",
            Status = RackStatus.Active,
            ApiKeyHash = hash,
            Salt = salt,
            KeyPrefix = RackKey.Prefix(key),
            RegisteredAtUtc = DateTime.UtcNow,
            AppVersion = "0.0.1",
        };

        db.Racks.Add(rack);
        db.SaveChanges();

        TenantId = tenant.Id;
        RackId = rack.Id;
        Key = key;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:PublicBaseUrl"] = "http://localhost:5097",
            }));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var doomed = services
                .Where(d => d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || (d.ServiceType.IsGenericType
                             && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                             && d.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext)))
                .ToList();

            foreach (var d in doomed) services.Remove(d);

            // ⚠️ نفس إعدادات الإنتاج — الإعادة بتغيّر طريقة تنفيذ الحفظ.
            services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
                _db.ConnectionString,
                sql =>
                {
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                    sql.CommandTimeout(60);
                }));

            services.Replace(ServiceDescriptor.Scoped<IUnitOfWork>(sp =>
                new RacingUnitOfWork(new UnitOfWork(sp.GetRequiredService<AppDbContext>()), this)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing) _db.Dispose();
    }

    /// <summary>بيدّي فرصة لطلب «منافس» يكتب قبل كل حفظ متسامح.</summary>
    private sealed class RacingUnitOfWork(IUnitOfWork inner, RackBatchServer server) : IUnitOfWork
    {
        private int _calls;

        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            inner.SaveChangesAsync(ct);

        public async Task<bool> TrySaveChangesAsync(CancellationToken ct = default)
        {
            int call = ++_calls;

            if (server.BeforeTrySave is { } hook) await hook(call);

            bool saved = await inner.TrySaveChangesAsync(ct);

            lock (server.TrySaveResults) server.TrySaveResults.Add(saved);

            return saved;
        }

        public Task<T> InTransactionAsync<T>(
            Func<CancellationToken, Task<T>> work, CancellationToken ct = default) =>
            inner.InTransactionAsync(work, ct);
    }

    /// <summary>اسم القاعدة من نص الاتصال — من غير مكتبة.</summary>
    private readonly record struct SqlConnectionStringParts(string ConnectionString)
    {
        public string Database =>
            ConnectionString.Split(';')
                .Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2
                         && p[0].Trim().Equals("Database", StringComparison.OrdinalIgnoreCase))
                .Select(p => p[1].Trim())
                .Single();
    }
}

/// <summary>
/// <c>POST /api/v2/sync/batch</c> — <b>من خلال الأنبوب كله، على قاعدة
/// حقيقية</b>.
///
/// <para>🔴 <b>أخطر نقطة في النظام.</b> الراكة بتقفل صف طابورها
/// (وبتمسحه) لو الرد <c>2xx</c> ومالقتش صفّها في <c>results</c> بحالة
/// <c>Rejected</c>. فكل فحص هنا بيبص على اللي الراكة بتشوفه: البايتات
/// اللي على السلك.</para>
/// </summary>
public class RackBatchTests(RackBatchServer server) : IClassFixture<RackBatchServer>
{
    private const string Version = "2.3.0.0";

    private readonly HttpClient _client = server.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // =================================================================
    //  المساعدات — بنفس شكل الراكة
    // =================================================================

    private sealed record WireItem(Guid OutboxId, object Body);

    private static string Sha(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// <summary>صف زي اللي <c>SyncWorker.SendAsync</c> بيبعته بالظبط.</summary>
    private static WireItem Item(
        string type, object? payload = null, string? raw = null, string? hash = null,
        long sequence = 0)
    {
        var outboxId = Guid.NewGuid();
        string body = raw ?? JsonSerializer.Serialize(payload);

        return new WireItem(outboxId, new
        {
            outboxId,
            entityType = type,
            entityId = "e-" + outboxId.ToString("N")[..6],
            sequenceNumber = sequence,
            occurredAtUtc = DateTime.UtcNow,
            payloadVersion = 1,
            payloadHash = hash ?? Sha(body),
            payload = body,
        });
    }

    private static object Batch(Guid batchId, params WireItem[] items) => new
    {
        batchId,
        rackCode = "R-BATCH",
        items = items.Select(i => i.Body).ToArray(),
    };

    private static DeviceSyncPayload Device(Guid id) => new()
    {
        Id = id,
        FirstSeenAtUtc = DateTime.UtcNow.AddHours(-1),
        LastSeenAtUtc = DateTime.UtcNow,
    };

    private static LaptopReportPayload Report(Guid deviceId) => new()
    {
        Id = Guid.NewGuid(),
        StartedAtUtc = DateTime.UtcNow.AddMinutes(-20),
        EndedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        DeviceId = deviceId,
    };

    private async Task<HttpResponseMessage> PostAsync(
        object batch, string? version = Version, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/sync/batch");

        request.Headers.TryAddWithoutValidation(RackKey.Header, key ?? server.Key);

        if (version is not null)
            request.Headers.TryAddWithoutValidation("X-Client-Version", version);

        request.Content = new StringContent(
            JsonSerializer.Serialize(batch), Encoding.UTF8, "application/json");

        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"{(int)response.StatusCode}: {body}");

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static JsonElement ResultFor(JsonElement root, Guid outboxId) =>
        root.GetProperty("results").EnumerateArray()
            .Single(r => Guid.Parse(r.GetProperty("outboxId").GetString()!) == outboxId);

    private static string StatusOf(JsonElement root, Guid outboxId) =>
        ResultFor(root, outboxId).GetProperty("status").GetString()!;

    private static JsonElement ErrorOf(JsonElement root, Guid outboxId) =>
        ResultFor(root, outboxId).GetProperty("error");

    // =================================================================
    //  ١ · العقد على السلك
    // =================================================================

    /// <summary>
    /// 🔴 <b>كل <c>outboxId</c> وصل بيرجع — مرة واحدة، مهما حصل له.</b>
    /// الغياب بيتقري نجاح عند الراكة وبيمسح الصف.
    /// </summary>
    [Fact]
    public async Task Every_outbox_id_comes_back_exactly_once()
    {
        var deviceId = Guid.NewGuid();

        var items = new[]
        {
            Item("device", Device(deviceId)),
            Item("report", Report(deviceId)),
            Item("device", raw: "{ broken"),
            Item("banana", new { x = 1 }),
            Item("report", Report(Guid.NewGuid())),
            Item("workitem", raw: "null"),
        };

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), items)));

        var returned = root.GetProperty("results").EnumerateArray()
            .Select(r => Guid.Parse(r.GetProperty("outboxId").GetString()!))
            .ToList();

        Assert.Equal(items.Length, returned.Count);
        Assert.Equal(
            items.Select(i => i.OutboxId).OrderBy(g => g),
            returned.OrderBy(g => g));
    }

    /// <summary>
    /// 🔴 <b>الشكل المجمّد بالحرف — الأسماء وترتيبها.</b> الراكة بتقرا
    /// بـ<c>JsonDocument.TryGetProperty</c> وهي حسّاسة لحالة الحروف.
    /// </summary>
    [Fact]
    public async Task The_reply_has_the_frozen_camelCase_shape()
    {
        var bad = Item("banana", new { x = 1 });

        using var response = await PostAsync(Batch(Guid.NewGuid(), bad));

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var root = await ReadAsync(response);

        Assert.Equal(
            ["batchId", "receivedAtUtc", "serverTimeUtc", "results", "summary"],
            root.EnumerateObject().Select(p => p.Name));

        var result = root.GetProperty("results")[0];

        Assert.Equal(
            ["outboxId", "entityId", "status", "hashMatch", "error"],
            result.EnumerateObject().Select(p => p.Name));

        Assert.Equal(
            ["code", "message", "retryable"],
            result.GetProperty("error").EnumerateObject().Select(p => p.Name));

        Assert.Equal(
            ["applied", "unchanged", "rejected", "reportsApplied"],
            root.GetProperty("summary").EnumerateObject().Select(p => p.Name));
    }

    /// <summary>
    /// ⚠️ <b>والصف المقبول عنده <c>"error": null</c> صريح</b> — زي القديم
    /// بالحرف، مش خانة ناقصة.
    /// </summary>
    [Fact]
    public async Task An_applied_row_carries_an_explicit_null_error()
    {
        var device = Item("device", Device(Guid.NewGuid()));

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), device)));

        var result = ResultFor(root, device.OutboxId);

        Assert.Equal("Applied", result.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("error").ValueKind);
        Assert.True(result.GetProperty("hashMatch").GetBoolean());
    }

    // =================================================================
    //  ٢ · الترتيب والأجهزة والفحوص
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفحص قبل جهازه في المصفوفة — والاتنين بيعدّوا.</b> السيرفر
    /// بيطبّق الأجهزة الأول مهما كان الترتيب.
    /// </summary>
    [Fact]
    public async Task A_report_listed_before_its_device_still_finds_it()
    {
        var deviceId = Guid.NewGuid();
        var reportDto = Report(deviceId);

        var report = Item("report", reportDto);
        var device = Item("device", Device(deviceId));

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), report, device)));

        Assert.Equal("Applied", StatusOf(root, report.OutboxId));
        Assert.Equal("Applied", StatusOf(root, device.OutboxId));

        var summary = root.GetProperty("summary");

        Assert.Equal(2, summary.GetProperty("applied").GetInt32());
        Assert.Equal(1, summary.GetProperty("reportsApplied").GetInt32());

        using var db = server.CreateDb();

        var stored = await db.Reports.SingleAsync(r => r.Id == reportDto.Id);

        Assert.Equal(deviceId, stored.DeviceId);
        Assert.Equal(server.RackId, stored.SourceRackId);
    }

    /// <summary>
    /// 🔴 <b>فحص جهازه ماوصلش = رفض <u>مؤقت</u>.</b> الفحص سليم — الرفض
    /// النهائي كان هيقفله من غير جهاز للأبد.
    /// </summary>
    [Fact]
    public async Task A_report_whose_device_never_arrived_is_a_retryable_rejection()
    {
        var report = Item("report", Report(Guid.NewGuid()));

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), report)));

        Assert.Equal("Rejected", StatusOf(root, report.OutboxId));

        var error = ErrorOf(root, report.OutboxId);

        Assert.Equal(SyncBatchCodes.DeviceNotSynced, error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("retryable").GetBoolean());

        Assert.Equal(0, root.GetProperty("summary").GetProperty("reportsApplied").GetInt32());
    }

    /// <summary>
    /// 🔴 <b>«دخل الاستقبال» مش «اتقبل».</b> فحص بيدّعي فني من ورشة
    /// تانية بيترفض جوّه الاستقبال — والرفض ده لازم يوصل للصف بالاسم،
    /// وإلا الراكة كانت هتمسحه كأنه اتقبل.
    /// </summary>
    [Fact]
    public async Task A_report_rejected_inside_ingest_is_rejected_on_its_row()
    {
        Guid strangerId;

        using (var db = server.CreateDb())
        {
            var other = new Tenant { Name = "ورشة تانية" };
            db.Tenants.Add(other);

            var stranger = new Technician
            {
                TenantId = other.Id,
                Code = "777001",
                DisplayName = "غريب",
                Username = "stranger" + Guid.NewGuid().ToString("N")[..6],
                NormalizedUsername = "stranger" + Guid.NewGuid().ToString("N")[..6],
            };

            db.Technicians.Add(stranger);
            await db.SaveChangesAsync();

            strangerId = stranger.Id;
        }

        var deviceId = Guid.NewGuid();

        var dto = Report(deviceId);
        dto.TechnicianId = strangerId.ToString();

        var device = Item("device", Device(deviceId));
        var report = Item("report", dto);

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), device, report)));

        Assert.Equal("Applied", StatusOf(root, device.OutboxId));
        Assert.Equal("Rejected", StatusOf(root, report.OutboxId));

        var error = ErrorOf(root, report.OutboxId);

        Assert.Equal("TechnicianTenantMismatch", error.GetProperty("code").GetString());
        Assert.False(error.GetProperty("retryable").GetBoolean());

        Assert.Equal(1, root.GetProperty("summary").GetProperty("rejected").GetInt32());
    }

    /// <summary>⚠️ نفس الجهاز مرتين = «زي ما هو» في المرة التانية.</summary>
    [Fact]
    public async Task Resending_the_same_device_in_a_new_batch_is_unchanged()
    {
        var dto = Device(Guid.NewGuid());

        await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), Item("device", dto))));

        var again = Item("device", dto);

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), again)));

        Assert.Equal("Unchanged", StatusOf(root, again.OutboxId));
        Assert.Equal(1, root.GetProperty("summary").GetProperty("unchanged").GetInt32());
    }

    // =================================================================
    //  ٣ · الرفض
    // =================================================================

    /// <summary>
    /// 🔴 <b>صف فاسد واحد مابيوقفش الطابور.</b> بيترفض لوحده نهائي،
    /// والسليم اللي معاه بيتطبّق.
    /// </summary>
    [Fact]
    public async Task A_broken_payload_is_final_and_does_not_stop_the_rest()
    {
        var broken = Item("device", raw: "{ not json");
        var fine = Item("device", Device(Guid.NewGuid()));

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), broken, fine)));

        var error = ErrorOf(root, broken.OutboxId);

        Assert.Equal(SyncBatchCodes.InvalidPayload, error.GetProperty("code").GetString());
        Assert.False(error.GetProperty("retryable").GetBoolean());

        Assert.Equal("Applied", StatusOf(root, fine.OutboxId));
    }

    /// <summary>
    /// ⚠️ <b>نوع مش معروف خالص = حمولة غلط، نهائي.</b> وبيتفرّق عن
    /// «السيرفر قديم» بالكود.
    /// </summary>
    [Fact]
    public async Task An_unknown_type_is_rejected_as_unknown_for_good()
    {
        var odd = Item("banana", new { x = 1 });

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), odd)));

        var error = ErrorOf(root, odd.OutboxId);

        Assert.Equal(SyncBatchCodes.UnknownEntityType, error.GetProperty("code").GetString());
        Assert.False(error.GetProperty("retryable").GetBoolean());
    }

    [Theory]
    [InlineData("device")]
    [InlineData("report")]
    [InlineData("workitem")]
    [InlineData("workflow")]
    public async Task A_literal_null_payload_is_missing_fields(string type)
    {
        var item = Item(type, raw: "null");

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), item)));

        Assert.Equal(
            SyncBatchCodes.MissingFields,
            ErrorOf(root, item.OutboxId).GetProperty("code").GetString());
    }

    /// <summary>
    /// ⚠️ <b>الهاش المختلف مش رفض</b> — الحمولة سليمة؛ الاختلاف بيتقال
    /// في <c>hashMatch</c> وبيتسجّل.
    /// </summary>
    [Fact]
    public async Task A_wrong_hash_still_applies_but_says_so()
    {
        var device = Item("device", Device(Guid.NewGuid()), hash: new string('0', 64));

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), device)));

        var result = ResultFor(root, device.OutboxId);

        Assert.Equal("Applied", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("hashMatch").GetBoolean());
    }

    // =================================================================
    //  ٤ · الشغل التشغيلي
    // =================================================================

    /// <summary>
    /// 🔴 <b>جهاز وأمر صيانة وحركة في دفعة واحدة — الكل بيعدّي.</b>
    /// والأمر بياخد رقمه من السيرفر.
    /// </summary>
    [Fact]
    public async Task A_device_its_work_item_and_a_move_all_apply_in_one_batch()
    {
        var deviceId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var move = Item("workflow", new DeviceWorkflowEventSyncPayload
        {
            EventId = eventId,
            DeviceId = deviceId,
            EventType = (int)DeviceWorkflowEventType.StageChanged,
            ToStage = (int)DeviceOperationalStage.NeedsRepair,
            RepairWorkItemId = workItemId,
            OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5),
        });

        var order = Item("workitem", new RepairWorkItemSyncPayload
        {
            Id = workItemId,
            DeviceId = deviceId,
            Status = (int)RepairStatus.WaitingForRepair,
            OpenedByName = "كريم",
            OpenedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            FaultSummary = "الشاشة بتطفى",
        });

        var device = Item("device", Device(deviceId));

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), move, order, device)));

        Assert.Equal("Applied", StatusOf(root, device.OutboxId));
        Assert.Equal("Applied", StatusOf(root, order.OutboxId));
        Assert.Equal("Applied", StatusOf(root, move.OutboxId));

        using var db = server.CreateDb();

        var stored = await db.RepairWorkItems.SingleAsync(w => w.Id == workItemId);

        Assert.StartsWith("RP-", stored.PublicCode);
        Assert.Equal(deviceId, stored.DeviceId);

        Assert.True(await db.DeviceWorkflowEvents.AnyAsync(e => e.EventId == eventId));
    }

    /// <summary>
    /// 🔴 <b>أمر لجهاز لسه ماوصلش = مؤقت.</b> الرفض النهائي هنا كان
    /// شغل صيانة بيضيع لأن ترتيب دفعة اتكسر.
    /// </summary>
    [Fact]
    public async Task A_work_item_for_a_device_not_yet_synced_is_retryable()
    {
        var order = Item("workitem", new RepairWorkItemSyncPayload
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Status = (int)RepairStatus.WaitingForRepair,
            OpenedAtUtc = DateTime.UtcNow,
        });

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), order)));

        var error = ErrorOf(root, order.OutboxId);

        Assert.Equal("DeviceNotSynced", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("retryable").GetBoolean());
    }

    [Fact]
    public async Task A_broken_operational_payload_is_invalid_for_good()
    {
        var order = Item("workitem", raw: "[1, 2");

        var root = await ReadAsync(await PostAsync(Batch(Guid.NewGuid(), order)));

        var error = ErrorOf(root, order.OutboxId);

        Assert.Equal(SyncBatchCodes.InvalidPayload, error.GetProperty("code").GetString());
        Assert.False(error.GetProperty("retryable").GetBoolean());
    }

    // =================================================================
    //  ٥ · عدم التكرار
    // =================================================================

    /// <summary>
    /// 🔴 <b>إعادة نفس الدفعة = نفس الرد بايت ببايت</b>، ومعاه ترويسة
    /// الإعادة — والمحطة مابتتعدّش مرتين.
    /// </summary>
    [Fact]
    public async Task A_resent_batch_gets_the_stored_reply_byte_for_byte()
    {
        var batchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var batch = Batch(batchId, Item("device", Device(deviceId)), Item("report", Report(deviceId)));

        using var first = await PostAsync(batch);
        string firstBody = await first.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.False(first.Headers.Contains("Idempotent-Replay"));

        int countedAfterFirst = await ReportsReceivedAsync();

        using var second = await PostAsync(batch);
        string secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstBody, secondBody);
        Assert.Equal("true", Assert.Single(second.Headers.GetValues("Idempotent-Replay")));

        Assert.Equal(countedAfterFirst, await ReportsReceivedAsync());

        using var db = server.CreateDb();

        Assert.Equal(1, await db.SyncBatches.CountAsync(b => b.BatchId == batchId));
    }

    /// <summary>
    /// 🔴 <b>رد قديم متخزّن PascalCase بيخرج camelCase.</b> ده العيب
    /// اللي كان بيخلّي كل إعادة تبان للراكة فاضية — والصف المرفوض
    /// يتقفل ويضيع.
    /// </summary>
    [Fact]
    public async Task A_legacy_PascalCase_stored_reply_goes_out_camelCase()
    {
        var batchId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();

        string legacy =
            "{\"BatchId\":\"" + batchId + "\",\"ReceivedAtUtc\":\"2026-09-01T10:00:00Z\"," +
            "\"ServerTimeUtc\":\"2026-09-01T10:00:00Z\",\"Results\":[{\"OutboxId\":\"" + outboxId +
            "\",\"EntityId\":\"x\",\"Status\":\"Rejected\",\"HashMatch\":true,\"Error\":" +
            "{\"Code\":\"DeviceNotSynced\",\"Message\":\"m\",\"Retryable\":true}}]," +
            "\"Summary\":{\"Applied\":0,\"Unchanged\":0,\"Rejected\":1,\"ReportsApplied\":0}}";

        await StoreReplyAsync(batchId, legacy);

        var root = await ReadAsync(await PostAsync(Batch(batchId, Item("banana", new { x = 1 }))));

        Assert.Equal("Rejected", StatusOf(root, outboxId));
        Assert.True(ErrorOf(root, outboxId).GetProperty("retryable").GetBoolean());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("rejected").GetInt32());
    }

    /// <summary>
    /// 🔴 <b>رد متخزّن مش مقروء = ٥٠٠، ومفيش حاجة بتتطبّق.</b> لو رجّع
    /// «مفيش دفعة قبل كده»، سجل التكرار كان بيتحوّل لشغل جديد.
    /// </summary>
    [Fact]
    public async Task An_unreadable_stored_reply_fails_closed()
    {
        var batchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        await StoreReplyAsync(batchId, "this is not json");

        using var response = await PostAsync(Batch(batchId, Item("device", Device(deviceId))));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var db = server.CreateDb();

        Assert.False(await db.Devices.AnyAsync(d => d.Id == deviceId));
    }

    /// <summary>⚠️ دفعة من غير معرّف بتتطبّق — بس مالهاش سجل تكرار.</summary>
    [Fact]
    public async Task A_batch_without_an_id_is_applied_but_not_recorded()
    {
        var device = Item("device", Device(Guid.NewGuid()));

        using (var db = server.CreateDb())
            Assert.Equal(0, await db.SyncBatches.CountAsync(b => b.BatchId == Guid.Empty));

        var root = await ReadAsync(await PostAsync(Batch(Guid.Empty, device)));

        Assert.Equal("Applied", StatusOf(root, device.OutboxId));

        using var after = server.CreateDb();

        Assert.Equal(0, await after.SyncBatches.CountAsync(b => b.BatchId == Guid.Empty));
    }

    // =================================================================
    //  ٦ · البوابات
    // =================================================================

    /// <summary>
    /// ⚠️ <b>نسخة ناقصة أو قديمة = <c>426</c> برسالة.</b> والراكة
    /// بتوقف الصف للمراجعة ومابتمسحوش.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0.9.0")]
    [InlineData("not-a-version")]
    public async Task A_missing_or_old_client_version_is_426(string? version)
    {
        var deviceId = Guid.NewGuid();

        using var response = await PostAsync(
            Batch(Guid.NewGuid(), Item("device", Device(deviceId))), version);

        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("ClientTooOld", body.GetProperty("code").GetString());
        Assert.Equal("1.0.0", body.GetProperty("minVersion").GetString());

        using var db = server.CreateDb();

        Assert.False(await db.Devices.AnyAsync(d => d.Id == deviceId));
    }

    [Fact]
    public async Task A_body_over_the_limit_is_413()
    {
        var huge = Item("device", raw: new string('a', (int)Core.Sync.SyncLimits.MaxBodyBytes));

        using var response = await PostAsync(Batch(Guid.NewGuid(), huge));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Theory]
    [InlineData("{ broken")]
    [InlineData("null")]
    [InlineData("")]
    public async Task A_malformed_body_is_400_with_a_message(string raw)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/sync/batch")
        {
            Content = new StringContent(raw, Encoding.UTF8, "application/json"),
        };

        request.Headers.TryAddWithoutValidation(RackKey.Header, server.Key);
        request.Headers.TryAddWithoutValidation("X-Client-Version", Version);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("error").GetString()));
    }

    /// <summary>
    /// ⚠️ <b>المحطة بتتلمس مرة: آخر ظهور، نسخة البرنامج، وعدّاد
    /// <u>الفحوص</u> بس</b> — الجهاز اللي معاهم مابيتعدّش.
    /// </summary>
    [Fact]
    public async Task The_rack_is_touched_with_its_version_and_the_reports_only()
    {
        int before = await ReportsReceivedAsync();
        var deviceId = Guid.NewGuid();

        await ReadAsync(await PostAsync(
            Batch(Guid.NewGuid(), Item("device", Device(deviceId)), Item("report", Report(deviceId))),
            version: "2.7.1.0"));

        using var db = server.CreateDb();

        var rack = await db.Racks.SingleAsync(r => r.Id == server.RackId);

        Assert.Equal(before + 1, rack.ReportsReceived);
        Assert.Equal("2.7.1.0", rack.AppVersion);
        Assert.NotNull(rack.LastSeenAtUtc);
        Assert.InRange(rack.LastSeenAtUtc!.Value, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    // =================================================================
    //  ٧ · السباق
    // =================================================================

    /// <summary>
    /// 🔴 <b>طلب منافس كتب الجهاز <u>وسجّل الدفعة</u> قبلنا — رده هو
    /// المرجع.</b> من غير كده كان ٥٠٠ على شغل سليم.
    /// </summary>
    [Fact]
    public async Task A_rival_that_wrote_the_device_and_the_reply_wins()
    {
        var batchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        int before = await ReportsReceivedAsync();

        server.BeforeTrySave = async call =>
        {
            if (call != 1) return;

            using var rival = server.CreateDb();

            rival.Devices.Add(new Device { Id = deviceId, TenantId = server.TenantId });

            rival.SyncBatches.Add(new SyncBatch
            {
                TenantId = server.TenantId,
                RackId = server.RackId,
                BatchId = batchId,
                ResponseJson = Reply(batchId, applied: 42),
            });

            await rival.SaveChangesAsync();
        };

        try
        {
            using var response = await PostAsync(Batch(batchId, Item("device", Device(deviceId))));

            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Idempotent-Replay")));

            var root = await ReadAsync(response);

            Assert.Equal(42, root.GetProperty("summary").GetProperty("applied").GetInt32());
            Assert.Equal(before, await ReportsReceivedAsync());
        }
        finally
        {
            server.BeforeTrySave = null;
        }
    }

    /// <summary>
    /// 🔴 <b>منافس كتب الجهاز بس — إعادة التطبيق بتمشي في مسار
    /// التحديث وبتعدّي.</b>
    /// </summary>
    [Fact]
    public async Task A_rival_that_wrote_only_the_device_is_absorbed()
    {
        var batchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        server.BeforeTrySave = async call =>
        {
            if (call != 1) return;

            using var rival = server.CreateDb();

            rival.Devices.Add(new Device { Id = deviceId, TenantId = server.TenantId });

            await rival.SaveChangesAsync();
        };

        server.TrySaveResults.Clear();

        try
        {
            var device = Item("device", Device(deviceId));

            var root = await ReadAsync(await PostAsync(Batch(batchId, device)));

            // ⚠️ السباق حصل فعلاً: أول حفظ وقع، والتسجيل بعد الإعادة عدّى.
            Assert.Equal([false, true], server.TrySaveResults);

            Assert.NotEqual("Rejected", StatusOf(root, device.OutboxId));

            using var db = server.CreateDb();

            Assert.Equal(1, await db.Devices.CountAsync(d => d.Id == deviceId));
            Assert.Equal(1, await db.SyncBatches.CountAsync(b => b.BatchId == batchId));
        }
        finally
        {
            server.BeforeTrySave = null;
        }
    }

    /// <summary>
    /// 🔴 <b>منافس سجّل الدفعة قبلنا بلحظة — رده هو اللي بيرجع.</b>
    /// نفس مسار الإعادة بالظبط، فنفس الإعادة مابتطلعش بشكلين حسب
    /// التوقيت.
    /// </summary>
    [Fact]
    public async Task A_rival_that_recorded_the_batch_first_wins()
    {
        var batchId = Guid.NewGuid();

        server.BeforeTrySave = async call =>
        {
            using var rival = server.CreateDb();

            rival.SyncBatches.Add(new SyncBatch
            {
                TenantId = server.TenantId,
                RackId = server.RackId,
                BatchId = batchId,
                ResponseJson = Reply(batchId, applied: 77),
            });

            await rival.SaveChangesAsync();
        };

        try
        {
            using var response = await PostAsync(Batch(batchId, Item("banana", new { x = 1 })));

            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Idempotent-Replay")));

            var root = await ReadAsync(response);

            Assert.Equal(77, root.GetProperty("summary").GetProperty("applied").GetInt32());
        }
        finally
        {
            server.BeforeTrySave = null;
        }
    }

    // =================================================================
    //  مساعدات القاعدة
    // =================================================================

    private async Task<int> ReportsReceivedAsync()
    {
        using var db = server.CreateDb();

        return await db.Racks
            .Where(r => r.Id == server.RackId)
            .Select(r => r.ReportsReceived)
            .SingleAsync();
    }

    private async Task StoreReplyAsync(Guid batchId, string json)
    {
        using var db = server.CreateDb();

        db.SyncBatches.Add(new SyncBatch
        {
            TenantId = server.TenantId,
            RackId = server.RackId,
            BatchId = batchId,
            ResponseJson = json,
        });

        await db.SaveChangesAsync();
    }

    private static string Reply(Guid batchId, int applied) =>
        JsonSerializer.Serialize(new SyncBatchResponse
        {
            BatchId = batchId,
            ReceivedAtUtc = DateTime.UtcNow,
            ServerTimeUtc = DateTime.UtcNow,
            Summary = new SyncBatchSummary { Applied = applied },
        }, RackWire.Wire);
}
