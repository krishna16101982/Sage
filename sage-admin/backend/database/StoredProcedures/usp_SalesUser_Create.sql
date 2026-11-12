-- Creates a sales user (Active, must change password at first sign-in). Returns the new PublicId.
-- Rules: username and email unique among non-deleted users (50409); a SalesPerson needs an active SalesManager as manager,
-- a SalesManager has no manager (50400).
CREATE OR ALTER PROCEDURE dbo.usp_SalesUser_Create
    @FirstName       NVARCHAR(100),
    @LastName        NVARCHAR(100),
    @Username        NVARCHAR(50),
    @Email           NVARCHAR(256),
    @MobileNumber    NVARCHAR(20)     = NULL,
    @PasswordHash    NVARCHAR(255),
    @Role            VARCHAR(20),
    @ManagerPublicId UNIQUEIDENTIFIER = NULL,
    @ActorUserId     BIGINT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.SalesUsers WITH (UPDLOCK, HOLDLOCK) WHERE Username = @Username AND IsDeleted = 0)
        THROW 50409, 'A sales user with this username already exists', 1;
    IF EXISTS (SELECT 1 FROM dbo.SalesUsers WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email AND IsDeleted = 0)
        THROW 50409, 'A sales user with this email already exists', 1;

    DECLARE @managerId BIGINT = NULL;
    IF @Role = 'SalesPerson'
    BEGIN
        IF @ManagerPublicId IS NULL
            THROW 50400, 'A sales person needs a sales manager', 1;
        SELECT @managerId = Id FROM dbo.SalesUsers WITH (HOLDLOCK)
        WHERE PublicId = @ManagerPublicId AND IsDeleted = 0 AND Role = 'SalesManager' AND Status = 'Active';
        IF @managerId IS NULL
            THROW 50400, 'The manager must be an active sales manager', 1;
    END
    ELSE IF @ManagerPublicId IS NOT NULL
        THROW 50400, 'A sales manager cannot have a manager', 1;

    DECLARE @publicId UNIQUEIDENTIFIER = NEWID();

    INSERT dbo.SalesUsers (PublicId, FirstName, LastName, Username, Email, MobileNumber, PasswordHash, MustChangePassword, Role, ManagerId, Status, CreatedBy)
    VALUES (@publicId, @FirstName, @LastName, @Username, @Email, @MobileNumber, @PasswordHash, 1, @Role, @managerId, 'Active', @ActorUserId);

    COMMIT;

    SELECT @publicId;
END
GO
