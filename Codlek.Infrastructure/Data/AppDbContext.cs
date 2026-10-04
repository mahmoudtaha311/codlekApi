using Codlek.Core.Entities;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<WebUser> WebUsers => Set<WebUser>();
    /// <summary>الرواكة — الهاردات اللي بتقلّع اللابات وبترفع الفحوصات.</summary>
    public DbSet<Rack> Racks => Set<Rack>();

    /// <summary>أكواد الاقتران المؤقتة.</summary>
    public DbSet<RackPairingCode> RackPairingCodes => Set<RackPairingCode>();

    /// <summary>عدّادات الأرقام المتسلسلة لكل شركة.</summary>
    public DbSet<TenantCounter> TenantCounters => Set<TenantCounter>();

    /// <summary>سجل الإجراءات الإدارية وإجراءات النظام.</summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>دفعات المزامنة المستقبَلة — سجل عدم التكرار.</summary>
    public DbSet<SyncBatch> SyncBatches => Set<SyncBatch>();

    /// <summary>أقسام الشركة — مركزية وقابلة للتعديل مش enum في الكود.</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>الأجهزة (اللابات) — الحاجة اللي بتعيش عبر كل الفحوصات.</summary>
    public DbSet<Device> Devices => Set<Device>();

    /// <summary>مراسي هوية الأجهزة — تاريخ مش أعمدة.</summary>
    public DbSet<DeviceIdentifierRow> DeviceIdentifiers => Set<DeviceIdentifierRow>();

    /// <summary>ترجمة معرّفات الراكات المحلية للجهاز الكانوني.</summary>
    public DbSet<DeviceAlias> DeviceAliases => Set<DeviceAlias>();

    /// <summary>بلوكات أرقام الأجهزة المؤجّرة للرواكة.</summary>
    public DbSet<DeviceCodeLease> DeviceCodeLeases => Set<DeviceCodeLease>();

    /// <summary>ملاحظات الأجهزة — بتتضاف وبس.</summary>
    public DbSet<DeviceNote> DeviceNotes => Set<DeviceNote>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportStep> Steps => Set<ReportStep>();
    public DbSet<ReportPart> Parts => Set<ReportPart>();
    public DbSet<ReportEdit> Edits => Set<ReportEdit>();
    public DbSet<ReportSnapshotComponent> SnapshotComponents => Set<ReportSnapshotComponent>();
    public DbSet<LoginEvent> LoginEvents => Set<LoginEvent>();

    /// <summary>الفنيون — بشر بيشتغلوا على محطات الفحص، مش مستخدمي موقع.</summary>
    public DbSet<Technician> Technicians => Set<Technician>();

    /// <summary>
    /// توكنات التجديد — <b>جدول جديد مش موجود في المشروع القديم</b>.
    ///
    /// <para>القديم بيستعمل كوكي، فمكانش محتاجه. وده اللي بيخلّي
    /// إلغاء جلسة واحدة ممكن من غير ما نقطع باقي أجهزة صاحبها.</para>
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>محاولات دخول الفنيين من المحطات — تدقيق وتقييد تخمين.</summary>
    public DbSet<TechnicianLoginAttempt> TechnicianLoginAttempts => Set<TechnicianLoginAttempt>();

    // ===== الشغل التشغيلي =====

    /// <summary>المواقع التشغيلية — مخزن، قسم، نقطة بيع.</summary>
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ImportContainer> Containers => Set<ImportContainer>();

    /// <summary>كتالوج الأعطال المعروفة — عشان العطل يبقى قابل للاستعلام.</summary>
    public DbSet<IssueCatalogItem> IssueCatalogItems => Set<IssueCatalogItem>();

    /// <summary>أوامر الصيانة.</summary>
    public DbSet<RepairWorkItem> RepairWorkItems => Set<RepairWorkItem>();

    /// <summary>أعطال أمر الصيانة — بالقيمة مش بمرجع للكتالوج.</summary>
    public DbSet<RepairWorkItemIssue> RepairWorkItemIssues => Set<RepairWorkItemIssue>();

    /// <summary>قطع الغيار اللي اتركّبت في صيانة.</summary>
    public DbSet<RepairPart> RepairParts => Set<RepairPart>();

    /// <summary>السجل التشغيلي — الحقيقة الوحيدة عن مكان الجهاز وحائزه.</summary>
    public DbSet<DeviceWorkflowEvent> DeviceWorkflowEvents => Set<DeviceWorkflowEvent>();

    /// <summary>ماركات اللابات — قايمة صاحب الشغل.</summary>
    public DbSet<LaptopBrand> LaptopBrands => Set<LaptopBrand>();

    /// <summary>الأسماء البديلة للماركات.</summary>
    public DbSet<LaptopBrandAlias> LaptopBrandAliases => Set<LaptopBrandAlias>();

    /// <summary>
    /// ربط الفني بالماركات — <b>أول جدول ربط في المخطّط</b>.
    ///
    /// <para>⚠️ القدرات قبل كده كانت بوليانين وenum واحد، فمكانش فيه
    /// مكان لمجموعة أصلاً.</para>
    /// </summary>
    public DbSet<TechnicianBrand> TechnicianBrands => Set<TechnicianBrand>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // 🔴 مفتاح الدخول فريد على مستوى النظام كله.
        //
        // كان جوّه الشركة — (TenantId, Username) — وده كان بيسمح
        // لشركتين إن كل واحدة يبقى عندها «ahmed». وصفحة الدخول بتستلم
        // اسم وباسورد وبس، فمكانش قدّامها غير إنها تاخد أول صف طالع من
        // القاعدة من غير ترتيب: يعني الشركة اللي بتتفتح كانت بتتحدد
        // بخطة التنفيذ، مش بحاجة المستخدم كتبها. دلوقتي الاسم المطبَّع
        // بيوصّل لصف واحد مهما كان عدد الشركات، والـ TenantId بييجي من
        // الصف ده — مش من المتصفح.
        b.Entity<WebUser>()
            .HasIndex(u => u.NormalizedUsername)
            .IsUnique();

        // فهرس عادي مش فريد. الفرادة بقت على العمود المطبَّع فوق، وده
        // بيفضل عشان عرض مستخدمي شركة والبحث عليهم.
        b.Entity<WebUser>()
            .HasIndex(u => new { u.TenantId, u.Username });

        b.Entity<WebUser>()
            .HasIndex(u => new { u.TenantId, u.Code });

        // الاستعلامات كلها بتترشّح بالشركة والتاريخ — ده أهم فهرس في النظام
        b.Entity<Report>()
            .HasIndex(r => new { r.TenantId, r.StartedAtUtc });

        b.Entity<Report>()
            .HasIndex(r => new { r.TenantId, r.TechnicianCode });

        b.Entity<Report>()
            .HasIndex(r => new { r.TenantId, r.Fingerprint });

        // ===== ربط الفحص بالفني المركزي =====

        // ⚠️ **Restrict مش Cascade، ولا حتى SetNull.** مسح فني مايمسحش
        // تاريخ شغله، ومايفكّهوش منه كمان. الفني بيتوقف مش بيتمسح
        // (SuspendedAtUtc)، ولو حد حاول يمسح فني عليه فحوصات القاعدة
        // بترفض — بدل ما تبلع نسبة الشغل في صمت.
        b.Entity<Report>()
            .HasOne(r => r.Technician)
            .WithMany()
            .HasForeignKey(r => r.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);

        // «وريني كل شغل الفني ده» بالهوية الثابتة مش بالكود. مفلتر
        // عشان الفهرس يفضل صغير — الفحوصات القديمة العمود ده فاضي
        // فيها، ومالهاش لازمة في الفهرس.
        b.Entity<Report>()
            .HasIndex(r => new { r.TenantId, r.TechnicianId, r.StartedAtUtc })
            .HasFilter("[TechnicianId] IS NOT NULL");

        b.Entity<Report>()
            .HasMany(r => r.Steps)
            .WithOne(s => s.Report!)
            .HasForeignKey(s => s.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Report>()
            .HasMany(r => r.Parts)
            .WithOne(p => p.Report!)
            .HasForeignKey(p => p.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Report>()
            .HasMany(r => r.Edits)
            .WithOne(e => e.Report!)
            .HasForeignKey(e => e.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== ربط الفحص بالجهاز =====

        // ⚠️ **Restrict مش Cascade.** مسح جهاز مايمسحش تاريخ فحصه —
        // التاريخ هو المنتج هنا. ولو حد حاول يمسح جهاز عليه فحوصات،
        // القاعدة بترفض بدل ما تبلع الصفوف في صمت.
        b.Entity<Report>()
            .HasOne(r => r.Device)
            .WithMany(d => d.Reports)
            .HasForeignKey(r => r.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // «وريني كل فحوصات اللاب ده بالترتيب» — الاستعلام الأساسي
        // لصفحة الجهاز.
        b.Entity<Report>()
            .HasIndex(r => new { r.DeviceId, r.StartedAtUtc });

        // شاشة «فحوصات محتاجة تحديد جهاز» للمدير. مفلتر عشان الفهرس
        // يفضل صغير — الوضع الطبيعي إن العمود ده false لكل الصفوف.
        b.Entity<Report>()
            .HasIndex(r => new { r.TenantId, r.NeedsDeviceResolution })
            .HasFilter("[NeedsDeviceResolution] = 1");

        // =============================================================
        //  حاويات الاستيراد
        // =============================================================
        //
        // 🔴 **التفرّد على الشكل المطبّع مش على الرمز الخام.** الفني
        // بيكتب نفس الحاوية بخمس طرق، وبفهرس على الخام الخمسة بيعدّوا
        // وبيبقى محتوى الحاوية مقسوم على خمس صفوف.
        //
        // ⚠️ ومفلتر على `<> ''` عشان الحاوية من غير رمز (لو حصلت)
        // ماتمنعش التانية.
        b.Entity<ImportContainer>()
            .HasIndex(c => new { c.TenantId, c.NormalizedCode })
            .IsUnique()
            .HasFilter("[NormalizedCode] <> ''");

        // =============================================================
        //  ماركات اللابات
        // =============================================================

        // التفرّد على الشكل المطبّع — نفس حجّة الحاويات بالظبط.
        b.Entity<LaptopBrand>()
            .HasIndex(x => new { x.TenantId, x.NormalizedName })
            .IsUnique()
            .HasFilter("[NormalizedName] <> ''");

        // 🔴 **الاسم البديل فريد على مستوى الشركة كلها، مش جوّه
        // الماركة الواحدة — ودي الحتة اللي بتخلّي الحل حتمي.**
        //
        // لو ماركتين ادّعوا «HP»، حل اللاب بيبقى معتمد على ترتيب
        // الصفوف — ونفس اللاب بيتحل لماركة مختلفة بعد ما حد يغيّر
        // ترتيب العرض. وده النوع اللي بيعيش شهور قبل ما حد يربطه
        // بسببه.
        //
        // ⚠️ والقيد في قاعدة البيانات مش في نقطة النهاية بس: نقطة
        // واحدة بتنسى الفحص بتكسر الحتمية كلها.
        b.Entity<LaptopBrandAlias>()
            .HasIndex(x => new { x.TenantId, x.NormalizedValue })
            .IsUnique()
            .HasFilter("[NormalizedValue] <> ''");

        b.Entity<LaptopBrandAlias>()
            .HasOne(x => x.Brand)
            .WithMany(x => x.Aliases)
            .HasForeignKey(x => x.BrandId)

            // ⚠️ الاسم البديل مالوش حياة من غير ماركته — ده الفرق
            // بينه وبين الماركة نفسها اللي بتتوقف مابتتمسحش.
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<TechnicianBrand>().HasKey(x => new { x.TechnicianId, x.BrandId });

        b.Entity<TechnicianBrand>()
            .HasOne(x => x.Technician)
            .WithMany()
            .HasForeignKey(x => x.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<TechnicianBrand>()
            .HasOne(x => x.Brand)
            .WithMany()
            .HasForeignKey(x => x.BrandId)

            // ⚠️ ماركة عليها فنيين مابتتمسحش — بتتوقف. مسحها كان
            // هيشيل القيد عن الفنيين دول **في صمت**.
            .OnDelete(DeleteBehavior.Restrict);

        // «وريني كل اللابات اللي في الحاوية دي» — الاستعلام الأساسي
        // لصفحة الحاويات وللتصدير.
        b.Entity<Device>()
            .HasOne(d => d.Container)
            .WithMany()
            .HasForeignKey(d => d.ContainerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Device>()
            .HasIndex(d => new { d.TenantId, d.ContainerId });

        // أيقونة «فيه جهاز اتغيّرت فيه قطعة» في ترويسة اللوحة.
        //
        // ⚠️ مفلتر بنفس السبب: الوضع الطبيعي إن العمود ده فاضي لكل
        // الصفوف، فالفهرس بيفضل صغير والعدّاد بيبقى CountAsync واحد
        // — وده شرط عشان الأيقونة تتحمّل إنها على كل صفحة.
        b.Entity<Device>()
            .HasIndex(d => new { d.TenantId, d.PartChangedAtUtc })
            .HasFilter("[PartChangedAtUtc] IS NOT NULL");

        b.Entity<Report>()
            .HasMany(r => r.SnapshotComponents)
            .WithOne(c => c.Report!)
            .HasForeignKey(c => c.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        // «القطعة اللي سيريالها كذا راحت فين؟» — ده السؤال اللي المخزن
        // المسلسل كله هيتبني عليه بعدين.
        b.Entity<ReportSnapshotComponent>()
            .HasIndex(c => new { c.TenantId, c.Type, c.ManufacturerSerial });

        b.Entity<LoginEvent>()
            .HasIndex(e => new { e.TenantId, e.AtUtc });

        // ===== الرواكة =====

        b.Entity<Rack>().ToTable("Racks");

        // كود الراكة فريد جوّه الشركة. مفلتر لأن الراكة بتتعمل أول
        // بكود فاضي وقت إنشاء كود الاقتران، والرقم بيتاخد وقت التسجيل.
        b.Entity<Rack>()
            .HasIndex(r => new { r.TenantId, r.RackCode })
            .IsUnique()
            .HasFilter("[RackCode] <> ''");

        // ده الاستعلام اللي بيتنفّذ مع **كل** رفع من أي راكة.
        b.Entity<Rack>()
            .HasIndex(r => new { r.TenantId, r.KeyPrefix });

        // بيمسك الاستنساخ: نفس هوية القرص مسجّلة مرتين.
        b.Entity<Rack>()
            .HasIndex(r => r.InstallationId)
            .HasFilter("[InstallationId] <> ''");

        b.Entity<RackPairingCode>()
            .HasIndex(c => new { c.TenantId, c.CodePrefix });

        // ===== العدّادات =====

        b.Entity<TenantCounter>()
            .HasKey(c => new { c.TenantId, c.CounterName });

        // ===== سجل المراجعة =====

        b.Entity<AuditEvent>()
            .HasIndex(e => new { e.TenantId, e.OccurredAtUtc });

        b.Entity<AuditEvent>()
            .HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });

        // حدث أصله من راكة ممكن يترفع أكتر من مرة (إعادة محاولة بعد
        // انقطاع). المعرّف بيمنع التكرار، والفلتر عشان الأحداث اللي
        // مصدرها السيرفر مالهاش معرّف أصلاً.
        b.Entity<AuditEvent>()
            .HasIndex(e => new { e.TenantId, e.EventId })
            .IsUnique()
            .HasFilter("[EventId] IS NOT NULL");

        // ===== دفعات المزامنة =====

        // ده القيد اللي بيخلي إعادة الإرسال آمنة: نفس الدفعة من نفس
        // الراكة مستحيل تتطبّق مرتين.
        b.Entity<SyncBatch>()
            .HasIndex(s => new { s.RackId, s.BatchId })
            .IsUnique();

        b.Entity<SyncBatch>()
            .HasIndex(s => new { s.TenantId, s.ReceivedAtUtc });

        // ===== الفنيون =====

        // اسم الدخول فريد جوّه الشركة — مش على مستوى النظام كله.
        // شركتين مختلفتين ممكن يبقى عندهم فني اسمه ahmed، وده صح.
        b.Entity<Technician>()
            .HasIndex(t => new { t.TenantId, t.NormalizedUsername })
            .IsUnique();

        // كود الفني فريد جوّه الشركة. مفلتر زي الراكة عشان صف اتعمل
        // بكود فاضي (لو حصل) ما يقفلش الجدول على الباقي.
        b.Entity<Technician>()
            .HasIndex(t => new { t.TenantId, t.Code })
            .IsUnique()
            .HasFilter("[Code] <> ''");

        // ده الاستعلام اللي بيتنفّذ مع كل محاولة دخول فني من محطة:
        // «كام محاولة فاشلة من المحطة دي في آخر شوية؟»
        b.Entity<TechnicianLoginAttempt>()
            .HasIndex(a => new { a.RackId, a.AtUtc });

        b.Entity<TechnicianLoginAttempt>()
            .HasIndex(a => new { a.TenantId, a.AtUtc });

        // ===== الأقسام =====

        b.Entity<Department>()
            .HasIndex(d => new { d.TenantId, d.Code })
            .IsUnique()
            .HasFilter("[Code] <> ''");

        // ⚠️ Restrict مش Cascade: مسح قسم مالوش لازمة يمسح الموظفين
        // اللي فيه. القسم بيتوقف (IsActive) مش بيتمسح.
        b.Entity<WebUser>()
            .HasOne<Department>()
            .WithMany()
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ===== الأجهزة =====

        // ⚠️ الكود العام **فريد فعلاً**. الفلتر عشان الأجهزة اللي اتعملت
        // والراكة أوفلاين وبلوكها خلص — دي بتفضل بكود فاضي لحد المزامنة،
        // ومن غير الفلتر كانوا هيتعارضوا مع بعض.
        b.Entity<Device>()
            .HasIndex(d => new { d.TenantId, d.PublicCode })
            .IsUnique()
            .HasFilter("[PublicCode] <> ''");

        b.Entity<Device>()
            .HasIndex(d => new { d.TenantId, d.LastKnownModel });

        b.Entity<Device>()
            .HasMany(d => d.Identifiers)
            .WithOne(i => i.Device!)
            .HasForeignKey(i => i.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ده استعلام المطابقة — بيتنفّذ لكل مرساة في كل فحص بيوصل.
        b.Entity<DeviceIdentifierRow>()
            .HasIndex(i => new { i.TenantId, i.Kind, i.NormalizedValue });

        // ⚠️ **مش فريد.** القيم الوهمية بتتكرر بطبيعتها، والتفرّد هنا كان
        // هيرفض استيراد شرعي. التكرار بيتعامل معاه كـ«تنبيه» مش كقيد —
        // نفس منطق عدم ربط كود الفني بمفتاح أجنبي.
        b.Entity<DeviceIdentifierRow>()
            .HasIndex(i => new { i.DeviceId, i.IsActive });

        // ===== ترجمة معرّفات الراكات =====
        //
        // 🔴 المفتاح هو معرّف الراكة المحلي نفسه: راكة واحدة مالهاش
        // حق تدّي نفس المعرّف لجهازين، والسيرفر لازم يترجمه لجهاز
        // واحد بالظبط.
        b.Entity<DeviceAlias>().HasKey(a => a.AliasDeviceId);

        b.Entity<DeviceAlias>()
            .HasIndex(a => new { a.TenantId, a.CanonicalDeviceId });

        // ⚠️ مفيش Cascade: مسح جهاز كانوني لازم يقع لو فيه ربط عليه،
        // مش يمسح الربط في صمت ويسيب راكة بتبعت لمعرّف مايتام.
        b.Entity<DeviceAlias>()
            .HasOne(a => a.CanonicalDevice)
            .WithMany()
            .HasForeignKey(a => a.CanonicalDeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // ملاحظات الجهاز — القراية دايماً «ملاحظات الجهاز ده، الأحدث الأول».
        b.Entity<DeviceNote>()
            .HasIndex(n => new { n.DeviceId, n.CreatedAtUtc });

        b.Entity<DeviceNote>()
            .HasOne(n => n.Device)
            .WithMany()
            .HasForeignKey(n => n.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== بلوكات الأرقام =====

        // بداية المدى فريدة جوّه الشركة — ده اللي بيمنع تخصيص نفس البلوك
        // لراكتين.
        b.Entity<DeviceCodeLease>()
            .HasIndex(l => new { l.TenantId, l.FromNumber })
            .IsUnique();

        b.Entity<DeviceCodeLease>()
            .HasIndex(l => new { l.TenantId, l.RackId, l.Status });

        // ===== الشغل التشغيلي =====

        // الموقع: كود فريد جوّه الشركة، بنفس فلتر القسم بالظبط — موقع
        // من غير كود مسموح، واتنين بنفس الكود لأ.
        b.Entity<Location>()
            .HasIndex(l => new { l.TenantId, l.Code })
            .IsUnique()
            .HasFilter("[Code] <> ''");

        b.Entity<Location>()
            .HasIndex(l => new { l.TenantId, l.Kind, l.IsActive });

        b.Entity<IssueCatalogItem>()
            .HasIndex(i => new { i.TenantId, i.Code })
            .IsUnique()
            .HasFilter("[Code] <> ''");

        // ⚠️ Restrict على الجهاز والفحص والفني: أمر صيانة تاريخ شغل
        // حقيقي، ومسح جهاز مالهوش يمسح إثبات إن حد اشتغل عليه.
        b.Entity<RepairWorkItem>()
            .HasOne(w => w.Device)
            .WithMany()
            .HasForeignKey(w => w.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<RepairWorkItem>()
            .HasOne(w => w.SourceReport)
            .WithMany()
            .HasForeignKey(w => w.SourceReportId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<RepairWorkItem>()
            .HasOne(w => w.AssignedTechnician)
            .WithMany()
            .HasForeignKey(w => w.AssignedTechnicianId)
            .OnDelete(DeleteBehavior.Restrict);

        // رقم الأمر فريد لما يتوزّع. بيفضل فاضي وهو لسه على راكة
        // أوفلاين، فنفس فلتر كود الجهاز.
        b.Entity<RepairWorkItem>()
            .HasIndex(w => new { w.TenantId, w.PublicCode })
            .IsUnique()
            .HasFilter("[PublicCode] <> ''");

        // ده استعلام صفحة الصيانة: حالة + تاريخ.
        b.Entity<RepairWorkItem>()
            .HasIndex(w => new { w.TenantId, w.Status, w.OpenedAtUtc });

        b.Entity<RepairWorkItem>()
            .HasIndex(w => new { w.TenantId, w.DeviceId });

        // وده استعلام «شغل الفني ده» — الإنتاجية بتعدّي منه.
        b.Entity<RepairWorkItem>()
            .HasIndex(w => new { w.TenantId, w.AssignedTechnicianId, w.CompletedAtUtc });

        // 🔴 وده استعلام التغذية النازلة: كل راكة في الشركة بتسأله كل
        // دورة مزامنة. من غير الفهرس ده، كل راكة بتعمل مسح كامل لجدول
        // أوامر الصيانة كل بضع دقايق — والجدول ده بيكبر ومابيتنضفش.
        b.Entity<RepairWorkItem>()
            .HasIndex(w => new { w.TenantId, w.UpdatedAtUtc });

        // الأعطال والقطع **مملوكة** للأمر — Cascade زي خطوات الفحص.
        b.Entity<RepairWorkItem>()
            .HasMany(w => w.Issues)
            .WithOne(i => i.WorkItem!)
            .HasForeignKey(i => i.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<RepairWorkItem>()
            .HasMany(w => w.Parts)
            .WithOne(p => p.WorkItem!)
            .HasForeignKey(p => p.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<RepairWorkItemIssue>()
            .HasIndex(i => new { i.TenantId, i.IssueCode });

        // ===== السجل التشغيلي =====

        // 🔴 معرّف الحدث فريد جوّه الشركة — ده اللي بيمنع تكرار نفس
        // الحركة لما الراكة ترفعها تاني بعد انقطاع. الفلتر عشان
        // الحركات اللي اتعملت على السيرفر مالهاش معرّف منشأ.
        b.Entity<DeviceWorkflowEvent>()
            .HasIndex(e => new { e.TenantId, e.EventId })
            .IsUnique()
            .HasFilter("[EventId] IS NOT NULL");

        // ده الاستعلام الأساسي: تاريخ جهاز واحد، الأحدث الأول.
        b.Entity<DeviceWorkflowEvent>()
            .HasIndex(e => new { e.TenantId, e.DeviceId, e.OccurredAtUtc });

        // وده صفحة مراحل الأجهزة والإحصائيات.
        b.Entity<DeviceWorkflowEvent>()
            .HasIndex(e => new { e.TenantId, e.EventType, e.OccurredAtUtc });

        b.Entity<DeviceWorkflowEvent>()
            .HasOne(e => e.Device)
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // ⚠️ Restrict: موقع بيتوقف مش بيتمسح، وحركة قديمة بتشاور عليه.
        b.Entity<Device>()
            .HasOne(d => d.CurrentLocation)
            .WithMany()
            .HasForeignKey(d => d.CurrentLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // فهرس صفحة «مراحل الأجهزة» — المرحلة مع الموقع.
        b.Entity<Device>()
            .HasIndex(d => new { d.TenantId, d.OperationalStage });

        // =============================================================
        //  توكنات التجديد — جدول جديد
        // =============================================================

        /*
          🔴 **فريد على البصمة.** التجديد بيدوّر بالبصمة وبياخد صف
          واحد. صفّين بنفس البصمة معناه إن اللي بيجدّد بياخد واحد منهم
          بالصدفة — فيلغي ده ويسيب ده، والتوكن يفضل شغّال بعد ما
          المفروض اتقفل.
        */
        b.Entity<RefreshToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        // ⚠️ «اقفل كل جلسات الحساب ده» بيترشّح بالمستخدم.
        b.Entity<RefreshToken>()
            .HasIndex(t => new { t.UserId, t.RevokedAtUtc });

        // ⚠️ وتنضيف المنتهي بيترشّح بالتاريخ.
        b.Entity<RefreshToken>()
            .HasIndex(t => t.ExpiresAtUtc);


        // =============================================================
        //  جداول Identity — المستخدمين والأدوار
        // =============================================================

        // 🔴 **اسم جدول `WebUser` مكتوب بالإيد دلوقتي، وده مش تحسين —
        // ده إصلاح لحاجة كانت هتوقع.**
        //
        // اسم الجدول كان بييجي من اسم خاصية الـDbSet: `Users`. ولما
        // الكلاس ورث `IdentityDbContext`، بقى فيه خاصية اسمها `Users`
        // كمان بتاعة مستخدمي Identity — اسم واحد لحاجتين. فاضطرينا
        // نسمّي القديمة `WebUsers`.
        //
        // ولو سكتنا بعد التسمية، EF كان هيستنتج اسم الجدول من الاسم
        // الجديد ويطلّع هجرة بتعمل `RENAME` لجدول فيه **كل حسابات
        // الورشة**. والسطر ده بيقطع الصلة بين اسم الخاصية واسم الجدول
        // خلاص: الخاصية تتسمّى أي حاجة، والجدول يفضل `Users`.
        b.Entity<WebUser>().ToTable("Users");

        /*
          ⚠️ **الجدولين بيعيشوا مع بعض فترة التحويل — ودي مش مشكلة
          بالعكس، دي اللي بتخلّي القديم مايقفش.**

          `Users`        ← المشروع القديم بيقرا ويكتب فيه، شغّال
          `AspNetUsers`  ← المشروع الجديد، فاضي لحد التحويل

          ونقل الحسابات بيحصل **مرة واحدة وقت التحويل**، مش دلوقتي. لأنه
          لو اتعمل من دلوقتي، أي حساب جديد بيتعمل من اللوحة القديمة
          مايبانش في الجديد — فنبقى عندنا نسختين بتفترقوا كل يوم.
        */

        // الشركة — نفس قاعدة باقي النظام: كل استعلام بيترشّح بيها.
        b.Entity<ApplicationUser>()
            .HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<ApplicationUser>()
            .HasIndex(u => new { u.TenantId, u.Code });

        base.OnModelCreating(b);
    }

    // =================================================================
    //  مفاتيح الدخول المطبَّعة — بتتحسب هنا، مش في مكان النداء
    // =================================================================

    /// <summary>
    /// بيملا <c>NormalizedUsername</c> من <c>Username</c> قبل أي حفظ.
    ///
    /// <para><b>ليه هنا مش في كل صفحة بتعمل مستخدم.</b> الفهرس الفريد
    /// شغّال على العمود المطبَّع. مكان واحد ينسى يملاه معناه صف بمفتاح
    /// فاضي — وتاني صف ناسي زيّه بيصطدم بيه، فالرسالة اللي بتطلع
    /// «الاسم مستخدم» وهو مش مستخدم. وأسوأ: صف بمفتاح فاضي عمره ما
    /// هيتلاقي في الدخول، يعني حساب اتعمل ومش بيدخل من غير أي خطأ
    /// ظاهر. الحساب هنا بيخلّي النسيان مستحيل: مفيش مسار حفظ بيعدّي
    /// من برّه الدالتين دول.</para>
    ///
    /// <para>نفس الكلام على <c>Technician</c> — بنفس الدالة بالظبط،
    /// عشان اسم الفني واسم مستخدم الموقع مايتطبّعوش بقاعدتين مختلفتين.</para>
    /// </summary>
    private void SyncLoginKeys()
    {
        foreach (var entry in ChangeTracker.Entries<WebUser>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.NormalizedUsername = LoginName.Normalize(entry.Entity.Username);
        }

        foreach (var entry in ChangeTracker.Entries<Technician>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.NormalizedUsername = LoginName.Normalize(entry.Entity.Username);
        }
    }

    /// <summary>
    /// بيختم مؤشّر التغذية النازلة على أوامر الصيانة.
    ///
    /// <para>🔴 <b>هنا عشان النسيان يبقى مستحيل — زي مفاتيح الدخول فوق.</b>
    /// أمر الصيانة بيتغيّر من ٨ أماكن على الأقل: إسناد من الموقع، بدء،
    /// إنهاء، تعذّر، إلغاء، ومزامنة الراكة. الراكة بتسحب اللي اتغيّر
    /// <b>بعد</b> آخر وقت شافته، فمكان واحد بينسى يحدّث المؤشّر معناه
    /// إن التغيير ده مايوصلش الفني أبداً — ومفيش أي عرض يبان منه إن
    /// حاجة ضاعت.</para>
    ///
    /// <para>⚠️ <b>ومابنختمش على صف مالوش تعديل حقيقي.</b> EF بيعتبر
    /// الكيان <c>Modified</c> لو أي خاصية اتلمست حتى بنفس القيمة؛
    /// الفحص على <c>HasChanges</c> بيمنع إن قراءة-وحفظ بتحرّك المؤشّر
    /// وتخلّي كل راكة تسحب الصف من تاني من غير داعي.</para>
    /// </summary>
    private void StampRepairCursor()
    {
        // 🔴 **مقصوصة للمللي ثانية عن قصد.**
        //
        // المؤشّر ده بيخرج على السلك وبيرجع في <c>?since=</c>. السلك
        // بيكتب التاريخ بدقة المللي (<c>UtcDateTimeConverter</c>)، فلو
        // خزّنّا دقة أعلى، العلامة اللي بترجع بتبقى **أقل** من قيمة
        // الصف بجزء من المللي — والصف بيرجع في كل سحبة للأبد، بيبان
        // كأنه بيتغيّر وهو ساكن.
        //
        // القص هنا بيخلّي اللي متخزّن هو نفسه اللي على السلك بالظبط،
        // فالمقارنة تطلع مظبوطة من غير أي هامش.
        var now = Truncate(DateTime.UtcNow);

        foreach (var entry in ChangeTracker.Entries<RepairWorkItem>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.UpdatedAtUtc = now;
                continue;
            }

            if (entry.State != EntityState.Modified) continue;

            bool changed = entry.Properties.Any(p =>
                p.IsModified &&
                p.Metadata.Name != nameof(RepairWorkItem.UpdatedAtUtc));

            if (changed) entry.Entity.UpdatedAtUtc = now;
        }
    }

    /// <summary>بيقص التاريخ لأقرب مللي ثانية، والنوع يفضل UTC.</summary>
    private static DateTime Truncate(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, value.Kind);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SyncLoginKeys();
        StampRepairCursor();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SyncLoginKeys();
        StampRepairCursor();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
