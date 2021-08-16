using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser.Utilities
{
    public static class StringExtensions
    {
        public static string MakePGSQLSafe(this string str)
        {
            return str.Replace("\'", "''");
        }
    }
}
