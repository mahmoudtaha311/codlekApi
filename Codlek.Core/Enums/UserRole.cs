// 🔴 رقم مجمّد — الراكة بتقراه كرقم على السلك. اقرا Enums/README.md قبل أي تعديل.

namespace Codlek.Core.Enums;

/// <summary>
/// صلاحية حساب اللوحة.
///
/// <para>⚠️ <b>مفيش ترتيب بين الأدوار.</b> كل حاجز في النظام عضوية
/// صريحة في مجموعة، مش مقارنة. فالدور الجديد مابيرثش أي صلاحية.</para>
///
/// <para>⚠️ والأسماء دي بتتزرع في <c>AspNetRoles</c> بالحرف، عشان
/// <c>[Authorize(Roles = "Manager")]</c> يطابق. تغيير حرف في الاسم =
/// صلاحية بتترفض لحساب من حقه.</para>
/// </summary>
public enum UserRole
{
    Technician = 0,
    Manager = 1,
    Owner = 2,
    FloorManager = 3,
    Accountant = 4
}
