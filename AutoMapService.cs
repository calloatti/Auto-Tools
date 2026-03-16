using Bindito.Core;
using Timberborn.Automation;
using Timberborn.PlayerDataSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using UnityEngine;
using System;
using System.IO;

namespace Calloatti.AutoTools
{
  public enum MapDisplayState
  {
    Hidden,
    Single,
    Global
  }

  public partial class AutoMapService : ILoadableSingleton, IPostLoadableSingleton, IUnloadableSingleton, IDisposable
  {
    // Updated filename to AutoTools.txt
    private readonly string _configPath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, "AutoTools.txt");

    private readonly AutomatorRegistry _automatorRegistry;
    private readonly EventBus _eventBus;
    private readonly AutoMapInputService _inputService;
    private readonly QuickNotificationService _notificationService;
    private readonly EntitySelectionService _selectionService;

    private MapDisplayState _currentState = MapDisplayState.Hidden;
    private Automator _singleVisualizedAutomator;
    private Material _lineMaterial;
    private bool _isDirty = true;
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

      try
      {
        if (File.Exists(_configPath))
        {
          string content = File.ReadAllText(_configPath).Trim();
          if (Enum.TryParse(content, out MapDisplayState restoredState))
          {
            _currentState = restoredState;
          }
        }
        else
        {
          // Create default file if it doesn't exist (Default: Hidden)
          SaveState();
        }
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[AutoTools] Load error: {e.Message}");
      }
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _inputService.OnToggleAutoMap += ToggleAutoMap;

      foreach (Automator automator in _automatorRegistry.Automators)
      {
        automator.RelationsChanged += OnRelationsChanged;
      }

      RefreshVisuals(suppressInfoNotification: true);
    }

    public void Unload()
    {
      SaveState();
    }

    public void SaveState()
    {
      try
      {
        if (!Directory.Exists(PlayerDataFileService.PlayerDataDirectory))
        {
          Directory.CreateDirectory(PlayerDataFileService.PlayerDataDirectory);
        }
        File.WriteAllText(_configPath, _currentState.ToString());
      }
      catch (Exception e)
      {
        Debug.LogError($"[AutoTools] Save error: {e.Message}");
      }
    }

    public void Dispose()
    {
      _eventBus.Unregister(this);
      _inputService.OnToggleAutoMap -= ToggleAutoMap;

      foreach (Automator automator in _automatorRegistry.Automators)
      {
        automator.RelationsChanged -= OnRelationsChanged;
      }

      OnDispose();
    }
  }
}