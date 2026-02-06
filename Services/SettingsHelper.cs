using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace OpenClaw.Windows.Services
{
    public static class SettingsHelper
    {
        private static Dictionary<string, object> _localCache = new();
        private static string _settingsFilePath => Path.Combine(AppContext.BaseDirectory, "local.settings.json");
        private static string _logFilePath => Path.Combine(AppContext.BaseDirectory, "settings_debug.log");
        private static readonly object _lock = new();

        static SettingsHelper()
        {
            if (!IsPackaged())
            {
                lock (_lock)
                {
                    LoadLocalSettings();
                }
            }
        }

        private static void Log(string message)
        {
             try
             {
                 File.AppendAllText(_logFilePath, $"{DateTime.Now}: {message}\n");
             }
             catch {}
        }

        private static bool IsPackaged()
        {
            try
            {
                // This property getter throws on unpackaged
                return ApplicationData.Current != null;
            }
            catch
            {
                return false;
            }
        }

        private static void LoadLocalSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    _localCache = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                    Log($"Loaded Settings: {json}");
                }
                else 
                {
                    Log("Settings file not found, new cache.");
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading settings: {ex}");
            }
        }

        private static void SaveLocalSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_localCache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                Log($"Saved Settings: {json}");
            }
            catch (Exception ex)
            {
                Log($"Error saving settings: {ex}");
            }
        }

        public static T Get<T>(string key, T defaultValue = default!)
        {
            if (IsPackaged())
            {
                 // Packaged logic...
                 try 
                 {
                    if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var value))
                    {
                        if (value is T typedValue) return typedValue;
                    }
                    return defaultValue;
                 }
                 catch { return defaultValue; }
            }
            
            lock (_lock)
            {
                if (_localCache.TryGetValue(key, out var value))
                {
                     if (value is JsonElement element) 
                     {
                         try 
                         {
                             if (typeof(T) == typeof(bool)) return (T)(object)element.GetBoolean();
                             if (typeof(T) == typeof(string)) return (T)(object)(element.GetString() ?? string.Empty);
                             if (typeof(T) == typeof(int)) return (T)(object)element.GetInt32();
                         }
                         catch (Exception ex)
                         {
                             Log($"Error casting {key}: {ex}");
                         }
                     }
                     
                     if (value is T typedValue)
                     {
                         return typedValue;
                     }
                }
                return defaultValue;
            }
        }

        public static void Set<T>(string key, T value)
        {
            if (value == null) return;
            Log($"Setting {key} = {value}");

            if (IsPackaged())
            {
                 try { ApplicationData.Current.LocalSettings.Values[key] = value; } catch {}
            }
            else
            {
                lock (_lock)
                {
                    _localCache[key] = value;
                    SaveLocalSettings();
                }
            }
        }
        
        public static void Remove(string key)
        {
             Log($"Removing {key}");
             if (IsPackaged())
            {
                 try { ApplicationData.Current.LocalSettings.Values.Remove(key); } catch {}
            }
            else
            {
                lock (_lock)
                {
                    if (_localCache.ContainsKey(key))
                    {
                        _localCache.Remove(key);
                        SaveLocalSettings();
                    }
                    else
                    {
                        Log($"Key {key} not found in cache during Remove.");
                    }
                }
            }
        }
    }
}
