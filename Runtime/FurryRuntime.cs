using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Reflection.Interop;
using Celeste.Core.Scheduling;
using Celeste.Mods.FurryHelper.Interop;

namespace Celeste.Mods.FurryHelper.Runtime
{
    public sealed class FurryRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, FurryRuntime> Owners = new();
        private readonly ISourceEngineContext engine;
        private IDisposable lease;
        private readonly CommunalHelperImports communal = new();
        private FurryRuntime(ISourceEngineContext engine) => this.engine = engine;

        public static FurryRuntime For(ISourceEngineContext engine) =>
            Owners.GetValue(engine, e => new FurryRuntime(e));

        public int DreamTunnelDashState => communal.GetDreamTunnelDashState?.Invoke() ?? -1;

        public void Load()
        {
            lease ??= ((ISourceModInteropEnvironment)engine).ModInterop.RegisterOwned(
                new SourceModInteropActualTypeBinding(typeof(CommunalHelperImports), communal,
                    exportedMethods: Array.Empty<MethodInfo>()));
        }

        public void Dispose()
        {
            lease?.Dispose();
            lease = null;
            communal.GetDreamTunnelDashState = null;
        }
    }
}
