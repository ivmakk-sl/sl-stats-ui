// Library copy of shared/item-pool 1.0.0. Do not edit: see src/Shared/item-pool/VERSION.
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;

namespace SlShared.ItemPool
{
    // Every change of the per-owner item cache of the game reaches one of the methods below, and the pool read goes
    // to that same cache. So an item that enters it, leaves it, or changes its count in place cannot miss the dirty
    // bit.
    //
    // Each of them returns void, which is the rule here and not a coincidence. A Harmony patch of a private
    // method of this game that returns a value gives the default value to the caller, also when the patch declares
    // __result and only reads it. The first build patched ItemManager.RemoveItemFromOwnerCache, which gives the
    // ItemData it took out, and ItemManager.OnMoveItem reads that item to tell a real move from a mirror that is out
    // of step. Every move then read as failed, the game logged "[bug][bag-mirror] OnMoveItem" and sent its correction
    // again about fifty times for one drag, and a player saw a delay of seconds on a drag inside a cooking station.
    // Declaring __result did not help: the second build read it as null in all but 6 of about 240 calls. So a
    // notification-only patch takes a void method, and these cover the same events:
    //
    // - AddItemToOwnerCache: an item enters a container, so a gain and the second half of a move.
    // - IncreaseItemCount: a stack grows in place.
    // - DetachItemFromOwnerCaches: an item leaves every container. Its callers are RemoveItem, ForceRemoveItem, and
    //   CostItem, so it carries a removal and a use that takes the whole stack.
    // - CostItem: a use, whether it takes the whole stack or part of it. This is the one the player sees when a
    //   number drops after eating or crafting.
    //
    // The smart drop of game 1.1.18385 (a drop onto an occupied cell: a swap, or a push of the blocking items) does not
    // go through the cache add: ItemManager.ExecuteSwap and ItemManager.CommitDropLayout write the owner cache
    // directly. Each of them ends with ItemManager.SyncBothBags or ItemManager.SyncSingleBag, which send the bags to the
    // web UI mirror that the pool reads, so two more hooks take those:
    //
    // - SyncBothBags: the two bags of a move after it is done.
    // - SyncSingleBag: one bag after a layout change in it.
    //
    // Not ItemManager.OnSyncItem and not ItemManager.SetItemCount: neither has a caller in the native code of the
    // game, so SetItemCount is inlined into IncreaseItemCount and DecreaseItemCount, and a patch on either would attach
    // with no error and never run. Not ItemManager.OnMoveItem either: a plain move goes through the cache add, and a smart drop
    // through the syncs above.
    //
    // All but CostItem are private, which Harmony patches, and each mod attaches each one on its own in its Load
    // (ItemPool.PatchTypes), so a method missing after a game update turns off only its own hook in that mod and the
    // log names it.

    [HarmonyPatch(typeof(ItemManager), "AddItemToOwnerCache")]
    internal static class InvalidateOnAddToOwnerCache
    {
        [HarmonyPostfix]
        private static void Postfix(long ownerId, ItemData item)
        {
            ItemPool.MarkDirty("AddItemToOwnerCache", item, ownerId);
        }
    }

    [HarmonyPatch(typeof(ItemManager), "IncreaseItemCount")]
    internal static class InvalidateOnIncreaseItemCount
    {
        [HarmonyPostfix]
        private static void Postfix(ItemData item, int delta)
        {
            ItemPool.MarkDirty("IncreaseItemCount +" + delta, item, item == null ? 0 : item.OwnerId);
        }
    }

    [HarmonyPatch(typeof(ItemManager), "DetachItemFromOwnerCaches")]
    internal static class InvalidateOnDetachFromOwnerCaches
    {
        [HarmonyPostfix]
        private static void Postfix(ItemData itemData, long expectedOwnerId)
        {
            ItemPool.MarkDirty("DetachItemFromOwnerCaches", itemData, expectedOwnerId);
        }
    }

    [HarmonyPatch(typeof(ItemManager), "CostItem")]
    internal static class InvalidateOnCostItem
    {
        [HarmonyPostfix]
        private static void Postfix(long ownerId, long itemId, int count)
        {
            ItemPool.MarkDirty("CostItem -" + count, null, ownerId, itemId);
        }
    }

    [HarmonyPatch(typeof(ItemManager), "SyncBothBags")]
    internal static class InvalidateOnSyncBothBags
    {
        [HarmonyPostfix]
        private static void Postfix(long fromOwnerId, long toOwnerId)
        {
            ItemPool.MarkDirty("SyncBothBags to=" + toOwnerId, null, fromOwnerId);
        }
    }

    [HarmonyPatch(typeof(ItemManager), "SyncSingleBag")]
    internal static class InvalidateOnSyncSingleBag
    {
        [HarmonyPostfix]
        private static void Postfix(long ownerId)
        {
            ItemPool.MarkDirty("SyncSingleBag", null, ownerId);
        }
    }
}
