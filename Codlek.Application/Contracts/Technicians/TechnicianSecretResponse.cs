namespace Codlek.Application.Contracts.Technicians;

/// <summary>
/// رد فيه الباسورد — <b>مرة واحدة وبس</b>.
///
/// <para>🔴 بيترجّع من الإنشاء ومن إعادة التعيين عشان المدير
/// يسلّمه للفني. وبعدها مش موجود في أي مكان: مش في القاعدة، مش في
/// السجل، ومش في أي رد تاني.</para>
/// </summary>
public sealed record TechnicianSecretResponse(
    TechnicianAccount Technician,
    string Password,
    string Message);
