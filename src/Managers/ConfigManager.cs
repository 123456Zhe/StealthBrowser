
using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using Newtonsoft.Json;
using StealthBrowser.Utils;

namespace StealthBrowser.Managers {

	/// <summary>
	/// App config manager which saves app-level settings in a JSON file.
	/// </summary>
	public static class ConfigManager {

		public static bool InitDone;
		public static string AppDataPath;
		private static string SettingsFilePath;
		private static Dictionary<string, object> SettingsDict;
		private static bool SettingsChanged = false;
		private static Timer SaveTimer;
		// UI 线程（Get/Set）与保存定时器线程（SaveSettings）都会读写 SettingsDict，
		// Dictionary 非线程安全，并发读写可能抛异常，故用同一把锁保护。
		private static readonly object SettingsLock = new object();

		/// <summary>
		/// Loads existing settings from the AppData folder if they exist.
		/// Starts a timer that saves settings every second if changes have occurred.
		/// </summary>
		public static void Init(string appName) {
			if (SettingsDict == null) {
				// 便携化：所有数据（设置、缓存、Cookie）都存在程序目录下的 data 文件夹，
				// 整个文件夹拷走即迁移，公共电脑不留痕迹。
				AppDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
				AppDataPath.EnsureFolderExists();
				SettingsFilePath = Path.Combine(AppDataPath, "settings.json");

				SettingsDict = LoadSettings();
				InitDone = true;

				SaveTimer = new Timer(1000);
				SaveTimer.Elapsed += SaveTimerElapsed;
				SaveTimer.Start();
			}
		}

		/// <summary>
		/// Returns the setting for the given key, with an optional default value
		/// </summary>
		public static object Get(string key, object defaultVal = null) {
			if (SettingsDict == null) throw new Exception("You need to call ZConfig.Init() first!");
			lock (SettingsLock) {
				if (SettingsDict.ContainsKey(key)) return SettingsDict[key];
			}
			return defaultVal;
		}

		/// <summary>
		/// Returns the integer setting for the given key
		/// </summary>
		public static int GetInt(string key, int defaultVal = 0) {
			var val = Convert.ToInt32(Get(key, defaultVal));
			return val == 0 ? defaultVal : val;
		}

		/// <summary>
		/// Returns the double setting for the given key
		/// </summary>
		public static double GetDouble(string key, double defaultVal = 0.0) {
			var val = Convert.ToDouble(Get(key, defaultVal));
			return val == 0 ? defaultVal : val;
		}

		/// <summary>
		/// Returns the string setting for the given key
		/// </summary>
		public static string GetString(string key, string defaultVal = "") {
			var value = Get(key, defaultVal);
			if (value == null) return defaultVal;
			return value is string valString ? valString : value.ToString();
		}

		/// <summary>
		/// Returns the boolean setting for the given key
		/// </summary>
		public static bool GetBool(string key, bool defaultVal = false) {
			var val = GetString(key, defaultVal.ToString());
			return val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1";
		}

		/// <summary>
		/// Saves a setting by key, and persists it to disk within 1 second.
		/// </summary>
		public static void Set(string key, object value) {
			if (SettingsDict == null) throw new Exception("Call ConfigManager.Init() first!");
			lock (SettingsLock) {
				SettingsDict[key] = value;
				SettingsChanged = true;
			}
		}

		/// <summary>
		/// Returns the settings from the JSON file or an empty dictionary if the file doesn't exist
		/// </summary>
		private static Dictionary<string, object> LoadSettings() {
			if (File.Exists(SettingsFilePath)) {
				try {
					var json = File.ReadAllText(SettingsFilePath);
					return JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
				} catch (Exception) {
					// settings.json 已损坏（例如上次写入中途崩溃）：备份坏文件后用空配置启动，
					// 否则每次启动都会在加载处崩溃。
					try {
						var backup = SettingsFilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
						File.Move(SettingsFilePath, backup);
					} catch (Exception) {
						// 备份失败也不要炸启动，直接用空配置。
					}
					return new Dictionary<string, object>();
				}
			}
			return new Dictionary<string, object>();
		}

		/// <summary>
		/// Saves the settings to the JSON file
		/// </summary>
		public static void SaveSettings() {
			lock (SettingsLock) {
				var json = JsonConvert.SerializeObject(SettingsDict, Formatting.Indented);
				// 原子写：先写临时文件再替换，避免写一半崩溃导致 settings.json 损坏；
				// 序列化与写盘同在锁内，避免 Set 的改动在快照后、清标志前丢失。
				var tempPath = SettingsFilePath + ".tmp";
				File.WriteAllText(tempPath, json);
				if (File.Exists(SettingsFilePath)) {
					File.Replace(tempPath, SettingsFilePath, null);
				} else {
					File.Move(tempPath, SettingsFilePath);
				}
				SettingsChanged = false;
			}
		}

		/// <summary>
		/// Timer event handler that checks if settings have changed and saves them
		/// </summary>
		private static void SaveTimerElapsed(object sender, ElapsedEventArgs e) {
			if (SettingsChanged) {
				SaveSettings();
			}
		}
	}

}
