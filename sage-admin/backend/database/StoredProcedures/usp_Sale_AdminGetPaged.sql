-- Admin Panel: one page of ALL sales, plus totals of the matching active sales.
--   @SellerPublicId:  sales made by one sales person (or manager).
--   @ManagerPublicId: manager-wise = the manager's own sales plus the sales of the people who reported to them when
--                     the sale was made (Sales.ManagerId).
-- @Search is a ready LIKE pattern ('\' escape) on sale number, customer and product; @Status Active / Cancelled / NULL;
-- @SoldFrom / @SoldTo filter on the day the sale was recorded (UTC). @SortBy: soldAt, saleNumber, customer, product,
-- seller, saleAmount, renewalDate. Column order is the same as the Admin API's single-sale query.
CREATE OR ALTER PROCEDURE dbo.usp_Sale_AdminGetPaged
    @SellerPublicId    UNIQUEIDENTIFIER = NULL,
    @ManagerPublicId   UNIQUEIDENTIFIER = NULL,
    @CustomerPublicId  UNIQUEIDENTIFIER = NULL,
    @Search            NVARCHAR(210)    = NULL,
    @Status            VARCHAR(20)      = NULL,
    @SoldFrom          DATE             = NULL,
    @SoldTo            DATE             = NULL,
    @SortBy            VARCHAR(20)      = 'soldAt',
    @SortDirection     VARCHAR(4)       = 'DESC',
    @Offset            INT              = 0,
    @PageSize          INT              = 20,
    @TotalCount        INT              OUTPUT,
    @TotalSaleAmount   DECIMAL(18,2)    OUTPUT,
    @TotalMargin       DECIMAL(18,2)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;
    DECLARE @sellerId BIGINT = NULL, @managerId BIGINT = NULL, @customerId BIGINT = NULL;
    IF @SellerPublicId IS NOT NULL SET @sellerId = ISNULL((SELECT Id FROM dbo.SalesUsers WHERE PublicId = @SellerPublicId), -1);
    IF @ManagerPublicId IS NOT NULL SET @managerId = ISNULL((SELECT Id FROM dbo.SalesUsers WHERE PublicId = @ManagerPublicId), -1);
    IF @CustomerPublicId IS NOT NULL SET @customerId = ISNULL((SELECT Id FROM dbo.Customers WHERE PublicId = @CustomerPublicId), -1);
    DECLARE @fromUtc DATETIME2(3) = CAST(@SoldFrom AS DATETIME2(3));
    DECLARE @toUtc DATETIME2(3) = DATEADD(DAY, 1, CAST(@SoldTo AS DATETIME2(3)));

    SELECT @TotalCount = COUNT(*),
           @TotalSaleAmount = ISNULL(SUM(CASE WHEN s.Status = 'Active' THEN s.SaleAmount END), 0),
           @TotalMargin = ISNULL(SUM(CASE WHEN s.Status = 'Active' THEN s.SaleAmount - s.CostAmount END), 0)
    FROM dbo.Sales s
    JOIN dbo.Customers cu ON cu.Id = s.CustomerId
    JOIN dbo.Products p ON p.Id = s.ProductId
    WHERE (@sellerId IS NULL OR s.SalesUserId = @sellerId)
      AND (@managerId IS NULL OR s.ManagerId = @managerId OR s.SalesUserId = @managerId)
      AND (@customerId IS NULL OR s.CustomerId = @customerId)
      AND (@Status IS NULL OR s.Status = @Status)
      AND (@fromUtc IS NULL OR s.CreatedAtUtc >= @fromUtc)
      AND (@toUtc IS NULL OR s.CreatedAtUtc < @toUtc)
      AND (@Search IS NULL OR s.SaleNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\' OR p.Name LIKE @Search ESCAPE '\')
    OPTION (RECOMPILE);

    SELECT s.PublicId, s.SaleNumber, cu.PublicId, cu.Name, c.Name, p.Name, pl.Name,
           su.PublicId, CONCAT(su.FirstName, N' ', su.LastName), su.Role,
           CASE WHEN mg.Id IS NULL THEN NULL ELSE CONCAT(mg.FirstName, N' ', mg.LastName) END,
           s.Quantity, s.UnitPrice, s.UnitCost, s.ListAmount, s.SaleAmount, s.DiscountAmount, s.CostAmount,
           s.DurationUnit, s.DurationCount, s.StartDate, s.RenewalDate, s.Status, s.Notes, s.CreatedAtUtc,
           s.CancelledAtUtc, CASE WHEN cb.Id IS NULL THEN NULL ELSE CONCAT(cb.FirstName, N' ', cb.LastName) END, s.CancelReason
    FROM dbo.Sales s
    JOIN dbo.Customers cu ON cu.Id = s.CustomerId
    JOIN dbo.ProductCategories c ON c.Id = s.CategoryId
    JOIN dbo.Products p ON p.Id = s.ProductId
    JOIN dbo.ProductPlans pl ON pl.Id = s.PlanId
    JOIN dbo.SalesUsers su ON su.Id = s.SalesUserId
    LEFT JOIN dbo.SalesUsers mg ON mg.Id = s.ManagerId
    LEFT JOIN dbo.SalesUsers cb ON cb.Id = s.CancelledBySalesUserId
    WHERE (@sellerId IS NULL OR s.SalesUserId = @sellerId)
      AND (@managerId IS NULL OR s.ManagerId = @managerId OR s.SalesUserId = @managerId)
      AND (@customerId IS NULL OR s.CustomerId = @customerId)
      AND (@Status IS NULL OR s.Status = @Status)
      AND (@fromUtc IS NULL OR s.CreatedAtUtc >= @fromUtc)
      AND (@toUtc IS NULL OR s.CreatedAtUtc < @toUtc)
      AND (@Search IS NULL OR s.SaleNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\' OR p.Name LIKE @Search ESCAPE '\')
    ORDER BY
        CASE WHEN @SortBy = 'soldAt'      AND @desc = 0 THEN s.CreatedAtUtc END ASC,
        CASE WHEN @SortBy = 'soldAt'      AND @desc = 1 THEN s.CreatedAtUtc END DESC,
        CASE WHEN @SortBy = 'saleNumber'  AND @desc = 0 THEN s.SaleNumber   END ASC,
        CASE WHEN @SortBy = 'saleNumber'  AND @desc = 1 THEN s.SaleNumber   END DESC,
        CASE WHEN @SortBy = 'customer'    AND @desc = 0 THEN cu.Name        END ASC,
        CASE WHEN @SortBy = 'customer'    AND @desc = 1 THEN cu.Name        END DESC,
        CASE WHEN @SortBy = 'product'     AND @desc = 0 THEN p.Name         END ASC,
        CASE WHEN @SortBy = 'product'     AND @desc = 1 THEN p.Name         END DESC,
        CASE WHEN @SortBy = 'seller'      AND @desc = 0 THEN su.FirstName   END ASC,
        CASE WHEN @SortBy = 'seller'      AND @desc = 1 THEN su.FirstName   END DESC,
        CASE WHEN @SortBy = 'saleAmount'  AND @desc = 0 THEN s.SaleAmount   END ASC,
        CASE WHEN @SortBy = 'saleAmount'  AND @desc = 1 THEN s.SaleAmount   END DESC,
        CASE WHEN @SortBy = 'renewalDate' AND @desc = 0 THEN s.RenewalDate  END ASC,
        CASE WHEN @SortBy = 'renewalDate' AND @desc = 1 THEN s.RenewalDate  END DESC,
        s.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
