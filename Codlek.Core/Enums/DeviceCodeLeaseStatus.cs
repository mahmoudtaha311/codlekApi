// 🔴 رقم مجمّد — الراكة بتقراه كرقم على السلك. اقرا Enums/README.md قبل أي تعديل.

namespace Codlek.Core.Enums;

/// <summary>حالة بلوك الأكواد المحجوز للراكة.</summary>
public enum DeviceCodeLeaseStatus
{
    Open = 0,
    Exhausted = 1,
    Cancelled = 2
}
