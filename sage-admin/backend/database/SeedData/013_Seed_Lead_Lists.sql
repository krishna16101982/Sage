-- 013: starting values of the lead lists (DDL 035). Admins change them later in Admin Panel > Settings > Lead lists.
-- Adds only missing values, so running it again changes nothing.
MERGE dbo.LeadProducts AS t
USING (VALUES (N'AWS', 1), (N'AZURE', 2), (N'BACKUP', 3), (N'COLOCATION', 4), (N'DLP', 5), (N'G-SUITE', 6), (N'GCP', 7),
              (N'LinkedIn Sales Nav', 8), (N'M365', 9), (N'TALLY ON CLOUD', 10), (N'i2k2 DC', 11)) AS s (Name, SortOrder)
    ON t.Name = s.Name
WHEN NOT MATCHED THEN INSERT (Name, SortOrder) VALUES (s.Name, s.SortOrder);
GO

MERGE dbo.LeadSources AS t
USING (VALUES (N'Cold Call', 1), (N'Email Campaign', 2), (N'Event', 3), (N'LinkedIn', 4), (N'Partner', 5),
              (N'Referral', 6), (N'Website', 7)) AS s (Name, SortOrder)
    ON t.Name = s.Name
WHEN NOT MATCHED THEN INSERT (Name, SortOrder) VALUES (s.Name, s.SortOrder);
GO

MERGE dbo.LeadBillingCycles AS t
USING (VALUES (N'Monthly', 1), (N'Quarterly', 2), (N'Half Yearly', 3), (N'Yearly', 4)) AS s (Name, SortOrder)
    ON t.Name = s.Name
WHEN NOT MATCHED THEN INSERT (Name, SortOrder) VALUES (s.Name, s.SortOrder);
GO
