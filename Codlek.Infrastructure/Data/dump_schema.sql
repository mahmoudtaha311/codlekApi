SET NOCOUNT ON;

-- شكل القاعدة الحقيقية، سطر لكل عمود، مرتّب — عشان المقارنة تبقى نصية.
SELECT
      t.name
    + '|' + c.name
    + '|' + ty.name
    + '|' + CASE
                WHEN ty.name IN ('nvarchar','varchar','nchar','char','varbinary','binary')
                    THEN CASE WHEN c.max_length = -1 THEN 'max'
                              WHEN ty.name IN ('nvarchar','nchar') THEN CAST(c.max_length / 2 AS varchar(10))
                              ELSE CAST(c.max_length AS varchar(10)) END
                WHEN ty.name IN ('decimal','numeric')
                    THEN CAST(c.precision AS varchar(10)) + ',' + CAST(c.scale AS varchar(10))
                ELSE ''
            END
    + '|' + CASE WHEN c.is_nullable = 1 THEN 'null' ELSE 'notnull' END
  AS line
FROM sys.columns c
JOIN sys.tables  t  ON t.object_id = c.object_id
JOIN sys.types   ty ON ty.user_type_id = c.user_type_id
WHERE t.name <> '__EFMigrationsHistory'
ORDER BY t.name, c.name;
