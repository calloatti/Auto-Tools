using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Timberborn.PlayerDataSystem;

namespace Calloatti.Config
{
  public class SimpleIniConfig
  {
    private readonly string _filePath;

    private readonly Dictionary<string, string> _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _comments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public SimpleIniConfig(string fileName)
    {
      _filePath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, fileName);
      Load();
    }

    public void Load()
    {
      _settings.Clear();
      _comments.Clear();
      if (!File.Exists(_filePath)) return;

      foreach (string line in File.ReadAllLines(_filePath))
      {
        string trimmed = line.Trim();

        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
          continue;

        int equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex > 0)
        {
          string key = trimmed.Substring(0, equalsIndex).Trim();
          string rawValue = trimmed.Substring(equalsIndex + 1);

          int hashIndex = rawValue.IndexOf('#');
          int slashIndex = rawValue.IndexOf("//");

          int commentIndex = -1;
          if (hashIndex >= 0 && slashIndex >= 0) commentIndex = Math.Min(hashIndex, slashIndex);
          else if (hashIndex >= 0) commentIndex = hashIndex;
          else if (slashIndex >= 0) commentIndex = slashIndex;

          if (commentIndex >= 0)
          {
            _settings[key] = rawValue.Substring(0, commentIndex).Trim();
            _comments[key] = rawValue.Substring(commentIndex).Trim();
          }
          else
          {
            _settings[key] = rawValue.Trim();
          }
        }
      }
    }

public void Save()
{
  Directory.CreateDirectory(PlayerDataFileService.PlayerDataDirectory);
  List<string> outputLines = new List<string>();
  HashSet<string> writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

  if (File.Exists(_filePath))
  {
    foreach (string line in File.ReadAllLines(_filePath))
    {
      string trimmed = line.Trim();
      // Keep comments and existing whitespace exactly as they were
      if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
      {
        outputLines.Add(line);
        continue;
      }

      int equalsIndex = trimmed.IndexOf('=');
      if (equalsIndex > 0)
      {
        string key = trimmed.Substring(0, equalsIndex).Trim();

        if (_settings.TryGetValue(key, out string val))
        {
          string comment = _comments.TryGetValue(key, out string c) ? $" {c}" : "";
          outputLines.Add($"{key}={val}{comment}");
          writtenKeys.Add(key);
        }
      }
      else
      {
        outputLines.Add(line);
      }
    }
  }

  // Add any keys that weren't in the original file
  foreach (var kvp in _settings)
  {
    if (!writtenKeys.Contains(kvp.Key))
    {
      // REMOVED: The check that was adding outputLines.Add("");
      string comment = _comments.TryGetValue(kvp.Key, out string c) ? $" {c}" : "";
      outputLines.Add($"{kvp.Key}={kvp.Value}{comment}");
    }
  }

  File.WriteAllLines(_filePath, outputLines);
}

    // --- UTILITY METHODS ---

    public bool HasKey(string key) => _settings.ContainsKey(key);

    public void DeleteKey(string key)
    {
      _settings.Remove(key);
      _comments.Remove(key);
    }

    // --- GETTERS ---

    public string GetString(string key, string defaultValue)
    {
      if (_settings.TryGetValue(key, out string val)) return val;
      _settings[key] = defaultValue;
      return defaultValue;
    }

    public bool GetBool(string key, bool defaultValue)
    {
      if (_settings.TryGetValue(key, out string val) && bool.TryParse(val, out bool result))
        return result;
      _settings[key] = defaultValue.ToString();
      return defaultValue;
    }

    public int GetInt(string key, int defaultValue)
    {
      if (_settings.TryGetValue(key, out string val) && int.TryParse(val, out int result))
        return result;
      _settings[key] = defaultValue.ToString();
      return defaultValue;
    }

    public float GetFloat(string key, float defaultValue)
    {
      if (_settings.TryGetValue(key, out string val) &&
          float.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
        return result;
      _settings[key] = defaultValue.ToString(CultureInfo.InvariantCulture);
      return defaultValue;
    }

    public T GetEnum<T>(string key, T defaultValue) where T : struct, Enum
    {
      // The 'true' parameter makes the parsing case-insensitive!
      if (_settings.TryGetValue(key, out string val) && Enum.TryParse<T>(val, true, out T result))
        return result;
      _settings[key] = defaultValue.ToString();
      return defaultValue;
    }

    // --- SETTERS ---

    public void Set(string key, object value)
    {
      if (value is float f)
        _settings[key] = f.ToString(CultureInfo.InvariantCulture);
      else
        _settings[key] = value.ToString();
    }

    public void Set(string key, object value, string comment)
    {
      Set(key, value);
      SetComment(key, comment);
    }

    public void SetComment(string key, string comment)
    {
      if (string.IsNullOrWhiteSpace(comment))
      {
        _comments.Remove(key);
        return;
      }

      string trimmed = comment.TrimStart();
      if (!trimmed.StartsWith("#") && !trimmed.StartsWith("//"))
      {
        comment = "# " + comment;
      }

      _comments[key] = comment;
    }
  }
}