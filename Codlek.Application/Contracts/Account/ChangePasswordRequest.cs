namespace Codlek.Application.Contracts.Account;

/// <summary>
/// تغيير كلمة المرور.
///
/// <para>⚠️ <b>الحالية مطلوبة ومش شكليات:</b> لو حد سابك قاعد على
/// الموقع مفتوح، من غيرها يقدر يغيّر الباسورد ويقفل عليك حسابك.</para>
/// </summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);
