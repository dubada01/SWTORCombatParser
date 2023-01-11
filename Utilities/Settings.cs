using System;
using System.Collections.Generic;
using System.IO;
using System.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SWTORCombatParser.Utilities;

public static class Settings
{
    public static List<T> GetListSetting<T>(string settingName)
    {
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("MiscSettings.json"));
        var stringsetting = settingList[settingName].ToString();
        return JsonConvert.DeserializeObject<List<T>>(stringsetting);
    }
    public static T ReadSettingOfType<T>(string settingName)
    {
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("MiscSettings.json"));
        return (settingList[settingName] ?? throw new InvalidOperationException()).Value<T>();

    }

    public static void WriteSetting<T>(string settingName, T value)
    {
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("MiscSettings.json"));
        settingList[settingName] = JsonConvert.SerializeObject(value);
        File.WriteAllText("MiscSettings.json",JsonConvert.SerializeObject(settingList));
    }
}