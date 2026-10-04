// 🔴 رقم مجمّد — الراكة بتقراه كرقم على السلك. اقرا Enums/README.md قبل أي تعديل.

namespace Codlek.Core.Enums;

/// <summary>حالة الراكة عند السيرفر.</summary>
public enum RackStatus
{
    PendingPairing = 0,
    Active = 1,
    Suspended = 2,
    Revoked = 3
}
