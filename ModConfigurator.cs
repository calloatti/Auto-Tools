using Bindito.Core;
using Timberborn.Automation;
using Timberborn.TemplateInstantiation;
using UnityEngine;

namespace Calloatti.AutoTools
{
  [Context("Game")]
  public class ModConfigurator : Configurator
  {
    protected override void Configure()
    {

      Debug.Log("[AutoTools] Configurator.Configure");

      Bind<AutoMapInputService>().AsSingleton();
      Bind<AutoMapService>().AsSingleton();
      Bind<AutoMapHoverService>().AsSingleton();

      // 1. Bind our custom listener
      Bind<AutoMapStateListener>().AsTransient();

      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule()
    {
      TemplateModule.Builder builder = new TemplateModule.Builder();

      // 2. Attach it to every Automator in the game
      builder.AddDecorator<Automator, AutoMapStateListener>();

      return builder.Build();
    }
  }
}