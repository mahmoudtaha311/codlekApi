namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// «اللاب فين، ومع مين، وبقاله قد إيه».
///
/// <para>⚠️ <b>الأعمدة دي موجودة في القاعدة من زمان ومحدش كان
/// بيطلّعها</b> — فالمدير كان بيسأل «فين اللاب» ومفيش إجابة في أي
/// شاشة.</para>
/// </summary>
/// <param name="StageAgeHours">
/// 🔴 <c>null</c> = <b>مش معروف</b>، مش صفر.
///
/// <para>اللاب اللي مرحلته عمرها ما اتغيّرت مالوش «عمر مرحلة»؛
/// والصفر معناه «اتغيّرت دلوقتي» — ودي حاجة تانية خالص. وتحويل
/// المجهول لصفر بيخلّي اللاب الواقف من سنة يطلع أول القايمة في
/// ترتيب «الأقدم».</para>
/// </param>
/// <param name="TestedAtFloor">
/// ⚠️ مكان <b>الراكة</b> اللي فحصته آخر مرة — بيجاوب «اتفحص في
/// أنهي دور»، وده سؤال مختلف عن «هو فين دلوقتي».
/// </param>
/// <param name="HandedOver">
/// ⚠️ وجود مكان حالي هو العلامة — مفيش عمود «اتسلّم».
/// </param>
public sealed record DeviceWhereabouts(
    string Stage,
    string StageText,
    DateTime? StageChangedAtUtc,
    double? StageAgeHours,
    Guid? LocationId,
    string LocationName,
    Guid? HolderTechnicianId,
    string HolderTechnicianName,
    string HolderTechnicianCode,
    string TestedAtFloor,
    bool HandedOver);
