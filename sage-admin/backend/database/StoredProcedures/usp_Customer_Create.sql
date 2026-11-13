-- Adds a customer, from the User Panel (@SalesUserId) or the Admin Panel (@AdminUserId). Raises 50409 when the GSTIN
-- is already used, or when a customer with the same name already exists in the same city. Returns the new PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_Customer_Create
    @Name           NVARCHAR(200),
    @ContactPerson  NVARCHAR(150) = NULL,
    @Email          NVARCHAR(256) = NULL,
    @Phone          NVARCHAR(20)  = NULL,
    @GSTIN          VARCHAR(15)   = NULL,
    @Address        NVARCHAR(500) = NULL,
    @City           NVARCHAR(100) = NULL,
    @State          NVARCHAR(100) = NULL,
    @Country        NVARCHAR(100) = N'India',
    @SalesUserId    BIGINT        = NULL,
    @AdminUserId    BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF @GSTIN IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE GSTIN = @GSTIN)
        THROW 50409, 'A customer with this GSTIN already exists', 1;

    IF EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
               WHERE Name = @Name AND ISNULL(City, N'') = ISNULL(@City, N''))
        THROW 50409, 'A customer with this name already exists in this city. Search for it instead of adding it again', 1;

    DECLARE @publicId UNIQUEIDENTIFIER = NEWID();

    INSERT dbo.Customers (PublicId, Name, ContactPerson, Email, Phone, GSTIN, Address, City, State, Country, CreatedBySalesUserId, CreatedByAdminUserId)
    VALUES (@publicId, @Name, @ContactPerson, @Email, @Phone, @GSTIN, @Address, @City, @State, ISNULL(@Country, N'India'), @SalesUserId, @AdminUserId);

    COMMIT;

    SELECT @publicId;
END
GO
