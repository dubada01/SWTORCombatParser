using System.Collections.Generic;
using System.Linq;

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
    public static class OrbsVariableManager
    {
        private static Dictionary<string, int> CustomVariables = new Dictionary<string, int>();
        public static void RefreshVariables()
        {
            var allcurrentvariables = DefaultOrbsTimersManager.GetAllDefaults().SelectMany(s => s.Timers).Where(t => !string.IsNullOrEmpty(t.ModifyVariableName)).Select(t => t.ModifyVariableName).Distinct();

            CustomVariables = allcurrentvariables.ToDictionary(v => v, v => 0);
        }
        public static void ResetVariables()
        {
            foreach (var variable in CustomVariables.Keys.ToList())
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
            CustomVariables.TryAdd(variableName, 0);
            CustomVariables[variableName] += addition;
        }
        public static List<string> GetVariables()
        {
            return CustomVariables.Keys.ToList();
        }
        public static int GetValue(string variableName)
        {
            if (string.IsNullOrEmpty(variableName) || !CustomVariables.ContainsKey(variableName)) return -1;
            return CustomVariables[variableName];
        }
    }
}
