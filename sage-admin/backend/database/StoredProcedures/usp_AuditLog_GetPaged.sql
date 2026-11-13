-- One page of the audit log, newest first, plus @TotalCount OUTPUT.
-- Who did it: an admin user (ActorUserId), a sales user (User Panel actions keep ActorUserId NULL and put the sales
-- user's name in NewValues.SalesUserName), or nobody (system / anonymous, e.g. a failed sign-in).
--   @ActorType: 'admin', 'sales', 'system' or NULL (all).   @ActorPublicId: one admin user.
--   @Search: a ready LIKE pattern ('\' escape) on entity id, action, entity type and the actor's name / username.
--   @From / @To: days (UTC), inclusive.
CREATE OR ALTER PROCEDURE dbo.usp_AuditLog_GetPaged
    @Search         NVARCHAR(210)    = NULL,
    @EntityType     VARCHAR(100)     = NULL,
    @Action         VARCHAR(100)     = NULL,
    @ActorType      VARCHAR(10)      = NULL,
    @ActorPublicId  UNIQUEIDENTIFIER = NULL,
    @From           DATE             = NULL,
    @To             DATE             = NULL,
    @SortDirection  VARCHAR(4)       = 'DESC',
    @Offset         INT              = 0,
    @PageSize       INT              = 50,
    @TotalCount     INT              OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @fromUtc DATETIME2(3) = CAST(@From AS DATETIME2(3));
    DECLARE @toUtc DATETIME2(3) = DATEADD(DAY, 1, CAST(@To AS DATETIME2(3)));
    DECLARE @actorId BIGINT = NULL;
    IF @ActorPublicId IS NOT NULL SET @actorId = ISNULL((SELECT Id FROM dbo.AdminUsers WHERE PublicId = @ActorPublicId), -1);

    ;WITH src AS
    (
        SELECT a.Id, a.CreatedAtUtc, a.Action, a.EntityType, a.EntityId, a.IpAddress, a.ActorUserId,
               CASE WHEN a.ActorUserId IS NOT NULL THEN 'admin'
                    WHEN ISJSON(a.NewValues) = 1 AND JSON_VALUE(a.NewValues, '$.By') = 'SalesUser' THEN 'sales'
                    ELSE 'system' END AS ActorType,
               CASE WHEN u.Id IS NOT NULL THEN CONCAT(u.FirstName, N' ', u.LastName)
                    WHEN ISJSON(a.NewValues) = 1 THEN JSON_VALUE(a.NewValues, '$.SalesUserName') END AS ActorName,
               u.Username AS ActorUsername,
               CASE WHEN a.OldValues IS NULL THEN 0 ELSE 1 END AS HasOld,
               CASE WHEN a.NewValues IS NULL THEN 0 ELSE 1 END AS HasNew
        FROM dbo.AuditLogs a
        LEFT JOIN dbo.AdminUsers u ON u.Id = a.ActorUserId
        WHERE (@EntityType IS NULL OR a.EntityType = @EntityType)
          AND (@Action IS NULL OR a.Action = @Action)
          AND (@actorId IS NULL OR a.ActorUserId = @actorId)
          AND (@fromUtc IS NULL OR a.CreatedAtUtc >= @fromUtc)
          AND (@toUtc IS NULL OR a.CreatedAtUtc < @toUtc)
    )
    SELECT * INTO #filtered FROM src
    WHERE (@ActorType IS NULL OR ActorType = @ActorType)
      AND (@Search IS NULL OR EntityId LIKE @Search ESCAPE '\' OR Action LIKE @Search ESCAPE '\' OR EntityType LIKE @Search ESCAPE '\'
           OR ActorName LIKE @Search ESCAPE '\' OR ActorUsername LIKE @Search ESCAPE '\' OR IpAddress LIKE @Search ESCAPE '\');

    SELECT @TotalCount = COUNT(*) FROM #filtered;

    SELECT Id, CreatedAtUtc, ActorType, ActorName, ActorUsername, Action, EntityType, EntityId, IpAddress, HasOld, HasNew
    FROM #filtered
    ORDER BY CASE WHEN @SortDirection = 'ASC' THEN CreatedAtUtc END ASC,
             CASE WHEN @SortDirection <> 'ASC' THEN CreatedAtUtc END DESC,
             Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO
