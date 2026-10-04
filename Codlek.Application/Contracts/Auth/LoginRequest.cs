namespace Codlek.Application.Contracts.Auth;

/// <summary>بيانات الدخول.</summary>
public sealed record LoginRequest(string Username, string Password);
