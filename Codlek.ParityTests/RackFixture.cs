extern alias newapi;
// PARITY COPY of CodlekWeb.Tests/RackFixture.cs - one change only: the client helper takes the new API's factory.
// Every assertion is the legacy one, byte for byte.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodlekWeb.Data;
using CodlekWeb.Models;
using CodlekWeb.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CodlekWeb.Tests;

/// <summary>بيانات اعتماد راكة اتعملت للاختبار — المفتاح الخام محفوظ في الذاكرة بس.</summary>
public sealed record RackCredentials(Guid RackId, string RackCode, string ApiKey);

/// <summary>
/// راكات للاختبار، بمفاتيح معروفة.
///
/// <para><b>ليه ملف مستقل عن <see cref="Fixture"/>.</b> المثبّت الموجود
/// بيجهّز مستخدمين وكوكيز — وده مجال هوية تاني خالص. الراكة بتتعرّف
/// بـ <c>X-Api-Key</c> ومالهاش كوكي ولا صلاحية ولا ادعاءات. خلطهم في
/// ملف واحد بيوحي إن الاتنين نفس النظام، وهما مش كده.</para>
///
/// <para>⚠️ المفتاح الخام عمره ما بيتخزّن في القاعدة — بيتولّد مرة
/// واحدة وبيتحفظ في الذاكرة للتشغيلة دي بس، بالظبط زي الإنتاج
/// (<c>RackService</c> بيرجّعه مرة واحدة وبعدين يختفي).</para>
/// </summary>
public static class RackFixture
{
    /// <summary>أقل نسخة عميل مقبولة — نفس <c>ClientVersions.Minimum</c>.</summary>
    public const string SupportedClientVersion = "1.0.0";

    private static readonly Dictionary<string, RackCredentials> Cache = new();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// بيرجّع راكة بالكود ده، وبيعملها لو لسه ماتعملتش.
    ///
    /// <para>⚠️ الشركة بتتحدد <b>جوّه</b> الدالة بعد ما المثبّت يجهّز.
    /// لو الـ TenantId اتقرا في مكان النداء، أول اختبار يشتغل في
    /// العملية كان بياخد <c>Guid.Empty</c> — المثبّت لسه ماشتغلش.</para>
    /// </summary>
    public static async Task<RackCredentials> EnsureAsync(
        TestServer server, string rackCode,
        RackStatus status = RackStatus.Active)
    {
        await Fixture.EnsureAsync(server);

        Guid tenantId = Fixture.TenantAId;

        await Gate.WaitAsync();

        try
        {
            if (Cache.TryGetValue(rackCode, out var known)) return known;

            await using var db = server.NewDbContext();

            string key = PasswordHasher.NewApiKey();
            var (hash, salt) = PasswordHasher.Create(key);

            var rack = new Rack
            {
                TenantId = tenantId,
                RackCode = rackCode,
                Name = "محطة اختبار " + rackCode,
                ApiKeyHash = hash,
                Salt = salt,

                // ⚠️ نفس التقطيع اللي المصادقة بتعمله: أول ١٠ حروف.
                KeyPrefix = key[..10],

                Status = status,
                InstallationId = Guid.NewGuid().ToString("N"),
                RegisteredAtUtc = DateTime.UtcNow,
                KeyIssuedAtUtc = DateTime.UtcNow
            };

            db.Racks.Add(rack);
            await db.SaveChangesAsync();

            var created = new RackCredentials(rack.Id, rackCode, key);
            Cache[rackCode] = created;

            return created;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>عميل بيتصرّف زي الراكة: مفتاح في الترويسة، ومش بيمشي ورا التحويل.</summary>
    /// <remarks>
    /// ⚠️ النوع <c>WebApplicationFactory&lt;Program&gt;</c> مش
    /// <c>TestServer</c>: فيه سيرفر تاني للاختبارات بحدود معدّل
    /// صغيرة (<c>ThrottledServer</c>)، ولازم يستعمل نفس العميل
    /// بالظبط — عميل تاني معناه إن اللي بيتقاس مش نفس السلوك.
    /// </remarks>
    public static HttpClient Client(
        WebApplicationFactory<newapi::Program> server, string? apiKey,
        string? clientVersion = SupportedClientVersion)
    {
        // ⚠️ AllowAutoRedirect = false مقصود. الراكة الحقيقية بتمشي ورا
        // التحويل (HttpClient افتراضي)، وده بالظبط اللي بيخلي تحويل
        // لصفحة دخول يبان للراكة نجاح. هنا بنقفله عشان الاختبار يشوف
        // الرد الحقيقي بدل ما يتبعه.
        var client = server.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        if (apiKey != null) client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        if (clientVersion != null) client.DefaultRequestHeaders.Add("X-Client-Version", clientVersion);

        return client;
    }

    // =================================================================
    //  حمولات
    // =================================================================

    /// <summary>
    /// نفس التسلسل اللي الراكة بتستخدمه: أسماء الخصائص زي ما هي
    /// (PascalCase)، من غير سياسة تسمية.
    ///
    /// <para><c>CanonicalPayload</c> في مشروع الراكة بيسلسل بالافتراضي،
    /// والسيرفر بيقراه بـ <c>PropertyNameCaseInsensitive</c>. الاختبار
    /// لازم يبعت زي الراكة بالظبط عشان يكون بيقيس الحقيقة.</para>
    /// </summary>
    private static readonly JsonSerializerOptions RackPayload = new();

    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static SyncItemRequest DeviceItem(
        Guid deviceId, string publicCode, Guid? outboxId = null, string? badHash = null)
    {
        var dto = new DeviceSyncDto
        {
            Id = deviceId,
            PublicCode = publicCode,
            CodeState = 2,
            Confidence = 1,
            IdentityBasis = "BiosSerial",
            Status = 0,
            FirstSeenAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            LastSeenAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            LastKnownManufacturer = "LENOVO",
            LastKnownModel = "RackContract"
        };

        string payload = JsonSerializer.Serialize(dto, RackPayload);

        return new SyncItemRequest
        {
            OutboxId = outboxId ?? Guid.NewGuid(),
            EntityType = "device",
            EntityId = deviceId.ToString(),
            SequenceNumber = 1,
            OccurredAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            PayloadVersion = 1,
            Payload = payload,
            PayloadHash = badHash ?? Sha256(payload)
        };
    }

    /// <summary>الاسم والكود الافتراضيين للفحوصات اللي مش بتقيس هوية الفني.</summary>
    public const string DefaultTechnicianName = "فني اختبار";
    public const string DefaultTechnicianCode = "910001";

    /// <summary>
    /// فحص مربوط بجهاز — لو الجهاز مش موجود بيترفض بـ DeviceNotSynced.
    /// </summary>
    /// <param name="technicianId">
    /// هوية الفني المركزية على السلك. <c>null</c> = زي الراكة القديمة
    /// اللي مابتبعتش الحقل ده (نص فاضي) — وده اللي بيخلي اختبارات
    /// التوافق تقيس الحقيقة.
    /// </param>
    public static SyncItemRequest ReportItem(
        Guid reportId, Guid? deviceId, Guid? outboxId = null, bool breakRequiredFields = false,
        string? technicianId = null,
        string? technicianCode = null,
        string? technicianName = null)
    {
        var dto = new LaptopReportDto
        {
            Id = breakRequiredFields ? Guid.Empty : reportId,
            DeviceId = deviceId,
            TechnicianId = technicianId ?? "",
            TechnicianName = technicianName ?? DefaultTechnicianName,
            TechnicianCode = technicianCode ?? DefaultTechnicianCode,
            StartedAtUtc = breakRequiredFields
                ? default
                : new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
            EndedAtUtc = new DateTime(2026, 9, 1, 9, 20, 0, DateTimeKind.Utc),
            DurationMs = 1_200_000,
            Specs = new DeviceSpecsDto { Manufacturer = "LENOVO", Model = "RackContract" }
        };

        string payload = JsonSerializer.Serialize(dto, RackPayload);

        return new SyncItemRequest
        {
            OutboxId = outboxId ?? Guid.NewGuid(),
            EntityType = "report",
            EntityId = reportId.ToString(),
            SequenceNumber = 2,
            OccurredAtUtc = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
            PayloadVersion = 1,
            Payload = payload,
            PayloadHash = Sha256(payload)
        };
    }

    public static SyncBatchRequest Batch(Guid batchId, string rackCode, params SyncItemRequest[] items) =>
        new() { BatchId = batchId, RackCode = rackCode, Items = items.ToList() };

    /// <summary>بيبعت الدفعة زي الراكة: JSON بأسماء الخصائص زي ما هي.</summary>
    public static async Task<HttpResponseMessage> PostBatchAsync(
        HttpClient client, SyncBatchRequest batch)
    {
        string json = JsonSerializer.Serialize(batch, RackPayload);

        // ⚠️ await جوّه الدالة مقصود. لو رجّعنا الـ Task من غير انتظار،
        // الـ using كان بيرمي المحتوى قبل ما الطلب يتبعت فعلاً.
        using var body = new StringContent(json, Encoding.UTF8, "application/json");

        return await client.PostAsync("/api/v2/sync/batch", body);
    }
}
