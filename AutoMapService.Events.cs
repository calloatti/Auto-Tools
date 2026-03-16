using System;
using Timberborn.Automation;
using Timberborn.EntitySystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;

namespace Calloatti.AutoTools
{
  public partial class AutoMapService
  {
    [OnEvent]
    public void OnEntityInitialized(EntityInitializedEvent e)
    {
      if (e.Entity.HasComponent<Automator>())
      {
        e.Entity.GetComponent<Automator>().RelationsChanged += OnRelationsChanged;
        MarkDirty();
      }
    }

    [OnEvent]
    public void OnEntityDeleted(EntityDeletedEvent e)
    {
      if (e.Entity.HasComponent<Automator>())
      {
        e.Entity.GetComponent<Automator>().RelationsChanged -= OnRelationsChanged;
        MarkDirty();
      }
    }

    [OnEvent]
    public void OnSelectableObjectSelected(SelectableObjectSelectedEvent e)
    {
      // Re-trigger visual logic so Partition mode knows which lines to show
      RefreshVisuals();
    }

    [OnEvent]
    public void OnSelectableObjectUnselected(SelectableObjectUnselectedEvent e)
    {
      // Re-trigger visual logic so lines hide when nothing is selected
      RefreshVisuals();
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