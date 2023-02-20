namespace SWTORCombatParser.Utilities
{
    public static class StringExtensions
    {
        public static string MakePGSQLSafe(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return "";
            return str.Replace("\'", "''");
        }
    }
}
