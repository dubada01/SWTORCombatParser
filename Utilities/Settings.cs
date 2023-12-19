using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace SWTORCombatParser.Utilities;

public static class Settings
{
    private static string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
    private static string _settingsPath = Path.Combine(_appDataPath, "general_settings.json");

    private static object _initLock = new object();

    private static void Init()
    {
        lock (_initLock)
        {
            if (!Directory.Exists(_appDataPath))
                Directory.CreateDirectory(_appDataPath);
            if (!File.Exists(_settingsPath))
            {
                File.WriteAllText(_settingsPath, "{\"overlay_bar_scale\": 1.0,\"custom_audio_paths\": []}");
            }
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
    public static Dictionary<T, T2> GetDictionarySetting<T, T2>(string settingName)
    {
        Init();
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_settingsPath));
        if (!settingList.ContainsKey(settingName))
            settingList[settingName] = JsonConvert.SerializeObject(new Dictionary<string, int>());
        var stringsetting = settingList[settingName].ToString();
        return JsonConvert.DeserializeObject<Dictionary<T, T2>>(stringsetting);
    }
    public static T ReadSettingOfType<T>(string settingName)
    {
        Init();
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_settingsPath));
        if (!settingList.ContainsKey(settingName) && settingName == "stub_logs")
            settingList[settingName] = false;
        if (!settingList.ContainsKey(settingName) && settingName == "offline_mode")
            settingList[settingName] = false;
        if (!settingList.ContainsKey(settingName) && settingName == "DynamicLayout")
            settingList[settingName] = false;
        if (!settingList.ContainsKey(settingName) && settingName == "force_log_updates")
            settingList[settingName] = false;
        if (!settingList.ContainsKey(settingName) && settingName == "combat_logs_path")
            settingList[settingName] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
        if(settingList.TryGetValue(settingName, out var returnVal))
            return returnVal.Value<T>();
        return default;
    }
    public static bool HasSetting(string settingName)
    {
        Init();
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_settingsPath));
        return settingList.ContainsKey(settingName);
    }
    public static void WriteSetting<T>(string settingName, T value)
    {
        Init();
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_settingsPath));
        settingList[settingName] = JsonConvert.SerializeObject(value);
        File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(settingList));
    }
}