using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Rack;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Rack.LeaseDeviceCodes;

/// <summary>
/// بلوك أكواد أجهزة لراكة.
///
/// <para>🔴 <b>والبلوك القديم بيتقفل <u>من غير ما يرجع
/// للمساحة</u></b> — أي أرقام متستخدمتش فيه بتفضل فجوة للأبد. وده
/// مقصود: الراكة ممكن تكون طبعت ليبل برقم من البلوك القديم وهي
/// أوفلاين، فإعادة الرقم للمساحة معناها استيكرين بنفس الكود على
/// لابين مختلفين.</para>
/// </summary>
public sealed class LeaseDeviceCodesCommandHandler(
    IDeviceCodeLeaseRepository leases,
    ITenantCounters counters,
    IUnitOfWork unitOfWork,
    ILogger<LeaseDeviceCodesCommandHandler> log)
    : IRequestHandler<LeaseDeviceCodesCommand, Result<DeviceCodeLeaseResponse>>
{
    /// <summary>
    /// ⚠️ <b>اسم العدّاد مفتاح مخزّن.</b> تغييره بيرجّع الترقيم
    /// لواحد <b>ويكرّر أكواد مطبوعة على ورق</b>.
    /// </summary>
    private const string Counter = "device";

    public async Task<Result<DeviceCodeLeaseResponse>> Handle(
        LeaseDeviceCodesCommand command, CancellationToken cancellationToken)
    {
        /*
          ⚠️ **الراكة بتقول لحد فين استهلكت قبل ما تاخد بلوك جديد.**

          وده بيتسجّل **قبل** القفل عشان البلوك المقفول يفضل شايل
          آخر رقم اتستخدم فعلاً — الرقم ده هو اللي بيجاوب «الفجوة
          دي من فين» بعد شهور.
        */
        if (command.ConsumedThrough is { } consumed && consumed > 0)
        {
            var open = await leases.ContainingAsync(
                command.TenantId, command.RackId, consumed, cancellationToken);

            /*
              🔴 **الرقم بيزيد وبس.**

              رد قديم وصل متأخر (الراكة بعتت مرتين والشبكة قلبت
              الترتيب) مش هيرجّع العدّاد لورا — والرجوع معناه إن
              أرقام اتستخدمت تتحسب فاضية وتتوزّع تاني.
            */
            if (open is not null && consumed > open.ConsumedThrough)
                open.ConsumedThrough = Math.Min(consumed, open.ToNumber);
        }

        /*
          🔴 **كل بلوكات الراكة المفتوحة بتتقفل.**

          بلوك واحد مفتوح في المرة: الراكة بتوزّع من اللي معاها،
          وبلوكين مفتوحين معناهم إنها ممكن توزّع من القديم بعد ما
          اتقفل في دماغها.
        */
        var others = await leases.OpenForRackAsync(
            command.TenantId, command.RackId, cancellationToken);

        foreach (var lease in others)
        {
            lease.Status = DeviceCodeLeaseStatus.Exhausted;
            lease.ClosedAtUtc = DateTime.UtcNow;
            lease.ClosedReason = "اتقفل عشان بلوك جديد اتاخد";
        }

        int size = DeviceCodeBlock.Size(command.Size);

        /*
          🔴 **الحجز ذرّي: العدّاد بيزيد بحجم البلوك في جملة
          واحدة.**

          فراكتين بيطلبوا في نفس اللحظة بياخدوا مديين مختلفين
          ومتصلين. و`MAX(number) + size` كانت بتتسابق: الاتنين
          بيقروا نفس الرقم وبياخدوا نفس المدى.
        */
        int start = await counters.ReserveAsync(
            command.TenantId, Counter, size, cancellationToken);

        var issued = new DeviceCodeLease
        {
            TenantId = command.TenantId,
            RackId = command.RackId,
            FromNumber = start,
            ToNumber = start + size - 1,

            // ⚠️ أقل من أول رقم بواحد = «لسه ماستهلكتش حاجة».
            ConsumedThrough = start - 1,

            Status = DeviceCodeLeaseStatus.Open,
        };

        leases.Add(issued);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "بلوك أكواد اتأجّر لراكة {Rack}: {From}–{To}",
            command.RackId,
            DeviceCodeBlock.Format(issued.FromNumber),
            DeviceCodeBlock.Format(issued.ToNumber));

        return Result.Success(new DeviceCodeLeaseResponse(
            FromNumber: issued.FromNumber,
            ToNumber: issued.ToNumber,
            FromCode: DeviceCodeBlock.Format(issued.FromNumber),
            ToCode: DeviceCodeBlock.Format(issued.ToNumber),

            // ⚠️ الحجم محسوب من المدى مش من اللي اتطلب — اللي اتطلب
            //    ممكن يكون اتقص.
            Size: issued.ToNumber - issued.FromNumber + 1));
    }
}
