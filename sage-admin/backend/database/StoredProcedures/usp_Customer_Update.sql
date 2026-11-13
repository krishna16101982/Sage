-- Edits a customer (Admin Panel). Raises 50404 when it does not exist, 50409 when another customer already has the
-- GSTIN, or the same name in the same city. Past sales keep pointing at the customer, so they show the new details.
CREATE OR ALTER PROCEDURE dbo.usp_Customer_Update
    @PublicId       UNIQUEIDENTIFIER,
    @Name           NVARCHAR(200),
    @ContactPerson  NVARCHAR(150) = NULL,
    @Email          NVARCHAR(256) = NULL,
    @Phone          NVARCHAR(20)  = NULL,
    @GSTIN          VARCHAR(15)   = NULL,
    @Address        NVARCHAR(500) = NULL,
    @City           NVARCHAR(100) = NULL,
    @State          NVARCHAR(100) = NULL,
    @Country        NVARCHAR(100) = N'India',
    @AdminUserId    BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT = (SELECT Id FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId AND IsActive = 1);
    IF @id IS NULL
        THROW 50404, 'Customer not found', 1;

    IF @GSTIN IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE GSTIN = @GSTIN AND Id <> @id)
        THROW 50409, 'Another customer already has this GSTIN', 1;

    IF EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
               WHERE Name = @Name AND ISNULL(City, N'') = ISNULL(@City, N'') AND Id <> @id)
        THROW 50409, 'Another customer with this name already exists in this city', 1;

    UPDATE dbo.Customers
    SET Name = @Name, ContactPerson = @ContactPerson, Email = @Email, Phone = @Phone, GSTIN = @GSTIN, Address = @Address,
        City = @City, State = @State, Country = ISNULL(@Country, N'India'), UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByAdminUserId = @AdminUserId
    WHERE Id = @id;
    DECLARE @rows INT = @@ROWCOUNT;

    COMMIT;

    SELECT @rows;
END
GO
