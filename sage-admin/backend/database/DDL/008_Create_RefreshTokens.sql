-- 008: refresh tokens (hash only). FamilyId enables rotation with reuse detection.
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens
    (
        Id                 BIGINT IDENTITY(1,1) NOT NULL,
        UserId             BIGINT           NOT NULL,
        TokenHash          CHAR(64)         NOT NULL,
        FamilyId           UNIQUEIDENTIFIER NOT NULL,
        ExpiresAtUtc       DATETIME2(3)     NOT NULL,
        RevokedAtUtc       DATETIME2(3)     NULL,
        RevokedReason      VARCHAR(50)      NULL,
        ReplacedByTokenId  BIGINT           NULL,
        CreatedAtUtc       DATETIME2(3)     NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedByIp        VARCHAR(45)      NULL,
        UserAgent          NVARCHAR(500)    NULL,
        CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_RefreshTokens_AdminUsers FOREIGN KEY (UserId) REFERENCES dbo.AdminUsers (Id),
        CONSTRAINT FK_RefreshTokens_ReplacedBy FOREIGN KEY (ReplacedByTokenId) REFERENCES dbo.RefreshTokens (Id),
        CONSTRAINT CK_RefreshTokens_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
    );
END
GO
