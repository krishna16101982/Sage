-- The Currency list of the order form (DDL 030). The price list is in INR, so INR is the default. Added only when missing.
MERGE dbo.MasterValues AS t
USING (VALUES ('Currency', 'INR', N'INR', 10, 1)) AS s (MasterType, Code, Name, SortOrder, IsDefault)
ON t.MasterType = s.MasterType AND t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (MasterType, Code, Name, SortOrder, IsDefault) VALUES (s.MasterType, s.Code, s.Name, s.SortOrder, s.IsDefault);
GO
