-- 018: User Panel refresh tokens (hash only). Same rotation and reuse detection as dbo.RefreshTokens, for sales users.
IF OBJECT_ID(N'dbo.SalesUserRefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesUserRefreshTokens
    (
        Id                 BIGINT IDENTITY(1,1) NOT NULL,
        SalesUserId        BIGINT           NOT NULL,
        TokenHash          CHAR(64)         NOT NULL,
        FamilyId           UNIQUEIDENTIFIER NOT NULL,
        ExpiresAtUtc       DATETIME2(3)     NOT NULL,
        RevokedAtUtc       DATETIME2(3)     NULL,
        RevokedReason      VARCHAR(50)      NULL,
        ReplacedByTokenId  BIGINT           NULL,
        CreatedAtUtc       DATETIME2(3)     NOT NULL CONSTRAINT DF_SalesUserRefreshTokens_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedByIp        VARCHAR(45)      NULL,
        UserAgent          NVARCHAR(500)    NULL,
        CONSTRAINT PK_SalesUserRefreshTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SalesUserRefreshTokens_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_SalesUserRefreshTokens_SalesUsers FOREIGN KEY (SalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_SalesUserRefreshTokens_ReplacedBy FOREIGN KEY (ReplacedByTokenId) REFERENCES dbo.SalesUserRefreshTokens (Id),
        CONSTRAINT CK_SalesUserRefreshTokens_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
    );
    CREATE NONCLUSTERED INDEX IX_SalesUserRefreshTokens_User_Family ON dbo.SalesUserRefreshTokens (SalesUserId, FamilyId) INCLUDE (RevokedAtUtc, ExpiresAtUtc);
END
GO
