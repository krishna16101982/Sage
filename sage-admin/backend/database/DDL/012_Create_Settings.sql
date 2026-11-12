-- 012: application settings (Settings module). Secret values must not be stored here.
IF OBJECT_ID(N'dbo.Settings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Settings
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        SettingKey    VARCHAR(100)   NOT NULL,
        SettingValue  NVARCHAR(2000) NULL,
        ValueType     VARCHAR(20)    NOT NULL CONSTRAINT DF_Settings_ValueType DEFAULT 'string',
        Description   NVARCHAR(500)  NULL,
        UpdatedAtUtc  DATETIME2(3)   NULL,
        UpdatedBy     BIGINT         NULL,
        CONSTRAINT PK_Settings PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Settings_SettingKey UNIQUE (SettingKey),
        CONSTRAINT CK_Settings_ValueType CHECK (ValueType IN ('string','int','bool','json'))
    );
END
GO
