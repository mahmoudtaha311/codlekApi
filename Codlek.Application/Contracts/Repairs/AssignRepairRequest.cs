namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// جسم نقطة الإسناد — <b>حقل واحد</b>.
///
/// <para>⚠️ ومفيش حقل لتعدية الماركة هنا عن قصد: التعدية بتحصل من
/// نقطة الموافقة بس، وزيادتها هنا بتدّي للمدير سلطة المحاسب في
/// صمت.</para>
/// </summary>
public sealed record AssignRepairRequest(Guid TechnicianId);
