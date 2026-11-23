-- Admin Panel > Sales users > History: one page of the audit rows about sales users (dbo.AuditLogs, EntityType SalesUser),
-- newest first, for one person (@SalesUserPublicId) or everyone. Paging happens here, never in the browser; the API turns
-- each row into the changes it records. Sign-ins and password changes are left out.
CREATE OR ALTER PROCEDURE dbo.usp_SalesUserHistory_GetPaged
    @SalesUserPublicId  UNIQUEIDENTIFIER = NULL,
    @Offset             INT              = 0,
    @PageSize           INT              = 20,
    @TotalCount         INT              OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @entityId NVARCHAR(100) = CASE WHEN @SalesUserPublicId IS NULL THEN NULL ELSE CAST(@SalesUserPublicId AS NVARCHAR(100)) END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.AuditLogs a
    WHERE a.EntityType = 'SalesUser'
      AND a.Action IN ('Create', 'Update', 'StatusChange', 'CreateTeamMember', 'TeamMemberStatus', 'CancelPermission')
      AND (@entityId IS NULL OR a.EntityId = @entityId)
    OPTION (RECOMPILE);

    SELECT a.CreatedAtUtc, a.Action, a.EntityId, a.OldValues, a.NewValues, a.ActorUserId,
           CONCAT(au.FirstName, N' ', au.LastName) AS AdminName,
           su.PublicId AS SalesUserId, CONCAT(su.FirstName, N' ', su.LastName) AS SalesUserName, su.Username
    FROM dbo.AuditLogs a
    LEFT JOIN dbo.AdminUsers au ON au.Id = a.ActorUserId
    LEFT JOIN dbo.SalesUsers su ON su.PublicId = TRY_CAST(a.EntityId AS UNIQUEIDENTIFIER)
    WHERE a.EntityType = 'SalesUser'
      AND a.Action IN ('Create', 'Update', 'StatusChange', 'CreateTeamMember', 'TeamMemberStatus', 'CancelPermission')
      AND (@entityId IS NULL OR a.EntityId = @entityId)
    ORDER BY a.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
