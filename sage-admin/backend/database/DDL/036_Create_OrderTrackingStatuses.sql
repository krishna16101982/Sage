-- 036: tracking status of an order form (Draft, Sent to DocuSign, Signed, Cancelled), shown on the order form, its
-- page and the Order forms list. Fixed list in its own table. Sales.TrackingStatusId is set for orders saved with the
-- New order form (NULL for the simple New sale page). Cancelled is not stored by choice: it is how a cancelled sale shows
-- (choosing it cancels the sale, or asks the manager to); TrackingStatusId keeps the last status before that.
IF OBJECT_ID(N'dbo.OrderTrackingStatuses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderTrackingStatuses
    (
        Id         INT            NOT NULL,
        Code       VARCHAR(20)    NOT NULL,
        Name       NVARCHAR(50)   NOT NULL,
        SortOrder  INT            NOT NULL,
        CONSTRAINT PK_OrderTrackingStatuses PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_OrderTrackingStatuses_Code UNIQUE (Code)
    );
    INSERT dbo.OrderTrackingStatuses (Id, Code, Name, SortOrder) VALUES
        (1, 'DRAFT', N'Draft', 1), (2, 'SENT_DOCUSIGN', N'Sent to DocuSign', 2), (3, 'SIGNED', N'Signed', 3), (4, 'CANCELLED', N'Cancelled', 4);
END
GO

IF COL_LENGTH(N'dbo.Sales', N'TrackingStatusId') IS NULL
BEGIN
    ALTER TABLE dbo.Sales ADD TrackingStatusId INT NULL
        CONSTRAINT FK_Sales_TrackingStatus FOREIGN KEY REFERENCES dbo.OrderTrackingStatuses (Id);
END
GO

-- Orders already saved with the order form start as Draft.
UPDATE dbo.Sales SET TrackingStatusId = 1 WHERE TrackingStatusId IS NULL AND BillingFrequencyId IS NOT NULL;
GO
