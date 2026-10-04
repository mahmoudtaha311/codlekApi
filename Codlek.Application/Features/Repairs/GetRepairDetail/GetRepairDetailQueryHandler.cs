using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Enums;
using Codlek.Core.Repairs;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetRepairDetail;

/// <summary>
/// شاشة أمر الصيانة الكاملة.
///
/// <para>🔴 <b>«الفني برّه ماركاته» بيتحسب وقت القراية مش بيتقرا من
/// عمود.</b> السؤال اللي الشاشة بتجاوب عليه هو «هل ده مخالف
/// <b>دلوقتي</b>» — مش «هل كان مخالف وقت الفتح». القواعد بتتغيّر:
/// ماركة بتتضاف لفني أو بتتشال منه، والشاشة لازم تقول الحقيقة
/// الحالية.</para>
/// </summary>
public sealed class GetRepairDetailQueryHandler(
    IRepairRepository repairs,
    ICurrentUser me)
    : IRequestHandler<GetRepairDetailQuery, Result<RepairDetail>>
{
    public async Task<Result<RepairDetail>> Handle(
        GetRepairDetailQuery query, CancellationToken cancellationToken)
    {
        var item = await repairs.FindDetailAsync(me.TenantId, query.Id, cancellationToken);

        if (item is null) return Result.Failure<RepairDetail>(RepairErrors.NotFound);

        /*
          ⚠️ **الجهاز الناقص مش خطأ — نصوص فاضية وخلاص.**

          أمر صيانة لجهاز اتمسح لسه ليه قيمة: هو سجل شغل اتعمل فعلاً
          وفيه قطع اتركّبت. ورمي ٤٠٤ هنا كان بيخبّي السجل كله.
        */
        var facts = await repairs.DeviceFactsAsync(
            me.TenantId, item.DeviceId, cancellationToken);

        /*
          🔴 **أسماء الفنيين قراية واحدة، مش قراية لكل معرّف.**

          الصف فيه فنيين (المتسند واللي قفل)، وكل واحد كان بيبقى
          استعلام — نفس شكل N+1 اللي كان في طابور المحاسب.
        */
        var names = await repairs.TechnicianNamesAsync(me.TenantId, cancellationToken);

        var workflow = await repairs.ListWorkflowAsync(
            me.TenantId, item.Id, cancellationToken);

        /*
          ⚠️ **الماركة بتتوحّد من اسم الشركة المصنّعة، مش من عمود
          ماركة.**

          اللاب مالوش عمود «ماركة»: اللي عنده هو
          <c>LastKnownManufacturer</c> خام من الفحص، والقواعد هي اللي
          بتردّه للاسم المعروف.
        */
        var rules = await repairs.BrandRulesAsync(me.TenantId, cancellationToken);

        var brand = BrandToken.Resolve(rules, facts?.Manufacturer ?? "");

        /*
          🔴 **وماركات الفني بتتقرا بس لو فيه فني متسند.**

          أمر من غير فني مالوش «مخالف»، والقراية دي استعلام زيادة على
          كل فتح شاشة.
        */
        bool outsideBrand = false;

        if (item.AssignedTechnicianId is { } technicianId)
        {
            var allowed = await repairs.TechnicianBrandsAsync(
                me.TenantId, technicianId, cancellationToken);

            outsideBrand = !RepairBrandGate.CanAssign(allowed, brand);
        }

        return Result.Success(new RepairDetail(
            Id: item.Id,
            PublicCode: item.PublicCode,
            DeviceId: item.DeviceId,
            DeviceCode: facts?.PublicCode ?? "",
            DeviceName: DeviceNaming.Display(
                facts?.Manufacturer, facts?.CommercialModelName, facts?.RawModel),

            // ⚠️ الموديل الخام جمب الاسم المرتّب عن قصد: الفني بيدوّر
            // بالخام اللي مكتوب على اللاب.
            DeviceRawModel: facts?.RawModel ?? "",

            SourceReportId: item.SourceReportId,
            RetestReportId: item.RetestReportId,

            Status: item.Status.ToString(),
            StatusText: RepairStatusRules.Text(item.Status),
            RequiredSpecialty: (int)item.RequiredSpecialty,
            RequiredSpecialtyText: TechnicianSpecialtyText.Arabic(item.RequiredSpecialty),

            /*
              🔴 **الزوجين دول بالترتيب ده بالظبط.**

              <c>Assigned*</c> وبعديه <c>CompletedBy*</c> — وكل واحد
              معرّف وبعديه اسم. قلب أي زوج بينسب الصيانة لفني غلط،
              والمترجم مابيشوفش حاجة.
            */
            AssignedTechnicianId: item.AssignedTechnicianId,
            AssignedTechnicianName: Name(names, item.AssignedTechnicianId),
            CompletedByTechnicianId: item.CompletedByTechnicianId,
            CompletedByTechnicianName: Name(names, item.CompletedByTechnicianId),

            OpenedByName: item.OpenedByName,

            OpenedAtUtc: item.OpenedAtUtc,
            ClaimedAtUtc: item.ClaimedAtUtc,
            StartedAtUtc: item.StartedAtUtc,
            CompletedAtUtc: item.CompletedAtUtc,
            DurationMs: RepairTiming.DurationMs(item.StartedAtUtc, item.CompletedAtUtc),

            FaultSummary: item.FaultSummary,
            RepairActions: item.RepairActions,
            Notes: item.Notes,
            OutcomeReason: item.OutcomeReason,

            Issues: item.Issues
                .Select(i => new RepairIssueItem(
                    /*
                      🔴 **الأسماء هنا مش أسماء الكيان.**

                      <c>Code</c> جاية من <c>IssueCode</c>
                      و<c>Title</c> من <c>IssueTitleSnapshot</c>.
                      والداش بورد بتقرا الاسمين دول، فأي إعادة تسمية
                      بتطلّع عمود فاضي في الشاشة من غير أي خطأ.
                    */
                    Code: i.IssueCode,
                    Title: i.IssueTitleSnapshot,
                    Category: i.Category,
                    CategoryText: RepairIssueCategoryText.Of(i.Category),
                    Resolved: i.Resolved))
                .ToList(),

            Parts: item.Parts
                .Select(p => new RepairPartItem(
                    Id: p.Id,
                    Name: p.Name,
                    InventoryCode: p.InventoryCode,
                    Quantity: p.Quantity,
                    SerialNumber: p.SerialNumber,
                    Notes: p.Notes))
                .ToList(),

            Workflow: workflow
                .Select(e => new RepairWorkflowItem(
                    EventType: e.EventType.ToString(),
                    EventTypeText: DeviceWorkflowEventTypeText.Of(e.EventType),

                    /*
                      🔴 **النصّين دول بيفضلوا فاضيين — وده مقصود.**

                      المشروع القديم بيحطّهم فاضيين في الإسقاط
                      ومابيعبّيهمش أبداً، والواجهة مابتعرضهمش.
                      تعبيتهم هنا بتزوّد عمودين مش متوقّعين — والأسوأ
                      إن حد يفتكر إنه صلّح حاجة وهو زوّد سطح.
                    */
                    FromStageText: "",
                    ToStageText: "",

                    ActorName: e.ActorName,
                    OccurredAtUtc: e.OccurredAtUtc,
                    Reason: e.Reason))
                .ToList(),

            Approval: (int)item.Approval,
            ApprovalText: RepairStatusRules.ApprovalText(item.Approval),
            ApprovedByName: item.ApprovedByName,
            ApprovalDecidedAtUtc: item.ApprovalDecidedAtUtc,
            ApprovalNote: item.ApprovalNote,

            /*
              🔴 **التلات بوول دول افتراضيهم كلهم <c>false</c>،
              ومابينهم نص واحد.**

              يعني قلب أي اتنين منهم بيترجم. و<c>BrandOverride</c>
              بالتحديد هو الدليل الوحيد إن المحاسب عدّى القاعدة —
              تعدية مابتتعرضش معناها إن القاعدة مالهاش أي معنى.
            */
            BrandOverride: item.BrandOverride,
            StartedWithoutApproval: item.StartedWithoutApproval,

            // ⚠️ فاضية معناها «الماركة مش في القايمة» — والقيد
            // مابيتطبّقش على اللاب ده خالص. الشاشة لازم تقول كده
            // صريح.
            Brand: brand.BrandId is null ? "" : brand.Name,

            TechnicianOutsideBrand: outsideBrand));
    }

    /// <summary>
    /// ⚠️ الفني المحذوف بيرجع باسم فاضي عشان الشاشة تفضل تفتح.
    /// </summary>
    private static string Name(IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id is { } key && names.TryGetValue(key, out var name) ? name : "";
}
