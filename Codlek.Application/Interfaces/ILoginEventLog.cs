using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces;

/// <summary>
/// سجل محاولات دخول اللوحة — <b>نفس جدول القديم</b> (<c>LoginEvents</c>).
///
/// <para>🔴 <b>ليه مهم:</b> ده الدليل الوحيد على تخمين باسورد — مين حاول،
/// من أنهي IP، وكام مرة. القديم بيسجّل كل محاولة من أول يوم، والمشروع
/// الجديد كان بيدخّل الناس من غير ولا سطر.</para>
///
/// <para>⚠️ <b>بيضيف ومابيحفظش</b> — المعالج بيحفظ.</para>
/// </summary>
public interface ILoginEventLog
{
    void Add(LoginEvent entry);
}
