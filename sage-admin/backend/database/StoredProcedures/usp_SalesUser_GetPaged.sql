-- One page of sales users (not deleted) with manager name and team size, plus @TotalCount OUTPUT.
-- @SortBy is one of: name, username, email, role, status, manager, createdAtUtc, lastLoginAtUtc (the API maps anything else to 'name').
-- @Search is a ready LIKE pattern with '\' as the escape character, or NULL. @ManagerPublicId filters a manager's team.
CREATE OR ALTER PROCEDURE dbo.usp_SalesUser_GetPaged
    @Search          NVARCHAR(210)    = NULL,
    @Role            VARCHAR(20)      = NULL,
    @Status          VARCHAR(20)      = NULL,
    @ManagerPublicId UNIQUEIDENTIFIER = NULL,
    @SortBy          VARCHAR(20)      = 'name',
    @SortDirection   VARCHAR(4)       = 'ASC',
    @Offset          INT              = 0,
    @PageSize        INT              = 20,
    @TotalCount      INT              OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;
    DECLARE @managerId BIGINT = (SELECT Id FROM dbo.SalesUsers WHERE PublicId = @ManagerPublicId);
    IF @ManagerPublicId IS NOT NULL AND @managerId IS NULL SET @managerId = -1; -- unknown manager: empty result

    SELECT @TotalCount = COUNT(*)
    FROM dbo.SalesUsers u
    WHERE u.IsDeleted = 0
      AND (@Role IS NULL OR u.Role = @Role)
      AND (@Status IS NULL OR u.Status = @Status)
      AND (@managerId IS NULL OR u.ManagerId = @managerId)
      AND (@Search IS NULL OR u.FirstName LIKE @Search ESCAPE '\' OR u.LastName LIKE @Search ESCAPE '\'
           OR CONCAT(u.FirstName, N' ', u.LastName) LIKE @Search ESCAPE '\'
           OR u.Username LIKE @Search ESCAPE '\' OR u.Email LIKE @Search ESCAPE '\' OR u.MobileNumber LIKE @Search ESCAPE '\')
    OPTION (RECOMPILE);

    SELECT u.PublicId, u.FirstName, u.LastName, u.Username, u.Email, u.MobileNumber, u.Role, u.Status,
           m.PublicId AS ManagerPublicId, CASE WHEN m.Id IS NULL THEN NULL ELSE CONCAT(m.FirstName, N' ', m.LastName) END AS ManagerName,
           ISNULL(t.TeamSize, 0) AS TeamSize, u.MustChangePassword, u.LockoutEndUtc, u.LastLoginAtUtc, u.CreatedAtUtc
    FROM dbo.SalesUsers u
    LEFT JOIN dbo.SalesUsers m ON m.Id = u.ManagerId
    OUTER APPLY (SELECT COUNT(*) AS TeamSize FROM dbo.SalesUsers x WHERE x.ManagerId = u.Id AND x.IsDeleted = 0) t
    WHERE u.IsDeleted = 0
      AND (@Role IS NULL OR u.Role = @Role)
      AND (@Status IS NULL OR u.Status = @Status)
      AND (@managerId IS NULL OR u.ManagerId = @managerId)
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
        CASE WHEN @SortBy = 'role'           AND @desc = 0 THEN u.Role           END ASC,
        CASE WHEN @SortBy = 'role'           AND @desc = 1 THEN u.Role           END DESC,
        CASE WHEN @SortBy = 'status'         AND @desc = 0 THEN u.Status         END ASC,
        CASE WHEN @SortBy = 'status'         AND @desc = 1 THEN u.Status         END DESC,
        CASE WHEN @SortBy = 'manager'        AND @desc = 0 THEN m.FirstName      END ASC,
        CASE WHEN @SortBy = 'manager'        AND @desc = 1 THEN m.FirstName      END DESC,
        CASE WHEN @SortBy = 'createdAtUtc'   AND @desc = 0 THEN u.CreatedAtUtc   END ASC,
        CASE WHEN @SortBy = 'createdAtUtc'   AND @desc = 1 THEN u.CreatedAtUtc   END DESC,
        CASE WHEN @SortBy = 'lastLoginAtUtc' AND @desc = 0 THEN u.LastLoginAtUtc END ASC,
        CASE WHEN @SortBy = 'lastLoginAtUtc' AND @desc = 1 THEN u.LastLoginAtUtc END DESC,
        u.FirstName ASC, u.LastName ASC, u.Id ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
