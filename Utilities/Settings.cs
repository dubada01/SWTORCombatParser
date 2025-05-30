using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.DataStructures.Hotkeys;
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
            settingList[settingName] = JsonConvert.SerializeObject(new List<string>());
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
        // options are: data_grid, details, plot, log
        if (!settingList.ContainsKey(settingName) && settingName == "current_tab")
            settingList[settingName] = "data_grid";
        if (!settingList.ContainsKey(settingName) && settingName == "grid_sort")
            settingList[settingName] = "Damage_+_1";
        if (!settingList.ContainsKey(settingName) && settingName == "offline_mode")
            settingList[settingName] = false;
        if (!settingList.ContainsKey(settingName) && settingName == "DynamicLayout")
            settingList[settingName] = false;
        if (!settingList.ContainsKey(settingName) && settingName == "force_log_updates")
            settingList[settingName] = false;
        if (!settingList.ContainsKey(settingName) && settingName == "threat_table_ids")
            settingList[settingName] = JToken.FromObject(new List<long>());
        if (!settingList.ContainsKey(settingName) && settingName == "combat_logs_path")
            settingList[settingName] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic/CombatLogs");
        if (!settingList.ContainsKey(settingName) && settingName == "Hotkeys")
            settingList[settingName] = JToken.FromObject(new HotkeySettings {
                HOTRefreshEnabled = true, HOTRefreshHotkeyMod1 = 2,  HOTRefreshHotkeyMod2 = 1, HOTRefreshHotkeyStroke = 0x52,
                UILockEnabled = true, UILockHotkeyMod1 = 2, UILockHotkeyMod2 = 1, UILockHotkeyStroke = 0x4c});
        if (settingList.TryGetValue(settingName, out var settingValue))
        {
            try
            {
                // Check if the type is string and handle directly
                if (typeof(T) == typeof(string))
                {
                    return settingValue.ToObject<T>();
                }
                // Handle numeric and other simple types directly
                else if (settingValue.Type == JTokenType.Integer || settingValue.Type == JTokenType.Float || settingValue.Type == JTokenType.Boolean)
                {
                    return settingValue.ToObject<T>();
                }
                // Handle complex types or settings stored as strings that need parsing/conversion
                else
                {
                    var serializedValue = settingValue.Type == JTokenType.String ? settingValue.ToString() : settingValue.ToString(Formatting.None);
                    return JsonConvert.DeserializeObject<T>(serializedValue);
                }
            }
            catch (JsonException ex)
            {
                // Log or handle the error appropriately
                Logging.LogError($"Error deserializing setting '{settingName}' to type {typeof(T).Name}: {ex.Message}");
            }
        }

        return default(T);

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

        // Check if the value is a string
        if (value is string stringValue)
        {
            // Directly assign the string without serializing again
            settingList[settingName] = stringValue;
        }
        else
        {
            // Serialize if it's not a string
            settingList[settingName] = JsonConvert.SerializeObject(value);
        }

        File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(settingList));
    }

}