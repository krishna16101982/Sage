-- Starting values for the order form lists (dbo.MasterValues) and its two number settings. Added only when missing, so
-- values an admin has changed or added are kept.
MERGE dbo.MasterValues AS t
USING (VALUES
    ('BillingFrequency', 'MONTHLY',     N'Monthly',        10, 0),
    ('BillingFrequency', 'QUARTERLY',   N'Quarterly',      20, 0),
    ('BillingFrequency', 'HALF_YEARLY', N'Half-yearly',    30, 0),
    ('BillingFrequency', 'ANNUAL',      N'Annual',         40, 1),
    ('BillingFrequency', 'ONE_TIME',    N'One-time',       50, 0),
    ('BillingMethod',    'UPFRONT',     N'Pay upfront',    10, 1),
    ('BillingMethod',    'ARREARS',     N'Pay in arrears', 20, 0),
    ('PaymentMethod',    'INVOICE',     N'Invoice',        10, 1),
    ('PaymentMethod',    'BANK',        N'Bank transfer',  20, 0),
    ('PaymentMethod',    'UPI',         N'UPI',            30, 0),
    ('PaymentMethod',    'CHEQUE',      N'Cheque',         40, 0),
    ('PaymentMethod',    'CARD',        N'Card',           50, 0),
    ('PaymentTerm',      'IMMEDIATE',   N'Immediate',      10, 1),
    ('PaymentTerm',      'NET_15',      N'Net 15',         20, 0),
    ('PaymentTerm',      'NET_30',      N'Net 30',         30, 0),
    ('PaymentTerm',      'NET_45',      N'Net 45',         40, 0),
    ('PaymentTerm',      'NET_60',      N'Net 60',         50, 0)
) AS s (MasterType, Code, Name, SortOrder, IsDefault)
ON t.MasterType = s.MasterType AND t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (MasterType, Code, Name, SortOrder, IsDefault) VALUES (s.MasterType, s.Code, s.Name, s.SortOrder, s.IsDefault);
GO

-- Admin Panel > Settings > Order form.
MERGE dbo.Settings AS t
USING (VALUES
    ('Sales.DefaultGstPercent', N'18', 'int', N'Order form', N'Default GST (%)',
        N'GST filled in on a new order form. Sales users can change it per order (0 to 28).', 10, 0, 28, NULL, NULL),
    ('Sales.MaxFreeMonths',     N'12', 'int', N'Order form', N'Most free months',
        N'Free months a sales user may add to a term at the same price (12 + 2 ...). 0 = no free months.', 20, 0, 24, NULL, NULL)
) AS s (SettingKey, SettingValue, ValueType, Category, Label, Description, SortOrder, MinValue, MaxValue, MaxLength, Pattern)
ON t.SettingKey = s.SettingKey
WHEN NOT MATCHED THEN
    INSERT (SettingKey, SettingValue, ValueType, Category, Label, Description, SortOrder, MinValue, MaxValue, MaxLength, Pattern)
    VALUES (s.SettingKey, s.SettingValue, s.ValueType, s.Category, s.Label, s.Description, s.SortOrder, s.MinValue, s.MaxValue, s.MaxLength, s.Pattern);
GO
