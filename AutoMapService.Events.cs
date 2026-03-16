using System;
using Timberborn.Automation;
using Timberborn.EntitySystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;

namespace Calloatti.AutoTools
{
  public partial class AutoMapService
  {
    private Automator _subscribedAutomator;

    [OnEvent]
    public void OnEntityInitialized(EntityInitializedEvent e)
    {
      if (e.Entity.HasComponent<Automator>()) MarkDirty();
    }

    [OnEvent]
    public void OnEntityDeleted(EntityDeletedEvent e)
    {
      if (e.Entity.HasComponent<Automator>()) MarkDirty();
    }

    [OnEvent]
    public void OnSelectableObjectSelected(SelectableObjectSelectedEvent e)
    {
      Automator newSelection = e.SelectableObject.GetComponent<Automator>();

      if (newSelection != null) SubscribeToRelations(newSelection);
      else UnsubscribeFromRelations();

      // Silently update the drawing WITHOUT triggering a heavy rebuild (unless dirty)
      RefreshVisuals();
    }

    [OnEvent]
    public void OnSelectableObjectUnselected(SelectableObjectUnselectedEvent e)
    {
      UnsubscribeFromRelations();
      RefreshVisuals();
    }

    private void SubscribeToRelations(Automator automator)
    {
      UnsubscribeFromRelations();
      _subscribedAutomator = automator;
      _subscribedAutomator.RelationsChanged += OnRelationsChanged;
    }

    private void UnsubscribeFromRelations()
    {
      if (_subscribedAutomator != null)
      {
        _subscribedAutomator.RelationsChanged -= OnRelationsChanged;
        _subscribedAutomator = null;
      }
    }

    private void OnRelationsChanged(object sender, EventArgs e)
    {
      MarkDirty();
    }

    private void MarkDirty()
    {
      _isDirty = true;
      RefreshVisuals();
    }
  }
}