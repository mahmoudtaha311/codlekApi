// 🔴 رقم مجمّد — الراكة بتقراه كرقم على السلك. اقرا Enums/README.md قبل أي تعديل.

namespace Codlek.Core.Enums;

/// <summary>مرحلة الجهاز في الورشة.</summary>
public enum DeviceOperationalStage
{
    Unknown = 0,
    Received = 1,
    Testing = 2,
    Tested = 3,
    NeedsRepair = 4,
    UnderRepair = 5,
    Ready = 6,
    WithSales = 7,
    AtPointOfSale = 8,
    Returned = 9
}
