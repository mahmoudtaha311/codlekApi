namespace Codlek.Application.Interfaces;

/// <summary>
/// سجل الإجراءات الإدارية.
///
/// <para>⚠️ <b>بيضيف الصف ومابيحفظش.</b> الحفظ مسؤولية
/// <see cref="IUnitOfWork"/> — عشان السجل والتغيير نفسه ينزلوا في
/// حفظة واحدة. لو كل واحد حفظ لوحده، وقوع القاعدة بينهم بيدّي تغيير
/// من غير سجل، أو سجل لتغيير مااتمش.</para>
/// </summary>
public interface IAuditTrail
{
    void Record(
        string action,
        string entityType,
        Guid? entityId,
        string entityCode,
        string summary);
}
