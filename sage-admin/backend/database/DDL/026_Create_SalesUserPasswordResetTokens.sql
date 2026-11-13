-- 026: one-time "forgot password" tokens for sales users (User Panel). Only the SHA-256 hash of the token is stored.
IF OBJECT_ID(N'dbo.SalesUserPasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesUserPasswordResetTokens
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        SalesUserId   BIGINT        NOT NULL,
        TokenHash     CHAR(64)      NOT NULL,
        ExpiresAtUtc  DATETIME2(3)  NOT NULL,
        UsedAtUtc     DATETIME2(3)  NULL,
        RequestedIp   VARCHAR(45)   NULL,
        CreatedAtUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_SalesUserPasswordResetTokens_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SalesUserPasswordResetTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SalesUserPasswordResetTokens_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_SalesUserPasswordResetTokens_SalesUsers FOREIGN KEY (SalesUserId) REFERENCES dbo.SalesUsers (Id)
    );
    CREATE NONCLUSTERED INDEX IX_SalesUserPasswordResetTokens_SalesUserId ON dbo.SalesUserPasswordResetTokens (SalesUserId) WHERE UsedAtUtc IS NULL;
END
GO
