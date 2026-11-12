-- One page of admin users (not deleted) with their group, plus @TotalCount OUTPUT.
-- @SortBy is one of: name, username, email, status, group, createdAtUtc, lastLoginAtUtc (the API maps anything else to 'name').
-- @Search is a ready LIKE pattern with '\' as the escape character, or NULL. @GroupPublicId filters one group's members.
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_GetPaged
    @Search        NVARCHAR(210)    = NULL,
    @Status        VARCHAR(20)      = NULL,
    @GroupPublicId UNIQUEIDENTIFIER = NULL,
    @SortBy        VARCHAR(20)      = 'name',
    @SortDirection VARCHAR(4)       = 'ASC',
    @Offset        INT              = 0,
    @PageSize      INT              = 20,
    @TotalCount    INT              OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;
    DECLARE @groupId BIGINT = (SELECT Id FROM dbo.PermissionGroups WHERE PublicId = @GroupPublicId);
    IF @GroupPublicId IS NOT NULL AND @groupId IS NULL SET @groupId = -1; -- unknown group: empty result

    SELECT @TotalCount = COUNT(*)
    FROM dbo.AdminUsers u
    LEFT JOIN dbo.AdminUserGroups ug ON ug.UserId = u.Id AND ug.IsPrimary = 1
    WHERE u.IsDeleted = 0
      AND (@Status IS NULL OR u.Status = @Status)
      AND (@groupId IS NULL OR ug.GroupId = @groupId)
      AND (@Search IS NULL OR u.FirstName LIKE @Search ESCAPE '\' OR u.LastName LIKE @Search ESCAPE '\'
           OR CONCAT(u.FirstName, N' ', u.LastName) LIKE @Search ESCAPE '\'
           OR u.Username LIKE @Search ESCAPE '\' OR u.Email LIKE @Search ESCAPE '\' OR u.MobileNumber LIKE @Search ESCAPE '\')
    OPTION (RECOMPILE);

    SELECT u.PublicId, u.FirstName, u.LastName, u.Username, u.Email, u.MobileNumber, u.Status, u.IsSuperAdmin,
           g.PublicId AS GroupPublicId, g.Name AS GroupName, g.Status AS GroupStatus,
           u.MustChangePassword, u.LockoutEndUtc, u.LastLoginAtUtc, u.CreatedAtUtc
    FROM dbo.AdminUsers u
    LEFT JOIN dbo.AdminUserGroups ug ON ug.UserId = u.Id AND ug.IsPrimary = 1
    LEFT JOIN dbo.PermissionGroups g ON g.Id = ug.GroupId
    WHERE u.IsDeleted = 0
      AND (@Status IS NULL OR u.Status = @Status)
      AND (@groupId IS NULL OR ug.GroupId = @groupId)
      AND (@Search IS NULL OR u.FirstName LIKE @Search ESCAPE '\' OR u.LastName LIKE @Search ESCAPE '\'
           OR CONCAT(u.FirstName, N' ', u.LastName) LIKE @Search ESCAPE '\'
           OR u.Username LIKE @Search ESCAPE '\' OR u.Email LIKE @Search ESCAPE '\' OR u.MobileNumber LIKE @Search ESCAPE '\')
    ORDER BY
        CASE WHEN @SortBy = 'name'           AND @desc = 0 THEN u.FirstName      END ASC,
        CASE WHEN @SortBy = 'name'           AND @desc = 1 THEN u.FirstName      END DESC,
        CASE WHEN @SortBy = 'name'           AND @desc = 0 THEN u.LastName       END ASC,
        CASE WHEN @SortBy = 'name'           AND @desc = 1 THEN u.LastName       END DESC,
        CASE WHEN @SortBy = 'username'       AND @desc = 0 THEN u.Username       END ASC,
        CASE WHEN @SortBy = 'username'       AND @desc = 1 THEN u.Username       END DESC,
        CASE WHEN @SortBy = 'email'          AND @desc = 0 THEN u.Email          END ASC,
        CASE WHEN @SortBy = 'email'          AND @desc = 1 THEN u.Email          END DESC,
        CASE WHEN @SortBy = 'status'         AND @desc = 0 THEN u.Status         END ASC,
        CASE WHEN @SortBy = 'status'         AND @desc = 1 THEN u.Status         END DESC,
        CASE WHEN @SortBy = 'group'          AND @desc = 0 THEN g.Name           END ASC,
        CASE WHEN @SortBy = 'group'          AND @desc = 1 THEN g.Name           END DESC,
        CASE WHEN @SortBy = 'createdAtUtc'   AND @desc = 0 THEN u.CreatedAtUtc   END ASC,
        CASE WHEN @SortBy = 'createdAtUtc'   AND @desc = 1 THEN u.CreatedAtUtc   END DESC,
        CASE WHEN @SortBy = 'lastLoginAtUtc' AND @desc = 0 THEN u.LastLoginAtUtc END ASC,
        CASE WHEN @SortBy = 'lastLoginAtUtc' AND @desc = 1 THEN u.LastLoginAtUtc END DESC,
        u.FirstName ASC, u.LastName ASC, u.Id ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
