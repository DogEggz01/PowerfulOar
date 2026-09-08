using HarmonyLib;
using UnityEngine;

namespace PowerfulOar
{
    [HarmonyPatch(typeof(ItemRigidbody), nameof(ItemRigidbody.LateUpdate))]
    internal static class ScaledOarRigidbodyScalePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemRigidbody __instance)
        {
            ShipItem item = __instance.GetShipItem();
            if (!OarStats.IsScaledOar(item))
            {
                return true;
            }

            // Preserve Sailwind's native inventory animation. Outside an
            // inventory, this method only resets both transforms to scale 1,
            // which would undo the one-time pre-physics custom-oar scale.
            return __instance.GetCurrentInventorySlot() != null;
        }
    }

    [HarmonyPatch(typeof(SaveablePrefab), nameof(SaveablePrefab.Load))]
    internal static class ScaledOarSavedStatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(SaveablePrefab __instance, SavePrefabData data)
        {
            ScaledOarFactory.MigrateLegacyBigOar(__instance, data);
            ScaledOarController controller =
                __instance.GetComponent<ScaledOarController>();
            controller?.PrepareForSavedState(data);
        }
    }

    [HarmonyPatch(typeof(ItemRigidbody), nameof(ItemRigidbody.EnterInventorySlot))]
    internal static class ScaledOarInventoryEnterPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemRigidbody __instance)
        {
            __instance.GetShipItem()
                ?.GetComponent<ScaledOarController>()
                ?.OnEnteredInventory();
        }
    }

    [HarmonyPatch(typeof(ItemRigidbody), nameof(ItemRigidbody.ExitInventorySlot))]
    internal static class ScaledOarInventoryExitPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemRigidbody __instance)
        {
            __instance.GetShipItem()
                ?.GetComponent<ScaledOarController>()
                ?.RestoreWorldScaleAfterStorage();
        }
    }

    [HarmonyPatch(typeof(CrateInventory), nameof(CrateInventory.InsertItem))]
    internal static class ScaledOarCrateInsertPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CrateInventory __instance, ShipItem item)
        {
            if (!OarStats.IsScaledOar(item))
            {
                return true;
            }

            SaveablePrefab itemSaveable = item.GetComponent<SaveablePrefab>();
            SaveablePrefab crateSaveable = __instance.GetComponent<SaveablePrefab>();
            int crateId = crateSaveable != null ? crateSaveable.instanceId : 0;
            ScaledOarController controller =
                item.GetComponent<ScaledOarController>();

            // Preserve CrateInventory.InsertItem's vanilla idempotency. A
            // duplicate call for an item already in this crate must not clear
            // its save association or disturb its storage physics state.
            bool alreadyContained = crateId > 0 &&
                                    __instance.containedItems.Contains(item);
            if (alreadyContained)
            {
                if (itemSaveable != null)
                {
                    itemSaveable.currentCrateId = crateId;
                }

                controller?.CompleteSavedCrateRestore(crateId);
                return false;
            }

            // Preserve old saves that already contain an oar. The restoration
            // is allowed exactly for the crate ID recorded in that save; once
            // withdrawn, currentCrateId becomes zero and reinsertion is blocked.
            bool restoringSavedCrate = controller != null
                ? controller.CanRestoreSavedCrate(crateId)
                : crateId > 0 && itemSaveable != null &&
                  itemSaveable.currentCrateId == crateId;
            if (restoringSavedCrate)
            {
                controller?.CompleteSavedCrateRestore(crateId);
                return true;
            }

            if (itemSaveable != null && itemSaveable.currentCrateId == crateId)
            {
                itemSaveable.currentCrateId = 0;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(CrateInventory), nameof(CrateInventory.WithdrawItem))]
    internal static class ScaledOarCrateWithdrawPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ShipItem item)
        {
            item?.GetComponent<ScaledOarController>()
                ?.RestoreWorldScaleAfterStorage();
        }
    }

    [HarmonyPatch(typeof(CrateInventoryButton), nameof(CrateInventoryButton.OnActivate))]
    internal static class ScaledOarCrateUiPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GoPointer activatingPointer)
        {
            PickupableItem heldItem = activatingPointer != null
                ? activatingPointer.GetHeldItem()
                : null;
            ShipItem shipItem = heldItem != null
                ? heldItem.GetComponent<ShipItem>()
                : null;
            if (!OarStats.IsScaledOar(shipItem))
            {
                return true;
            }

            if (NotificationUi.instance != null)
            {
                NotificationUi.instance.ShowNotification(
                    "Big Oar and BFO 5000 cannot be stored in crates.");
            }

            CrateInventoryUI.instance?.RefreshButtons();
            return false;
        }
    }
}
