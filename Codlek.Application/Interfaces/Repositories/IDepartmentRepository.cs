using Codlek.Application.Contracts.Departments;
using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// أقسام الورشة.
///
/// <para>⚠️ <b>الواجهة ضيّقة عن قصد: الدوال اللي محتاجينها وبس.</b>
/// المستودع العام (<c>GetAll</c> · <c>Find</c> · <c>Where</c>) بيرجّع
/// الترشيح بالشركة لكل مكان نداء — وأول واحد ينساه يفتح بيانات شركة
/// تانية. الدوال دي بتاخد <c>tenantId</c> إجباري.</para>
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>
    /// القايمة مرتّبة، ومعاها عدد الفنيين في كل قسم.
    ///
    /// <para>⚠️ <b>الموقوفة بترجع برضه.</b> فني ممكن يكون متسند لقسم
    /// اتوقف، ولو القسم اختفى من القايمة اسمه كان هيبان فاضي في كل
    /// مكان من غير أي تفسير.</para>
    /// </summary>
    Task<IReadOnlyList<DepartmentRow>> ListAsync(Guid tenantId, CancellationToken ct = default);

    Task<Department?> FindAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// فيه قسم بنفس الاسم؟ — <b>الاسم المطبَّع، مش الخام</b>.
    /// </summary>
    /// <param name="exceptId">القسم اللي بنعدّله — عشان مايصطدمش بنفسه.</param>
    Task<bool> NameTakenAsync(
        Guid tenantId, string name, Guid? exceptId = null, CancellationToken ct = default);

    void Add(Department department);

    Task<int> CountTechniciansAsync(Guid departmentId, CancellationToken ct = default);
}
