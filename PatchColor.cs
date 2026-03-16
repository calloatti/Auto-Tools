using HarmonyLib;
using Timberborn.Illumination;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Reflection;

namespace Calloatti.AutoTools
{
  public static class PatchColor
  {
    private static Label _colorNameLabel;
    private static bool _colorsLoaded = false;
    private static CustomizableIlluminator _cachedIlluminator;
    private static object _currentFragment;

    private static readonly Dictionary<int, string> ColorNames = new Dictionary<int, string>();

    public static void Apply(Harmony harmony)
    {
      var targetType = AccessTools.TypeByName("Timberborn.IlluminationUI.CustomizableIlluminatorFragment");
      if (targetType == null) return;

      var initMethod = AccessTools.Method(targetType, "InitializeFragment");
      var updateColorMethod = AccessTools.Method(targetType, "UpdateCustomColor");

      harmony.Patch(initMethod, postfix: new HarmonyMethod(typeof(PatchColor), nameof(InitializeLabel)));
      harmony.Patch(updateColorMethod, postfix: new HarmonyMethod(typeof(PatchColor), nameof(UpdateLabelText)));
    }

    public static void LoadColorNamesFromText(string text)
    {
      if (_colorsLoaded) return;
      _colorsLoaded = true;
      try
      {
        string[] lines = text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
          if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || (line.StartsWith("#") && !line.Contains(","))) continue;
          string[] parts = line.Split(',');
          if (parts.Length >= 2 && int.TryParse(parts[0].Trim().TrimStart('#'), System.Globalization.NumberStyles.HexNumber, null, out int colorInt))
          {
            ColorNames[colorInt] = parts[1].Trim();
          }
        }
      }
      catch (System.Exception e) { Debug.LogError($"[AutoTools] Load Error: {e.Message}"); }
    }

    private static void InitializeLabel(object __instance, VisualElement __result)
    {
      _currentFragment = __instance;
      var rgbField = __result.Q<TextField>("Rgb");
      var rgbContainer = rgbField?.parent;
      if (rgbContainer == null) return;

      // Event Cleanup
      if (_cachedIlluminator != null) _cachedIlluminator.AppliedColorChanged -= OnAppliedColorChanged;

      // UI Injection
      rgbContainer.style.flexDirection = FlexDirection.Column;
      _colorNameLabel = new Label("Selected Color") { style = { unityTextAlign = TextAnchor.MiddleCenter, color = new Color(0.7f, 0.7f, 0.7f), marginTop = -4 } };
      rgbContainer.Add(_colorNameLabel);

      // Link to Component
      var field = __instance.GetType().GetField("_customizableIlluminator", BindingFlags.NonPublic | BindingFlags.Instance);
      _cachedIlluminator = field?.GetValue(__instance) as CustomizableIlluminator;

      if (_cachedIlluminator != null)
      {
        // This native event captures the Copy-Paste action perfectly
        _cachedIlluminator.AppliedColorChanged += OnAppliedColorChanged;
      }

      // Hook Hover logic for Preset Buttons
      var presetButtonsField = AccessTools.Field(__instance.GetType(), "_presetColorButtons");
      var list = presetButtonsField?.GetValue(__instance) as System.Collections.IList;
      if (list != null)
      {
        foreach (object item in list)
        {
          var itemType = item.GetType();
          Color color = (Color)AccessTools.Field(itemType, "Item1").GetValue(item);
          Button btn = (Button)AccessTools.Field(itemType, "Item2").GetValue(item);
          Color32 c32 = color;
          int colorKey = (c32.r << 16) | (c32.g << 8) | c32.b;

          if (ColorNames.TryGetValue(colorKey, out string name))
          {
            btn.RegisterCallback<MouseEnterEvent>(evt => { if (_colorNameLabel != null) _colorNameLabel.text = name; });
            btn.RegisterCallback<MouseLeaveEvent>(evt => UpdateLabelText(__instance));
          }
        }
      }
    }

    private static void OnAppliedColorChanged(object sender, System.EventArgs e)
    {
      if (_currentFragment == null) return;

      // 1. Update our custom Name Label
      UpdateLabelText(_currentFragment);

      // 2. Force the Game's UI to refresh its Toggle and Hex field
      // This calls the internal method that syncs the UI elements with the component's state
      var refreshMethod = _currentFragment.GetType().GetMethod("ShowFragment", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
      if (refreshMethod != null)
      {
        // We pass the existing illuminator back in to trigger the native UI refresh
        refreshMethod.Invoke(_currentFragment, new object[] { _cachedIlluminator });
      }
    }

    private static void UpdateLabelText(object __instance)
    {
      if (_colorNameLabel == null || __instance == null) return;
      var field = __instance.GetType().GetField("_customizableIlluminator", BindingFlags.NonPublic | BindingFlags.Instance);
      if (field?.GetValue(__instance) is CustomizableIlluminator illuminator)
      {
        Color32 c = illuminator.CustomColor;
        int key = (c.r << 16) | (c.g << 8) | c.b;
        _colorNameLabel.text = ColorNames.TryGetValue(key, out string name) ? name : "Custom Hex Color";
      }
    }
  }
}