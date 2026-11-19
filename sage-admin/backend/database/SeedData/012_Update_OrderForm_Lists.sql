-- New order form drop-downs (dbo.MasterValues), as agreed with the business:
--   Billing frequency: Annual, Half Yearly, Quarterly, Monthly
--   Billing method:    Pay Upfront, Pay in Arrears
--   Payment method:    Invoice, Credit card, Bank transfer
--   Payment term:      Immediate, Net 15, Net 30, Net 45, Net 60
--   Currency:          INR, USD, GBP, EUR
-- Listed values get these names and this order and are switched on (missing ones are added). Other values of these
-- lists are switched off (orders that used them keep them). Runs once; admins can change the lists afterwards in
-- Admin Panel > Settings > Order form lists.
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @lists TABLE (MasterType VARCHAR(30), Code VARCHAR(30), Name NVARCHAR(100), SortOrder INT, IsDefault BIT);
INSERT @lists VALUES
    ('BillingFrequency', 'ANNUAL',      N'Annual',         10, 1),
    ('BillingFrequency', 'HALF_YEARLY', N'Half Yearly',    20, 0),
    ('BillingFrequency', 'QUARTERLY',   N'Quarterly',      30, 0),
    ('BillingFrequency', 'MONTHLY',     N'Monthly',        40, 0),
    ('BillingMethod',    'UPFRONT',     N'Pay Upfront',    10, 1),
    ('BillingMethod',    'ARREARS',     N'Pay in Arrears', 20, 0),
    ('PaymentMethod',    'INVOICE',     N'Invoice',        10, 1),
    ('PaymentMethod',    'CARD',        N'Credit card',    20, 0),
    ('PaymentMethod',    'BANK',        N'Bank transfer',  30, 0),
    ('PaymentTerm',      'IMMEDIATE',   N'Immediate',      10, 1),
    ('PaymentTerm',      'NET_15',      N'Net 15',         20, 0),
    ('PaymentTerm',      'NET_30',      N'Net 30',         30, 0),
    ('PaymentTerm',      'NET_45',      N'Net 45',         40, 0),
    ('PaymentTerm',      'NET_60',      N'Net 60',         50, 0),
    ('Currency',         'INR',         N'INR',            10, 1),
    ('Currency',         'USD',         N'USD',            20, 0),
    ('Currency',         'GBP',         N'GBP',            30, 0),
    ('Currency',         'EUR',         N'EUR',            40, 0);

-- Defaults move first (one default per list), so the unique "one default" index is never broken.
UPDATE m SET IsDefault = 0, UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.MasterValues m
WHERE m.IsDefault = 1 AND m.MasterType IN (SELECT MasterType FROM @lists)
  AND NOT EXISTS (SELECT 1 FROM @lists l WHERE l.MasterType = m.MasterType AND l.Code = m.Code AND l.IsDefault = 1);

UPDATE m SET Name = l.Name, SortOrder = l.SortOrder, IsActive = 1, IsDefault = l.IsDefault, UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.MasterValues m
JOIN @lists l ON l.MasterType = m.MasterType AND l.Code = m.Code;

INSERT dbo.MasterValues (MasterType, Code, Name, SortOrder, IsDefault, IsActive)
SELECT l.MasterType, l.Code, l.Name, l.SortOrder, l.IsDefault, 1
FROM @lists l
WHERE NOT EXISTS (SELECT 1 FROM dbo.MasterValues m WHERE m.MasterType = l.MasterType AND m.Code = l.Code);

-- Values not in the agreed lists are switched off (still shown on the orders that used them).
UPDATE m SET IsActive = 0, IsDefault = 0, UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.MasterValues m
WHERE m.MasterType IN (SELECT MasterType FROM @lists)
  AND NOT EXISTS (SELECT 1 FROM @lists l WHERE l.MasterType = m.MasterType AND l.Code = m.Code);

COMMIT;
GO
