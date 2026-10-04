namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// فني ينفع يستلم صيانة — صف في المنسدلة.
///
/// <para>⚠️ <c>Specialty</c> رقم و<c>SpecialtyText</c> نصه — الاتنين
/// بيخرجوا مع بعض عن قصد. لو الواجهة ترجمت لوحدها، أول تخصص جديد
/// بيظهر بالإنجليزي في شاشة وبالعربي في التانية.</para>
/// </summary>
public sealed record RepairTechnicianOption(
    Guid Id,
    string Code,
    string DisplayName,
    int Specialty,
    string SpecialtyText);
