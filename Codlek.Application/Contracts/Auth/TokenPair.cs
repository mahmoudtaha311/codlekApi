namespace Codlek.Application.Contracts.Auth;

/// <summary>التوكنين اللي الدخول بيرجّعهم.</summary>
/// <param name="AccessToken">قصير العمر — بيتبعت مع كل طلب.</param>
/// <param name="RefreshToken">أطول — بيتبدّل بواحد جديد لما الأول ينتهي.</param>
/// <param name="ExpiresInSeconds">عمر توكن الوصول، عشان العميل يعرف يجدّد قبله.</param>
public sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresInSeconds);
