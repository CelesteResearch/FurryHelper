using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Mods.FurryHelper.Runtime;

namespace Celeste.Mods.FurryHelper.Modules
{
    public sealed class FurryHelperModule(ISourceEngineContext engine) : SourceEverestModule(engine)
    {
        public override void Load() => FurryRuntime.For(SourceEngine).Load();
        public override void Unload() => FurryRuntime.For(SourceEngine).Dispose();
    }
}
