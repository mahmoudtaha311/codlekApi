using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Departments;

public static class DepartmentErrors
{
    /// <summary>
    /// 🔴 <b>الاسم المكرر بيتمنع.</b>
    ///
    /// <para>قسمين بنفس الاسم معناهم إن المدير بيختار من قايمة فيها
    /// سطرين متطابقين — وبعدين الأرقام بتتقسم بينهم من غير ما حد ياخد
    /// باله.</para>
    /// </summary>
    public static Error NameTaken(string name) =>
        new("department.name_taken", $"فيه قسم اسمه «{name}» خلاص.", 400);

    public static readonly Error NotFound =
        new("department.not_found", "القسم ده مش موجود.", 404);
}
