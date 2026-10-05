# بيئة التجربة — موقع للسيرفر الجديد، وموقع للوحة، وقاعدة نسخة

> كله **تجربة**. الإنتاج (`CodlekWeb` على codlek.runasp.net) مابيتلمسش.

## الحاجات اللي بتتعمل على لوحة MonsterASP

1. **موقعين جداد**: واحد للسيرفر (مثلاً `api-test`) وواحد للوحة (`dash-test`).
2. **قاعدة جديدة** فيها نسخة من قاعدة الإنتاج (استرجاع من نسخة احتياطي).
   ⚠️ **«Upload BAK file» بيخزّن الملف في قايمة النسخ وبس** — القاعدة
   بتفضل زي ما هي لحد ما تدوس **Restore** جنب الملف. (حصلت يوم ٥
   أكتوبر: الملف اترفع والقاعدة فضلت فاضية.) التأكد: عدد الصفوف في
   `Users` بقى عدد حسابات الإنتاج.
3. **ملف النشر (Web Deploy)** لكل موقع — بيتحفظ **برّه المشروع**، لأن فيه
   باسورد الاستضافة.
4. **شهادة SSL لموقع اللوحة قبل أي دخول بحساب حقيقي.** من غيرها الصفحة
   بتتغيّر في السكة (الباسورد بيتقري)، والمتصفح بيقفل حاجات اللوحة محتاجاها
   (قفل تجديد التوكن بين التبويبات، والنسخ). و`dashboard.web.config` بيحوّل
   http لـhttps — **فمايترفعش قبل الشهادة**، وإلا كل زيارة بتروح لعنوان
   مابيردّش.

## ١ · القاعدة

النسخة اللي اترجعت لسه بشكل القديم. خطوات التحويل نفسها بتتعمل عليها
(وده بيجرّب التحويل الحقيقي على بيانات حقيقية):

```sql
-- أ) علامة الأساس
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
               WHERE MigrationId = '20261004005214_ShapeBaseline')
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20261004005214_ShapeBaseline', '9.0.0');
```

```bash
# ب) السكريبت — من الهجرات نفسها، وينفع يتعاد من غير ضرر
dotnet ef migrations script 20261004005214_ShapeBaseline --idempotent \
  --project Codlek.Infrastructure --startup-project Codlek.Api -o cutover.sql
```

والتأكد: عدد `AspNetUsers` = عدد `Users`.

⚠️ **ولو السيرفر كان شغّال على القاعدة قبل الاسترجاع، يتعمله إعادة تشغيل
بعد التحويل.** الأدوار بتتزرع عند الإقلاع بس، والاسترجاع بيقطع الاتصالات
المفتوحة.

## ٢ · السيرفر

```powershell
dotnet publish Codlek.Api\Codlek.Api.csproj -c Release -o C:\stage\api
```

`web.config` من `api.web.config.template` بالقيم الحقيقية جوّه
`C:\stage\api` (**مش جوّه المشروع**)، وأول نشر بيرفعه:

```powershell
.\deploy\deploy-site.ps1 -PublishProfile C:\stage\api-test.PublishSettings `
                         -Source C:\stage\api -IncludeWebConfig
```

🔴 **`Server__PublicBaseUrl` = عنوان موقع التجربة.** لو اتساب، أي راكة
تتسجّل على التجربة بتتقال «ارفع على الإنتاج».

النشر اللي بعد كده من غير `-IncludeWebConfig` — اللي على الموقع فيه الأسرار.

🔴 **`deploy-site.ps1` بيرفع على مواقع التجربة بس** — أرقامها مكتوبة جوّاه
(`$TestSites`)، وأي ملف نشر تاني بيترفض قبل ما يتصل. السبب إنه بيخلّي الموقع
نسخة طبق الأصل من المجلد: ملف نشر الإنتاج بالغلط كان هيمسح البرنامج الشغّال.
الإنتاج له `deploy-web.ps1` (بيضيف من غير ما يمسح) — شوف `CUTOVER.md`. وموقع
تجربة جديد بيتضاف للقايمة بالإيد وبقصد.

## ٣ · اللوحة

اللوحة لازم تعرف عنوان السيرفر **وقت البناء**:

```powershell
cd codlek-dashboard
$env:VITE_API_ORIGIN = "https://api-test.runasp.net"; npm run build
```

⚠️ **النشر بيخلّي الموقع نسخة طبق الأصل من المجلد** — أي ملف مش في المجلد
بيتمسح من الموقع. فالمجلد لازم يبقى شكل الموقع كله:

```
C:\stage\dash\web.config     ← نسخة من deploy\dashboard.web.config
C:\stage\dash\app\...        ← محتوى codlek-dashboard\dist
```

```powershell
.\deploy\deploy-site.ps1 -PublishProfile C:\stage\dash-test.PublishSettings `
                         -Source C:\stage\dash -IncludeWebConfig
```

## ٤ · التأكد

1. `https://api-test.../api/health` ← `ok: true`
2. `https://dash-test.../` ← صفحة الدخول، والدخول بحساب حقيقي
3. الصفحات بتفتح، والخروج بيرجّع لصفحة الدخول
4. `https://api-test.../d/LP-xxxxxxxx` ← بيفتح الجهاز على اللوحة

`-DryRun` على أي نشر بيقول هيتغيّر إيه من غير ما يغيّر حاجة.
