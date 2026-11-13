-- Admin Panel: sales totals per sales person (@GroupBy 'seller') or per manager (@GroupBy 'manager').
--   seller:  every sales user, with the sales they made.
--   manager: every sales manager, with their own sales plus their team's (the sale's ManagerId at the time of sale).
-- Amounts cover ACTIVE sales only; CancelledCount counts cancelled ones. @SoldFrom / @SoldTo filter on the recorded day.
-- People with no sales in the period are listed with zeros (deleted users only when they have sales in the period).
CREATE OR ALTER PROCEDURE dbo.usp_Sale_Summary
    @GroupBy   VARCHAR(10) = 'seller',
    @SoldFrom  DATE        = NULL,
    @SoldTo    DATE        = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @manager BIT = CASE WHEN @GroupBy = 'manager' THEN 1 ELSE 0 END;
    DECLARE @fromUtc DATETIME2(3) = CAST(@SoldFrom AS DATETIME2(3));
    DECLARE @toUtc DATETIME2(3) = DATEADD(DAY, 1, CAST(@SoldTo AS DATETIME2(3)));

    WITH agg AS
    (
        SELECT CASE WHEN @manager = 1 THEN ISNULL(s.ManagerId, s.SalesUserId) ELSE s.SalesUserId END AS UserId,
               SUM(CASE WHEN s.Status = 'Active' THEN 1 ELSE 0 END) AS SaleCount,
               SUM(CASE WHEN s.Status = 'Cancelled' THEN 1 ELSE 0 END) AS CancelledCount,
               SUM(CASE WHEN s.Status = 'Active' THEN s.Quantity ELSE 0 END) AS Quantity,
               SUM(CASE WHEN s.Status = 'Active' THEN s.ListAmount ELSE 0 END) AS ListAmount,
               SUM(CASE WHEN s.Status = 'Active' THEN s.DiscountAmount ELSE 0 END) AS DiscountAmount,
               SUM(CASE WHEN s.Status = 'Active' THEN s.SaleAmount ELSE 0 END) AS SaleAmount,
               SUM(CASE WHEN s.Status = 'Active' THEN s.CostAmount ELSE 0 END) AS CostAmount
        FROM dbo.Sales s
        WHERE (@fromUtc IS NULL OR s.CreatedAtUtc >= @fromUtc)
          AND (@toUtc IS NULL OR s.CreatedAtUtc < @toUtc)
        GROUP BY CASE WHEN @manager = 1 THEN ISNULL(s.ManagerId, s.SalesUserId) ELSE s.SalesUserId END
    )
    SELECT u.PublicId, CONCAT(u.FirstName, N' ', u.LastName) AS Name, u.Username, u.Role,
           CASE WHEN m.Id IS NULL THEN NULL ELSE CONCAT(m.FirstName, N' ', m.LastName) END AS ManagerName,
           (SELECT COUNT(*) FROM dbo.SalesUsers t WHERE t.ManagerId = u.Id AND t.IsDeleted = 0) AS TeamSize,
           ISNULL(a.SaleCount, 0), ISNULL(a.CancelledCount, 0), ISNULL(a.Quantity, 0),
           ISNULL(a.ListAmount, 0), ISNULL(a.DiscountAmount, 0), ISNULL(a.SaleAmount, 0), ISNULL(a.CostAmount, 0),
           ISNULL(a.SaleAmount, 0) - ISNULL(a.CostAmount, 0) AS Margin
    FROM dbo.SalesUsers u
    LEFT JOIN dbo.SalesUsers m ON m.Id = u.ManagerId
    LEFT JOIN agg a ON a.UserId = u.Id
    WHERE (a.UserId IS NOT NULL OR u.IsDeleted = 0)
      AND (@manager = 0 OR u.Role = 'SalesManager' OR a.UserId IS NOT NULL)
    ORDER BY ISNULL(a.SaleAmount, 0) DESC, u.FirstName, u.LastName;
END
GO
