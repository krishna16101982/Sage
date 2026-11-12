namespace SageCommonLib.Globals
{
    public class Enumeration
    {
        public enum LogLevel
        {
            Trace = 0,
            Debug = 1,
            Information = 2,
            Warning = 3,
            Error = 4,
            Critical = 5
        }

        public enum SortDirection
        {
            Asc,
            Desc
        }

        public enum UserStatus
        {
            Active,
            Inactive,
            Locked
        }
    }
}
