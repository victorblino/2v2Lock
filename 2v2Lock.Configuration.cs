using System.Text.Json;
using System.Text.Json.Serialization;

namespace _2v2Lock;

public partial class _2v2Lock
{
    private const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions ConfigSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private sealed record PluginConfig
    {
        [JsonPropertyName("debug")]
        public bool Debug { get; init; } = false;
    }

    private PluginConfig _config = new();

    private bool IsDebugEnabled()
    {
        return _config.Debug;
    }

    private void LoadConfig()
    {
        var configPath = GetConfigPath();

        try
        {
            if (!File.Exists(configPath))
            {
                _config = new PluginConfig();
                WriteConfig(configPath, _config);
                return;
            }

            var json = File.ReadAllText(configPath);
            _config = JsonSerializer.Deserialize<PluginConfig>(json, ConfigSerializerOptions) ?? new PluginConfig();
        }
        catch (Exception ex)
        {
            _config = new PluginConfig();
            Console.WriteLine($"[2v2Lock] Failed to load {ConfigFileName}: {ex.Message}");
        }
    }

    private static string GetConfigPath()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(_2v2Lock).Assembly.Location);
        var baseDirectory = string.IsNullOrWhiteSpace(assemblyDirectory)
            ? AppContext.BaseDirectory
            : assemblyDirectory;

        return Path.Combine(baseDirectory, ConfigFileName);
    }

    private static void WriteConfig(string configPath, PluginConfig config)
    {
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(config, ConfigSerializerOptions);
        File.WriteAllText(configPath, json);
    }
}
