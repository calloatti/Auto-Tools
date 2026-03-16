using Bindito.Core;
using HarmonyLib;
using UnityEngine;
using Timberborn.AssetSystem;
using Timberborn.SingletonSystem;

namespace Calloatti.AutoTools
{
  [Context("Game")]
  [Context("MapEditor")]
  internal class PatchConfigurator : IConfigurator
  {
    private const string HarmonyId = "calloatti.autotools";
    private static Harmony _harmony;

    public void Configure(IContainerDefinition containerDefinition)
    {
      containerDefinition.Bind<ColorNamesLoader>().AsSingleton();

      if (_harmony == null)
      {
        _harmony = new Harmony(HarmonyId);
        _harmony.PatchAll(typeof(PatchConfigurator).Assembly);
        PatchColor.Apply(_harmony);

        Debug.Log($"[{HarmonyId}] All Harmony patches applied successfully!");
      }
    }
  }

  internal class ColorNamesLoader : ILoadableSingleton
  {
    private readonly IAssetLoader _assetLoader;

    public ColorNamesLoader(IAssetLoader assetLoader)
    {
      _assetLoader = assetLoader;
    }

    public void Load()
    {
      // Updated to match your exact internal asset path!
      var textAsset = _assetLoader.LoadSafe<TextAsset>("resources/autotools.colornames");

      if (textAsset != null)
      {
        PatchColor.LoadColorNamesFromText(textAsset.text);
      }
      else
      {
        Debug.LogWarning("[AutoTools] Could not find 'resources/autotools.colornames' text asset.");
      }
    }
  }
}