using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Atlatl.src
{
    internal class ItemAPL : Item
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            // First checks on being loaded which side its on.
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            // Sets up a api called "atlatlInteractions"
            interactions = ObjectCacheUtil.GetOrCreate(api, "atlatlInteractions", () =>
            {
                // Checks each item which matches the critera (being a dart) and adds it to a list. Used later when checking how many darts you have.
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is ItemAPD)
                    {
                        stacks.Add(new ItemStack(obj));
                    }

                }

                // Adds in text via language which tells you to charge the sling using the right mouse button.
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-chargesling",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }

        // Below just makes sure the held "use" animation doesent go off by default.
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        // Checks the inventory for darts. If no darts can be found, the Atlatl can't be fired. 
        protected ItemSlot GetNextDart(EntityAgent byEntity)
        {
            ItemSlot slot = null;
            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                ItemStack stack = invslot.Itemstack;
                if (stack != null && stack.Collectible != null && stack.Collectible is ItemAPD && stack.StackSize > 0)
                {
                    slot = invslot;
                    return false;
                }

                return true;
            });

            return slot;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            // Checks to make sure the player has ammo before starting anything.
            ItemSlot invslot = GetNextDart(byEntity);
            if (invslot == null) return;

            // Starts the aiming animations.
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.StartAnimation("aim");

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);
            }
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            slot.Itemstack.Attributes.SetInt("renderVariant", 1);

            handling = EnumHandHandling.PreventDefault;
        }
        // Below orients the model of the Atlatl to look appropriate as the player readies to fire.
        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {

            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return false;

            if (byEntity.World is IClientWorldAccessor)
            {
                int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed * 5), 0, 2);
                int prevRenderVariant = slot.Itemstack.Attributes.GetInt("renderVariant", 2);

                slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
                slot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);

                if (prevRenderVariant != renderVariant)
                {
                    (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
                }

            }

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.SetInt("renderVariant", 2);
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 2);

            return true;
        }
        // When the player stops interacting, it checks if the reason was the button released? If so, go to aiming cancel, which deals with firing.
        // If it was any other reason, then it wont try to fire.
        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.Attributes.SetInt("aimingCancel", 1);
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack?.Attributes.SetInt("renderVariant", 0);

            if (cancelReason != EnumItemUseCancelReason.Destroyed) (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            return true;
        }

        // Below deals entirely with the actual firing after being "drawn" for enough time. If held for long enough, it fires a dart in the direction aimed.
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.AnimManager.StopAnimation("slingaimbalearic");

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            byEntity.World.RegisterCallback((dt) =>
            {
                if (byEntity.World is IClientWorldAccessor)
                {
                    slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");
                }
                slot.Itemstack?.Attributes.SetInt("renderVariant", 0);
            }, 450);

            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            // Allows me to set a "minimum" time spent aiming before firing.
            if (secondsUsed < 0.75f) return;

            // Uses the "GetNextDart" from up above and sets what it found to be variable "dartSlot". If it null (no darts) returns to top.
            ItemSlot dartSlot = GetNextDart(byEntity);
            if (dartSlot == null) return;

            float damage = 0;

            // Takes the damage attribute from the Atlatl Launcher and adds it to the float variable "damage".
            if (slot.Itemstack.Collectible.Attributes != null)
            {
                damage += slot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            // Takes the damage from the Atlatl Dart and adds it to the float variable "damage" which is 0 + launcherdamage already.
            if (dartSlot.Itemstack.Collectible.Attributes != null)
            {
                damage += dartSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            if (byEntity != null) damage *= byEntity.Stats.GetBlended("rangedWeaponsDamage");

            // Takes one dart away from the slot previously identified. Also marks it as "dirty" which essentially tells the server to ensure the client has the right number.
            ItemStack stack = dartSlot.TakeOut(1);
            dartSlot.MarkDirty();

            // Plays the sound effect of a sling launching at the player.
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

            float breakChance = 0.5f;
            if (stack.ItemAttributes != null) breakChance = stack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f);

            // Sets the Entity properties of the new entity called "launcheddart" in the world and gives it the appropriate variables as seen above. Who fired it, the damage, the stack it came from and the weapon it came from.
            // Note to self; need to create a new "entity" type to match "thrownstone-" with a dart.

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(stack.ItemAttributes["dartEntityCode"].AsString("atatl:dart-" + stack.Collectible.Variant["material"])));

            Entity entityToSpawn = byEntity.World.ClassRegistry.CreateEntity(type);
            var entitydart = entityToSpawn as IProjectile;
            entitydart.FiredBy = byEntity;
            entitydart.Damage = damage;
            entitydart.DamageTier = Attributes["damageTier"].AsInt(0);
            entitydart.ProjectileStack = stack;
            entitydart.DropOnImpactChance = 1 - breakChance;
            entitydart.IgnoreInvFrames = Attributes["ignoreInvFrames"].AsBool(false);
            entitydart.WeaponStack = slot.Itemstack;

            float acc = Math.Max(0.001f, 1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75f;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75f;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.SidedPos.Pitch + rndpitch, byEntity.SidedPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * byEntity.Stats.GetBlended("bowDrawingStrength");


            entityToSpawn.ServerPos.SetPosWithDimension(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
            entityToSpawn.ServerPos.Motion.Set(velocity);
            entityToSpawn.Pos.SetFrom(entityToSpawn.ServerPos);
            entityToSpawn.World = byEntity.World;
            entitydart.PreInitialize();

            byEntity.World.SpawnPriorityEntity(entityToSpawn);

            // Causes the item to have its durability decreased by one.
            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot);
            slot.MarkDirty();

            byEntity.AnimManager.StartAnimation("bowhit");

            // Stops the animation after a set amount of... something. Its 400 right now, but I don't know if thats ticks, seconds, or frames. Might be frames.
            byEntity.World.RegisterCallback((dt) => byEntity.AnimManager.StopAnimation("slingthrowbalearic"), 400);
        }
        // Takes the attribute of the held item (in this case, the Atlatl launcher) and checks it for a damage attribute. If it has one, it sets the "dmg" variable to be that, and adds a line to the description indicating the damage according to the language file.
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            if (dmg != 0) dsc.AppendLine(Lang.Get("sling-piercingdamage", dmg));

            float accuracyBonus = inSlot.Itemstack.Collectible?.Attributes["statModifier"]["rangedWeaponsAcc"].AsFloat(0) ?? 0;
            if (accuracyBonus != 0) dsc.AppendLine(Lang.Get("bow-accuracybonus", accuracyBonus > 0 ? "+" : "", (int)(100 * accuracyBonus)));
        }
    }
}
