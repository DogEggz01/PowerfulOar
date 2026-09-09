using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PowerfulOar
{
    [HarmonyPatch(typeof(ShopItemSpawner), "Start")]
    internal static class BigOarVendorPatch
    {
        private sealed class VendorPlacement
        {
            internal readonly string SpawnerName;
            internal readonly Vector3 LocalPosition;
            internal readonly Vector3 LocalEuler;

            internal VendorPlacement(
                string spawnerName,
                Vector3 localPosition,
                Vector3 localEuler)
            {
                SpawnerName = spawnerName;
                LocalPosition = localPosition;
                LocalEuler = localEuler;
            }
        }

        private static readonly Dictionary<string, VendorPlacement> TargetPlacements =
            new Dictionary<string, VendorPlacement>
        {
            {
                "island 9 E Dragon Cliffs",
                new VendorPlacement(
                    "shop item spawner (236)",
                    new Vector3(0.198f, 1.5f, -0.049f),
                    new Vector3(0.625f, -176.864f, 0.7f))
            },
            {
                "island 1 A Gold Rock",
                new VendorPlacement(
                    "shop item (224)",
                    new Vector3(0.145f, 1.467f, -0.004f),
                    new Vector3(0f, -12.273f, 0f))
            },
            {
                "island 15 M (Fort)",
                new VendorPlacement(
                    "shop item (284)",
                    new Vector3(0.287f, 1.46f, -0.251f),
                    new Vector3(-2.876f, -2.876f, -1.02f))
            }
        };

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ShopItemSpawner __instance)
        {
            Scene scene = __instance.gameObject.scene;
            VendorPlacement placement;
            if (!scene.IsValid() ||
                !TargetPlacements.TryGetValue(scene.name, out placement) ||
                __instance.gameObject.name != placement.SpawnerName)
            {
                return;
            }

            PrefabsDirectory directory = PrefabsDirectory.instance;
            if (directory == null || !ScaledOarFactory.ValidateRuntimeCache(directory))
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    $"Could not add Big Oar to vendor in {scene.name}: prefab directory unavailable.");
                return;
            }

            ShipItem configuredItem = __instance.itemPrefab != null
                ? __instance.itemPrefab.GetComponent<ShipItem>()
                : null;
            int prefabIndex = OarStats.GetPrefabIndex(configuredItem);
            if (prefabIndex == ScaledOarFactory.BigOarPrefabIndex)
            {
                return;
            }

            if (prefabIndex != ScaledOarFactory.VanillaOarPrefabIndex)
            {
                PowerfulOarPlugin.LogSource?.LogWarning(
                    $"Configured Big Oar vendor slot '{placement.SpawnerName}' in " +
                    $"{scene.name} does not contain vanilla oar prefab " +
                    $"{ScaledOarFactory.VanillaOarPrefabIndex}; found {prefabIndex}.");
                return;
            }

            __instance.itemPrefab = directory.directory[ScaledOarFactory.BigOarPrefabIndex];
            ScaledOarVendorDisplaySpawner display =
                __instance.GetComponent<ScaledOarVendorDisplaySpawner>();
            if (display == null)
            {
                display = __instance.gameObject.AddComponent<ScaledOarVendorDisplaySpawner>();
            }

            display.Initialize(
                scene.name,
                "Big Oar",
                ScaledOarFactory.BigOarPrefabIndex,
                placement.LocalPosition,
                placement.LocalEuler,
                ScaledOarFactory.BigOarScale);
            PowerfulOarPlugin.LogSource?.LogInfo(
                $"Replaced one vanilla-oar vendor slot with Big Oar in {scene.name} " +
                $"at '{placement.SpawnerName}' using its configured local pose.");
        }

    }

    [HarmonyPatch(typeof(Shopkeeper), "Start")]
    internal static class HugeOarVendorPatch
    {
        internal const string SceneName = "island 27 Lagoon SwampShipyard";
        internal const string ShopkeeperName = "shopkeeper (16)";
        internal const string NpcName = "Modular NPC";

        [HarmonyPostfix]
        private static void Postfix(Shopkeeper __instance)
        {
            if (!IsTarget(__instance))
            {
                return;
            }

            HugeOarVendorInstaller installer =
                __instance.GetComponent<HugeOarVendorInstaller>();
            if (installer == null)
            {
                installer = __instance.gameObject.AddComponent<HugeOarVendorInstaller>();
            }

            installer.Initialize(__instance);
        }

        internal static bool IsTarget(Shopkeeper shopkeeper)
        {
            if (shopkeeper == null || shopkeeper.gameObject.name != ShopkeeperName)
            {
                return false;
            }

            Scene scene = shopkeeper.gameObject.scene;
            return scene.IsValid() && scene.name == SceneName;
        }
    }

    internal sealed class HugeOarVendorInstaller : MonoBehaviour
    {
        private const string SpawnerName = "PowerfulOar Huge Oar shop spawner";
        private static readonly Vector3 LocalPosition =
            new Vector3(-1.444f, 4.292f, 0.854f);
        private static readonly Vector3 LocalEuler =
            new Vector3(8.857f, -110.947f, 1.63f);

        private Shopkeeper shopkeeper;
        private HugeOarPurchaseDialogueArea dialogueArea;
        private float nextAttempt;
        private bool complete;
        private bool reportedMissingNpc;

        internal void Initialize(Shopkeeper newShopkeeper)
        {
            shopkeeper = newShopkeeper;
            nextAttempt = 0f;
        }

        internal void ShowPurchaseReaction(int purchaseCount)
        {
            if (dialogueArea != null)
            {
                dialogueArea.ShowPurchase(
                    HugeOarPurchaseState.GetDialogue(purchaseCount));
            }
        }

        private void Update()
        {
            if (complete || Time.unscaledTime < nextAttempt)
            {
                return;
            }

            if (shopkeeper == null)
            {
                enabled = false;
                return;
            }

            nextAttempt = Time.unscaledTime + 1f;
            Transform npc = shopkeeper.transform.Find(HugeOarVendorPatch.NpcName);
            if (npc == null)
            {
                if (!reportedMissingNpc)
                {
                    reportedMissingNpc = true;
                    PowerfulOarPlugin.LogSource?.LogError(
                        "Could not add Huge Oar to Kicia Bay: shopkeeper (16)/" +
                        "Modular NPC was not found; retrying.");
                }

                nextAttempt = Time.unscaledTime + 5f;
                return;
            }

            PrefabsDirectory directory = PrefabsDirectory.instance;
            if (directory == null)
            {
                return;
            }

            if (!ScaledOarFactory.ValidateRuntimeCache(directory))
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not add Huge Oar to Kicia Bay: scaled-oar prefab " +
                    "validation failed.");
                enabled = false;
                return;
            }

            if (npc.Find(SpawnerName) != null)
            {
                PowerfulOarPlugin.LogSource?.LogWarning(
                    "Skipped duplicate Kicia Bay Huge Oar shop spawner.");
                complete = true;
                enabled = false;
                return;
            }

            GameObject spawnerObject = null;
            try
            {
                spawnerObject = new GameObject(SpawnerName);
                spawnerObject.SetActive(false);
                spawnerObject.transform.SetParent(npc, false);
                spawnerObject.transform.localPosition = LocalPosition;
                spawnerObject.transform.localRotation = Quaternion.Euler(LocalEuler);
                spawnerObject.transform.localScale = Vector3.one;

                ShopItemSpawner spawner =
                    spawnerObject.AddComponent<ShopItemSpawner>();
                spawner.itemPrefab =
                    directory.directory[ScaledOarFactory.HugeOarPrefabIndex];
                spawner.availableAtNight = false;
                spawner.priceMult = 1f;

                ScaledOarVendorDisplaySpawner display =
                    spawnerObject.AddComponent<ScaledOarVendorDisplaySpawner>();
                display.Initialize(
                    HugeOarVendorPatch.SceneName,
                    "Huge Oar",
                    ScaledOarFactory.HugeOarPrefabIndex,
                    Vector3.zero,
                    Vector3.zero,
                    ScaledOarFactory.HugeOarScale);

                dialogueArea = HugeOarPurchaseDialogueArea.Create(npc);
                spawnerObject.SetActive(true);
                complete = true;
                enabled = false;

                PowerfulOarPlugin.LogSource?.LogInfo(
                    "Added Huge Oar prefab 603 beneath Kicia Bay shopkeeper " +
                    "(16)/Modular NPC at local position " + LocalPosition +
                    " and local rotation " + LocalEuler + ".");
            }
            catch (System.Exception exception)
            {
                if (dialogueArea != null)
                {
                    Destroy(dialogueArea.gameObject);
                    dialogueArea = null;
                }

                if (spawnerObject != null)
                {
                    Destroy(spawnerObject);
                }

                nextAttempt = Time.unscaledTime + 10f;
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not install the Kicia Bay Huge Oar vendor; retrying: " +
                    exception);
            }
        }
    }

    [HarmonyPatch(typeof(ShopItemSpawner), "SpawnItem")]
    internal static class ScaledOarVendorSpawnPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ShopItemSpawner __instance, ShipItem ___item)
        {
            ScaledOarVendorDisplaySpawner display =
                __instance.GetComponent<ScaledOarVendorDisplaySpawner>();
            if (display != null)
            {
                display.ConfigureSpawnedItem(___item);
            }
        }
    }

    internal sealed class ScaledOarVendorDisplaySpawner : MonoBehaviour
    {
        private string sceneName;
        private string itemName;
        private int prefabIndex;
        private Vector3 localPosition;
        private Quaternion localRotation;
        private float targetWorldScale;

        internal void Initialize(
            string newSceneName,
            string newItemName,
            int newPrefabIndex,
            Vector3 newLocalPosition,
            Vector3 newLocalEuler,
            float newTargetWorldScale)
        {
            sceneName = newSceneName;
            itemName = newItemName;
            prefabIndex = newPrefabIndex;
            localPosition = newLocalPosition;
            localRotation = Quaternion.Euler(newLocalEuler);
            targetWorldScale = newTargetWorldScale;
        }

        internal void ConfigureSpawnedItem(ShipItem item)
        {
            if (item == null || OarStats.GetPrefabIndex(item) != prefabIndex)
            {
                return;
            }

            ScaledOarVendorDisplayItem controller =
                item.GetComponent<ScaledOarVendorDisplayItem>();
            if (controller == null)
            {
                controller = item.gameObject.AddComponent<ScaledOarVendorDisplayItem>();
            }

            controller.Initialize(
                item,
                localPosition,
                localRotation,
                targetWorldScale);

            PowerfulOarPlugin.LogSource?.LogInfo(
                $"Positioned {itemName} vendor display in {sceneName} at the " +
                "configured spawner-local pose.");
        }
    }

    [DefaultExecutionOrder(10001)]
    internal sealed class ScaledOarVendorDisplayItem : MonoBehaviour
    {
        private ShipItem item;
        private Vector3 localPosition;
        private Quaternion localRotation;
        private float targetWorldScale;

        internal void Initialize(
            ShipItem newItem,
            Vector3 newLocalPosition,
            Quaternion newLocalRotation,
            float newTargetWorldScale)
        {
            item = newItem;
            localPosition = newLocalPosition;
            localRotation = newLocalRotation;
            targetWorldScale = newTargetWorldScale;
            ApplyPlacement();
        }

        private void LateUpdate()
        {
            if (item == null || item.sold || item.held)
            {
                enabled = false;
                return;
            }

            // Preserve the configured world size when a vendor spawner has a
            // non-unit parent scale (notably Gold Rock's 2x spawner).
            ApplyPlacement();
        }

        private void ApplyPlacement()
        {
            Transform itemParent = transform.parent;
            if (itemParent == null)
            {
                return;
            }

            Vector3 parentScale = itemParent.lossyScale;
            transform.localScale = new Vector3(
                SafeLocalScale(parentScale.x),
                SafeLocalScale(parentScale.y),
                SafeLocalScale(parentScale.z));
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
        }

        private float SafeLocalScale(float parentScale)
        {
            return Mathf.Abs(parentScale) > 0.0001f
                ? targetWorldScale / Mathf.Abs(parentScale)
                : targetWorldScale;
        }
    }
}
