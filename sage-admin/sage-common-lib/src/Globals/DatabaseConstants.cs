namespace SageCommonLib.Globals
{
    public class DatabaseConstants
    {
        public static class ColumnNames
        {
            public const string ASC = "ASC";
            public const string DESC = "DESC";
            public const string Id = "Id";
            public const string Code = "Code";
            public const string Name = "Name";
            public const string Description = "Description";
            public const string Status = "Status";
            public const string CreatedAtUtc = "CreatedAtUtc";
            public const string UpdatedAtUtc = "UpdatedAtUtc";
        }

        public static class DataTypes
        {
            public const string String = "string";
            public const string Int = "int";
            public const string Long = "long";
            public const string Bool = "bool";
            public const string DateTime = "datetime";
            public const string Guid = "guid";
        }

        /// <summary>SQL Server error numbers the data layer maps to application errors.</summary>
        public static class SqlErrorNumbers
        {
            public const int UniqueConstraintViolation = 2627;
            public const int UniqueIndexViolation = 2601;
            public const int ForeignKeyViolation = 547;
            public const int Deadlock = 1205;

            // Raised by our stored procedures with THROW (user error numbers start at 50000). The THROW message is shown to the user.
            /// <summary>THROW 50400: the input breaks a business rule.</summary>
            public const int ProcedureInvalidInput = 50400;
            /// <summary>THROW 50404: the referenced record does not exist.</summary>
            public const int ProcedureNotFound = 50404;
            /// <summary>THROW 50409: the record already exists.</summary>
            public const int ProcedureAlreadyExists = 50409;
        }
    }
}
