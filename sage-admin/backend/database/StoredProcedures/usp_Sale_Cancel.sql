-- Cancels a sale. Allowed for the seller and for the seller's manager (the sale's ManagerId).
-- Raises 50404 when the sale does not exist or is not visible to @SalesUserId, 50400 when it is already cancelled.
CREATE OR ALTER PROCEDURE dbo.usp_Sale_Cancel
    @SalePublicId  UNIQUEIDENTIFIER,
    @SalesUserId   BIGINT,
    @Reason        NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT, @status VARCHAR(20);
    SELECT @id = Id, @status = Status
    FROM dbo.Sales WITH (UPDLOCK, HOLDLOCK)
    WHERE PublicId = @SalePublicId AND (SalesUserId = @SalesUserId OR ManagerId = @SalesUserId);

    IF @id IS NULL
        THROW 50404, 'Sale not found', 1;
    IF @status = 'Cancelled'
        THROW 50400, 'This sale is already cancelled', 1;

    UPDATE dbo.Sales
    SET Status = 'Cancelled', CancelledAtUtc = SYSUTCDATETIME(), CancelledBySalesUserId = @SalesUserId, CancelReason = @Reason
    WHERE Id = @id;
    DECLARE @rows INT = @@ROWCOUNT;

    COMMIT;

    SELECT @rows;
END
GO
