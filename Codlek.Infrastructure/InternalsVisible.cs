using System.Runtime.CompilerServices;

/*
  ⚠️ **الأجزاء النقية في كاتب الإكسل لازم تتقاس.**

  `SheetNames` و`Clean` و`Serial` و`ColumnName` دوال نقية، وكل
  واحدة فيهم بتمنع عطل بيخلّي إكسل يقول «الملف تالف» من غير أي
  إشارة لمكان المشكلة.

  ⚠️ وخليناهم `internal` مش `public`: دي تفاصيل صيغة الملف، ولو
  بقت سطح عام هيبقى فيه كود بره الكاتب بيحسب رقم تاريخ إكسل
  بنفسه.
*/
[assembly: InternalsVisibleTo("Codlek.Tests")]
