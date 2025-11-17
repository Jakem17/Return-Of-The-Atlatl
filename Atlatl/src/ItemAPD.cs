using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Atlatl.src
{
    internal class ItemAPD : Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            // Checks the item.json attributes portion for damage. If its over 0, adds language saying it adds. If its negative, adds language saying it subtracts.
            float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            if (dmg >= 0)
            {
                dsc.AppendLine(Lang.Get("arrow-piercingdamage-add", "+" + dmg));
            }
            else
            {
                dsc.AppendLine(Lang.Get("arrow-piercingdamage-remove", dmg));
            }

            // Also adds in the break chance on impact from the attributes of the json.
            float breakChanceOnImpact = inSlot.Itemstack.Collectible.Attributes["breakChanceOnImpact"].AsFloat(0.5f);
            dsc.AppendLine(Lang.Get("breakchanceonimpact", (int)(breakChanceOnImpact * 100)));

        }
    }
}