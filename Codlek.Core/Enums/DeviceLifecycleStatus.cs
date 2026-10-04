// 🔴 رقم مجمّد — الراكة بتقراه كرقم على السلك. اقرا Enums/README.md قبل أي تعديل.

namespace Codlek.Core.Enums;

/// <summary>عمر الجهاز في النظام — مكرر؟ مدموج؟ خارج الخدمة؟</summary>
public enum DeviceLifecycleStatus
{
    Active = 0,
    DuplicateSuspected = 1,
    Merged = 2,
    Retired = 3
}
