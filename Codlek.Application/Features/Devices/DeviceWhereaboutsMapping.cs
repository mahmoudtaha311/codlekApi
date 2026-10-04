using Codlek.Application.Contracts.Devices;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;

namespace Codlek.Application.Features.Devices;

/// <summary>
/// بناء «اللاب فين ومع مين» — <b>مكان واحد للقايمة وللصفحة</b>.
///
/// <para>🔴 <b>وده مش ترتيب كود.</b> القايمة والصفحة بيعرضوا نفس
/// اللوحة بالظبط، ولو كل واحدة حسبتها لوحدها بيبقى فيه نسختين من
/// قاعدة «فاضي = مش معروف مش صفر» و«وجود مكان = اتسلّم» — وأول
/// تعديل في واحدة بيخلّي الصفحة تقول حاجة والقايمة تقول غيرها عن
/// نفس اللاب.</para>
///
/// <para>⚠️ <b>وبتاخد قيم أساسية مش صف.</b> القايمة عندها
/// <c>DeviceListRow</c> والصفحة عندها الكيان نفسه — فلو الدالة
/// أخدت نوع صف، واحدة منهم كانت لازم تبني صف وهمي عشان تناديها.</para>
/// </summary>
internal static class DeviceWhereaboutsMapping
{
    public static DeviceWhereabouts Build(
        DeviceOperationalStage stage,
        DateTime? stageChangedAtUtc,
        Guid? currentLocationId,
        Guid? currentHolderTechnicianId,
        Guid? lastTestRackId,
        IReadOnlyDictionary<Guid, string> places,
        IReadOnlyDictionary<Guid, TechnicianLabel> holders,
        IReadOnlyDictionary<Guid, string> floors,
        DateTime nowUtc)
    {
        /*
          🔴 **فاضي = مش معروف، ومش صفر.**

          اللاب اللي مرحلته عمرها ما اتغيّرت مالوش «عمر مرحلة»؛
          والصفر معناه «اتغيّرت دلوقتي» — ودي حاجة تانية خالص.
          وتحويل المجهول لصفر بيخلّي اللاب الواقف من سنة يطلع أول
          القايمة في ترتيب «الأقدم».
        */
        double? ageHours = stageChangedAtUtc is { } since
            ? Math.Max(0, (nowUtc - since).TotalHours)
            : null;

        var holder = currentHolderTechnicianId is { } h && holders.TryGetValue(h, out var t)
            ? t
            : new TechnicianLabel("", "");

        return new DeviceWhereabouts(
            Stage: stage.ToString(),
            StageText: DeviceOperationalStageText.Arabic(stage),
            StageChangedAtUtc: stageChangedAtUtc,
            StageAgeHours: ageHours,
            LocationId: currentLocationId,
            LocationName: Text(places, currentLocationId),
            HolderTechnicianId: currentHolderTechnicianId,
            HolderTechnicianName: holder.Name,
            HolderTechnicianCode: holder.Code,

            // ⚠️ مكان الراكة اللي فحصته — سؤال مختلف عن «هو فين
            // دلوقتي». لو اللاب اتنقل بعد الفحص، `LocationName` فوق
            // هو الأحدث.
            TestedAtFloor: Text(floors, lastTestRackId),

            // 🔴 **ووجود مكان حالي هو علامة التسليم.**
            //
            // `CurrentLocationId` بيتكتب من مسار التسليم وبس، فوجود
            // مكان معناه عملياً «خرج من الورشة لجهة».
            //
            // ⚠️ وعلم مستقل مش استنتاج من المرحلة: التسليم لمخزن
            // **مابيغيّرش** المرحلة بقصد — مفيش قيمة مناسبة لمخزن،
            // و«جاهز» بتوصف الصلاحية مش المكان.
            HandedOver: currentLocationId != null);
    }

    private static string Text(IReadOnlyDictionary<Guid, string> map, Guid? id) =>
        id is { } key && map.TryGetValue(key, out var value) ? value : "";
}
