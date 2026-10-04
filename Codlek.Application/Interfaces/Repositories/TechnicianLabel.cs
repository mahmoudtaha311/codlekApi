namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// اسم فني وكوده.
///
/// <para>⚠️ الاتنين مع بعض: الاسم للعرض، والكود عشان الواجهة
/// تشاور على صفحة الفني (<c>/technicians/{code}</c>).</para>
/// </summary>
public sealed record TechnicianLabel(string Name, string Code);
