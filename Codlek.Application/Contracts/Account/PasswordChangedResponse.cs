namespace Codlek.Application.Contracts.Account;

/// <summary>
/// رد تغيير الباسورد — <b>ومعاه توكنات جديدة</b>.
///
/// <para>🔴 <b>التوكنات مش زيادة — من غيرها المستخدم بيتطرد بعد ما
/// ينجح.</b> تغيير الباسورد بيزوّد <c>CredentialVersion</c>، فتوكن
/// التجديد اللي معاه بقى على رقم قديم و<b>بيترفض</b>. والنظام القديم
/// كان بيحل ده بإعادة إصدار الكوكي؛ هنا بنرجّع توكنات جديدة.</para>
///
/// <para>⚠️ وفيه سبب تاني: التوكن القديم شايل ادعاء «الباسورد مؤقت».
/// من غير توكن جديد، المستخدم بيفضل محبوس على شاشة التغيير رغم إنه
/// غيّرها فعلاً.</para>
/// </summary>
public sealed record PasswordChangedResponse(
    string Message,
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds);
