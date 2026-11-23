-- Admin Panel > Settings > Lead lists: adds (@Id NULL) or changes one value of a lead list (DDL 035).
-- @List: Product (dbo.LeadProducts), Source (dbo.LeadSources) or BillingCycle (dbo.LeadBillingCycles).
-- A value is turned off rather than deleted, so leads that use it keep showing it.
-- Raises 50404 (not found), 50409 (same name already in the list) or 50400. Returns the value's Id.
CREATE OR ALTER PROCEDURE dbo.usp_LeadListValue_Save
    @List         VARCHAR(20),
    @Id           INT            = NULL,
    @Name         NVARCHAR(100),
    @SortOrder    INT            = 0,
    @IsActive     BIT            = 1,
    @ActorUserId  BIGINT         = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@Name)), N'') IS NULL
        THROW 50400, 'Name is required', 1;

    BEGIN TRAN;

    IF @List = 'Product'
    BEGIN
        IF @Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LeadProducts WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
            THROW 50404, 'List value not found', 1;
        IF EXISTS (SELECT 1 FROM dbo.LeadProducts WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name AND (@Id IS NULL OR Id <> @Id))
            THROW 50409, 'This list already has a value with the same name', 1;
        IF @Id IS NULL
        BEGIN
            INSERT dbo.LeadProducts (Name, SortOrder, IsActive, UpdatedBy) VALUES (@Name, @SortOrder, @IsActive, @ActorUserId);
            SET @Id = SCOPE_IDENTITY();
        END
        ELSE
            UPDATE dbo.LeadProducts SET Name = @Name, SortOrder = @SortOrder, IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId WHERE Id = @Id;
    END
    ELSE IF @List = 'Source'
    BEGIN
        IF @Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LeadSources WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
            THROW 50404, 'List value not found', 1;
        IF EXISTS (SELECT 1 FROM dbo.LeadSources WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name AND (@Id IS NULL OR Id <> @Id))
            THROW 50409, 'This list already has a value with the same name', 1;
        IF @Id IS NULL
        BEGIN
            INSERT dbo.LeadSources (Name, SortOrder, IsActive, UpdatedBy) VALUES (@Name, @SortOrder, @IsActive, @ActorUserId);
            SET @Id = SCOPE_IDENTITY();
        END
        ELSE
            UPDATE dbo.LeadSources SET Name = @Name, SortOrder = @SortOrder, IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId WHERE Id = @Id;
    END
    ELSE IF @List = 'BillingCycle'
    BEGIN
        IF @Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LeadBillingCycles WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
            THROW 50404, 'List value not found', 1;
        IF EXISTS (SELECT 1 FROM dbo.LeadBillingCycles WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name AND (@Id IS NULL OR Id <> @Id))
            THROW 50409, 'This list already has a value with the same name', 1;
        IF @Id IS NULL
        BEGIN
            INSERT dbo.LeadBillingCycles (Name, SortOrder, IsActive, UpdatedBy) VALUES (@Name, @SortOrder, @IsActive, @ActorUserId);
            SET @Id = SCOPE_IDENTITY();
        END
        ELSE
            UPDATE dbo.LeadBillingCycles SET Name = @Name, SortOrder = @SortOrder, IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId WHERE Id = @Id;
    END
    ELSE
        THROW 50400, 'Unknown list', 1;

    COMMIT;

    SELECT CAST(@Id AS BIGINT);
END
GO
