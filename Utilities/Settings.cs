using System;
using System.IO;
using System.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SWTORCombatParser.Utilities;

public static class Settings
{
    public static T ReadSettingOfType<T>(string settingName)
    {
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("MiscSettings.json"));
        return (settingList[settingName] ?? throw new InvalidOperationException()).Value<T>();
    }

    public static void WriteSetting<T>(string settingName, T value)
    {
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("MiscSettings.json"));
        settingList[settingName] = value.ToString();
        File.WriteAllText("MiscSettings.json",JsonConvert.SerializeObject(settingList));
    }
}