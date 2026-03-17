using Bindito.Core;
using Calloatti.Config;
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
    private SimpleIniConfig _config;

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

    public bool IsReady { get; private set; } = false;

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
        _config = new SimpleIniConfig("AutoTools.txt");

        _currentState = _config.GetEnum("MapDisplayState", MapDisplayState.Hidden);

        // Load visual tuning variables with your new defaults
        _connectionHeightFraction = _config.GetFloat("ConnectionHeightFraction", 0.75f);
        _glowWidthMultiplier = _config.GetFloat("GlowWidthMultiplier", 8.0f);
        _glowAlpha = _config.GetFloat("GlowAlpha", 0.2f);
        _offStateBrightnessMultiplier = _config.GetFloat("OffStateBrightnessMultiplier", 0.6f);

        _config.Save();
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
      IsReady = true;
    }

    public void Unload() => SaveState();

    public void SaveState()
    {
      try
      {
        if (_config != null)
        {
          _config.Set("MapDisplayState", _currentState);
          _config.Save();
        }
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