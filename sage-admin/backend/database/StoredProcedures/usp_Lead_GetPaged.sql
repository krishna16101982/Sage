-- User Panel > Lead Records: one page of leads (every sales user sees every lead that is not deleted), or one lead
-- (@LeadPublicId). Paging, filters and sorting all happen here, never in the browser.
--   @Search: a ready LIKE pattern ('\' escape) matched against lead number, company, client names and people's names.
--   @StatusId / @ProductId / @AssignedPublicId / @GeneratedByPublicId: exact filters (NULL = all).
--   @Aging: days since the lead's created date (India time): 'lt7' (< 7), '7to30', '31to60', 'gt60' (> 60).
--   @From / @To: the lead's created date. @SortBy: leadNumber (default), leadDate, company, revenue, aging.
-- For @ActorSalesUserId each row also says what that person may do:
--   CanEdit   the creator or the creator's manager;   CanDelete   CanEdit and a sales manager;
--   CanOrder  a Won lead with no order yet, for CanEdit or the assigned person.
CREATE OR ALTER PROCEDURE dbo.usp_Lead_GetPaged
    @ActorSalesUserId     BIGINT,
    @LeadPublicId         UNIQUEIDENTIFIER = NULL,
    @Search               NVARCHAR(210)    = NULL,
    @StatusId             INT              = NULL,
    @ProductId            INT              = NULL,
    @AssignedPublicId     UNIQUEIDENTIFIER = NULL,
    @GeneratedByPublicId  UNIQUEIDENTIFIER = NULL,
    @Aging                VARCHAR(10)      = NULL,
    @From                 DATE             = NULL,
    @To                   DATE             = NULL,
    @WithoutOrder         BIT              = 0,      -- 1: only leads no order was made from yet (the order form's lead picker)
    @SortBy               VARCHAR(20)      = 'leadNumber',
    @SortDirection        VARCHAR(4)       = 'DESC',
    @Offset               INT              = 0,
    @PageSize             INT              = 20,
    @TotalCount           INT              OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;
    DECLARE @today DATE = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:30') AS DATE);
    DECLARE @isManager BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @ActorSalesUserId AND Role = 'SalesManager') THEN 1 ELSE 0 END;
    DECLARE @assignedId BIGINT = NULL, @generatedById BIGINT = NULL;
    IF @AssignedPublicId IS NOT NULL
        SET @assignedId = ISNULL((SELECT Id FROM dbo.SalesUsers WHERE PublicId = @AssignedPublicId), -1);      -- unknown: empty result
    IF @GeneratedByPublicId IS NOT NULL
        SET @generatedById = ISNULL((SELECT Id FROM dbo.SalesUsers WHERE PublicId = @GeneratedByPublicId), -1);
    -- Aging bucket as a range of created dates.
    DECLARE @agingFrom DATE = CASE @Aging WHEN 'lt7' THEN DATEADD(DAY, -6, @today) WHEN '7to30' THEN DATEADD(DAY, -30, @today)
                                          WHEN '31to60' THEN DATEADD(DAY, -60, @today) END;
    DECLARE @agingTo DATE = CASE @Aging WHEN '7to30' THEN DATEADD(DAY, -7, @today) WHEN '31to60' THEN DATEADD(DAY, -31, @today)
                                        WHEN 'gt60' THEN DATEADD(DAY, -61, @today) END;

    ;WITH matching AS
    (
        SELECT l.Id
        FROM dbo.Leads l
        JOIN dbo.Customers cu ON cu.Id = l.CustomerId
        JOIN dbo.SalesUsers gb ON gb.Id = l.GeneratedBySalesUserId
        JOIN dbo.SalesUsers asg ON asg.Id = l.AssignedSalesUserId
        WHERE l.IsDeleted = 0
          AND (@LeadPublicId IS NULL OR l.PublicId = @LeadPublicId)
          AND (@StatusId IS NULL OR l.StatusId = @StatusId)
          AND (@ProductId IS NULL OR l.ProductId = @ProductId)
          AND (@assignedId IS NULL OR l.AssignedSalesUserId = @assignedId)
          AND (@generatedById IS NULL OR l.GeneratedBySalesUserId = @generatedById)
          AND (@agingFrom IS NULL OR l.LeadDate >= @agingFrom)
          AND (@agingTo IS NULL OR l.LeadDate <= @agingTo)
          AND (@From IS NULL OR l.LeadDate >= @From)
          AND (@To IS NULL OR l.LeadDate <= @To)
          AND (ISNULL(@WithoutOrder, 0) = 0 OR l.SaleId IS NULL)
          AND (@Search IS NULL OR l.LeadNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\'
               OR l.ClientName LIKE @Search ESCAPE '\' OR l.SecondaryClientName LIKE @Search ESCAPE '\'
               OR CONCAT(gb.FirstName, N' ', gb.LastName) LIKE @Search ESCAPE '\'
               OR CONCAT(asg.FirstName, N' ', asg.LastName) LIKE @Search ESCAPE '\')
    )
    SELECT @TotalCount = COUNT(*) FROM matching
    OPTION (RECOMPILE);

    SELECT l.PublicId, l.LeadNumber, cu.PublicId AS CustomerPublicId, cu.Name AS CompanyName,
           l.ClientName, l.ClientDesignation, l.ClientEmail, l.ClientPhone,
           l.SecondaryClientName, l.SecondaryClientDesignation, l.SecondaryClientEmail, l.SecondaryClientPhone,
           l.ProductId, pr.Name AS ProductName, l.SourceId, so.Name AS SourceName, l.Requirement,
           l.StatusId, st.Code AS StatusCode, st.Name AS StatusName, l.TypeId, ty.Name AS TypeName,
           gb.PublicId AS GeneratedByPublicId, CONCAT(gb.FirstName, N' ', gb.LastName) AS GeneratedByName,
           asg.PublicId AS AssignedPublicId, CONCAT(asg.FirstName, N' ', asg.LastName) AS AssignedName,
           l.MonthlyExpectedRevenue, l.BillingCycleId, bc.Name AS BillingCycleName,
           l.NextFollowUpDate, l.LeadDate, l.ClosingDate, l.Remarks,
           DATEDIFF(DAY, l.LeadDate, @today) AS AgingDays,
           s.PublicId AS SalePublicId, s.SaleNumber,
           CONCAT(cb.FirstName, N' ', cb.LastName) AS CreatedByName, l.CreatedAtUtc, l.UpdatedAtUtc,
           CAST(x.CanEdit AS BIT) AS CanEdit,
           CAST(CASE WHEN x.CanEdit = 1 AND @isManager = 1 THEN 1 ELSE 0 END AS BIT) AS CanDelete,
           CAST(CASE WHEN st.Code = 'WON' AND l.SaleId IS NULL AND (x.CanEdit = 1 OR l.AssignedSalesUserId = @ActorSalesUserId) THEN 1 ELSE 0 END AS BIT) AS CanOrder
    FROM dbo.Leads l
    JOIN dbo.Customers cu ON cu.Id = l.CustomerId
    JOIN dbo.SalesUsers gb ON gb.Id = l.GeneratedBySalesUserId
    JOIN dbo.SalesUsers asg ON asg.Id = l.AssignedSalesUserId
    JOIN dbo.SalesUsers cb ON cb.Id = l.CreatedBySalesUserId
    JOIN dbo.LeadStatuses st ON st.Id = l.StatusId
    JOIN dbo.LeadTypes ty ON ty.Id = l.TypeId
    LEFT JOIN dbo.LeadProducts pr ON pr.Id = l.ProductId
    LEFT JOIN dbo.LeadSources so ON so.Id = l.SourceId
    LEFT JOIN dbo.LeadBillingCycles bc ON bc.Id = l.BillingCycleId
    LEFT JOIN dbo.Sales s ON s.Id = l.SaleId
    CROSS APPLY (SELECT CASE WHEN l.CreatedBySalesUserId = @ActorSalesUserId OR cb.ManagerId = @ActorSalesUserId THEN 1 ELSE 0 END AS CanEdit) x
    WHERE l.IsDeleted = 0
      AND (@LeadPublicId IS NULL OR l.PublicId = @LeadPublicId)
      AND (@StatusId IS NULL OR l.StatusId = @StatusId)
      AND (@ProductId IS NULL OR l.ProductId = @ProductId)
      AND (@assignedId IS NULL OR l.AssignedSalesUserId = @assignedId)
      AND (@generatedById IS NULL OR l.GeneratedBySalesUserId = @generatedById)
      AND (@agingFrom IS NULL OR l.LeadDate >= @agingFrom)
      AND (@agingTo IS NULL OR l.LeadDate <= @agingTo)
      AND (@From IS NULL OR l.LeadDate >= @From)
      AND (@To IS NULL OR l.LeadDate <= @To)
      AND (ISNULL(@WithoutOrder, 0) = 0 OR l.SaleId IS NULL)
      AND (@Search IS NULL OR l.LeadNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\'
           OR l.ClientName LIKE @Search ESCAPE '\' OR l.SecondaryClientName LIKE @Search ESCAPE '\'
           OR CONCAT(gb.FirstName, N' ', gb.LastName) LIKE @Search ESCAPE '\'
           OR CONCAT(asg.FirstName, N' ', asg.LastName) LIKE @Search ESCAPE '\')
    ORDER BY
        CASE WHEN @SortBy = 'leadNumber' AND @desc = 0 THEN l.Id END ASC,
        CASE WHEN @SortBy = 'leadNumber' AND @desc = 1 THEN l.Id END DESC,
        CASE WHEN @SortBy = 'leadDate'   AND @desc = 0 THEN l.LeadDate END ASC,
        CASE WHEN @SortBy = 'leadDate'   AND @desc = 1 THEN l.LeadDate END DESC,
        CASE WHEN @SortBy = 'aging'      AND @desc = 0 THEN l.LeadDate END DESC,
        CASE WHEN @SortBy = 'aging'      AND @desc = 1 THEN l.LeadDate END ASC,
        CASE WHEN @SortBy = 'company'    AND @desc = 0 THEN cu.Name END ASC,
        CASE WHEN @SortBy = 'company'    AND @desc = 1 THEN cu.Name END DESC,
        CASE WHEN @SortBy = 'revenue'    AND @desc = 0 THEN l.MonthlyExpectedRevenue END ASC,
        CASE WHEN @SortBy = 'revenue'    AND @desc = 1 THEN l.MonthlyExpectedRevenue END DESC,
        l.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
