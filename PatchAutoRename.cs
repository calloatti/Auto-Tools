using HarmonyLib;
using Timberborn.EntityNamingUI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;
using UnityEngine; // Required for Debug.Log

namespace Calloatti.AutoTools
{
  // CRITICAL: This empty attribute tells PatchAll() to scan the methods inside this class!
  [HarmonyPatch]
  public static class RenameIsolationPatch
  {
    // A flag to track if the renaming dialog logic is currently running
    private static bool _isRenamingInProgress = false;

    // 1. Intercept the start of the Rename dialog
    [HarmonyPatch(typeof(EntityNameDialog), nameof(EntityNameDialog.Show))]
    [HarmonyPrefix]
    static void Prefix_EntityNameDialog_Show()
    {
      _isRenamingInProgress = true;
      Debug.Log("[AutoTools] EntityNameDialog.Show started. Flag set to TRUE.");
    }

    // 2. Intercept the end of the Rename dialog call
    [HarmonyPatch(typeof(EntityNameDialog), nameof(EntityNameDialog.Show))]
    [HarmonyPostfix]
    static void Postfix_EntityNameDialog_Show()
    {
      _isRenamingInProgress = false;
      Debug.Log("[AutoTools] EntityNameDialog.Show finished. Flag set to FALSE.");
    }

    // 3. Patch the Builder to check the flag before applying the limit
    // Change this from Prefix to Postfix!
    [HarmonyPatch(typeof(InputBoxShower.Builder), nameof(InputBoxShower.Builder.Show))]
    [HarmonyPostfix]
    static void Postfix_InputBoxBuilder_Show(TextField ____input)
    {
      if (_isRenamingInProgress)
      {
        // This now runs AFTER the vanilla code sets it to 24
        ____input.maxLength = 64;
        Debug.Log($"[AutoTools] SUCCESS: maxLength securely overridden to {____input.maxLength}!");
      }
    }
  }
}