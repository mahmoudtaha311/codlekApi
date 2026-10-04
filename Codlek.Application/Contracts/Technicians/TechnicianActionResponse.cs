namespace Codlek.Application.Contracts.Technicians;

/// <summary>رد إجراء على حساب فني — من غير باسورد.</summary>
public sealed record TechnicianActionResponse(
    TechnicianAccount Technician,
    string Message);
