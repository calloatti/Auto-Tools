using System;
using Timberborn.Automation;
using Timberborn.AutomationBuildings;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Illumination;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Calloatti.AutoTools
{
  public class RelayColorReplicator : BaseComponent, IAwakableComponent, IPersistentEntity, IFinishedStateListener
  {
    private static readonly ComponentKey ReplicatorKey = new ComponentKey("RelayColorReplicator");
    private static readonly PropertyKey<bool> IsEnabledKey = new PropertyKey<bool>("IsColorReplicationEnabled");

    private Relay _relay;
    private Automator _automator;
    private CustomizableIlluminator _customizableIlluminator;
    private CustomizableIlluminator _inputACustomizableIlluminator;
    private CustomizableIlluminator _inputBCustomizableIlluminator;

    private int _triggeringInput = 1; // 1 for A, 2 for B
    private bool _prevAActive;
    private bool _prevBActive;

    public bool IsColorReplicationEnabled { get; private set; }

    public void Awake()
    {
      _relay = GetComponent<Relay>();
      _automator = GetComponent<Automator>();
      _customizableIlluminator = GetComponent<CustomizableIlluminator>();
    }

    public void Save(IEntitySaver entitySaver)
    {
      IObjectSaver component = entitySaver.GetComponent(ReplicatorKey);
      component.Set(IsEnabledKey, IsColorReplicationEnabled);
    }

    public void Load(IEntityLoader entityLoader)
    {
      if (entityLoader.TryGetComponent(ReplicatorKey, out var objectLoader))
      {
        IsColorReplicationEnabled = objectLoader.Has(IsEnabledKey) && objectLoader.Get(IsEnabledKey);
      }
    }

    public void OnEnterFinishedState()
    {
      ResubscribeToInputColors();
      ReplicateInputColors();
      _automator.RelationsChanged += OnRelationsChanged;
    }

    public void OnExitFinishedState()
    {
      _automator.RelationsChanged -= OnRelationsChanged;
      UnsubscribeFromInputColors();
    }

    public void SetColorReplicationEnabled(bool value)
    {
      if (IsColorReplicationEnabled != value)
      {
        IsColorReplicationEnabled = value;
        ResubscribeToInputColors();
        ReplicateInputColors();
      }
    }

    public void EvaluateColors()
    {
      if (IsColorReplicationEnabled)
      {
        ReplicateInputColors();
      }
    }

    private void OnRelationsChanged(object sender, EventArgs e)
    {
      ResubscribeToInputColors();
      ReplicateInputColors();
    }

    private void ResubscribeToInputColors()
    {
      UnsubscribeFromInputColors();
      if (!IsColorReplicationEnabled) return;

      if (_relay.InputA != null)
      {
        _inputACustomizableIlluminator = _relay.InputA.GetComponent<CustomizableIlluminator>();
        if (_inputACustomizableIlluminator != null && _inputACustomizableIlluminator)
        {
          _inputACustomizableIlluminator.CustomColorChanged += OnInputCustomColorChanged;
        }
      }

      if (_relay.UsesInputB && _relay.InputB != null)
      {
        _inputBCustomizableIlluminator = _relay.InputB.GetComponent<CustomizableIlluminator>();
        if (_inputBCustomizableIlluminator != null && _inputBCustomizableIlluminator)
        {
          _inputBCustomizableIlluminator.CustomColorChanged += OnInputCustomColorChanged;
        }
      }

      if (_inputACustomizableIlluminator != null || _inputBCustomizableIlluminator != null)
      {
        _customizableIlluminator.Lock();
      }
    }

    private void UnsubscribeFromInputColors()
    {
      if (_inputACustomizableIlluminator != null)
      {
        _inputACustomizableIlluminator.CustomColorChanged -= OnInputCustomColorChanged;
        _inputACustomizableIlluminator = null;
      }
      if (_inputBCustomizableIlluminator != null)
      {
        _inputBCustomizableIlluminator.CustomColorChanged -= OnInputCustomColorChanged;
        _inputBCustomizableIlluminator = null;
      }
      _customizableIlluminator.Unlock();
    }

    private void OnInputCustomColorChanged(object sender, EventArgs e)
    {
      ReplicateInputColors();
    }

    private void ReplicateInputColors()
    {
      if (!IsColorReplicationEnabled) return;

      bool aActive = _relay.InputA != null && _relay.InputA.State == AutomatorState.On;
      bool bActive = _relay.UsesInputB && _relay.InputB != null && _relay.InputB.State == AutomatorState.On;

      // Determine which input caused the relay to turn ON based on its logic mode
      if (_relay.Mode == RelayMode.And)
      {
        if (aActive && bActive)
        {
          // The one that turned on last completed the AND circuit
          if (!_prevAActive && _prevBActive) _triggeringInput = 1;
          else if (_prevAActive && !_prevBActive) _triggeringInput = 2;
        }
      }
      else if (_relay.Mode == RelayMode.Or)
      {
        // The most recent input to turn ON becomes the trigger
        if (aActive && !_prevAActive) _triggeringInput = 1;
        if (bActive && !_prevBActive) _triggeringInput = 2;

        // Fallback: if the triggering input just turned off but the other is still on, revert to it
        if (_triggeringInput == 1 && !aActive && bActive) _triggeringInput = 2;
        if (_triggeringInput == 2 && !bActive && aActive) _triggeringInput = 1;
      }
      else if (_relay.Mode == RelayMode.Xor)
      {
        // The active input triggers the XOR circuit
        if (aActive && !bActive) _triggeringInput = 1;
        else if (bActive && !aActive) _triggeringInput = 2;
      }
      else
      {
        // NOT / Passthrough defaults to Input A
        _triggeringInput = 1;
      }

      // Save the state for the next evaluation tick
      _prevAActive = aActive;
      _prevBActive = bActive;

      // Only push a replicated color if the Relay output is actually evaluating to ON right now
      if (_automator.UnfinishedState != AutomatorState.On)
      {
        return;
      }

      Color finalColor = Color.white;
      bool colorFound = false;

      if (_triggeringInput == 2 && _relay.UsesInputB && _inputBCustomizableIlluminator != null)
      {
        finalColor = _inputBCustomizableIlluminator.CustomColor;
        colorFound = true;
      }
      else if (_inputACustomizableIlluminator != null)
      {
        // Fallback to A if B is missing or if A is the actual trigger
        finalColor = _inputACustomizableIlluminator.CustomColor;
        colorFound = true;
      }

      if (colorFound)
      {
        _customizableIlluminator.SetIsCustomized(true);
        _customizableIlluminator.SetCustomColor(finalColor);
      }
    }
  }
}