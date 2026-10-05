extern alias newapi;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodlekWeb.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewDb = Codlek.Infrastructure.Data.AppDbContext;

namespace CodlekWeb.Tests;

/// <summary>
/// PARITY server: the legacy suite's <c>TestServer</c>, same public
/// surface, but every HTTP request goes to the <b>new</b> API.
///
/// <para>🔴 <b>The database is prepared the way production will be on
/// cutover day:</b></para>
/// <list type="number">
/// <item>the <b>legacy</b> app boots once on an empty database — its own
/// migrations, seeder and backfills, exactly as in production today;</item>
/// <item>the cutover steps run: the new baseline migration is marked as
/// applied (the legacy schema already has those tables), then the new
/// migrations apply — refresh tokens, Identity tables, timestamps, and
/// the legacy user import;</item>
/// <item>the new API boots on that database and serves every request.</item>
/// </list>
///
/// <para>⚠️ <b><see cref="Services"/> is the legacy container.</b> 23
/// legacy files resolve legacy services (RepairService, TechnicianService…)
/// to set up state. They keep working as a seeding tool on the shared
/// database, while the assertions read the new API over HTTP.</para>
/// </summary>
public class TestServer : WebApplicationFactory<newapi::Program>, IAsyncLifetime
{
    public const string DatabaseName = "codlek_parity";
    public const string PublicBaseUrl = "https://cloud.test.codlek";

    /// <summary>The new baseline: the 31 legacy tables, already present.</summary>
    public const string BaselineMigration = "20261004005214_ShapeBaseline";

    private const string Master =
        "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    public static string ConnectionString =>
        $"Server=localhost;Database={DatabaseName};Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    private LegacyServer? _legacy;

    /// <summary>
    /// ⚠️ <b>The legacy container</b> — hides the base property on
    /// purpose. See the class remarks.
    /// </summary>
    public new IServiceProvider Services =>
        (_legacy ?? throw new InvalidOperationException("Legacy server not started")).Services;

    /// <summary>The new API's own container — for the harness only.</summary>
    public IServiceProvider NewServices => base.Services;

    // =================================================================
    //  Configuration
    // =================================================================

    /// <summary>
    /// 🔴 <b>Settings the new API reads at registration time.</b>
    /// <c>UseSetting</c> lands too late for those, so they go in as
    /// environment variables only while the host is being built.
    /// </summary>
    public static IHost WithEnvironment(
        IReadOnlyDictionary<string, string> variables, Func<IHost> build)
    {
        var previous = variables.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);

        try
        {
            foreach (var (key, value) in variables) Environment.SetEnvironmentVariable(key, value);

            return build();
        }
        finally
        {
            foreach (var (key, value) in previous) Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// 🔴 <b>The database is swapped in the container.</b> The new API
    /// resolves its connection string while registering services, and in
    /// Development user secrets point at codlek_dev — a setting from here
    /// would lose to them and the suite would write to the dev database.
    /// </summary>
    public static void UseParityDatabase(IServiceCollection services)
    {
        var doomed = services
            .Where(d => d.ServiceType == typeof(NewDb)
                     || d.ServiceType == typeof(DbContextOptions<NewDb>)
                     || d.ServiceType == typeof(DbContextOptions)
                     || (d.ServiceType.IsGenericType
                         && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                         && d.ServiceType.GenericTypeArguments[0] == typeof(NewDb)))
            .ToList();

        foreach (var d in doomed) services.Remove(d);

        services.AddDbContext<NewDb>(o => o.UseSqlServer(
            ConnectionString,
            sql =>
            {
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                sql.CommandTimeout(60);
            }));

        services.Replace(ServiceDescriptor.Singleton(
            new newapi::Codlek.Api.Racks.CloudAddresses(PublicBaseUrl)));
    }

    protected override IHost CreateHost(IHostBuilder builder) =>
        WithEnvironment(
            new Dictionary<string, string>
            {
                ["Server__PublicBaseUrl"] = PublicBaseUrl,
                ["RateLimits__Enabled"] = "false",
            },
            () => base.CreateHost(builder));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Server:PublicBaseUrl", PublicBaseUrl);
        builder.UseSetting("RateLimits:Enabled", "false");
        builder.ConfigureTestServices(UseParityDatabase);
        builder.ConfigureLogging(logging => logging.AddProvider(new ParityLog()));
    }

    // =================================================================
    //  Lifecycle
    // =================================================================

    async Task IAsyncLifetime.InitializeAsync()
    {
        await using (var connection = new SqlConnection(Master))
        {
            await connection.OpenAsync();

            await using var drop = connection.CreateCommand();

            drop.CommandText = $@"
IF DB_ID('{DatabaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{DatabaseName}];
END;
CREATE DATABASE [{DatabaseName}] COLLATE Arabic_CI_AI;";

            await drop.ExecuteNonQueryAsync();
        }

        // ١ · the legacy app prepares the database, like production today.
        _legacy = new LegacyServer();

        using (var legacyClient = _legacy.CreateClient())
        using (var health = await legacyClient.GetAsync("/api/health"))
            health.EnsureSuccessStatusCode();

        await using (var legacyDb = NewDbContext())
        {
            string onLegacy = _legacy.Services.CreateScope().ServiceProvider
                .GetRequiredService<AppDbContext>().Database.GetDbConnection().Database;

            if (!string.Equals(onLegacy, DatabaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"The legacy app prepared {onLegacy}, not {DatabaseName} — stopping.");
        }

        // ٢ · the cutover steps.
        await CutoverAsync();

        // ٣ · the new API boots on the result.
        using (var client = CreateClient())
        using (var health = await client.GetAsync("/api/health"))
            health.EnsureSuccessStatusCode();

        using var scope = base.Services.CreateScope();

        string onNew = scope.ServiceProvider.GetRequiredService<NewDb>()
            .Database.GetDbConnection().Database;

        if (!string.Equals(onNew, DatabaseName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"The new API is on {onNew}, not {DatabaseName} — stopping.");
    }

    /// <summary>
    /// 🔴 <b>The cutover runbook, as code.</b> Mark the baseline (its
    /// tables exist), then let EF apply only what is genuinely new.
    /// </summary>
    public static async Task CutoverAsync()
    {
        await using var db = new NewDb(new DbContextOptionsBuilder<NewDb>()
            .UseSqlServer(ConnectionString)
            .Options);

        await db.Database.ExecuteSqlRawAsync(BaselineMarkerSql);

        /*
          🔴 **The very script the host will run — not MigrateAsync.**

          On cutover day the owner runs a SQL script on the hosted
          database. This generates the same idempotent script
          (`dotnet ef migrations script <baseline> --idempotent`) and runs
          it batch by batch, so the parity suite rehearses the artifact,
          not a different code path that happens to end in the same place.
        */
        string script = db.GetService<IMigrator>().GenerateScript(
            fromMigration: BaselineMigration,
            toMigration: null,
            options: MigrationsSqlGenerationOptions.Idempotent);

        foreach (string batch in SplitBatches(script))
            await db.Database.ExecuteSqlRawAsync(batch);

        var pending = await db.Database.GetPendingMigrationsAsync();

        if (pending.Any())
            throw new InvalidOperationException(
                "The cutover script left migrations pending: " + string.Join(", ", pending));
    }

    /// <summary>Step 1 of the runbook: the legacy schema already has the baseline's tables.</summary>
    public const string BaselineMarkerSql = $"""
        IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '{BaselineMigration}')
            INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES ('{BaselineMigration}', '9.0.0');
        """;

    private static IEnumerable<string> SplitBatches(string script) =>
        Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(b => b.Trim().TrimStart('﻿'))
            .Where(b => b.Length > 0);

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_legacy is not null) await _legacy.DisposeAsync();

        await base.DisposeAsync();
    }

    // =================================================================
    //  The legacy surface the tests use
    // =================================================================

    public AppDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Same contract as the legacy one — a ready <c>HttpClient</c> for
    /// that account — but through the new API's token login.
    ///
    /// <para>⚠️ <b>The import runs first.</b> Legacy tests add accounts
    /// straight into <c>Users</c> on the fly; the import statement is the
    /// cutover migration's own, and it only adds what is missing.</para>
    /// </summary>
    public async Task<HttpClient> SignInAsync(string username, string password)
    {
        await using (var db = new NewDb(new DbContextOptionsBuilder<NewDb>()
                         .UseSqlServer(ConnectionString).Options))
        {
            await db.Database.ExecuteSqlRawAsync(
                Codlek.Infrastructure.Migrations.ImportLegacyUsers.Sql);
        }

        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username, password });

        string body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"الدخول بـ {username} ما نجحش: {(int)response.StatusCode}. " +
                body[..Math.Min(400, body.Length)]);

        string token = JsonDocument.Parse(body).RootElement.GetProperty("accessToken").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static readonly Regex TokenPattern = new(
        """name="__RequestVerificationToken"[^>]*value="([^"]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// Kept for the files that call it. The new API has no Razor pages,
    /// so this throws for them — and those tests are listed as
    /// not-applicable, not as gaps.
    /// </summary>
    public static async Task<string> AntiforgeryTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        string html = await response.Content.ReadAsStringAsync();
        var match = TokenPattern.Match(html);

        if (!match.Success)
            throw new InvalidOperationException($"مفيش توكن حماية في {path}");

        return match.Groups[1].Value;
    }

    /// <summary>The legacy app — boots once to prepare the database.</summary>
    private sealed class LegacyServer : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:SqlServer", ConnectionString);
            builder.UseSetting(CodlekWeb.Services.PublicBaseUrl.Key, PublicBaseUrl);
            builder.UseSetting("RateLimits:Enabled", "false");
            builder.UseEnvironment("Development");
        }
    }
}

[CollectionDefinition(Name)]
public class TestServerCollection : ICollectionFixture<TestServer>
{
    public const string Name = "codlek-web";
}
