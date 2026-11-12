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
        }
    }
}
