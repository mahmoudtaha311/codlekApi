namespace Codlek.Application.Contracts.Rack;

/// <summary>
/// بلوك أكواد مؤجّر — <b>بالأرقام وبالأكواد المقروءة</b>.
///
/// <para>⚠️ <b>والاتنين في نفس الرد عن قصد:</b> الراكة بتحسب
/// بالأرقام (بتزيد واحد لكل لاب) وبتطبع الأكواد. وحسابها للكود من
/// الرقم بنفسها كان معناه نسختين من قاعدة الحشو — ولو اختلفوا،
/// الاستيكر المطبوع مابيطابقش اللي في القاعدة.</para>
/// </summary>
public sealed record DeviceCodeLeaseResponse(
    int FromNumber,
    int ToNumber,
    string FromCode,
    string ToCode,
    int Size);
