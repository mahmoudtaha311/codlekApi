namespace Codlek.Core.Entities;

/// <summary>
/// الماركات اللي الفني ده بيصلّحها.
///
/// <para>🔴 <b>فاضي معناه «كل الماركات» مش «ولا ماركة».</b> كل فني
/// في الشركة ماركاته فاضية لحظة ما الميزة دي تنزل — والقراية
/// التانية، وهي الأقرب للذهن، كانت هتقفل على <b>كل</b> الفنيين في
/// نفس اللحظة.</para>
///
/// <para>⚠️ نفس شكل <c>RepairWorkItem.RequiredSpecialty == None</c>
/// اللي معناه «معروض على كل فني صيانة».</para>
///
/// <para>⚠️ <b>ومفيش علامة «ممنوع من كل حاجة»</b>: «الفني ده
/// مايشتغلش على أي لاب لحد ما أقول» مش قابلة للتعبير هنا — دي
/// <c>IsActive = false</c> على الفني نفسه.</para>
/// </summary>
public class TechnicianBrand
{
    public Guid TenantId { get; set; }

    public Guid TechnicianId { get; set; }
    public Technician? Technician { get; set; }

    public Guid BrandId { get; set; }
    public LaptopBrand? Brand { get; set; }
}
