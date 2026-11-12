-- 019: User Panel sign-in attempts (successful and failed). SalesUserId is NULL when the username is unknown.
IF OBJECT_ID(N'dbo.SalesUserLoginHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesUserLoginHistories
    (
        Id                 BIGINT IDENTITY(1,1) NOT NULL,
        SalesUserId        BIGINT        NULL,
        UsernameAttempted  NVARCHAR(256) NOT NULL,
        IsSuccess          BIT           NOT NULL,
        FailureReason      VARCHAR(50)   NULL,
        IpAddress          VARCHAR(45)   NULL,
        UserAgent          NVARCHAR(500) NULL,
        AttemptedAtUtc     DATETIME2(3)  NOT NULL CONSTRAINT DF_SalesUserLoginHistories_AttemptedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SalesUserLoginHistories PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_SalesUserLoginHistories_SalesUsers FOREIGN KEY (SalesUserId) REFERENCES dbo.SalesUsers (Id)
    );
    CREATE NONCLUSTERED INDEX IX_SalesUserLoginHistories_User_AttemptedAtUtc ON dbo.SalesUserLoginHistories (SalesUserId, AttemptedAtUtc DESC);
END
GO
