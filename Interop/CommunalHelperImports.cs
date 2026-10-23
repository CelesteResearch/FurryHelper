using System;
using MonoMod.ModInterop;

namespace Celeste.Mods.FurryHelper.Interop
{
    [ModImportName("CommunalHelper.DashStates")]
    public sealed class CommunalHelperImports
    {
        public Func<int> GetDreamTunnelDashState;
    }
}
