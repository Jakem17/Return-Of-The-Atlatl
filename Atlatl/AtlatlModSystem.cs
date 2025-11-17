using Atlatl.src;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Atlatl
{
    public class AtlatlModSystem : ModSystem
    {

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            Mod.Logger.Notification("History has been researched! Ready on:" + api.Side);
            api.RegisterItemClass(Mod.Info.ModID + ".apl", typeof(ItemAPL));
            api.RegisterItemClass(Mod.Info.ModID + ".apd", typeof(ItemAPD));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification("History has been researched! Ready on:" + Lang.Get("atlatl:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Mod.Logger.Notification("History has been researched! Ready on:" + Lang.Get("atlatl:hello"));
        }

    }
}
