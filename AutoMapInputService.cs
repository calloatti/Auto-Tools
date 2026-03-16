using Bindito.Core;
using System;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.AutoTools
{
  public class AutoMapInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    public event Action OnToggleAutoMap;

    [Inject]
    public AutoMapInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
      Debug.Log("[AutoTools] Input service loaded and listening for hotkeys.");
    }

    public bool ProcessInput()
    {
      // Ensure you register this specific keybinding in your mod's config!
      if (_inputService.IsKeyDown("Calloatti.AutoTools.KeyBind.Toggle.Map"))
      {
        OnToggleAutoMap?.Invoke();
        return false;
      }
      return false;
    }
  }
}