// Library copy of shared/item-pool 1.0.0. Do not edit: see src/Shared/item-pool/VERSION.
using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;

namespace SlShared.ItemPool
{
    /// <summary>
    /// Reads the item pool: the backpack of the character and each home storage, by the game's own home scope rule.
    ///
    /// The items come from the bag mirror of the web UI (State_Data_Item.OwnerCache), the copy that the reducers of
    /// the game keep, and not from the item manager. Reading it is a dictionary read and calls no method of the game.
    ///
    /// ItemManager.GetAllAvailableItems is not used, which is a rule and not a preference. That call walks the home
    /// scope, and it makes one later item move reach about fifty ItemManager objects instead of one. The forty-nine
    /// that do not hold the item each log "[bug][bag-mirror] OnMoveItem" and send the correction of the game again,
    /// and a player sees a delay of seconds on a drag inside a cooking station. Measured: with the call the move
    /// reaches 49 managers, without it 1, and the error count goes from 48 for each drag to zero. A lookup of a
    /// single item (GetItemData) does not do this, and neither does the static IsInHomeScope, which reads config
    /// tables.
    ///
    /// Better Crafting reads the same mirror for its home storage list, which is where this shape comes from.
    /// </summary>
    internal static class Pool
    {
        // The last reason a read gave nothing, so a save that has not loaded yet does not fill the log: the reason is
        // logged when it changes, not on each frame.
        private static string lastNotReady;

        /// <summary>
        /// The item totals of the pool now. TotalSet.Failure when the game is not in a battle world yet, or when the
        /// pool cannot be read, so the caller leaves the dirty bit set and tries again on the next tick.
        /// </summary>
        internal static TotalSet Read()
        {
            try
            {
                return ReadPool();
            }
            catch (Exception e)
            {
                // Before a save loads, reaching into the battle world can throw instead of giving null.
                NotReady("read failed: " + e.Message);
                return TotalSet.Failure;
            }
        }

        private static TotalSet ReadPool()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var itemState = ReduxUISystem.Instance?.GetData<State_Data_Item>(Il2CppType.Of<State_Data_Item>());
            var cache = itemState?.OwnerCache;
            if (cache == null) return NotReady("no item state in the store yet");

            var world = World;
            if (world == null) return NotReady("no battle world yet");
            var agents = world._AgentManager;
            if (agents == null) return NotReady("no agent manager yet");
            var lead = agents.GetLeadingRole();
            if (lead == null) return NotReady("no leading role yet");

            int homeMapId = agents.GetHomeMapId();
            int homeGroup = ConfigManager.Instance?.Get_Config_MapPoint(homeMapId)?.HomeGroup ?? 0;

            var pool = new List<PoolItem>();
            int owners = 0;
            // The backpack of the character, which is an owner of the mirror but not a furniture.
            if (Take(cache, lead.InstanceId, pool)) owners++;

            var furnitures = agents.GetAllFurnitures();
            for (int i = 0; furnitures != null && i < furnitures.Count; i++)
            {
                var f = furnitures[i];
                if (f == null) continue;
                // The furniture list holds the whole world, so the home scope rule of the game decides. A locked
                // floor or an area outside the home does not pass it.
                if (!ItemManager.IsInHomeScope(f, homeMapId, homeGroup)) continue;
                if (Take(cache, f.InstanceId, pool)) owners++;
            }

            // The workbench drawer, which the game counts and no furniture list holds: it is a synthetic owner with
            // an owner id of its own. ItemManager.GetSyntheticOwnersInHomeScope is the list of the game and its only
            // entry is this drawer, but it gives an IL2CPP iterator that the interop cannot step, so the mod reads
            // the owner id itself.
            if (Take(cache, WorkbenchDrawerConfig.DrawerOwnerId, pool)) owners++;

            // The game leaves some owners out of its own count at home ("Usable at home"): a Brewing Barrel, a locked
            // bag, a shredder hopper, and more. The item total asks the same static rule of the game for each owner,
            // so it gives the number that the game gives, also after a later game update adds an owner to the rule.
            var totals = TotalsLogic.Build(pool, ItemManager.IsExcludedFromCostPool);
            lastNotReady = null;
            if (ItemPool.Verbose)
                ItemPool.Debug("pool: storages=" + owners + " left out=" + totals.LeftOutOwners
                         + " stacks=" + pool.Count + " item kinds=" + totals.ByConfigId.Count
                         + " frame=" + UnityEngine.Time.frameCount
                         + " timing: " + clock.Elapsed.TotalMilliseconds.ToString("F1") + " ms");
            return totals;
        }

        // The stacks of one owner of the mirror. False when the mirror holds no such owner or the owner is empty, so
        // the caller does not count it as a storage.
        private static bool Take(Il2CppSystem.Collections.Generic.Dictionary<long, Il2CppSystem.Collections.Generic.Dictionary<long, Data_Item>> cache,
                                 long ownerId, List<PoolItem> pool)
        {
            if (!cache.ContainsKey(ownerId)) return false;
            var items = cache[ownerId];
            if (items == null || items.Count == 0) return false;
            // An IL2CPP dictionary needs its own enumerator: C# foreach does not cross the interop boundary.
            var e = items.Values.GetEnumerator();
            while (e.MoveNext())
            {
                var item = e.Current;
                if (item == null) continue;
                pool.Add(new PoolItem(item.LogicId, item.ItemConfigId, item.ItemCount, ownerId));
            }
            return true;
        }

        // A read that gave nothing. The reason is logged once, not on each frame, because the tick asks again while
        // the dirty bit stays set and a save can take many hundred frames to load.
        private static TotalSet NotReady(string why)
        {
            if (lastNotReady != why)
            {
                lastNotReady = why;
                ItemPool.Debug("pool: " + why);
            }
            return TotalSet.Failure;
        }

        // The battle world of the game, or null before the game creates it. BaseSingleton.Instance creates the world
        // when it does not exist yet, so a read of Instance from a frame hook at the title screen built a world
        // outside the game's flow, again on each frame while its constructor failed.
        private static BattleLogicWorld World =>
            BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;
    }
}
