-- Changes one setting's value (Admin Panel > Settings). The API has already checked the value against the setting's
-- type and limits. Raises 50404 when the key does not exist. Returns 1 (a number, like the other write procedures: the
-- API reads the old value itself for the audit log; returning the text value broke saving any non-number setting).
CREATE OR ALTER PROCEDURE dbo.usp_Setting_Update
    @SettingKey   VARCHAR(100),
    @SettingValue NVARCHAR(2000),
    @ActorUserId  BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @old NVARCHAR(2000), @found BIT = 0;
    SELECT @found = 1, @old = SettingValue FROM dbo.Settings WITH (UPDLOCK, HOLDLOCK) WHERE SettingKey = @SettingKey;
    IF @found = 0
        THROW 50404, 'Setting not found', 1;

    UPDATE dbo.Settings
    SET SettingValue = @SettingValue, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE SettingKey = @SettingKey;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
