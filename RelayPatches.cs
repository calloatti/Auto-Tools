using HarmonyLib;
using Timberborn.AutomationBuildings;
using Timberborn.BaseComponentSystem;
using UnityEngine.UIElements;

namespace Calloatti.AutoTools
{
  // State container to share UI references across the different string-patched methods
  public static class RelayUIState
  {
    public static Toggle ColorReplicationToggle;
    public static RelayColorReplicator CurrentReplicator;
  }

  // 1. InitializeFragment Patch (Using String name for the internal class)
  [HarmonyPatch("Timberborn.AutomationBuildingsUI.RelayFragment", "InitializeFragment")]
  public static class Patch_RelayFragment_InitializeFragment
  {
    [HarmonyPostfix]
    public static void Postfix(VisualElement __result)
    {
      RelayUIState.ColorReplicationToggle = new Toggle();
      RelayUIState.ColorReplicationToggle.text = "Replicate Input Colors";

      RelayUIState.ColorReplicationToggle.AddToClassList("game-toggle");
      RelayUIState.ColorReplicationToggle.AddToClassList("entity-panel__text");
      RelayUIState.ColorReplicationToggle.AddToClassList("entity-panel__toggle");

      RelayUIState.ColorReplicationToggle.RegisterValueChangedCallback(evt =>
      {
        if (RelayUIState.CurrentReplicator != null)
        {
          RelayUIState.CurrentReplicator.SetColorReplicationEnabled(evt.newValue);
        }
      });

      __result.Add(RelayUIState.ColorReplicationToggle);
    }
  }

  // 2. ShowFragment Patch
  [HarmonyPatch("Timberborn.AutomationBuildingsUI.RelayFragment", "ShowFragment")]
  public static class Patch_RelayFragment_ShowFragment
  {
    [HarmonyPostfix]
    public static void Postfix(BaseComponent entity)
    {
      if (entity != null)
      {
        RelayUIState.CurrentReplicator = entity.GetComponent<RelayColorReplicator>();
      }
    }
  }

  // 3. UpdateFragment Patch
  [HarmonyPatch("Timberborn.AutomationBuildingsUI.RelayFragment", "UpdateFragment")]
  public static class Patch_RelayFragment_UpdateFragment
  {
    [HarmonyPostfix]
    public static void Postfix()
    {
      if (RelayUIState.CurrentReplicator != null && RelayUIState.ColorReplicationToggle != null)
      {
        RelayUIState.ColorReplicationToggle.SetValueWithoutNotify(RelayUIState.CurrentReplicator.IsColorReplicationEnabled);
      }
    }
  }

  // 4. ClearFragment Patch
  [HarmonyPatch("Timberborn.AutomationBuildingsUI.RelayFragment", "ClearFragment")]
  public static class Patch_RelayFragment_ClearFragment
  {
    [HarmonyPostfix]
    public static void Postfix()
    {
      RelayUIState.CurrentReplicator = null;
    }
  }

  // 5. Relay Logic Patch (Relay is public, so typeof() works fine here)
  [HarmonyPatch(typeof(Relay), "Evaluate")]
  public static class Patch_Relay_Evaluate
  {
    [HarmonyPostfix]
    public static void Postfix(Relay __instance)
    {
      RelayColorReplicator replicator = __instance.GetComponent<RelayColorReplicator>();
      if (replicator != null)
      {
        replicator.EvaluateColors();
      }
    }
  }
}