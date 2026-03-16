using Bindito.Core;
using HarmonyLib;
using System;
using Timberborn.Automation;
using Timberborn.AutomationBuildings;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.CoreUI;
using Timberborn.EntitySystem;
using Timberborn.Illumination;
using Timberborn.IlluminationUI;
using Timberborn.Persistence;
using Timberborn.TemplateInstantiation;
using Timberborn.WorldPersistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.AutoTools
{
  // 1. DATA COMPONENT
  // Manages the logic for On/Off colors and ensures the light material remains powered.
  public class IndicatorDualColor : BaseComponent, IPersistentEntity, IInitializableEntity, IFinishedStateListener
  {
    private static readonly ComponentKey DualColorKey = new ComponentKey("IndicatorDualColor");
    private static readonly PropertyKey<Color> OnColorKey = new PropertyKey<Color>("OnColor");
    private static readonly PropertyKey<Color> OffColorKey = new PropertyKey<Color>("OffColor");

    public Color OnColor;
    public Color OffColor;
    public bool EditingOnColor = true;
    private bool _loadedFromSave = false;
    private IlluminatorToggle _myToggle;

    private IlluminationService _illuminationService;

    [Inject]
    public void InjectDependencies(IlluminationService illuminationService)
    {
      _illuminationService = illuminationService;
    }

    public void Load(IEntityLoader entityLoader)
    {
      if (entityLoader.TryGetComponent(DualColorKey, out IObjectLoader component))
      {
        if (component.Has(OnColorKey)) OnColor = component.Get(OnColorKey);
        if (component.Has(OffColorKey)) OffColor = component.Get(OffColorKey);
        _loadedFromSave = true;
      }
    }

    public void InitializeEntity()
    {
      if (!_loadedFromSave)
      {
        // Resolve the default color (Red) from the blueprint via IlluminationService
        Type defaultColorType = AccessTools.TypeByName("Timberborn.Illumination.DefaultIlluminatorColor");
        if (defaultColorType != null)
        {
          var defaultColorComponent = GameObject.GetComponent(defaultColorType);
          if (defaultColorComponent != null && _illuminationService != null)
          {
            string colorId = (string)AccessTools.Property(defaultColorType, "ColorId").GetValue(defaultColorComponent);
            OnColor = _illuminationService.FindColorById(colorId);
          }
        }

        // Failsafe
        if (OnColor.a < 0.01f) OnColor = Color.red;

        // Set default dimmed color to match vanilla unpowered look
        OffColor = new Color32(18, 22, 26, 255);
      }
    }

    public void OnEnterFinishedState()
    {
      if (_myToggle == null)
      {
        var illuminator = GetComponent<Illuminator>();
        if (illuminator != null) _myToggle = illuminator.CreateToggle();
      }
      // Keep the light material active so our custom Off Color is actually rendered
      _myToggle?.TurnOn();
    }

    public void OnExitFinishedState()
    {
      _myToggle?.TurnOff();
    }

    public void Save(IEntitySaver entitySaver)
    {
      IObjectSaver component = entitySaver.GetComponent(DualColorKey);
      component.Set(OnColorKey, OnColor);
      component.Set(OffColorKey, OffColor);
    }
  }

  // 2. CONFIGURATOR
  [Context("Game")]
  public class IndicatorDualColorConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<IndicatorDualColor>().AsTransient();
      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule()
    {
      TemplateModule.Builder builder = new TemplateModule.Builder();
      builder.AddDecorator<Indicator, IndicatorDualColor>();
      return builder.Build();
    }
  }

  // 3. LOGIC PATCHES
  [HarmonyPatch(typeof(CustomizableIlluminator), "Apply")]
  public static class Patch_CustomizableIlluminator_Apply
  {
    public static bool Prefix(CustomizableIlluminator __instance, IlluminatorColorizer ____illuminatorColorizer, ref Color? ____appliedColor)
    {
      if (__instance.TryGetComponent(out IndicatorDualColor dualColor) &&
          __instance.TryGetComponent(out Automatable automatable) &&
          __instance.TryGetComponent(out Indicator indicator))
      {
        Color targetColor;
        if (automatable.State == ConnectionState.On)
        {
          if (indicator.IsColorReplicationEnabled && automatable.Input != null && automatable.Input.TryGetComponent(out CustomizableIlluminator inputIllum))
          {
            targetColor = inputIllum.CustomColor;
          }
          else targetColor = dualColor.OnColor;
        }
        else targetColor = dualColor.OffColor;

        if (____appliedColor != targetColor)
        {
          ____illuminatorColorizer.SetColor(targetColor);
          ____appliedColor = targetColor;
          var eventDelegate = (MulticastDelegate)AccessTools.Field(typeof(CustomizableIlluminator), "AppliedColorChanged").GetValue(__instance);
          if (eventDelegate != null)
          {
            foreach (var handler in eventDelegate.GetInvocationList())
            {
              handler.Method.Invoke(handler.Target, new object[] { __instance, EventArgs.Empty });
            }
          }
        }
        return false;
      }
      return true;
    }
  }

  [HarmonyPatch(typeof(Indicator), "ReplicateInputColor")]
  public static class Patch_Indicator_ReplicateInputColor
  {
    public static bool Prefix(CustomizableIlluminator ____customizableIlluminator)
    {
      AccessTools.Method(typeof(CustomizableIlluminator), "Apply").Invoke(____customizableIlluminator, null);
      return false;
    }
  }

  [HarmonyPatch(typeof(Indicator), "Evaluate")]
  public static class Patch_Indicator_Evaluate
  {
    public static void Prefix(Automatable ____automatable, ref bool? ____previousState, out bool __state)
    {
      bool currentState = ____automatable.State == ConnectionState.On;
      __state = ____previousState != currentState;
    }

    public static void Postfix(bool __state, CustomizableIlluminator ____customizableIlluminator)
    {
      if (__state) AccessTools.Method(typeof(CustomizableIlluminator), "Apply").Invoke(____customizableIlluminator, null);
    }
  }

  [HarmonyPatch(typeof(CustomizableIlluminator), nameof(CustomizableIlluminator.SetCustomColor))]
  public static class Patch_CustomizableIlluminator_SetCustomColor
  {
    public static void Postfix(CustomizableIlluminator __instance, Color value)
    {
      if (__instance.TryGetComponent(out IndicatorDualColor dualColor))
      {
        if (dualColor.EditingOnColor) dualColor.OnColor = value;
        else dualColor.OffColor = value;
        AccessTools.Method(typeof(CustomizableIlluminator), "Apply").Invoke(__instance, null);
      }
    }
  }

  // 4. UI PATCHES
  [HarmonyPatch("Timberborn.IlluminationUI.CustomizableIlluminatorFragment", "InitializeFragment")]
  public static class Patch_CustomizableIlluminatorFragment_InitializeFragment
  {
    private static readonly string SelectedClass = "selected";

    public static void Postfix(object __instance, VisualElement __result, VisualElementLoader ____visualElementLoader)
    {
      VisualElement radioContainer = new VisualElement { name = "DualColorRadios" };
      radioContainer.style.flexDirection = FlexDirection.Row;
      radioContainer.style.marginBottom = 5;

      VisualElement onContainer = ____visualElementLoader.LoadVisualElement("Core/RadioButton");
      VisualElement offContainer = ____visualElementLoader.LoadVisualElement("Core/RadioButton");

      Button onBtn = onContainer.Q<Button>("RadioButton");
      Button offBtn = offContainer.Q<Button>("RadioButton");

      onBtn.name = "OnColorBtn";
      onBtn.Q<Label>("Text").text = "ON Color";
      onContainer.style.flexGrow = 1;

      offBtn.name = "OffColorBtn";
      offBtn.Q<Label>("Text").text = "OFF Color";
      offContainer.style.flexGrow = 1;

      radioContainer.Add(onContainer);
      radioContainer.Add(offContainer);
      __result.Insert(0, radioContainer);

      Type fragmentType = AccessTools.TypeByName("Timberborn.IlluminationUI.CustomizableIlluminatorFragment");

      onBtn.RegisterCallback<ClickEvent>(evt =>
      {
        var customIllum = AccessTools.Field(fragmentType, "_customizableIlluminator").GetValue(__instance) as CustomizableIlluminator;
        if (customIllum != null && customIllum.TryGetComponent(out IndicatorDualColor dualColor))
        {
          dualColor.EditingOnColor = true;
          onBtn.EnableInClassList(SelectedClass, true);
          offBtn.EnableInClassList(SelectedClass, false);
          customIllum.SetCustomColor(dualColor.OnColor);
        }
      });

      offBtn.RegisterCallback<ClickEvent>(evt =>
      {
        var customIllum = AccessTools.Field(fragmentType, "_customizableIlluminator").GetValue(__instance) as CustomizableIlluminator;
        if (customIllum != null && customIllum.TryGetComponent(out IndicatorDualColor dualColor))
        {
          dualColor.EditingOnColor = false;
          offBtn.EnableInClassList(SelectedClass, true);
          onBtn.EnableInClassList(SelectedClass, false);
          customIllum.SetCustomColor(dualColor.OffColor);
        }
      });
    }
  }

  [HarmonyPatch("Timberborn.IlluminationUI.CustomizableIlluminatorFragment", "ShowFragment")]
  public static class Patch_CustomizableIlluminatorFragment_ShowFragment
  {
    public static void Postfix(BaseComponent entity, VisualElement ____root)
    {
      VisualElement radioContainer = ____root.Q<VisualElement>("DualColorRadios");
      if (radioContainer == null) return;

      if (entity.TryGetComponent(out IndicatorDualColor dualColor))
      {
        radioContainer.style.display = DisplayStyle.Flex;
        Button onBtn = radioContainer.Q<Button>("OnColorBtn");
        Button offBtn = radioContainer.Q<Button>("OffColorBtn");

        onBtn.EnableInClassList("selected", dualColor.EditingOnColor);
        offBtn.EnableInClassList("selected", !dualColor.EditingOnColor);

        CustomizableIlluminator customIllum = entity.GetComponent<CustomizableIlluminator>();
        customIllum.SetCustomColor(dualColor.EditingOnColor ? dualColor.OnColor : dualColor.OffColor);
      }
      else radioContainer.style.display = DisplayStyle.None;
    }
  }
}