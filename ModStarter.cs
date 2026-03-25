using Timberborn.ModManagerScene;
using Calloatti.Config;
using UnityEngine;

namespace Calloatti.AutoTools
{
  public class ModStarter : IModStarter
  {
    // 1. Declare the globally accessible static instance
    public static SimpleConfig Config { get; private set; }

    public void StartMod(IModEnvironment modEnvironment)
    {

      Debug.Log("[AutoTools] IModStarter.StartMod");

      // 2. Instantiate the config. This instantly runs the TXT synchronization.
      Config = new SimpleConfig(modEnvironment.ModPath);

      // The rest of your mod's initialization goes here...
    }
  }
}