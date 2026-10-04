SET NOCOUNT ON;

-- الفهارس: اسم الجدول | الأعمدة بالترتيب | فريد؟ | الفلتر
-- ⚠️ الاسم نفسه مش مهم — المهم الشكل. فهرس باسم مختلف على نفس
--    الأعمدة بنفس الفلتر هو نفس الفهرس.
SELECT
      t.name
    + '|' + STUFF((
          SELECT ',' + c2.name + CASE WHEN ic2.is_descending_key = 1 THEN ' DESC' ELSE '' END
          FROM sys.index_columns ic2
          JOIN sys.columns c2
            ON c2.object_id = ic2.object_id AND c2.column_id = ic2.column_id
          WHERE ic2.object_id = i.object_id
            AND ic2.index_id = i.index_id
            AND ic2.is_included_column = 0
          ORDER BY ic2.key_ordinal
          FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, '')
    + '|' + CASE WHEN i.is_unique = 1 THEN 'unique' ELSE 'plain' END
    + '|' + ISNULL(i.filter_definition, '')
  AS line
FROM sys.indexes i
JOIN sys.tables  t ON t.object_id = i.object_id
WHERE t.name <> '__EFMigrationsHistory'
  AND i.type_desc <> 'HEAP'
  AND i.is_primary_key = 0
ORDER BY t.name, i.name;
