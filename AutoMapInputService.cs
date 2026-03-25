using Bindito.Core;
using System;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.AutoTools
{
  // 1. Added IDisposable
  public class AutoMapInputService : ILoadableSingleton, IInputProcessor, IDisposable
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
      Debug.Log("[AutoTools] AddInputProcessor");
    }

    public bool ProcessInput()
    {
      if (_inputService.IsKeyDown("Calloatti.AutoTools.KeyBind.Toggle.Map"))
      {
        OnToggleAutoMap?.Invoke();
        return false;
      }
      return false;
    }

    // 2. Added Dispose to clean up the dangling reference
    public void Dispose()
    {
      _inputService.RemoveInputProcessor(this);
    }
  }
}