-- Cancels a sale directly. Only sales managers: the seller's manager (the sale's ManagerId), or a manager for their own
-- sale. A sales person asks instead (usp_SaleCancelRequest_Create). An open cancellation request for the sale is closed
-- as Approved. Raises 50404 when the sale does not exist or is not visible to @SalesUserId, 50400 when it is already
-- cancelled or when @SalesUserId is not allowed to cancel it.
CREATE OR ALTER PROCEDURE dbo.usp_Sale_Cancel
    @SalePublicId  UNIQUEIDENTIFIER,
    @SalesUserId   BIGINT,
    @Reason        NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT, @status VARCHAR(20), @managerId BIGINT;
    SELECT @id = Id, @status = Status, @managerId = ManagerId
    FROM dbo.Sales WITH (UPDLOCK, HOLDLOCK)
    WHERE PublicId = @SalePublicId AND (SalesUserId = @SalesUserId OR ManagerId = @SalesUserId);

    IF @id IS NULL
        THROW 50404, 'Sale not found', 1;
    IF @status = 'Cancelled'
        THROW 50400, 'This sale is already cancelled', 1;

    -- Not the sale's manager: only a sales manager may cancel their own sale.
    IF (@managerId IS NULL OR @managerId <> @SalesUserId)
       AND NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @SalesUserId AND Role = 'SalesManager')
        THROW 50400, 'Only your manager can cancel a sale. Ask them to cancel it', 1;

    DECLARE @now DATETIME2(3) = SYSUTCDATETIME();

    UPDATE dbo.Sales
    SET Status = 'Cancelled', CancelledAtUtc = @now, CancelledBySalesUserId = @SalesUserId, CancelReason = @Reason
    WHERE Id = @id;
    DECLARE @rows INT = @@ROWCOUNT;

    UPDATE dbo.SaleCancelRequests
    SET Status = 'Approved', DecidedBySalesUserId = @SalesUserId, DecidedAtUtc = @now, DecisionNote = N'Sale cancelled by the manager'
    WHERE SaleId = @id AND Status = 'Pending';

    COMMIT;

    SELECT @rows;
END
GO
