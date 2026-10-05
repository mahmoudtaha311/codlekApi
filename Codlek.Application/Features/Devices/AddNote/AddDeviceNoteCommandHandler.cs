using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Devices.AddNote;

/// <summary>
/// ملاحظة جديدة على لاب — <b>بتتضاف وبس</b>.
///
/// <para>🔴 <b>ومفيش متحقّق للأمر ده عن قصد — القواعد كلها هنا.</b>
/// المتحقّق بيشتغل <b>قبل</b> المعالج، والقديم بيحمّل اللاب الأول:
/// نص فاضي على لاب شركة تانية بياخد <c>404</c> مش <c>400</c>. لو
/// الطول والفراغ اتحطّوا في متحقّق، نفس الطلب ياخد رد مختلف من
/// الشاشتين.</para>
///
/// <para>⚠️ <b>ومفيش سجل مراجعة.</b> القديم ماكانش بيسجّل الملاحظة
/// — هي نفسها سجل، فيها الكاتب والوقت ومابتتعدّلش.</para>
/// </summary>
public sealed class AddDeviceNoteCommandHandler(
    IDeviceRepository devices,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<AddDeviceNoteCommand, Result<DeviceNoteItem>>
{
    public async Task<Result<DeviceNoteItem>> Handle(
        AddDeviceNoteCommand command, CancellationToken cancellationToken)
    {
        // 🔴 التقييد بالشركة قبل أي حاجة تانية. من غيره، مدير شركة
        //    يكتب على لاب شركة تانية بمعرّف متخمّن.
        //
        // ⚠️ والمدموج بيقبل ملاحظة — زي القديم: صفحته بتفتح، والتاب
        //    فيه الفورم.
        var device = await devices.FindDetailAsync(
            me.TenantId, command.DeviceId, cancellationToken);

        if (device is null)
            return Result.Failure<DeviceNoteItem>(DeviceErrors.NotFound);

        string text = (command.Body ?? "").Trim();

        if (text.Length == 0)
            return Result.Failure<DeviceNoteItem>(DeviceErrors.NoteEmpty);

        if (text.Length > DeviceNoteRules.MaxLength)
            return Result.Failure<DeviceNoteItem>(DeviceErrors.NoteTooLong);

        var note = new DeviceNote
        {
            TenantId = me.TenantId,
            DeviceId = device.Id,
            Body = text,
            CreatedAtUtc = DateTime.UtcNow,

            // 🔴 الكاتب من التوكن — مش من الجسم.
            CreatedByUserId = me.Id,

            // ⚠️ مقصوص على طول العمود: اسم أطول من ١٢٠ كان هيوقّع
            //    الحفظ باستثناء قص من SQL Server بدل ما الملاحظة تتسجّل.
            CreatedByName = TextClip.To(me.DisplayName, TextClip.Lengths.PersonName),
        };

        devices.AddNote(note);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new DeviceNoteItem(
            note.Id, note.Body, note.CreatedByName, note.CreatedAtUtc));
    }
}
