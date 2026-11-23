-- User Panel: one page of cancellation requests. A sales manager (@IsManager = 1) sees the requests on their team's
-- sales (the sale's ManagerId); a sales person sees their own requests. @Status: Pending / Approved / Rejected / NULL (all).
-- Waiting requests first, then newest first. Paging happens here, never in the browser.
CREATE OR ALTER PROCEDURE dbo.usp_SaleCancelRequest_GetPaged
    @SalesUserId  BIGINT,
    @IsManager    BIT,
    @Status       VARCHAR(20) = NULL,
    @Offset       INT         = 0,
    @PageSize     INT         = 20,
    @TotalCount   INT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.SaleCancelRequests r
    JOIN dbo.Sales s ON s.Id = r.SaleId
    WHERE (@Status IS NULL OR r.Status = @Status)
      AND ((@IsManager = 1 AND s.ManagerId = @SalesUserId) OR (@IsManager = 0 AND r.RequestedBySalesUserId = @SalesUserId))
    OPTION (RECOMPILE);

    SELECT r.PublicId, s.PublicId AS SalePublicId, s.SaleNumber, cu.Name AS CustomerName, p.Name AS ProductName, s.SaleAmount,
           rb.PublicId AS RequestedByPublicId, CONCAT(rb.FirstName, N' ', rb.LastName) AS RequestedByName,
           r.Reason, r.Status, r.RequestedAtUtc,
           CASE WHEN db.Id IS NULL THEN NULL ELSE CONCAT(db.FirstName, N' ', db.LastName) END AS DecidedByName,
           r.DecidedAtUtc, r.DecisionNote
    FROM dbo.SaleCancelRequests r
    JOIN dbo.Sales s ON s.Id = r.SaleId
    JOIN dbo.Customers cu ON cu.Id = s.CustomerId
    JOIN dbo.Products p ON p.Id = s.ProductId
    JOIN dbo.SalesUsers rb ON rb.Id = r.RequestedBySalesUserId
    LEFT JOIN dbo.SalesUsers db ON db.Id = r.DecidedBySalesUserId
    WHERE (@Status IS NULL OR r.Status = @Status)
      AND ((@IsManager = 1 AND s.ManagerId = @SalesUserId) OR (@IsManager = 0 AND r.RequestedBySalesUserId = @SalesUserId))
    ORDER BY CASE WHEN r.Status = 'Pending' THEN 0 ELSE 1 END, r.RequestedAtUtc DESC, r.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
