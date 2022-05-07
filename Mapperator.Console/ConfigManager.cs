﻿using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Mapperator.Console.Resources;

namespace Mapperator.Console {
    public class ConfigManager {
        private static readonly JsonSerializer Serializer = new JsonSerializer {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        public static Config Config { get; set; } = new();
        public static bool InstanceComplete { get; private set; }

        public static void LoadConfig() {
            if (File.Exists(Constants.ConfigPath)) {
                InstanceComplete = LoadFromJson();
            } else {
                DefaultPaths();
                InstanceComplete = CreateJson();
            }
        }

        private static bool LoadFromJson() {
            try {
                using StreamReader sr = new StreamReader(Constants.ConfigPath);
                using JsonReader reader = new JsonTextReader(sr);
                Config = Serializer.Deserialize<Config>(reader);
            } catch (Exception ex) {
                System.Console.WriteLine(ex);
                return false;
            }
            return true;
        }

        private static bool CreateJson() {
            try {
                using StreamWriter sw = new StreamWriter(Constants.ConfigPath);
                using JsonWriter writer = new JsonTextWriter(sw);
                Serializer.Serialize(writer, Config);
            } catch (Exception ex) {
                System.Console.WriteLine(ex);
                return false;
            }
            return true;
        }

        public static void DefaultPaths() {
            if (string.IsNullOrWhiteSpace(Config.OsuPath)) {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    try {
                        var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                        Config.OsuPath = FindByDisplayName(regKey, "osu!");
                    } catch (KeyNotFoundException) {
                        try {
#pragma warning disable CA1416
                            var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
#pragma warning restore CA1416
                            Config.OsuPath = FindByDisplayName(regKey, "osu!");
                        }
                        catch (KeyNotFoundException) {
                            Config.OsuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!");
                        }
                    }
                } else {
                    Config.OsuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!");
                }
            }

            if (string.IsNullOrWhiteSpace(Config.SongsPath)) {
                var beatmapDirectory =
                    GetBeatmapDirectory(Path.Combine(Config.OsuPath, $"osu!.{Environment.UserName}.cfg"));
                Config.SongsPath = Path.Combine(Config.OsuPath, beatmapDirectory);
            }
        }

        private static string FindByDisplayName(RegistryKey parentKey, string name) {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new InvalidOperationException(Strings.WindowsOnlyOperation);

            string[] nameList = parentKey.GetSubKeyNames();
            foreach (var t in nameList) {
                RegistryKey regKey = parentKey.OpenSubKey(t);
                try {
                    if (regKey?.GetValue("DisplayName") != null && regKey.GetValue("DisplayName").ToString() == name) {
                        return Path.GetDirectoryName(regKey.GetValue("UninstallString").ToString());
                    }
                } catch (NullReferenceException) { }
            }

            throw new KeyNotFoundException($"Could not find registry key with display name \"{name}\".");
        }

        private static string GetBeatmapDirectory(string configPath) {
            try {
                foreach (var line in File.ReadLines(configPath)) {
                    var split = line.Split('=');
                    if (split[0].Trim() == "BeatmapDirectory") {
                        return split[1].Trim();
                    }
                }
            } catch (Exception exception) {
                System.Console.WriteLine(exception);
            }

            return "Songs";
        }
    }
}
