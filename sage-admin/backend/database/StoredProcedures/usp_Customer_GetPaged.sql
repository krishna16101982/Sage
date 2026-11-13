-- One page of customers (shared by all sales users) with how many active sales each has, plus @TotalCount OUTPUT.
-- @SortBy is one of: name, city, createdAtUtc (the API maps anything else to 'name').
-- @Search is a ready LIKE pattern with '\' as the escape character, or NULL.
CREATE OR ALTER PROCEDURE dbo.usp_Customer_GetPaged
    @Search         NVARCHAR(210) = NULL,
    @SortBy         VARCHAR(20)   = 'name',
    @SortDirection  VARCHAR(4)    = 'ASC',
    @Offset         INT           = 0,
    @PageSize       INT           = 20,
    @TotalCount     INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.Customers c
    WHERE c.IsActive = 1
      AND (@Search IS NULL OR c.Name LIKE @Search ESCAPE '\' OR c.ContactPerson LIKE @Search ESCAPE '\' OR c.Email LIKE @Search ESCAPE '\'
           OR c.Phone LIKE @Search ESCAPE '\' OR c.GSTIN LIKE @Search ESCAPE '\' OR c.City LIKE @Search ESCAPE '\')
    OPTION (RECOMPILE);

    SELECT c.PublicId, c.Name, c.ContactPerson, c.Email, c.Phone, c.GSTIN, c.Address, c.City, c.State, c.Country, c.CreatedAtUtc,
           CASE WHEN u.Id IS NULL THEN NULL ELSE CONCAT(u.FirstName, N' ', u.LastName) END AS CreatedByName,
           (SELECT COUNT(*) FROM dbo.Sales s WHERE s.CustomerId = c.Id AND s.Status = 'Active') AS SaleCount
    FROM dbo.Customers c
    LEFT JOIN dbo.SalesUsers u ON u.Id = c.CreatedBySalesUserId
    WHERE c.IsActive = 1
      AND (@Search IS NULL OR c.Name LIKE @Search ESCAPE '\' OR c.ContactPerson LIKE @Search ESCAPE '\' OR c.Email LIKE @Search ESCAPE '\'
           OR c.Phone LIKE @Search ESCAPE '\' OR c.GSTIN LIKE @Search ESCAPE '\' OR c.City LIKE @Search ESCAPE '\')
    ORDER BY
        CASE WHEN @SortBy = 'name'         AND @desc = 0 THEN c.Name         END ASC,
        CASE WHEN @SortBy = 'name'         AND @desc = 1 THEN c.Name         END DESC,
        CASE WHEN @SortBy = 'city'         AND @desc = 0 THEN c.City         END ASC,
        CASE WHEN @SortBy = 'city'         AND @desc = 1 THEN c.City         END DESC,
        CASE WHEN @SortBy = 'createdAtUtc' AND @desc = 0 THEN c.CreatedAtUtc END ASC,
        CASE WHEN @SortBy = 'createdAtUtc' AND @desc = 1 THEN c.CreatedAtUtc END DESC,
        c.Name ASC, c.Id ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
