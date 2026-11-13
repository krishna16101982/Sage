-- The sale's manager approves (@Approve = 1: the sale is cancelled, with the request's reason) or rejects a waiting
-- cancellation request. Raises 50404 when the request does not exist or the sale is not in @ManagerId's team, 50400
-- when it was already decided. Returns the sale's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_SaleCancelRequest_Decide
    @RequestPublicId  UNIQUEIDENTIFIER,
    @ManagerId        BIGINT,
    @Approve          BIT,
    @Note             NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @requestId BIGINT, @requestStatus VARCHAR(20), @reason NVARCHAR(500), @saleId BIGINT, @salePublicId UNIQUEIDENTIFIER, @saleStatus VARCHAR(20);
    SELECT @requestId = r.Id, @requestStatus = r.Status, @reason = r.Reason, @saleId = s.Id, @salePublicId = s.PublicId, @saleStatus = s.Status
    FROM dbo.SaleCancelRequests r WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.Sales s WITH (UPDLOCK, HOLDLOCK) ON s.Id = r.SaleId
    WHERE r.PublicId = @RequestPublicId AND s.ManagerId = @ManagerId;

    IF @requestId IS NULL
        THROW 50404, 'Cancellation request not found', 1;
    IF @requestStatus <> 'Pending'
        THROW 50400, 'This request has already been decided', 1;

    DECLARE @now DATETIME2(3) = SYSUTCDATETIME();

    IF @Approve = 1 AND @saleStatus = 'Active'
        UPDATE dbo.Sales
        SET Status = 'Cancelled', CancelledAtUtc = @now, CancelledBySalesUserId = @ManagerId, CancelReason = @reason
        WHERE Id = @saleId;

    UPDATE dbo.SaleCancelRequests
    SET Status = CASE WHEN @Approve = 1 THEN 'Approved' ELSE 'Rejected' END,
        DecidedBySalesUserId = @ManagerId, DecidedAtUtc = @now, DecisionNote = @Note
    WHERE Id = @requestId;

    COMMIT;

    SELECT @salePublicId;
END
GO
