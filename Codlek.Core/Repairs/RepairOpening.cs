using Codlek.Core.Enums;

namespace Codlek.Core.Repairs;

/// <summary>
/// حالة الأمر وقت الفتح — <b>الإسناد هو اللي بيعمل المسؤولية</b>.
/// </summary>
public static class RepairOpening
{
    /// <summary>
    /// ⚠️ مسنود من أول لحظة = «بانتظار الصيانة»؛ من غير إسناد =
    /// «جديدة» ومعروضة على كل فني مطابق. الفرق ده هو اللي بيخلّي
    /// «مين المسؤول دلوقتي» له إجابة، وهو اللي بيبني طابور غير
    /// المستلم على الراكة.
    /// </summary>
    public static RepairStatus StatusAtOpen(bool assigned) =>
        assigned ? RepairStatus.WaitingForRepair : RepairStatus.New;
}
