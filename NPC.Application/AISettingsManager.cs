namespace NPC.Application;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NPC.LLM;

public class AISettings
{
    public LLMConfig GlobalConfig { get; set; } = new LLMConfig();
    public Dictionary<string, LLMConfig> IndividualOverrides { get; set; } = new Dictionary<string, LLMConfig>();
    public string LogDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NPCSimulator_Logs");
}

public static class AISettingsManager
{
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai_settings.json");
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

    public static AISettings LoadSettings()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AISettings>(json, Options);
                if (settings != null)
                {
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading AI settings: {ex.Message}");
            }
        }
        return new AISettings();
    }

    public static void SaveSettings(AISettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving AI settings: {ex.Message}");
        }
    }
}
