using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Timers
{
    public enum VariableComparisons
    {
        Equals,
        Less,
        Greater,
        Between
    }
    public enum VariableModifications
    {
        Add,
        Subtract,
        Set
    }
    public static class VariableManager
    {
        private static Dictionary<string,int> CustomVariables= new Dictionary<string,int>();
        public static void Init()
        {
            var allcurrentvariables = DefaultTimersManager.GetAllDefaults().SelectMany(s => s.Timers).Where(t=>!string.IsNullOrEmpty(t.ModifyVariableName)).Select(t=>t.ModifyVariableName).Distinct();

            CustomVariables = allcurrentvariables.ToDictionary(v => v, v => 0);
        }
        public static void ResetVariables()
        {
            foreach(var variable in CustomVariables.Keys.ToList())
            {
                CustomVariables[variable] = 0;
            }
        }
        public static void SetVariable(string variableName, int value)
        {
            CustomVariables[variableName] = value;
        }
        public static void AddToVariable(string variableName, int addition)
        {
            CustomVariables[variableName] += addition;
        }
        public static List<string> GetVariables()
        {
            return CustomVariables.Keys.ToList();
        }
        public static int GetValue(string variableName)
        {
            if(string.IsNullOrEmpty(variableName)) return 0;
            return CustomVariables[variableName];
        }
    }
}
