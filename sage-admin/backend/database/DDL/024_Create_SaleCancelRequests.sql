-- 024: cancelling sales.
--   * A sales manager cancels their own sales and their team's directly.
--   * A sales person cannot cancel. If their manager switched on CanRequestCancellation (User Panel > My team), they can
--     ask to cancel one of their own sales, with a reason. The manager approves (the sale is cancelled) or rejects it.
IF COL_LENGTH(N'dbo.SalesUsers', N'CanRequestCancellation') IS NULL
BEGIN
    ALTER TABLE dbo.SalesUsers ADD CanRequestCancellation BIT NOT NULL CONSTRAINT DF_SalesUsers_CanRequestCancellation DEFAULT 0;
END
GO

IF OBJECT_ID(N'dbo.SaleCancelRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SaleCancelRequests
    (
        Id                      BIGINT IDENTITY(1,1) NOT NULL,
        PublicId                UNIQUEIDENTIFIER     NOT NULL CONSTRAINT DF_SaleCancelRequests_PublicId DEFAULT NEWID(),
        SaleId                  BIGINT               NOT NULL,
        RequestedBySalesUserId  BIGINT               NOT NULL,
        Reason                  NVARCHAR(500)        NOT NULL,
        Status                  VARCHAR(20)          NOT NULL CONSTRAINT DF_SaleCancelRequests_Status DEFAULT 'Pending',
        RequestedAtUtc          DATETIME2(3)         NOT NULL CONSTRAINT DF_SaleCancelRequests_RequestedAtUtc DEFAULT SYSUTCDATETIME(),
        DecidedBySalesUserId    BIGINT               NULL,
        DecidedAtUtc            DATETIME2(3)         NULL,
        DecisionNote            NVARCHAR(500)        NULL,
        CONSTRAINT PK_SaleCancelRequests PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SaleCancelRequests_PublicId UNIQUE (PublicId),
        CONSTRAINT FK_SaleCancelRequests_Sales FOREIGN KEY (SaleId) REFERENCES dbo.Sales (Id),
        CONSTRAINT FK_SaleCancelRequests_RequestedBy FOREIGN KEY (RequestedBySalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_SaleCancelRequests_DecidedBy FOREIGN KEY (DecidedBySalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT CK_SaleCancelRequests_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
        CONSTRAINT CK_SaleCancelRequests_Decided CHECK ((Status = 'Pending' AND DecidedAtUtc IS NULL) OR (Status <> 'Pending' AND DecidedAtUtc IS NOT NULL))
    );
    -- At most one open request per sale.
    CREATE UNIQUE NONCLUSTERED INDEX UX_SaleCancelRequests_PendingPerSale ON dbo.SaleCancelRequests (SaleId) WHERE Status = 'Pending';
    CREATE NONCLUSTERED INDEX IX_SaleCancelRequests_SaleId ON dbo.SaleCancelRequests (SaleId, RequestedAtUtc DESC);
END
GO
