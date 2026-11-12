-- 009: one-time password reset tokens (hash only)
IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        UserId        BIGINT        NOT NULL,
        TokenHash     CHAR(64)      NOT NULL,
        ExpiresAtUtc  DATETIME2(3)  NOT NULL,
        UsedAtUtc     DATETIME2(3)  NULL,
        RequestedIp   VARCHAR(45)   NULL,
        CreatedAtUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_PasswordResetTokens_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_PasswordResetTokens_AdminUsers FOREIGN KEY (UserId) REFERENCES dbo.AdminUsers (Id)
    );
END
GO
