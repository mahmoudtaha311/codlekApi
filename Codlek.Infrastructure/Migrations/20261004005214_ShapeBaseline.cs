using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Codlek.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ShapeBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorRackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntityCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceCodeLeases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromNumber = table.Column<int>(type: "int", nullable: false),
                    ToNumber = table.Column<int>(type: "int", nullable: false),
                    ConsumedThrough = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCodeLeases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LaptopBrands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaptopBrands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RackPairingCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CodePrefix = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedByRackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    IntendedName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IntendedLocation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RackPairingCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianLoginAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AttemptedUsername = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianLoginAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantCounters",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CounterName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NextValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantCounters", x => new { x.TenantId, x.CounterName });
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LaptopBrandAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawValue = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NormalizedValue = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaptopBrandAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaptopBrandAliases_LaptopBrands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "LaptopBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Containers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NormalizedCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Containers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Containers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueCatalogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueCatalogItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Racks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RackCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ApiKeyHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InstallationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastMachineIdentifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AppVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KeyIssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ReportsReceived = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Racks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Racks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Technicians",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NormalizedUsername = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CanTest = table.Column<bool>(type: "bit", nullable: false),
                    CanRepair = table.Column<bool>(type: "bit", nullable: false),
                    CapabilityChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SuspendedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SuspendedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    CredentialVersion = table.Column<int>(type: "int", nullable: false),
                    Specialty = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSuccessfulLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technicians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Technicians_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NormalizedUsername = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SuspendedReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SuspendedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SuspendedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    CredentialVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Specialty = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodeState = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    IdentityBasis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FirstSeenByTechnicianCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstSeenByRackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MergedIntoDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CommercialModelName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CommercialModelSource = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    MachineType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LastKnownManufacturer = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LastKnownModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationalStage = table.Column<int>(type: "int", nullable: false),
                    StageChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentHolderTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReadyForHandoverAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReadyByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReadyByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PartChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PartChangeSummary = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Devices_Locations_CurrentLocationId",
                        column: x => x.CurrentLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianBrands",
                columns: table => new
                {
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianBrands", x => new { x.TechnicianId, x.BrandId });
                    table.ForeignKey(
                        name: "FK_TechnicianBrands_LaptopBrands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "LaptopBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicianBrands_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceAliases",
                columns: table => new
                {
                    AliasDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SourceRackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceAliases", x => x.AliasDeviceId);
                    table.ForeignKey(
                        name: "FK_DeviceAliases_Devices_CanonicalDeviceId",
                        column: x => x.CanonicalDeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceIdentifiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    RawValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SupersededAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SupersededByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceIdentifiers_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceNotes_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceWorkflowEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    FromStage = table.Column<int>(type: "int", nullable: false),
                    ToStage = table.Column<int>(type: "int", nullable: false),
                    FromTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RepairWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BaselineReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaselineIsFresh = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceWorkflowEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceWorkflowEvents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceWorkflowEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TechnicianCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TechnicianName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CommercialModelName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CommercialModelSource = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    MachineType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Cpu = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RamText = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    StorageText = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Gpu = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ScreenSummary = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScreenInches = table.Column<double>(type: "float", nullable: false),
                    RefreshRate = table.Column<int>(type: "int", nullable: false),
                    IsTouch = table.Column<bool>(type: "bit", nullable: false),
                    BatteryHealthPercent = table.Column<double>(type: "float", nullable: false),
                    BenchmarkScore = table.Column<int>(type: "int", nullable: true),
                    MaxCpuTemp = table.Column<double>(type: "float", nullable: true),
                    ThrottlingDetected = table.Column<bool>(type: "bit", nullable: false),
                    PassCount = table.Column<int>(type: "int", nullable: false),
                    FailCount = table.Column<int>(type: "int", nullable: false),
                    SkipCount = table.Column<int>(type: "int", nullable: false),
                    NotPresentCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    NotRunCount = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: true),
                    GeneralNote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DeletedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestoredByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RestoredReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RestoredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImportedFrom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NeedsDeviceResolution = table.Column<bool>(type: "bit", nullable: false),
                    SnapshotCapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SnapshotCollectorVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SnapshotRanAsAdministrator = table.Column<bool>(type: "bit", nullable: false),
                    SnapshotIsPartial = table.Column<bool>(type: "bit", nullable: false),
                    SnapshotWarnings = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceRackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScreenGrade = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    ScreenRepair = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HousingPaint = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    HousingCrack = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    BatteryService = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Disassembly = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reports_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Edits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Field = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Edits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Edits_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parts_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepairWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetestReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequiredSpecialty = table.Column<int>(type: "int", nullable: false),
                    AssignedTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedByTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedByActorType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpenedByTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FaultSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RepairActions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutcomeReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Approval = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ApprovalDecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    BrandOverride = table.Column<bool>(type: "bit", nullable: false),
                    StartedWithoutApproval = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairWorkItems_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepairWorkItems_Reports_SourceReportId",
                        column: x => x.SourceReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepairWorkItems_Technicians_AssignedTechnicianId",
                        column: x => x.AssignedTechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepairWorkItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SnapshotComponents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    InstanceIndex = table.Column<int>(type: "int", nullable: false),
                    SlotOrPosition = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ManufacturerSerial = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    HardwareFingerprint = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PnPDeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IdentityMethod = table.Column<int>(type: "int", nullable: false),
                    IdentityConfidence = table.Column<int>(type: "int", nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: false),
                    CapacityBytes = table.Column<long>(type: "bigint", nullable: true),
                    SpeedMhz = table.Column<int>(type: "int", nullable: true),
                    HealthPercent = table.Column<int>(type: "int", nullable: true),
                    PowerOnHours = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AttributesJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnapshotComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SnapshotComponents_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Steps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SkipReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Steps_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepairParts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InventoryCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairParts_RepairWorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "RepairWorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepairWorkItemIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IssueTitleSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Resolved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairWorkItemIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairWorkItemIssues_RepairWorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "RepairWorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_EntityType_EntityId",
                table: "AuditEvents",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_EventId",
                table: "AuditEvents",
                columns: new[] { "TenantId", "EventId" },
                unique: true,
                filter: "[EventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_OccurredAtUtc",
                table: "AuditEvents",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Containers_TenantId_NormalizedCode",
                table: "Containers",
                columns: new[] { "TenantId", "NormalizedCode" },
                unique: true,
                filter: "[NormalizedCode] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId_Code",
                table: "Departments",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[Code] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAliases_CanonicalDeviceId",
                table: "DeviceAliases",
                column: "CanonicalDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAliases_TenantId_CanonicalDeviceId",
                table: "DeviceAliases",
                columns: new[] { "TenantId", "CanonicalDeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCodeLeases_TenantId_FromNumber",
                table: "DeviceCodeLeases",
                columns: new[] { "TenantId", "FromNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCodeLeases_TenantId_RackId_Status",
                table: "DeviceCodeLeases",
                columns: new[] { "TenantId", "RackId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceIdentifiers_DeviceId_IsActive",
                table: "DeviceIdentifiers",
                columns: new[] { "DeviceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceIdentifiers_TenantId_Kind_NormalizedValue",
                table: "DeviceIdentifiers",
                columns: new[] { "TenantId", "Kind", "NormalizedValue" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceNotes_DeviceId_CreatedAtUtc",
                table: "DeviceNotes",
                columns: new[] { "DeviceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ContainerId",
                table: "Devices",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CurrentLocationId",
                table: "Devices",
                column: "CurrentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TenantId_ContainerId",
                table: "Devices",
                columns: new[] { "TenantId", "ContainerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TenantId_LastKnownModel",
                table: "Devices",
                columns: new[] { "TenantId", "LastKnownModel" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TenantId_OperationalStage",
                table: "Devices",
                columns: new[] { "TenantId", "OperationalStage" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TenantId_PartChangedAtUtc",
                table: "Devices",
                columns: new[] { "TenantId", "PartChangedAtUtc" },
                filter: "[PartChangedAtUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TenantId_PublicCode",
                table: "Devices",
                columns: new[] { "TenantId", "PublicCode" },
                unique: true,
                filter: "[PublicCode] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceWorkflowEvents_DeviceId",
                table: "DeviceWorkflowEvents",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceWorkflowEvents_TenantId_DeviceId_OccurredAtUtc",
                table: "DeviceWorkflowEvents",
                columns: new[] { "TenantId", "DeviceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceWorkflowEvents_TenantId_EventId",
                table: "DeviceWorkflowEvents",
                columns: new[] { "TenantId", "EventId" },
                unique: true,
                filter: "[EventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceWorkflowEvents_TenantId_EventType_OccurredAtUtc",
                table: "DeviceWorkflowEvents",
                columns: new[] { "TenantId", "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Edits_ReportId",
                table: "Edits",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueCatalogItems_TenantId_Code",
                table: "IssueCatalogItems",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[Code] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_LaptopBrandAliases_BrandId",
                table: "LaptopBrandAliases",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_LaptopBrandAliases_TenantId_NormalizedValue",
                table: "LaptopBrandAliases",
                columns: new[] { "TenantId", "NormalizedValue" },
                unique: true,
                filter: "[NormalizedValue] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_LaptopBrands_TenantId_NormalizedName",
                table: "LaptopBrands",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true,
                filter: "[NormalizedName] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_TenantId_Code",
                table: "Locations",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[Code] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_TenantId_Kind_IsActive",
                table: "Locations",
                columns: new[] { "TenantId", "Kind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginEvents_TenantId_AtUtc",
                table: "LoginEvents",
                columns: new[] { "TenantId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_ReportId",
                table: "Parts",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_RackPairingCodes_TenantId_CodePrefix",
                table: "RackPairingCodes",
                columns: new[] { "TenantId", "CodePrefix" });

            migrationBuilder.CreateIndex(
                name: "IX_Racks_InstallationId",
                table: "Racks",
                column: "InstallationId",
                filter: "[InstallationId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Racks_TenantId_KeyPrefix",
                table: "Racks",
                columns: new[] { "TenantId", "KeyPrefix" });

            migrationBuilder.CreateIndex(
                name: "IX_Racks_TenantId_RackCode",
                table: "Racks",
                columns: new[] { "TenantId", "RackCode" },
                unique: true,
                filter: "[RackCode] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_RepairParts_WorkItemId",
                table: "RepairParts",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItemIssues_TenantId_IssueCode",
                table: "RepairWorkItemIssues",
                columns: new[] { "TenantId", "IssueCode" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItemIssues_WorkItemId",
                table: "RepairWorkItemIssues",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItems_AssignedTechnicianId",
                table: "RepairWorkItems",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItems_DeviceId",
                table: "RepairWorkItems",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItems_SourceReportId",
                table: "RepairWorkItems",
                column: "SourceReportId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItems_TenantId_AssignedTechnicianId_CompletedAtUtc",
                table: "RepairWorkItems",
                columns: new[] { "TenantId", "AssignedTechnicianId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItems_TenantId_DeviceId",
                table: "RepairWorkItems",
                columns: new[] { "TenantId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItems_TenantId_PublicCode",
                table: "RepairWorkItems",
                columns: new[] { "TenantId", "PublicCode" },
                unique: true,
                filter: "[PublicCode] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItems_TenantId_Status_OpenedAtUtc",
                table: "RepairWorkItems",
                columns: new[] { "TenantId", "Status", "OpenedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairWorkItems_TenantId_UpdatedAtUtc",
                table: "RepairWorkItems",
                columns: new[] { "TenantId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_DeviceId_StartedAtUtc",
                table: "Reports",
                columns: new[] { "DeviceId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TechnicianId",
                table: "Reports",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TenantId_Fingerprint",
                table: "Reports",
                columns: new[] { "TenantId", "Fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TenantId_NeedsDeviceResolution",
                table: "Reports",
                columns: new[] { "TenantId", "NeedsDeviceResolution" },
                filter: "[NeedsDeviceResolution] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TenantId_StartedAtUtc",
                table: "Reports",
                columns: new[] { "TenantId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TenantId_TechnicianCode",
                table: "Reports",
                columns: new[] { "TenantId", "TechnicianCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TenantId_TechnicianId_StartedAtUtc",
                table: "Reports",
                columns: new[] { "TenantId", "TechnicianId", "StartedAtUtc" },
                filter: "[TechnicianId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SnapshotComponents_ReportId",
                table: "SnapshotComponents",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_SnapshotComponents_TenantId_Type_ManufacturerSerial",
                table: "SnapshotComponents",
                columns: new[] { "TenantId", "Type", "ManufacturerSerial" });

            migrationBuilder.CreateIndex(
                name: "IX_Steps_ReportId",
                table: "Steps",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncBatches_RackId_BatchId",
                table: "SyncBatches",
                columns: new[] { "RackId", "BatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncBatches_TenantId_ReceivedAtUtc",
                table: "SyncBatches",
                columns: new[] { "TenantId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianBrands_BrandId",
                table: "TechnicianBrands",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianLoginAttempts_RackId_AtUtc",
                table: "TechnicianLoginAttempts",
                columns: new[] { "RackId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianLoginAttempts_TenantId_AtUtc",
                table: "TechnicianLoginAttempts",
                columns: new[] { "TenantId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_TenantId_Code",
                table: "Technicians",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[Code] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_TenantId_NormalizedUsername",
                table: "Technicians",
                columns: new[] { "TenantId", "NormalizedUsername" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Code",
                table: "Users",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Username",
                table: "Users",
                columns: new[] { "TenantId", "Username" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "DeviceAliases");

            migrationBuilder.DropTable(
                name: "DeviceCodeLeases");

            migrationBuilder.DropTable(
                name: "DeviceIdentifiers");

            migrationBuilder.DropTable(
                name: "DeviceNotes");

            migrationBuilder.DropTable(
                name: "DeviceWorkflowEvents");

            migrationBuilder.DropTable(
                name: "Edits");

            migrationBuilder.DropTable(
                name: "IssueCatalogItems");

            migrationBuilder.DropTable(
                name: "LaptopBrandAliases");

            migrationBuilder.DropTable(
                name: "LoginEvents");

            migrationBuilder.DropTable(
                name: "Parts");

            migrationBuilder.DropTable(
                name: "RackPairingCodes");

            migrationBuilder.DropTable(
                name: "Racks");

            migrationBuilder.DropTable(
                name: "RepairParts");

            migrationBuilder.DropTable(
                name: "RepairWorkItemIssues");

            migrationBuilder.DropTable(
                name: "SnapshotComponents");

            migrationBuilder.DropTable(
                name: "Steps");

            migrationBuilder.DropTable(
                name: "SyncBatches");

            migrationBuilder.DropTable(
                name: "TechnicianBrands");

            migrationBuilder.DropTable(
                name: "TechnicianLoginAttempts");

            migrationBuilder.DropTable(
                name: "TenantCounters");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "RepairWorkItems");

            migrationBuilder.DropTable(
                name: "LaptopBrands");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Technicians");

            migrationBuilder.DropTable(
                name: "Containers");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
