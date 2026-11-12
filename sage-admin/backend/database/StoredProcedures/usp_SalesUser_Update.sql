-- Updates a sales user's details, role and manager (the username never changes).
-- Rules: 50404 not found; 50409 email taken; a SalesPerson needs an active SalesManager (50400); a SalesManager has no
-- manager (50400); a manager who still has team members cannot become a SalesPerson (50409).
-- A role or manager change bumps PermissionVersion (what the user can see in the User Panel depends on them).
CREATE OR ALTER PROCEDURE dbo.usp_SalesUser_Update
    @PublicId        UNIQUEIDENTIFIER,
    @FirstName       NVARCHAR(100),
    @LastName        NVARCHAR(100),
    @Email           NVARCHAR(256),
    @MobileNumber    NVARCHAR(20)     = NULL,
    @Role            VARCHAR(20),
    @ManagerPublicId UNIQUEIDENTIFIER = NULL,
    @ActorUserId     BIGINT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT, @oldRole VARCHAR(20), @oldManagerId BIGINT;
    SELECT @id = Id, @oldRole = Role, @oldManagerId = ManagerId
    FROM dbo.SalesUsers WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId AND IsDeleted = 0;
    IF @id IS NULL
        THROW 50404, 'Sales user not found', 1;

    IF EXISTS (SELECT 1 FROM dbo.SalesUsers WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email AND IsDeleted = 0 AND Id <> @id)
        THROW 50409, 'A sales user with this email already exists', 1;

    DECLARE @managerId BIGINT = NULL;
    IF @Role = 'SalesPerson'
    BEGIN
        DECLARE @teamSize INT = (SELECT COUNT(*) FROM dbo.SalesUsers WITH (UPDLOCK, HOLDLOCK) WHERE ManagerId = @id AND IsDeleted = 0);
        IF @teamSize > 0
        BEGIN
            DECLARE @msg NVARCHAR(200) = CONCAT(N'This manager still has ', @teamSize, N' sales person(s). Move them to another manager first');
            THROW 50409, @msg, 1;
        END
        IF @ManagerPublicId IS NULL
            THROW 50400, 'A sales person needs a sales manager', 1;
        SELECT @managerId = Id FROM dbo.SalesUsers WITH (HOLDLOCK)
        WHERE PublicId = @ManagerPublicId AND IsDeleted = 0 AND Role = 'SalesManager' AND Status = 'Active' AND Id <> @id;
        IF @managerId IS NULL
            THROW 50400, 'The manager must be an active sales manager', 1;
    END
    ELSE IF @ManagerPublicId IS NOT NULL
        THROW 50400, 'A sales manager cannot have a manager', 1;

    UPDATE dbo.SalesUsers
    SET FirstName = @FirstName, LastName = @LastName, Email = @Email, MobileNumber = @MobileNumber, Role = @Role, ManagerId = @managerId,
        PermissionVersion = PermissionVersion + CASE WHEN @oldRole <> @Role OR ISNULL(@oldManagerId, 0) <> ISNULL(@managerId, 0) THEN 1 ELSE 0 END,
        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE Id = @id;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
