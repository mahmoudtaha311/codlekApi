using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Core.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Codlek.Infrastructure.Data;

/// <summary>
/// بيسجّل حركة جهاز، وبيحدّث الكاش عليه.
///
/// <para>⚠️ القرارات لكل حقل في <see cref="WorkflowFieldResolution"/> —
/// الكلاس ده بيستعلم ويكتب.</para>
/// </summary>
public sealed class DeviceWorkflowRecorder(
    AppDbContext db,
    IDeviceReference devices,
    ILogger<DeviceWorkflowRecorder> log) : IDeviceWorkflowRecorder
{
    public async Task<MoveResult> RecordAsync(
        Guid tenantId, WorkflowMove move, CancellationToken ct = default)
    {
        /*
          🔴 **الترجمة الأول — نفس سبب الحادثة.**

          الحركة بتيجي من راكة بمعرّفها المحلي، واللي ممكن يكون اتعرّف
          عليه كجهاز موجود بمعرّف تاني. والبحث المباشر في `Devices`
          كان بيرفض حركة لجهاز **واصل ومتعرّف عليه**.
        */
        Guid? canonical = await devices.ResolveAsync(tenantId, move.DeviceId, ct);

        var device = canonical is { } id
            ? await db.Devices.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct)
            : null;

        if (device is null) return MoveResult.Fail("الجهاز مش موجود.");

        /*
          🔴 **الجهاز المدموج مش لاب مستقل** — حركاته بتتسجّل على
          الكانوني.

          من غير الفحص ده، كود متقاعد مطبوع على ليبل بيفتح سجل حركة
          موازي لجهاز موجود، والسؤال «اللاب ده فين» بيبقى ليه
          إجابتين.
        */
        if (device.Status == DeviceLifecycleStatus.Merged)
            return MoveResult.Fail("الجهاز ده مدموج في جهاز تاني — سجّل الحركة على الكانوني.");

        /*
          ⚠️ **إعادة رفع نفس الحركة من راكة بعد انقطاع مش حركة جديدة.**

          والرد بنجاح ومعاه الصف القديم مقصود: الرفض بيخلّي الراكة
          تعيد المحاولة للأبد.
        */
        if (move.EventId is { } eventId)
        {
            var existing = await db.DeviceWorkflowEvents
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.EventId == eventId, ct);

            if (existing is not null)
            {
                log.LogInformation(
                    "حركة مكرّرة اتجاهلت — {EventId} على الجهاز {DeviceId}.",
                    eventId, device.Id);

                return MoveResult.Recorded(existing);
            }
        }

        // ⚠️ المرجع لازم يكون موجود — مفتاح أجنبي غلط بيرمي عند
        // الحفظ، والاستثناء ساعتها بيفصل الدفعة كلها.
        if (move.ToLocationId is { } locationId
            && !await db.Locations.AnyAsync(
                l => l.Id == locationId && l.TenantId == tenantId, ct))
        {
            return MoveResult.Fail("الموقع مش موجود.");
        }

        if (move.ToTechnicianId is { } technicianId
            && !await db.Technicians.AnyAsync(
                t => t.Id == technicianId && t.TenantId == tenantId, ct))
        {
            return MoveResult.Fail("الفني مش موجود.");
        }

        var now = DateTime.UtcNow;

        var row = new DeviceWorkflowEvent
        {
            EventId = move.EventId,
            TenantId = tenantId,
            DeviceId = device.Id,
            EventType = move.EventType,

            // ⚠️ «من» و«لـ» الاتنين متخزّنين: الحركة بتفسّر نفسها من
            // غير ما حد يقرا اللي قبلها.
            FromStage = device.OperationalStage,
            ToStage = WorkflowFieldResolution.ToStage(move.ToStage, device.OperationalStage),

            FromTechnicianId = device.CurrentHolderTechnicianId,
            ToTechnicianId = WorkflowFieldResolution.ToTechnician(
                move.ClearsHolder, move.ToTechnicianId, device.CurrentHolderTechnicianId),

            FromLocationId = device.CurrentLocationId,
            ToLocationId = WorkflowFieldResolution.ToLocation(
                move.ToLocationId, device.CurrentLocationId),

            ActorType = move.Actor.Type,
            ActorUserId = move.Actor.UserId,
            ActorTechnicianId = move.Actor.TechnicianId,
            ActorName = TextClip.To(move.Actor.Name, TextClip.Lengths.PersonName),

            /*
              ⚠️ **وقت الحركة ممكن يبقى أقدم من وقت التسجيل بأيام** —
              راكة اشتغلت أوفلاين. الاتنين متخزّنين عشان الفرق يفضل
              مقروء: «اتعمل إمتى» سؤال مختلف عن «وصلنا إمتى».
            */
            OccurredAtUtc = move.OccurredAtUtc ?? now,
            RecordedAtUtc = now,

            RepairWorkItemId = move.RepairWorkItemId,
            BaselineReportId = move.BaselineReportId,
            BaselineIsFresh = move.BaselineIsFresh,

            Reason = TextClip.To(move.Reason, TextClip.Lengths.Reason),
            Notes = move.Notes,
            ReceivedByName = TextClip.To(move.ReceivedByName, TextClip.Lengths.PersonName),
        };

        db.DeviceWorkflowEvents.Add(row);

        /*
          🔴 **الكاش بيتحدّث هنا وبس، جنب الحدث اللي بيفسّره.**

          الأعمدة دي على `Device` ملخّص لآخر حركة. ولو حد حدّثهم من
          مكان تاني، الجهاز بيقول «في الصيانة» ومفيش ولا حركة بتفسّر
          إزاي — والسؤال «مين سلّمه ومتى» بيبقى مالوش جواب.
        */
        if (WorkflowFieldResolution.StageChanges(move.ToStage, device.OperationalStage))
        {
            device.OperationalStage = move.ToStage!.Value;

            // ⚠️ بوقت **الحركة** مش بوقت التسجيل — راكة أوفلاين.
            device.StageChangedAtUtc = row.OccurredAtUtc;
        }

        if (move.ToLocationId is { } location) device.CurrentLocationId = location;

        var holder = WorkflowFieldResolution.HolderWrite(
            move.ClearsHolder, move.ToTechnicianId, out bool touchHolder);

        if (touchHolder) device.CurrentHolderTechnicianId = holder;

        return MoveResult.Recorded(row);
    }
}
