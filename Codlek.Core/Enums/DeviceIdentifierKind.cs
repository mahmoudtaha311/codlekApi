// 🔴 رقم مجمّد — الراكة بتقراه كرقم على السلك. اقرا Enums/README.md قبل أي تعديل.

namespace Codlek.Core.Enums;

/// <summary>نوع المرسى اللي الجهاز اتعرّف بيه.</summary>
public enum DeviceIdentifierKind
{
    SystemUuid = 0,
    BiosSerial = 1,
    BoardSerial = 2,
    DiskSerial = 3,
    MacAddress = 4,
    PanelEdidSerial = 5,
    BatterySerial = 6,
    CompanyCode = 7
}
