-- My Profile: the signed-in admin changes their own name and mobile number. Email, username, group and status are not
-- changed here (an admin with AdminUser.Edit changes those), so the permission version is not bumped.
-- Raises 50404 when the user does not exist (or was deleted).
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_UpdateProfile
    @PublicId      UNIQUEIDENTIFIER,
    @FirstName     NVARCHAR(100),
    @LastName      NVARCHAR(100),
    @MobileNumber  NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @id BIGINT = (SELECT Id FROM dbo.AdminUsers WHERE PublicId = @PublicId AND IsDeleted = 0);
    IF @id IS NULL
        THROW 50404, 'Admin user not found', 1;

    UPDATE dbo.AdminUsers
    SET FirstName = @FirstName, LastName = @LastName, MobileNumber = @MobileNumber, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @id
    WHERE Id = @id;

    SELECT CAST(1 AS BIGINT);
END
GO
