# بوابة الـschema — إزاي نتأكد إن الكيانات مطابقة لقاعدة الإنتاج

## ليه البوابة دي موجودة

المشروع الجديد بيتوصّل على **نفس قاعدة الإنتاج** اللي المشروع القديم
شغّال عليها، و**مش بيعمل هجرات عليها**. المشروع القديم هو اللي بيملك
الـschema.

يعني: أي اختلاف بين الكيانات هنا والجداول الحقيقية = EF بيفتكر الـschema
غلط. والنتيجة مش رسالة خطأ — هي إن أول هجرة تتعمل بعدين هتحاول تعدّل
جدول فيه شغل ورشة حقيقي.

🔴 **طول نص واحد غلط بواحد كفاية.** جرّبناها: غيّرنا `MaxLength(60)`
لـ`(61)` على عمود واحد، وEF طلّع `AlterColumn` على طول.

## إزاي تشغّلها

**١ · خط الأساس — من القاعدة الحقيقية**

```bash
SCR=".../scratchpad"
sqlcmd -S localhost -E -d codlek_test -I -h-1 -W -i "$SCR/dump_schema.sql" \
  | grep -E "^[A-Za-z]" | sed 's/ *$//' | sort > real_schema.txt

sqlcmd -S localhost -E -d codlek_test -I -h-1 -W -i "$SCR/dump_indexes.sql" \
  | grep -E "^[A-Za-z]" | sed 's/ *$//' | sort > real_indexes.txt
```

⚠️ `codlek_test` هي اللي المشروع القديم بيهاجرها، فهي صورة مطابقة
للإنتاج. **متستعملش الإنتاج نفسها.**

**٢ · الشكل اللي EF فاهمه — في قاعدة معزولة**

```bash
sqlcmd -S localhost -E -I -Q "DROP DATABASE IF EXISTS codlek_shape; \
  CREATE DATABASE codlek_shape COLLATE Arabic_CI_AI"

rm -f Codlek.Infrastructure/Migrations/*.cs
dotnet build Codlek.Infrastructure/Codlek.Infrastructure.csproj -m:1
dotnet ef migrations add ShapeBaseline --project Codlek.Infrastructure --no-build
dotnet build Codlek.Infrastructure/Codlek.Infrastructure.csproj -m:1
dotnet ef database update --project Codlek.Infrastructure --no-build
```

🔴 **`codlek_shape` قاعدة فاضية مخصوصة للقياس.** مفيهاش بيانات، فأي
غلط هنا مايأذيش حاجة. و`DesignTimeDbContextFactory` بيوجّه كل أوامر
`ef` عليها افتراضياً عشان الغلط يبقى صعب.

**٣ · المقارنة**

```bash
diff real_schema.txt  new_schema.txt     # لازم يبقى فاضي
diff real_indexes.txt new_indexes.txt    # لازم يبقى فاضي
```

## النتيجة وقت ما اتعملت (٤ أكتوبر ٢٠٢٦)

| | الحقيقي | الجديد | |
|---|---|---|---|
| جداول | ٣١ | ٣١ | ✔ |
| أعمدة (اسم · نوع · طول · nullable) | ٤٠٣ | ٤٠٣ | **مطابق** |
| فهارس (أعمدة · فريد · فلتر) | ٦٨ | ٦٨ | **مطابق** |

## ⚠️ فخ لازم تعرفه

`dotnet ef migrations remove` بيشيل **آخر هجرة**، مش اللي إنت فاكر.
حصل هنا: شلت هجرة تجريبية فشال خط الأساس معاها. لو حصلت، امسح مجلد
`Migrations` كله وأعد من الخطوة ٢ — القاعدة معزولة فمفيش خسارة.

وفخ تاني: الأمر بيقول أحياناً «pending changes» بعد أول هجرة مباشرة.
اتأكد بإنك تعمل هجرة تجريبية تانية وتشوف `Up()` بتاعتها — لو فاضية،
الموديل مطابق والتحذير من تتابع الأوامر مش من فرق حقيقي.
