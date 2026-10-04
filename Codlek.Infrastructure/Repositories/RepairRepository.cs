using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class RepairRepository(AppDbContext db) : IRepairRepository
{
    // =================================================================
    //  الأمر
    // =================================================================

    public Task<RepairWorkItem?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.RepairWorkItems.FirstOrDefaultAsync(
            w => w.Id == id && w.TenantId == tenantId, ct);

    public Task<RepairWorkItem?> FindWithPartsAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.RepairWorkItems
            .Include(w => w.Parts)
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId, ct);

    public void Add(RepairWorkItem item) => db.RepairWorkItems.Add(item);

    // =================================================================
    //  الجهاز
    // =================================================================

    public Task<Device?> FindDeviceAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.TenantId == tenantId, ct);

    /// <summary>
    /// ⚠️ <c>!IsDeleted</c> جزء من السؤال مش ترشيح زيادة — أمر
    /// متعلّق بفحص اتمسح معناه سلسلة مقطوعة.
    /// </summary>
    public Task<bool> ReportExistsAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default) =>
        db.Reports.AnyAsync(
            r => r.Id == reportId && r.TenantId == tenantId && !r.IsDeleted, ct);

    // =================================================================
    //  الفني
    // =================================================================

    public Task<Technician?> FindTechnicianAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default) =>
        db.Technicians.FirstOrDefaultAsync(
            t => t.Id == technicianId && t.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<Technician>> AssignableTechniciansAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Technicians.AsNoTracking()
            // 🔴 نشط **و** يقدر يصلّح — الموقوف مايظهرش حتى لو قدرته
            // شغّالة. إسناد شغل لحد مش قادر يدخل معناه أمر بيقعد.
            .Where(t => t.TenantId == tenantId && t.IsActive && t.CanRepair)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<Guid>> TechnicianBrandsAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default) =>
        await db.TechnicianBrands.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TechnicianId == technicianId)
            .Select(x => x.BrandId)
            .ToListAsync(ct);

    // =================================================================
    //  الماركات
    // =================================================================

    public async Task<IReadOnlyList<BrandRule>> BrandRulesAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        /*
          ⚠️ **الماركات الموقوفة بتترجع برضه.**

          فني متسند لماركة اتوقفت لازم القيد بتاعه يفضل مفهوم — لو
          اختفت من القواعد، لابات الماركة دي كانت هتبقى «مش معروفة»
          فجأة وتعدّي من القيد في صمت.
        */
        var rows = await db.LaptopBrands.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                Aliases = x.Aliases.Select(a => a.RawValue).ToList(),
            })
            .ToListAsync(ct);

        return rows.Select(x => new BrandRule(x.Id, x.Name, x.Aliases)).ToList();
    }

    public async Task<string> DeviceManufacturerAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        await db.Devices.AsNoTracking()
            .Where(d => d.Id == deviceId && d.TenantId == tenantId)
            .Select(d => d.LastKnownManufacturer)
            .FirstOrDefaultAsync(ct) ?? "";

    // =================================================================
    //  الطابور
    // =================================================================

    public async Task<IReadOnlyList<RepairWorkItem>> PendingAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.RepairWorkItems.AsNoTracking()
            .Include(w => w.Device)
            .Include(w => w.AssignedTechnician)
            .Include(w => w.Parts)
            .Where(w => w.TenantId == tenantId && w.Approval == RepairApproval.Pending)

            // ⚠️ الأقدم الأول — دي طابور شغل مش قايمة أخبار.
            .OrderBy(w => w.OpenedAtUtc)
            .ToListAsync(ct);

    // =================================================================
    //  القايمة
    // =================================================================

    public async Task<(IReadOnlyList<RepairListRow> Rows, int TotalItems)> ListAsync(
        Guid tenantId, RepairListFilter filter, CancellationToken ct = default)
    {
        var q = db.RepairWorkItems.AsNoTracking().Where(w => w.TenantId == tenantId);

        /*
          ⚠️ **البحث بيجرّب الكود المضبوط الأول، وبعدين نص البحث.**

          المدير بيلزّق كود أمر كامل في الخانة. والمقارنة المضبوطة
          بتخلّيه يلاقي أمره **هو** بدل ما يلاقي كل أمر فيه الرقم ده
          جوّه وصف العطل.
        */
        if (filter.SearchPattern is { } pattern)
        {
            string exact = filter.ExactCode ?? "";

            q = q.Where(w =>
                w.PublicCode == exact
                || EF.Functions.Like(w.SearchText, pattern, SearchPattern.Escape));
        }

        /*
          🔴 **القيمة المش مفهومة اتجاهلت خلاص قبل ما توصل هنا.**

          الـHandler بيحوّل الحالة بـ`TryParse`، والفشل بيدّي `null` —
          يعني «مفيش فلتر». ولو كان بيرجّع ٤٠٠، الداش بورد كانت
          بتفضّي الشاشة على أي قيمة قديمة في الرابط.
        */
        if (filter.Status is { } status) q = q.Where(w => w.Status == status);
        if (filter.Approval is { } approval) q = q.Where(w => w.Approval == approval);

        if (filter.TechnicianId is { } technicianId)
            q = q.Where(w => w.AssignedTechnicianId == technicianId);

        if (filter.FromUtc is { } fromUtc) q = q.Where(w => w.OpenedAtUtc >= fromUtc);

        // 🔴 أصغر من، مش أصغر من أو يساوي — المدى نصف مفتوح.
        if (filter.ToUtc is { } toUtc) q = q.Where(w => w.OpenedAtUtc < toUtc);

        /*
          🔴 **العدّ بيتعمل على الاستعلام المفلتر وقبل التصفيح.**

          ولو اتعمل بعد `Skip/Take`، «٤٠ من ٤٠» كانت بتظهر على كل
          صفحة وأزرار التنقّل بتختفي.
        */
        int total = await q.CountAsync(ct);

        /*
          🔴 **`ThenBy(Id)` إجباري على الفرعين.**

          دفعة أوامر اتفتحت من نفس الفحص بتاخد نفس `OpenedAtUtc`
          بالمللي ثانية. ومن غير الفاصل، SQL Server **حر** يرتّبهم
          بأي شكل في كل استعلام — فالصف بيظهر في صفحة ١ وصفحة ٢، وصف
          تاني مابيظهرش خالص.

          ⚠️ **وده مابيبانش في فحص على عشرة صفوف:** الخطة واحدة في
          الاستعلامين فالترتيب بيطلع ثابت بالعرض. الفحص الحقيقي على
          القاعدة بيقرا جملة `ORDER BY` المولّدة ويتأكد إن فيها عمود
          فريد — راجع `RepairListRepositoryTests`.
        */
        q = filter.Oldest
            ? q.OrderBy(w => w.OpenedAtUtc).ThenBy(w => w.Id)
            : q.OrderByDescending(w => w.OpenedAtUtc).ThenBy(w => w.Id);

        var rows = await q
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(Row)
            .ToListAsync(ct);

        return (rows, total);
    }

    public Task<int> CountAwaitingApprovalAsync(
        Guid tenantId, CancellationToken ct = default) =>
        db.RepairWorkItems.AsNoTracking()
            .CountAsync(
                w => w.TenantId == tenantId && w.Approval == RepairApproval.Pending, ct);

    public async Task<IReadOnlyList<RepairListRow>> ListForDeviceAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        await db.RepairWorkItems.AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.DeviceId == deviceId)

            // ⚠️ ونفس الفاصل هنا كمان — الصفحة دي مش مصفّحة، بس
            // الترتيب الثابت هو اللي بيخلّي الشاشة ماترقصش مع كل
            // تحديث.
            .OrderByDescending(w => w.OpenedAtUtc).ThenBy(w => w.Id)
            .Select(Row)
            .ToListAsync(ct);

    /*
      🔴 **الإسقاط ده مكتوب مرة واحدة ومستعمل في المكانين.**

      القايمة وتبويب الجهاز لازم يدّوا **نفس** الأعمدة. ولو اتكتب
      مرتين، أول عمود يتزاد في واحدة بيخلّي الشاشتين يعرضوا بيانات
      مختلفة لنفس الأمر.

      ⚠️ ولازم يفضل `Expression` مخزّن: EF بيترجمه لـSQL، فأي نداء
      دالة عادية جوّاه بيرمي وقت التشغيل.
    */
    private static readonly System.Linq.Expressions.Expression<
        Func<RepairWorkItem, RepairListRow>> Row =
        w => new RepairListRow(
            w.Id,
            w.PublicCode,
            w.DeviceId,
            w.Device!.PublicCode,
            w.Device!.LastKnownManufacturer,
            w.Device!.CommercialModelName ?? "",
            w.Device!.LastKnownModel,
            w.Status,
            w.FaultSummary,
            w.AssignedTechnicianId,

            /*
              🔴 **الـ`?? ""` ده إجباري — وفحص على قاعدة حقيقية لقطه.**

              أمر من غير فني بيخلّي EF يعمل وصلة خارجية ترجّع `null`.
              والعمود في العقد `string` **غير قابل للعدم**، فالـ`null`
              بيتحشر جوّاه ويوصل للداش بورد — وأول `.Trim()` أو
              `.Length` عليه بيرمي.
            */
            w.AssignedTechnician!.DisplayName ?? "",

            w.OpenedAtUtc,
            w.StartedAtUtc,
            w.CompletedAtUtc,

            // 🔴 ونفس الحكاية: اللاب اللي مش في مكان مسجّل.
            w.Device!.CurrentLocation!.Name ?? "",

            // 🔴 العدد من SQL مش من الذاكرة — صفحة ٢٠٠ أمر كانت
            // بتسحب كل عطل وكل قطعة عشان نعدّهم.
            w.Issues.Count,
            w.Parts.Count,
            w.Approval);

    // =================================================================
    //  التفاصيل
    // =================================================================

    public Task<RepairWorkItem?> FindDetailAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.RepairWorkItems.AsNoTracking()
            .Include(w => w.Issues)
            .Include(w => w.Parts)
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<RepairWorkflowFacts>> ListWorkflowAsync(
        Guid tenantId, Guid workItemId, CancellationToken ct = default) =>
        await db.DeviceWorkflowEvents.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.RepairWorkItemId == workItemId)

            // ⚠️ بترتيب الحدوث مش بترتيب الوصول: الراكة بتبعت شغل
            // أوفلاين متأخر، و`Id` بيرتّبه غلط.
            .OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id)
            .Select(e => new RepairWorkflowFacts(
                e.EventType, e.ActorName, e.OccurredAtUtc, e.Reason))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> TechnicianNamesAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Technicians.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .ToDictionaryAsync(t => t.Id, t => t.DisplayName, ct);

    public Task<bool> DeviceExistsAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        db.Devices.AsNoTracking()
            .AnyAsync(d => d.Id == deviceId && d.TenantId == tenantId, ct);

    public Task<RepairDeviceFacts?> DeviceFactsAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        db.Devices.AsNoTracking()
            .Where(d => d.Id == deviceId && d.TenantId == tenantId)
            .Select(d => new RepairDeviceFacts(
                d.PublicCode,
                d.LastKnownManufacturer,
                d.CommercialModelName ?? "",
                d.LastKnownModel))
            .FirstOrDefaultAsync(ct);
}
