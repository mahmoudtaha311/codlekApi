namespace Codlek.Application.Contracts.Departments;

/// <summary>
/// صف قسم كما بيخرج للواجهة.
///
/// <para>🔴 <b>أسماء الحقول دي عقد.</b> الداش بورد الحالية بتقراها
/// بالأسماء دي بالظبط. أي تغيير = خانة فاضية في الشاشة من غير أي خطأ.</para>
/// </summary>
public sealed record DepartmentRow(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    int SortOrder,
    int TechnicianCount);
