-- One page of permission groups with member and permission counts, plus the total row count (@TotalCount OUTPUT).
-- @SortBy is one of: name, code, status, userCount, createdAtUtc (the API maps anything else to 'name').
-- @Search is a ready LIKE pattern with '\' as the escape character (the API escapes user input), or NULL.
CREATE OR ALTER PROCEDURE dbo.usp_Group_GetPaged
    @Search        NVARCHAR(210) = NULL,
    @Status        VARCHAR(20)   = NULL,
    @SortBy        VARCHAR(20)   = 'name',
    @SortDirection VARCHAR(4)    = 'ASC',
    @Offset        INT           = 0,
    @PageSize      INT           = 20,
    @TotalCount    INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.PermissionGroups g
    WHERE (@Status IS NULL OR g.Status = @Status)
      AND (@Search IS NULL OR g.Name LIKE @Search ESCAPE '\' OR g.Code LIKE @Search ESCAPE '\' OR g.Description LIKE @Search ESCAPE '\')
    OPTION (RECOMPILE);

    SELECT g.PublicId, g.Code, g.Name, g.Description, g.Status, g.IsSystem,
           ISNULL(u.UserCount, 0) AS UserCount, ISNULL(p.PermissionCount, 0) AS PermissionCount,
           g.CreatedAtUtc, g.UpdatedAtUtc
    FROM dbo.PermissionGroups g
    OUTER APPLY (SELECT COUNT(*) AS UserCount
                 FROM dbo.AdminUserGroups ug JOIN dbo.AdminUsers au ON au.Id = ug.UserId AND au.IsDeleted = 0
                 WHERE ug.GroupId = g.Id) u
    OUTER APPLY (SELECT COUNT(*) AS PermissionCount FROM dbo.GroupPermissions gp WHERE gp.GroupId = g.Id) p
    WHERE (@Status IS NULL OR g.Status = @Status)
      AND (@Search IS NULL OR g.Name LIKE @Search ESCAPE '\' OR g.Code LIKE @Search ESCAPE '\' OR g.Description LIKE @Search ESCAPE '\')
    ORDER BY
        CASE WHEN @SortBy = 'name'         AND @desc = 0 THEN g.Name         END ASC,
        CASE WHEN @SortBy = 'name'         AND @desc = 1 THEN g.Name         END DESC,
        CASE WHEN @SortBy = 'code'         AND @desc = 0 THEN g.Code         END ASC,
        CASE WHEN @SortBy = 'code'         AND @desc = 1 THEN g.Code         END DESC,
        CASE WHEN @SortBy = 'status'       AND @desc = 0 THEN g.Status       END ASC,
        CASE WHEN @SortBy = 'status'       AND @desc = 1 THEN g.Status       END DESC,
        CASE WHEN @SortBy = 'userCount'    AND @desc = 0 THEN u.UserCount    END ASC,
        CASE WHEN @SortBy = 'userCount'    AND @desc = 1 THEN u.UserCount    END DESC,
        CASE WHEN @SortBy = 'createdAtUtc' AND @desc = 0 THEN g.CreatedAtUtc END ASC,
        CASE WHEN @SortBy = 'createdAtUtc' AND @desc = 1 THEN g.CreatedAtUtc END DESC,
        g.Name ASC, g.Id ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
