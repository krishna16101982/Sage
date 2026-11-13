-- Settings the system reads (edited on Admin Panel > Settings). Added only when missing, so admin changes are kept.
--   Sales.MaxDiscountPercent  usp_Sale_Create refuses a bigger discount; the sale page shows the limit.
--   Sales.SaleNumberPrefix    first part of new sale numbers (S-2026-000123).
--   Sales.MaxBackdateDays     how far in the past a sale's start date may be (User API).
MERGE dbo.Settings AS t
USING (VALUES
    ('Sales.MaxDiscountPercent', N'100', 'int',    N'Sales', N'Maximum discount (%)',
        N'The largest discount a sales user can give, as a percentage of the list amount. 100 = no limit.', 10, 0, 100, NULL, NULL),
    ('Sales.SaleNumberPrefix',   N'S',   'string', N'Sales', N'Sale number prefix',
        N'Start of every new sale number, e.g. S gives S-2026-000123. 1 to 5 capital letters. Existing sales keep their numbers.', 20, NULL, NULL, 5, '^[A-Z]{1,5}$'),
    ('Sales.MaxBackdateDays',    N'365', 'int',    N'Sales', N'Backdating limit (days)',
        N'How many days in the past a sale''s start date may be. 0 = from today only.', 30, 0, 3650, NULL, NULL)
) AS s (SettingKey, SettingValue, ValueType, Category, Label, Description, SortOrder, MinValue, MaxValue, MaxLength, Pattern)
ON t.SettingKey = s.SettingKey
WHEN NOT MATCHED THEN
    INSERT (SettingKey, SettingValue, ValueType, Category, Label, Description, SortOrder, MinValue, MaxValue, MaxLength, Pattern)
    VALUES (s.SettingKey, s.SettingValue, s.ValueType, s.Category, s.Label, s.Description, s.SortOrder, s.MinValue, s.MaxValue, s.MaxLength, s.Pattern);
GO
