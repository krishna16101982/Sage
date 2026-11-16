-- Admin Panel > Settings > Order form lists: adds (@Id NULL) or changes one master value. Making a value the default
-- clears the list's previous default in the same transaction. A list always keeps its default: the default cannot be
-- turned off or made "not default" directly (make another value the default instead).
-- Raises 50404 (not found), 50409 (same code or name already in the list) or 50400. Returns the value's Id.
CREATE OR ALTER PROCEDURE dbo.usp_MasterValue_Save
    @Id           INT           = NULL,
    @MasterType   VARCHAR(30),
    @Code         VARCHAR(30),
    @Name         NVARCHAR(100),
    @SortOrder    INT           = 0,
    @IsDefault    BIT           = 0,
    @IsActive     BIT           = 1,
    @ActorUserId  BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @MasterType NOT IN ('BillingFrequency', 'BillingMethod', 'PaymentMethod', 'PaymentTerm', 'Currency')
        THROW 50400, 'Unknown list', 1;
    IF @IsDefault = 1 AND @IsActive = 0
        THROW 50400, 'A default value must be active', 1;

    BEGIN TRAN;

    IF @Id IS NOT NULL
    BEGIN
        DECLARE @wasDefault BIT, @type VARCHAR(30);
        SELECT @wasDefault = IsDefault, @type = MasterType FROM dbo.MasterValues WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id;
        IF @type IS NULL OR @type <> @MasterType
            THROW 50404, 'List value not found', 1;
        IF @wasDefault = 1 AND (@IsDefault = 0 OR @IsActive = 0)
            THROW 50400, 'This is the default. Make another value the default first', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.MasterValues WITH (UPDLOCK, HOLDLOCK)
               WHERE MasterType = @MasterType AND (Code = @Code OR Name = @Name) AND (@Id IS NULL OR Id <> @Id))
        THROW 50409, 'This list already has a value with the same code or name', 1;

    IF @IsDefault = 1
        UPDATE dbo.MasterValues SET IsDefault = 0, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
        WHERE MasterType = @MasterType AND IsDefault = 1 AND (@Id IS NULL OR Id <> @Id);

    IF @Id IS NULL
    BEGIN
        INSERT dbo.MasterValues (MasterType, Code, Name, SortOrder, IsDefault, IsActive, UpdatedBy)
        VALUES (@MasterType, @Code, @Name, @SortOrder, @IsDefault, @IsActive, @ActorUserId);
        SET @Id = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.MasterValues
        SET Code = @Code, Name = @Name, SortOrder = @SortOrder, IsDefault = @IsDefault, IsActive = @IsActive,
            UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
        WHERE Id = @Id;

    COMMIT;

    SELECT CAST(@Id AS BIGINT);
END
GO
