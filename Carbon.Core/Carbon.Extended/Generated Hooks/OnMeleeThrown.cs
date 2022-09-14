using Carbon.Core;
using Harmony;

namespace Carbon.Extended
{
    [HarmonyPatch ( typeof ( BaseMelee ), "CLProject" )]
    public class OnMeleeThrown
    {
        public static void Postfix ( BaseEntity.RPCMessage msg )
        {
            HookExecutor.CallStaticHook ( "OnMeleeThrown" );
        }
    }
}