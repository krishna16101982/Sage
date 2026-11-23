-- Order forms list: changes an order's tracking status (Draft, Sent to DocuSign, Signed; DDL 036) without opening it.
-- The seller or the seller's manager, for an active order saved with the order form. Cancelled is not set here: the API
-- cancels the sale (manager) or sends a cancellation request (sales person) instead.
-- Raises 50404 (not found) or 50400. Returns the sale's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_SaleOrder_SetTracking
    @SalePublicId      UNIQUEIDENTIFIER,
    @SalesUserId       BIGINT,
    @TrackingStatusId  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @saleId BIGINT, @status VARCHAR(20), @hasOrderForm BIT;
    SELECT @saleId = Id, @status = Status, @hasOrderForm = CASE WHEN BillingFrequencyId IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.Sales
    WHERE PublicId = @SalePublicId AND (SalesUserId = @SalesUserId OR ManagerId = @SalesUserId);
    IF @saleId IS NULL
        THROW 50404, 'Sale not found', 1;
    IF @hasOrderForm = 0
        THROW 50400, 'Only orders saved with the New order form have a tracking status', 1;
    IF @status <> 'Active'
        THROW 50400, 'A cancelled order cannot change its tracking status', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.OrderTrackingStatuses WHERE Id = @TrackingStatusId AND Code <> 'CANCELLED')
        THROW 50400, 'Choose Draft, Sent to DocuSign or Signed', 1;

    UPDATE dbo.Sales SET TrackingStatusId = @TrackingStatusId WHERE Id = @saleId;
    SELECT @SalePublicId;
END
GO
