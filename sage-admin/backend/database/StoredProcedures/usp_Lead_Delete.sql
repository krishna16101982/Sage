-- User Panel > Lead Records: deletes a lead. Only sales managers, for their own leads and leads created by their team.
-- The lead is hidden (IsDeleted, with who and when), never removed. Raises 50404 or 50400. Returns the lead's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_Lead_Delete
    @LeadPublicId      UNIQUEIDENTIFIER,
    @ActorSalesUserId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @ActorSalesUserId AND IsDeleted = 0 AND Status = 'Active' AND Role = 'SalesManager')
        THROW 50400, 'Only sales managers can delete leads', 1;

    DECLARE @leadId BIGINT, @creatorId BIGINT;
    SELECT @leadId = Id, @creatorId = CreatedBySalesUserId FROM dbo.Leads WHERE PublicId = @LeadPublicId AND IsDeleted = 0;
    IF @leadId IS NULL
        THROW 50404, 'Lead not found', 1;
    IF @creatorId <> @ActorSalesUserId
       AND NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @creatorId AND ManagerId = @ActorSalesUserId)
        THROW 50400, 'You can delete your own leads and leads created by your team', 1;

    UPDATE dbo.Leads
    SET IsDeleted = 1, DeletedBySalesUserId = @ActorSalesUserId, DeletedAtUtc = SYSUTCDATETIME()
    WHERE Id = @leadId;

    SELECT @LeadPublicId;
END
GO
