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
            BigOarVendorDisplaySpawner display =
                __instance.GetComponent<BigOarVendorDisplaySpawner>();
            if (display == null)
            {
                display = __instance.gameObject.AddComponent<BigOarVendorDisplaySpawner>();
            }

            display.Initialize(
                scene.name,
                placement.LocalPosition,
                placement.LocalEuler);
            PowerfulOarPlugin.LogSource?.LogInfo(
                $"Replaced one vanilla-oar vendor slot with Big Oar in {scene.name} " +
                $"at '{placement.SpawnerName}' using its configured local pose.");
        }

    }

    [HarmonyPatch(typeof(ShopItemSpawner), "SpawnItem")]
    internal static class BigOarVendorSpawnPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ShopItemSpawner __instance, ShipItem ___item)
        {
            BigOarVendorDisplaySpawner display =
                __instance.GetComponent<BigOarVendorDisplaySpawner>();
            if (display != null &&
                OarStats.GetPrefabIndex(___item) == ScaledOarFactory.BigOarPrefabIndex)
            {
                display.ConfigureSpawnedItem(___item);
            }
        }
    }

    internal sealed class BigOarVendorDisplaySpawner : MonoBehaviour
    {
        private string sceneName;
        private Vector3 localPosition;
        private Quaternion localRotation;

        internal void Initialize(
            string newSceneName,
            Vector3 newLocalPosition,
            Vector3 newLocalEuler)
        {
            sceneName = newSceneName;
            localPosition = newLocalPosition;
            localRotation = Quaternion.Euler(newLocalEuler);
        }

        internal void ConfigureSpawnedItem(ShipItem item)
        {
            if (item == null)
            {
                return;
            }

            BigOarVendorDisplayItem controller =
                item.GetComponent<BigOarVendorDisplayItem>();
            if (controller == null)
            {
                controller = item.gameObject.AddComponent<BigOarVendorDisplayItem>();
            }

            controller.Initialize(
                item,
                localPosition,
                localRotation,
                ScaledOarFactory.BigOarScale);

            PowerfulOarPlugin.LogSource?.LogInfo(
                $"Positioned Big Oar vendor display in {sceneName} at the " +
                "configured spawner-local pose.");
        }
    }

    [DefaultExecutionOrder(10001)]
    internal sealed class BigOarVendorDisplayItem : MonoBehaviour
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
