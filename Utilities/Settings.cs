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
        try
        {
            return (settingList[settingName] ?? throw new InvalidOperationException()).Value<T>();
        }
        catch
        {
            var stringsetting = settingList[settingName].ToString();
            return JsonConvert.DeserializeObject<T>(stringsetting);
        }
        
    }

    public static void WriteSetting<T>(string settingName, T value)
    {
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("MiscSettings.json"));
        settingList[settingName] = JsonConvert.SerializeObject(value);
        File.WriteAllText("MiscSettings.json",JsonConvert.SerializeObject(settingList));
    }
}