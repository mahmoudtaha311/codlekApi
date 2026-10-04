// 🔴 رقم مجمّد — الراكة بتقراه كرقم على السلك. اقرا Enums/README.md قبل أي تعديل.

namespace Codlek.Core.Enums;

/// <summary>
/// قرار المحاسب — <b>محور مستقل عن <see cref="RepairStatus"/></b>.
///
/// <para>⚠️ أمر «مستني موافقة» ممكن يكون <c>New</c> أو
/// <c>WaitingForRepair</c>: الحالة بتقول اللاب فين، والموافقة بتقول
/// هل مسموح يتحرك.</para>
/// </summary>
public enum RepairApproval
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
