using Bindito.Core;
using Timberborn.AutomationBuildings;
using Timberborn.TemplateInstantiation;

namespace Calloatti.AutoTools
{
  [Context("Game")]
  public class RelayColorReplicatorConfigurator : Configurator
  {
    protected override void Configure()
    {
      // Explicitly bind the component so the DI container knows how to instantiate it
      Bind<RelayColorReplicator>().AsTransient();

      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule()
    {
      TemplateModule.Builder builder = new TemplateModule.Builder();

      // This tells the game to attach our custom component to any entity that has a Relay component
      builder.AddDecorator<Relay, RelayColorReplicator>();

      return builder.Build();
    }
  }
}