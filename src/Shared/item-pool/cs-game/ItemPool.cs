// Library copy of shared/item-pool 1.0.0. Do not edit: see src/Shared/item-pool/VERSION.
using System;
using GameCore.HotUpdate.Battle.Logic;

namespace SlShared.ItemPool
{
    /// <summary>
    /// The one store of the item totals of a mod, and its entry for the game code of the mod. The patch classes of
    /// PatchTypes set the dirty bit on each item change, and the mod reads the totals through Read or Refresh, which
    /// read the pool again only when the bit is set. Each mod has its own copy of this class, so each mod keeps its own
    /// item totals and attaches the patch classes of its own copy.
    /// </summary>
    internal static class ItemPool
    {
        private static Action<string> debug;
        private static Func<bool> verbose;

        /// <summary>The item totals of the last read, the dirty bit, and the data version.</summary>
        internal static readonly TotalsStore Store = new TotalsStore();

        /// <summary>
        /// The patch classes of the item changes. The mod adds each one to its patch list and attaches each one on its
        /// own, so a target missing after a game update turns off only that hook.
        /// </summary>
        internal static readonly Type[] PatchTypes =
        {
            typeof(InvalidateOnAddToOwnerCache),
            typeof(InvalidateOnIncreaseItemCount),
            typeof(InvalidateOnDetachFromOwnerCaches),
            typeof(InvalidateOnCostItem),
            typeof(InvalidateOnSyncBothBags),
            typeof(InvalidateOnSyncSingleBag),
        };

        /// <summary>
        /// The log of the mod. debug: a debug line, which the mod writes only when its Verbose is on. verbose: the
        /// Verbose switch of the mod, so a debug line that needs work to build is built only when it is on. A mod
        /// calls it once in Load, before its patches.
        /// </summary>
        internal static void Setup(Action<string> debug, Func<bool> verbose)
        {
            ItemPool.debug = debug;
            ItemPool.verbose = verbose;
        }

        internal static void Debug(string line) => debug?.Invoke(line);

        // The Verbose switch of the mod, so a debug line that needs work to build is built only when it is on.
        internal static bool Verbose => verbose != null && verbose();

        /// <summary>An item changed. Names the hook that fired and the item, for the log of a checkpoint.</summary>
        internal static void MarkDirty(string why, ItemData item, long ownerId, long itemId = 0)
        {
            Store.MarkDirty();
            if (!Verbose) return;
            int configId = item == null ? 0 : item.ItemConfigId;
            long id = item != null ? item.InstanceId : itemId;
            Debug("invalidate: " + why + " item=" + id + " config=" + configId + " owner=" + ownerId
                  + " frame=" + UnityEngine.Time.frameCount);
        }

        /// <summary>The item totals now, read again first when an item changed.</summary>
        internal static TotalSet Read()
        {
            return Store.Read(Pool.Read);
        }

        /// <summary>Reads the item totals again when an item changed. True when it read.</summary>
        internal static bool Refresh()
        {
            return Store.Refresh(Pool.Read);
        }
    }
}
