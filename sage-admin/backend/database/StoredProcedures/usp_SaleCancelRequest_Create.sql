-- A sales person asks their manager to cancel one of their own sales. Needs SalesUsers.CanRequestCancellation (set by
-- the manager) and a manager on the sale. Raises 50404 (sale not found / not theirs), 50400 (not allowed, sale already
-- cancelled, no manager) or 50409 (a request is already waiting). Returns the request's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_SaleCancelRequest_Create
    @SalePublicId  UNIQUEIDENTIFIER,
    @SalesUserId   BIGINT,
    @Reason        NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @SalesUserId AND CanRequestCancellation = 1 AND Role = 'SalesPerson')
        THROW 50400, 'Your manager has not allowed you to request cancellations', 1;

    DECLARE @saleId BIGINT, @status VARCHAR(20), @managerId BIGINT;
    SELECT @saleId = Id, @status = Status, @managerId = ManagerId
    FROM dbo.Sales WITH (UPDLOCK, HOLDLOCK)
    WHERE PublicId = @SalePublicId AND SalesUserId = @SalesUserId;

    IF @saleId IS NULL
        THROW 50404, 'Sale not found', 1;
    IF @status = 'Cancelled'
        THROW 50400, 'This sale is already cancelled', 1;
    IF @managerId IS NULL
        THROW 50400, 'This sale has no manager to approve a cancellation', 1;
    IF EXISTS (SELECT 1 FROM dbo.SaleCancelRequests WITH (UPDLOCK, HOLDLOCK) WHERE SaleId = @saleId AND Status = 'Pending')
        THROW 50409, 'A cancellation request for this sale is already waiting for your manager', 1;

    DECLARE @publicId UNIQUEIDENTIFIER = NEWID();
    INSERT dbo.SaleCancelRequests (PublicId, SaleId, RequestedBySalesUserId, Reason)
    VALUES (@publicId, @saleId, @SalesUserId, @Reason);

    COMMIT;

    SELECT @publicId;
END
GO
