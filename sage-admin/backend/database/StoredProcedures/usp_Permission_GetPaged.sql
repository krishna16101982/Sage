-- One page of permissions plus the total row count (@TotalCount OUTPUT), in one call.
-- @SortBy is one of: module, code, name, action, isActive, createdAtUtc (the API maps anything else to 'module').
-- @Search is a ready LIKE pattern with '\' as the escape character (the API escapes user input), or NULL.
CREATE OR ALTER PROCEDURE dbo.usp_Permission_GetPaged
    @ModuleId      BIGINT        = NULL,
    @IsActive      BIT           = NULL,
    @Search        NVARCHAR(210) = NULL,
    @SortBy        VARCHAR(20)   = 'module',
    @SortDirection VARCHAR(4)    = 'ASC',
    @Offset        INT           = 0,
    @PageSize      INT           = 20,
    @TotalCount    INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.Permissions p
    JOIN dbo.Modules m ON m.Id = p.ModuleId
    WHERE (@ModuleId IS NULL OR p.ModuleId = @ModuleId)
      AND (@IsActive IS NULL OR p.IsActive = @IsActive)
      AND (@Search IS NULL OR p.Code LIKE @Search ESCAPE '\' OR p.Name LIKE @Search ESCAPE '\' OR m.Name LIKE @Search ESCAPE '\')
    OPTION (RECOMPILE);

    SELECT p.Id, p.ModuleId, m.Code AS ModuleCode, m.Name AS ModuleName, p.Action, p.Code, p.Name, p.Description, p.IsActive, p.IsSystem
    FROM dbo.Permissions p
    JOIN dbo.Modules m ON m.Id = p.ModuleId
    WHERE (@ModuleId IS NULL OR p.ModuleId = @ModuleId)
      AND (@IsActive IS NULL OR p.IsActive = @IsActive)
      AND (@Search IS NULL OR p.Code LIKE @Search ESCAPE '\' OR p.Name LIKE @Search ESCAPE '\' OR m.Name LIKE @Search ESCAPE '\')
    ORDER BY
        CASE WHEN @SortBy = 'module'       AND @desc = 0 THEN m.SortOrder    END ASC,
        CASE WHEN @SortBy = 'module'       AND @desc = 1 THEN m.SortOrder    END DESC,
        CASE WHEN @SortBy = 'module'       AND @desc = 0 THEN m.Name         END ASC,
        CASE WHEN @SortBy = 'module'       AND @desc = 1 THEN m.Name         END DESC,
        CASE WHEN @SortBy = 'code'         AND @desc = 0 THEN p.Code         END ASC,
        CASE WHEN @SortBy = 'code'         AND @desc = 1 THEN p.Code         END DESC,
        CASE WHEN @SortBy = 'name'         AND @desc = 0 THEN p.Name         END ASC,
        CASE WHEN @SortBy = 'name'         AND @desc = 1 THEN p.Name         END DESC,
        CASE WHEN @SortBy = 'action'       AND @desc = 0 THEN p.Action       END ASC,
        CASE WHEN @SortBy = 'action'       AND @desc = 1 THEN p.Action       END DESC,
        CASE WHEN @SortBy = 'isActive'     AND @desc = 0 THEN p.IsActive     END ASC,
        CASE WHEN @SortBy = 'isActive'     AND @desc = 1 THEN p.IsActive     END DESC,
        CASE WHEN @SortBy = 'createdAtUtc' AND @desc = 0 THEN p.CreatedAtUtc END ASC,
        CASE WHEN @SortBy = 'createdAtUtc' AND @desc = 1 THEN p.CreatedAtUtc END DESC,
        CASE WHEN @SortBy = 'createdAtUtc' THEN p.Id END ASC,
        p.Code ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
