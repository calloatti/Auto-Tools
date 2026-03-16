using Timberborn.Automation;
using UnityEngine;

namespace Calloatti.AutoTools
{
  public partial class AutoMapService
  {
    public void ToggleAutoMap()
    {
      // 1. The hotkey is the ONLY thing that changes the state
      switch (_currentState)
      {
        case MapDisplayState.Hidden:
          _currentState = MapDisplayState.Global;
          _notificationService.SendNotification("Auto Map: GLOBAL");
          break;
        case MapDisplayState.Global:
          _currentState = MapDisplayState.Single; // This is the Partition mode
          _notificationService.SendNotification("Auto Map: PARTITION");
          break;
        case MapDisplayState.Single:
          _currentState = MapDisplayState.Hidden;
          _notificationService.SendNotification("Auto Map: OFF");
          break;
      }

      // 2. Redraw the map, but SUPPRESS the partition info so the mode notification stays visible
      RefreshVisuals(suppressInfoNotification: true);
    }

    // 3. Added a default parameter so Events.cs can still call RefreshVisuals() normally
    public void RefreshVisuals(bool suppressInfoNotification = false)
    {
      if (_currentState == MapDisplayState.Hidden)
      {
        SetVisibility(false);
        _singleVisualizedAutomator = null;
        _lastActivePartitionId = -1;
      }
      else if (_currentState == MapDisplayState.Global)
      {
        if (_isDirty)
        {
          RebuildAllLines();
          _isDirty = false;
        }

        _singleVisualizedAutomator = null;
        _lastActivePartitionId = -1; // Reset so the notification works if we switch back to Single
        SetAllPartitionsActive(true);
        SetVisibility(true);
      }
      else if (_currentState == MapDisplayState.Single) // Partition mode
      {
        if (_isDirty)
        {
          RebuildAllLines();
          _isDirty = false;
        }

        Automator selectedAutomator = null;
        if (_selectionService.IsAnythingSelected)
        {
          selectedAutomator = _selectionService.SelectedObject.GetComponent<Automator>();
        }

        _singleVisualizedAutomator = selectedAutomator;

        if (selectedAutomator != null)
        {
          GameObject activeContainer = ShowOnlyPartition(selectedAutomator);
          SetVisibility(true);

          int currentId = -1;
          int currentCount = 0;

          // Extract the cached ID and count for the currently active container
          if (activeContainer != null && _networkInfo.TryGetValue(activeContainer, out var info))
          {
            currentId = info.id;
            currentCount = info.count;
          }

          // Only notify if we are looking at a DIFFERENT partition ID than before
          if (currentId != _lastActivePartitionId)
          {
            _lastActivePartitionId = currentId;

            // Don't overwrite the notification if we just pressed the mode toggle hotkey
            if (!suppressInfoNotification && currentId != -1)
            {
              _notificationService.SendNotification($"Partition ID: {currentId} | Items: {currentCount}");
            }
          }
        }
        else
        {
          SetVisibility(false); // Hide lines until a building is selected
          _lastActivePartitionId = -1;
        }
      }
    }
  }
}