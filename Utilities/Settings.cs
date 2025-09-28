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

    private static JObject GetValidSettings()
    {
        Init();
        var settingList = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_settingsPath));
        return settingList ?? new JObject();
    }

    public static List<T>? GetListSetting<T>(string settingName)
    {
        var settingList = GetValidSettings();
        if (!settingList.ContainsKey(settingName) || settingList[settingName] == null)
        {
            var setting = new List<T>();
            settingList[settingName] = JsonConvert.SerializeObject(setting);
            return setting;
        }
        return JsonConvert.DeserializeObject<List<T>>(settingList[settingName]!.ToString());
    }
    public static Dictionary<T, T2>? GetDictionarySetting<T, T2>(string settingName) where T : notnull
    {
        var settingList = GetValidSettings();
        if (!settingList.ContainsKey(settingName) || settingList[settingName] == null)
        {
            var setting = new Dictionary<T, T2>();
            settingList[settingName] = JsonConvert.SerializeObject(setting);
            return setting;
        }
        return JsonConvert.DeserializeObject<Dictionary<T, T2>>(settingList[settingName]!.ToString());
    }
    public static T? GetSettingDefault<T>(string settingName)
    {
        switch (settingName)
        {
            case "stub_logs":
                return (T)(object)false;
            case "current_tab":
                return (T)(object)"data_grid";
            case "grid_sort":
                return (T)(object)"Damage_+_1";
            case "offline_mode":
                return (T)(object)false;
            case "DynamicLayout":
                return (T)(object)false;
            case "force_log_updates":
                return (T)(object)false;
            case "threat_table_ids":
                return (T)(object)new List<long>();
            case "combat_logs_path":
                return (T)(object)Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Star Wars - The Old Republic/CombatLogs");
            case "Hotkeys":
                return (T)(object)new HotkeySettings
                {
                    HOTRefreshEnabled = true,
                    HOTRefreshHotkeyMod1 = 2,
                    HOTRefreshHotkeyMod2 = 1,
                    HOTRefreshHotkeyStroke = 0x52,
                    UILockEnabled = true,
                    UILockHotkeyMod1 = 2,
                    UILockHotkeyMod2 = 1,
                    UILockHotkeyStroke = 0x4c
                };
        }
        return default;
    }
    public static T ReadSettingOfType<T>(string settingName)
    {
        var settingList = GetValidSettings();
        if (!settingList.ContainsKey(settingName))
        {
            T? default_value = GetSettingDefault<T>(settingName);
            if (default_value != null)
            {
                settingList[settingName] = JToken.FromObject(default_value);
            }
        }

        if (settingList.TryGetValue(settingName, out var settingValue))
        {
            try
            {
                // Check if the type is string and handle directly
                if (typeof(T) == typeof(string))
                {
                    return settingValue.ToObject<T>()!;
                }
                // Handle numeric and other simple types directly
                else if (settingValue.Type == JTokenType.Integer || settingValue.Type == JTokenType.Float || settingValue.Type == JTokenType.Boolean)
                {
                    return settingValue.ToObject<T>()!;
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

        return default;
    }

    public static bool HasSetting(string settingName)
    {
        var settingList = GetValidSettings();
        return settingList.ContainsKey(settingName);
    }
    public static void WriteSetting<T>(string settingName, T value)
    {
        var settingList = GetValidSettings();

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