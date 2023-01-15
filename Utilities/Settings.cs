using System;
using System.Collections.Generic;
using System.IO;
using System.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SWTORCombatParser.Utilities;

public static class Settings
{
    private static string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
    private static string _settingsPath = Path.Combine(_appDataPath, "general_settings.json");

    private static void Init()
    {
        if (!Directory.Exists(_appDataPath))
            Directory.CreateDirectory(_appDataPath);
        if (!File.Exists(_settingsPath))
        {
            File.WriteAllText(_settingsPath,"{\"overlay_bar_scale\": 1.0,\"custom_audio_paths\": []}");
        }
    }
    
    public static List<T> GetListSetting<T>(string settingName)
    {
        Init();
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_settingsPath));
        if (!settingList.ContainsKey(settingName))
            settingList[settingName] = JsonConvert.ToString(new List<string>());
        var stringsetting = settingList[settingName].ToString();
        return JsonConvert.DeserializeObject<List<T>>(stringsetting);
    }
    public static T ReadSettingOfType<T>(string settingName)
    {
        Init();
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_settingsPath));
        if (!settingList.ContainsKey(settingName) && settingName == "stub_logs")
            settingList[settingName] = false;
        return settingList[settingName].Value<T>();
    }

    public static void WriteSetting<T>(string settingName, T value)
    {
        Init();
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_settingsPath));
        settingList[settingName] = JsonConvert.SerializeObject(value);
        File.WriteAllText(_settingsPath,JsonConvert.SerializeObject(settingList));
    }
}