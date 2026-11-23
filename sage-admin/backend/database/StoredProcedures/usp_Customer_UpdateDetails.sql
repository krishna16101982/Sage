-- User Panel > Companies > Edit: changes a company's contact, GSTIN and address. The company name cannot be changed here
-- (it is read-only on the screen). Companies are shared, so any active sales user may edit them; the API audits who did.
-- Raises 50404 (not found), 50409 (another company has the GSTIN, or the same name in the new city) or 50400.
CREATE OR ALTER PROCEDURE dbo.usp_Customer_UpdateDetails
    @PublicId       UNIQUEIDENTIFIER,
    @ContactPerson  NVARCHAR(150) = NULL,
    @Email          NVARCHAR(256) = NULL,
    @Phone          NVARCHAR(20)  = NULL,
    @GSTIN          VARCHAR(15)   = NULL,
    @Address        NVARCHAR(500) = NULL,
    @City           NVARCHAR(100) = NULL,
    @State          NVARCHAR(100) = NULL,
    @Country        NVARCHAR(100) = N'India',
    @SalesUserId    BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @SalesUserId AND IsDeleted = 0 AND Status = 'Active')
        THROW 50400, 'Your account is not active', 1;

    BEGIN TRAN;

    DECLARE @id BIGINT, @name NVARCHAR(200);
    SELECT @id = Id, @name = Name FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId AND IsActive = 1;
    IF @id IS NULL
        THROW 50404, 'Company not found', 1;

    IF @GSTIN IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE GSTIN = @GSTIN AND Id <> @id)
        THROW 50409, 'Another company already has this GSTIN', 1;

    IF EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
               WHERE Name = @name AND ISNULL(City, N'') = ISNULL(@City, N'') AND Id <> @id)
        THROW 50409, 'Another company with this name already exists in this city', 1;

    UPDATE dbo.Customers
    SET ContactPerson = @ContactPerson, Email = @Email, Phone = @Phone, GSTIN = @GSTIN, Address = @Address,
        City = @City, State = @State, Country = ISNULL(@Country, N'India'), UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Id = @id;

    COMMIT;

    SELECT @PublicId;
END
GO
