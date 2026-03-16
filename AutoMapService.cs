using Bindito.Core;
using Timberborn.Automation;
using Timberborn.QuickNotificationSystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using UnityEngine;
using System;

namespace Calloatti.AutoTools
{
  public enum MapDisplayState
  {
    Hidden,
    Single,
    Global
  }

  public partial class AutoMapService : ILoadableSingleton, IPostLoadableSingleton, IDisposable
  {
    private readonly AutomatorRegistry _automatorRegistry;
    private readonly EventBus _eventBus;
    private readonly AutoMapInputService _inputService;
    private readonly QuickNotificationService _notificationService;
    private readonly EntitySelectionService _selectionService;

    private MapDisplayState _currentState = MapDisplayState.Hidden;
    private Automator _singleVisualizedAutomator;
    private Material _lineMaterial;

    // Tracks if the graph topology has changed and needs a full rebuild
    private bool _isDirty = true;

    // Tracks the currently visible partition ID to prevent notification spam
    private int _lastActivePartitionId = -1;

    [Inject]
    public AutoMapService(
        AutomatorRegistry automatorRegistry,
        EventBus eventBus,
        AutoMapInputService inputService,
        QuickNotificationService notificationService,
        EntitySelectionService selectionService)
    {
      _automatorRegistry = automatorRegistry;
      _eventBus = eventBus;
      _inputService = inputService;
      _notificationService = notificationService;
      _selectionService = selectionService;
    }

    public void Load()
    {
      InitializeVisuals();
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _inputService.OnToggleAutoMap += ToggleAutoMap;
    }

    public void Dispose()
    {
      _eventBus.Unregister(this);
      _inputService.OnToggleAutoMap -= ToggleAutoMap;

      UnsubscribeFromRelations(); // <-- ADDED: Safely unhook the C# event if a building was selected during unload

      OnDispose();
    }
  }
}