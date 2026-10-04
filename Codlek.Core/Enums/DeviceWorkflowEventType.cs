// 🔴 رقم مجمّد — الراكة بتقراه كرقم على السلك. اقرا Enums/README.md قبل أي تعديل.

namespace Codlek.Core.Enums;

/// <summary>نوع الحدث في سجل حركة الجهاز.</summary>
public enum DeviceWorkflowEventType
{
    StageChanged = 0,
    CustodyHandoff = 1,
    LocationMoved = 2,
    SentToRepair = 3,
    RepairStarted = 4,
    RepairCompleted = 5,
    DispatchedToSales = 6,
    DispatchedToPointOfSale = 7,
    ReturnReceived = 8,
    CodeChanged = 9
}
