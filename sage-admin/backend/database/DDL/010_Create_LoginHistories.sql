-- 010: login attempts (successful and failed). UserId is NULL when the username is unknown.
IF OBJECT_ID(N'dbo.LoginHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginHistories
    (
        Id                 BIGINT IDENTITY(1,1) NOT NULL,
        UserId             BIGINT        NULL,
        UsernameAttempted  NVARCHAR(256) NOT NULL,
        IsSuccess          BIT           NOT NULL,
        FailureReason      VARCHAR(50)   NULL,
        IpAddress          VARCHAR(45)   NULL,
        UserAgent          NVARCHAR(500) NULL,
        AttemptedAtUtc     DATETIME2(3)  NOT NULL CONSTRAINT DF_LoginHistories_AttemptedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_LoginHistories PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_LoginHistories_AdminUsers FOREIGN KEY (UserId) REFERENCES dbo.AdminUsers (Id)
    );
END
GO
