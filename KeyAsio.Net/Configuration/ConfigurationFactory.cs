﻿using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Net.Configuration;

public static class ConfigurationFactory
{
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(ConfigurationFactory));

    public static bool TryLoadConfigFromFile<T>(string path,
        [NotNullWhen(true)] out T? config,
        [NotNullWhen(false)] out Exception? e,
        YamlConverter? converter = null)
        where T : ConfigurationBase
    {
        converter ??= new YamlConverter();
        var type = typeof(T);
        ConfigurationBase? retConfig;

        if (!Path.IsPathRooted(path))
            path = Path.Combine(Environment.CurrentDirectory, path);

        if (!File.Exists(path))
        {
            retConfig = CreateDefaultConfigByPath(type, path, converter);
            Logger.LogInformation($"Config file \"{Path.GetFileName(path)}\" was not found. " +
                                  $"Default config was created and used.");
        }
        else
        {
            var content = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(content)) content = "default:\r\n";
            try
            {
                retConfig = converter.DeserializeSettings(content, type);
                SaveConfig(retConfig, path, converter);
                Logger.LogInformation($"Config file \"{Path.GetFileName(path)}\" was loaded.");
            }
            catch (Exception ex)
            {
                retConfig = null;
                config = (T?)retConfig;
                e = ex;
                return false;
            }
        }

        e = null;
        config = (T)retConfig;
        config!.SaveAction = async () => SaveConfig(retConfig, path, converter);
        return true;
    }

    private static ConfigurationBase CreateDefaultConfigByPath(Type type, string path, YamlConverter converter)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, "");
        var config = converter.DeserializeSettings("default:\r\n", type);
        SaveConfig(config, path, converter);
        return config;
    }

    private static void SaveConfig(ConfigurationBase config, string path, YamlConverter converter)
    {
        var content = converter.SerializeSettings(config);
        File.WriteAllText(path, content, config.Encoding);
    }
}