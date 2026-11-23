-- User Panel > Home (lead dashboard): every figure in one pass over the leads that are not deleted.
-- @Mine = 1 counts only the leads @ActorSalesUserId created, generated or is assigned to; 0 = every lead (everyone sees all).
-- One row per figure: Section, Id / Code / Name, SortOrder, Total (leads), Won (leads), Revenue (monthly expected revenue
-- of the Won leads, INR).
--   Total    one row: all leads, won leads, won revenue.
--   Status   one row per status (fixed list), zero rows included.
--   Source   one row per lead source with leads, plus "Not set".
--   Aging    < 7, 7-30, 31-60, > 60 days since the created date (India time).
--   Type     one row per lead type: total and won (win %).
--   Product  one row per product with won leads: won count and won revenue (the "product breakdown").
CREATE OR ALTER PROCEDURE dbo.usp_Lead_Summary
    @ActorSalesUserId  BIGINT,
    @Mine              BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @today DATE = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:30') AS DATE);
    DECLARE @won INT = (SELECT Id FROM dbo.LeadStatuses WHERE Code = 'WON');

    ;WITH l AS
    (
        SELECT ld.StatusId, ld.SourceId, ld.TypeId, ld.ProductId,
               CASE WHEN ld.StatusId = @won THEN 1 ELSE 0 END AS IsWon,
               CASE WHEN ld.StatusId = @won THEN ISNULL(ld.MonthlyExpectedRevenue, 0) ELSE 0 END AS WonRevenue,
               DATEDIFF(DAY, ld.LeadDate, @today) AS AgingDays
        FROM dbo.Leads ld
        WHERE ld.IsDeleted = 0
          AND (ISNULL(@Mine, 0) = 0 OR @ActorSalesUserId IN (ld.CreatedBySalesUserId, ld.GeneratedBySalesUserId, ld.AssignedSalesUserId))
    )
    SELECT 'Total' AS Section, 0 AS Id, CAST(NULL AS VARCHAR(20)) AS Code, N'All leads' AS Name, 0 AS SortOrder,
           COUNT(*) AS Total, ISNULL(SUM(IsWon), 0) AS Won, ISNULL(SUM(WonRevenue), 0) AS Revenue
    FROM l
    UNION ALL
    SELECT 'Status', s.Id, s.Code, s.Name, s.SortOrder, COUNT(l.StatusId), ISNULL(SUM(l.IsWon), 0), ISNULL(SUM(l.WonRevenue), 0)
    FROM dbo.LeadStatuses s LEFT JOIN l ON l.StatusId = s.Id
    GROUP BY s.Id, s.Code, s.Name, s.SortOrder
    UNION ALL
    SELECT 'Source', ISNULL(src.Id, 0), NULL, ISNULL(src.Name, N'Not set'), ISNULL(src.SortOrder, 100000), COUNT(*), SUM(l.IsWon), SUM(l.WonRevenue)
    FROM l LEFT JOIN dbo.LeadSources src ON src.Id = l.SourceId
    GROUP BY src.Id, src.Name, src.SortOrder
    UNION ALL
    SELECT 'Aging', b.Id, b.Code, b.Name, b.Id, COUNT(l.AgingDays), ISNULL(SUM(l.IsWon), 0), ISNULL(SUM(l.WonRevenue), 0)
    FROM (VALUES (1, 'lt7', N'< 7 days', -100000, 6), (2, '7to30', N'7-30 days', 7, 30),
                 (3, '31to60', N'31-60 days', 31, 60), (4, 'gt60', N'> 60 days', 61, 100000)) AS b (Id, Code, Name, MinDays, MaxDays)
    LEFT JOIN l ON l.AgingDays BETWEEN b.MinDays AND b.MaxDays
    GROUP BY b.Id, b.Code, b.Name
    UNION ALL
    SELECT 'Type', t.Id, t.Code, t.Name, t.SortOrder, COUNT(l.TypeId), ISNULL(SUM(l.IsWon), 0), ISNULL(SUM(l.WonRevenue), 0)
    FROM dbo.LeadTypes t LEFT JOIN l ON l.TypeId = t.Id
    GROUP BY t.Id, t.Code, t.Name, t.SortOrder
    UNION ALL
    SELECT 'Product', ISNULL(p.Id, 0), NULL, ISNULL(p.Name, N'Not set'), ISNULL(p.SortOrder, 100000), SUM(l.IsWon), SUM(l.IsWon), SUM(l.WonRevenue)
    FROM l LEFT JOIN dbo.LeadProducts p ON p.Id = l.ProductId
    WHERE l.IsWon = 1
    GROUP BY p.Id, p.Name, p.SortOrder
    ORDER BY Section, SortOrder, Name;
END
GO
