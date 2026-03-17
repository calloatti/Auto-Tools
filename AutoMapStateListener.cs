using Bindito.Core;
using Timberborn.Automation;
using Timberborn.BaseComponentSystem;

namespace Calloatti.AutoTools
{
  // FIX: Added IAwakableComponent so Timberborn actually calls Awake()
  public class AutoMapStateListener : BaseComponent, IAwakableComponent, IAutomatorListener
  {
    private AutoMapService _autoMapService;
    private Automator _automator;

    [Inject]
    public void InjectDependencies(AutoMapService autoMapService)
    {
      _autoMapService = autoMapService;
    }

    public void Awake()
    {
      _automator = GetComponent<Automator>();
    }

    public void OnAutomatorStateChanged()
    {
      // 1. Guard against volatile loading phases. Completely ignore until PostLoad finishes!
      if (_autoMapService == null || !_autoMapService.IsReady)
      {
        return;
      }

      // 2. Defensive check just in case, and verify it's a transmitter
      if (_automator == null || !_automator.IsTransmitter)
      {
        return;
      }

      // Tell the global service to update the lines for this specific building
      _autoMapService.UpdateLineColors(_automator);
    }
  }
}