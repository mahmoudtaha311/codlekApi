namespace Codlek.Application.Contracts.Workflow;

/// <summary>
/// مين عمل الحركة.
///
/// <para>🔴 <b>التلات أنواع مش واحد.</b> المستخدم بيعمل الحركة من
/// اللوحة، والفني بيعملها من الراكة، والنظام بيعملها من مسار مزامنة
/// أو مهمة مجدولة. وسجل بيقول «النظام» على حركة فني بيخلّي السؤال
/// «مين سلّم اللاب» مالوش جواب.</para>
///
/// <para>⚠️ <b>والاسم لقطة مش ربط.</b> لو الموظف اتشال بعدين، الحركة
/// لازم تفضل بتقول مين عملها.</para>
/// </summary>
public sealed record WorkflowActor(string Type, Guid? UserId, Guid? TechnicianId, string Name)
{
    public static WorkflowActor User(Guid id, string name) => new("User", id, null, name);

    public static WorkflowActor Technician(Guid id, string name) =>
        new("Technician", null, id, name);

    public static WorkflowActor System(string name = "النظام") => new("System", null, null, name);
}
