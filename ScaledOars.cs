using System;
using System.Collections.Generic;
using System.Reflection;
using cakeslice;
using HarmonyLib;
using UnityEngine;

namespace PowerfulOar
{
    internal static class OarStats
    {
        internal const float OriginalForce = 35000f;
        internal const float OriginalMaxBoatSpeed = 2.06f;
        internal const float OriginalNeedsCostPerSecond = 0.1f;

        internal const float BigForce = 76500f;
        internal const float BigMaxBoatSpeed = 2.57f;
        internal const float BigNeedsCostPerSecond = 0.2f;

        internal const float Bfo5000Force = 510000f;
        internal const float Bfo5000MaxBoatSpeed = 10.29f;
        internal const float Bfo5000NeedsCostPerSecond = 1f;

        internal static bool Apply(ShipItemOar oar)
        {
            int prefabIndex = GetPrefabIndex(oar);
            switch (prefabIndex)
            {
                case ScaledOarFactory.VanillaOarPrefabIndex:
                    oar.rowForce = OriginalForce;
                    oar.maxBoatSpeed = OriginalMaxBoatSpeed;
                    return true;

                case ScaledOarFactory.BigOarPrefabIndex:
                    oar.rowForce = BigForce;
                    oar.maxBoatSpeed = BigMaxBoatSpeed;
                    return true;

                case ScaledOarFactory.Bfo5000PrefabIndex:
                    oar.rowForce = Bfo5000Force;
                    oar.maxBoatSpeed = Bfo5000MaxBoatSpeed;
                    return true;

                default:
                    return false;
            }
        }

        internal static float GetWaterCostPerSecond(ShipItemOar oar)
        {
            float baseline = GetNeedsCostPerSecond(oar, 0.4f);
            return baseline * RadRefinementCompatibility.NeedsCostMultiplier;
        }

        internal static float GetFoodCostPerSecond(ShipItemOar oar)
        {
            float baseline = GetNeedsCostPerSecond(oar, 0.6f);
            return baseline * RadRefinementCompatibility.NeedsCostMultiplier;
        }

        private static float GetNeedsCostPerSecond(ShipItemOar oar, float fallback)
        {
            switch (GetPrefabIndex(oar))
            {
                case ScaledOarFactory.VanillaOarPrefabIndex:
                    return OriginalNeedsCostPerSecond;

                case ScaledOarFactory.BigOarPrefabIndex:
                    return BigNeedsCostPerSecond;

                case ScaledOarFactory.Bfo5000PrefabIndex:
                    return Bfo5000NeedsCostPerSecond;

                default:
                    return fallback;
            }
        }

        internal static int GetPrefabIndex(ShipItem item)
        {
            SaveablePrefab saveable =
                item != null ? item.GetComponent<SaveablePrefab>() : null;
            return saveable != null ? saveable.prefabIndex : -1;
        }
    }

    internal sealed class ScaledOarDefinition
    {
        internal ScaledOarDefinition(
            int prefabIndex,
            string itemName,
            float scale,
            float waterPosDistance,
            float waterPosHeight,
            float rowDipHeight,
            float massMultiplier,
            int valueMultiplier)
        {
            PrefabIndex = prefabIndex;
            ItemName = itemName;
            Scale = scale;
            WaterPosDistance = waterPosDistance;
            WaterPosHeight = waterPosHeight;
            RowDipHeight = rowDipHeight;
            MassMultiplier = massMultiplier;
            ValueMultiplier = valueMultiplier;
        }

        internal int PrefabIndex { get; }
        internal string ItemName { get; }
        internal float Scale { get; }
        internal float WaterPosDistance { get; }
        internal float WaterPosHeight { get; }
        internal float RowDipHeight { get; }
        internal float MassMultiplier { get; }
        internal int ValueMultiplier { get; }
        internal GameObject Prefab { get; set; }
        internal bool CacheValidationLogged { get; set; }
    }

    internal static class ScaledOarFactory
    {
        internal const int VanillaOarPrefabIndex = 168;
        internal const int BigOarPrefabIndex = 169;
        internal const int Bfo5000PrefabIndex = 666;

        private static readonly ScaledOarDefinition[] Definitions =
        {
            new ScaledOarDefinition(
                169, "Big Oar", 2f, 1.50f, 2.80f, 1.00f, 8f, 4),
            new ScaledOarDefinition(
                666, "BFO 5000", 5f, 3.75f, 7.00f, 2.50f, 125f, 10)
        };

        internal static bool EnsureRegistered(PrefabsDirectory directory)
        {
            if (directory == null || directory.directory == null)
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not register scaled oars: PrefabsDirectory is unavailable.");
                return false;
            }

            if (directory.directory.Length <= VanillaOarPrefabIndex ||
                directory.directory[VanillaOarPrefabIndex] == null)
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    $"Could not register scaled oars: vanilla oar prefab " +
                    $"{VanillaOarPrefabIndex} was not found.");
                return false;
            }

            ShipItemOar vanillaOar =
                directory.directory[VanillaOarPrefabIndex].GetComponent<ShipItemOar>();
            if (vanillaOar == null)
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not register scaled oars: prefab 168 is not a ShipItemOar.");
                return false;
            }

            OarStats.Apply(vanillaOar);
            EnsureDirectoryCapacity(directory);

            bool success = true;
            foreach (ScaledOarDefinition definition in Definitions)
            {
                success &= EnsureRegistered(directory, definition, vanillaOar);
            }

            return success;
        }

        internal static bool ValidateRuntimeCache(PrefabsDirectory directory)
        {
            if (!EnsureRegistered(directory))
            {
                return false;
            }

            EnsureDirectoryCapacity(directory);
            EnsureShipItemCacheCapacity(directory);

            bool success = true;
            foreach (ScaledOarDefinition definition in Definitions)
            {
                GameObject prefab = directory.directory[definition.PrefabIndex];
                ShipItem item = prefab != null ? prefab.GetComponent<ShipItem>() : null;
                SaveablePrefab saveable = prefab != null ? prefab.GetComponent<SaveablePrefab>() : null;
                ScaledOarController controller =
                    prefab != null ? prefab.GetComponent<ScaledOarController>() : null;

                if (item == null || saveable == null || controller == null ||
                    saveable.prefabIndex != definition.PrefabIndex ||
                    controller.PrefabIndex != definition.PrefabIndex)
                {
                    PowerfulOarPlugin.LogSource?.LogError(
                        $"{definition.ItemName} registration failed validation: directory entry " +
                        "is missing ShipItem, ScaledOarController, or the expected prefab index.");
                    success = false;
                    continue;
                }

                directory.shipItems[definition.PrefabIndex] = item;

                if (!definition.CacheValidationLogged)
                {
                    definition.CacheValidationLogged = true;
                    PowerfulOarPlugin.LogSource?.LogInfo(
                        $"Validated {definition.ItemName} prefab and item cache at index " +
                        $"{definition.PrefabIndex}.");
                }
            }

            return success;
        }

        private static bool EnsureRegistered(
            PrefabsDirectory directory,
            ScaledOarDefinition definition,
            ShipItemOar vanillaOar)
        {
            GameObject existing = directory.directory[definition.PrefabIndex];
            if (existing != null)
            {
                ScaledOarController controller = existing.GetComponent<ScaledOarController>();
                if (existing == definition.Prefab ||
                    (controller != null && controller.PrefabIndex == definition.PrefabIndex))
                {
                    ShipItemOar existingOar = existing.GetComponent<ShipItemOar>();
                    if (existingOar == null)
                    {
                        PowerfulOarPlugin.LogSource?.LogError(
                            $"Could not configure {definition.ItemName}: ShipItemOar is missing.");
                        return false;
                    }

                    if (controller == null)
                    {
                        controller = existing.AddComponent<ScaledOarController>();
                    }

                    ConfigureScaledOar(existing, existingOar, vanillaOar, definition);
                    definition.Prefab = existing;
                    controller.Initialize(definition.PrefabIndex, definition.Scale);
                    return true;
                }

                PowerfulOarPlugin.LogSource?.LogError(
                    $"Could not register {definition.ItemName}: prefab index " +
                    $"{definition.PrefabIndex} is already occupied by '{existing.name}'.");
                return false;
            }

            if (definition.Prefab != null)
            {
                ShipItemOar cachedOar = definition.Prefab.GetComponent<ShipItemOar>();
                ScaledOarController cachedController =
                    definition.Prefab.GetComponent<ScaledOarController>();
                if (cachedOar == null || cachedController == null)
                {
                    PowerfulOarPlugin.LogSource?.LogError(
                        $"Could not restore {definition.ItemName}: cached components are missing.");
                    return false;
                }

                ConfigureScaledOar(definition.Prefab, cachedOar, vanillaOar, definition);
                cachedController.Initialize(definition.PrefabIndex, definition.Scale);
                directory.directory[definition.PrefabIndex] = definition.Prefab;
                return true;
            }

            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(
                    directory.directory[VanillaOarPrefabIndex]);
                SaveablePrefab saveable = clone.GetComponent<SaveablePrefab>();
                ShipItemOar oar = clone.GetComponent<ShipItemOar>();

                if (saveable == null || oar == null)
                {
                    PowerfulOarPlugin.LogSource?.LogError(
                        $"Could not register {definition.ItemName}: the cloned vanilla prefab " +
                        "is missing SaveablePrefab or ShipItemOar.");
                    UnityEngine.Object.Destroy(clone);
                    return false;
                }

                ConfigureScaledOar(clone, oar, vanillaOar, definition);

                ScaledOarController controller = clone.GetComponent<ScaledOarController>();
                if (controller == null)
                {
                    controller = clone.AddComponent<ScaledOarController>();
                }

                controller.Initialize(definition.PrefabIndex, definition.Scale);
                directory.directory[definition.PrefabIndex] = clone;
                definition.Prefab = clone;

                PowerfulOarPlugin.LogSource?.LogInfo(
                    $"Registered {definition.ItemName} at prefab index " +
                    $"{definition.PrefabIndex}. Scale={definition.Scale:0.0}x, " +
                    $"mass={oar.mass:0.0}, value={oar.value}, " +
                    $"water distance={definition.WaterPosDistance:0.00}, " +
                    $"water height={definition.WaterPosHeight:0.00}, " +
                    $"dip={definition.RowDipHeight:0.00}, big=false.");
                return true;
            }
            catch (Exception exception)
            {
                if (clone != null)
                {
                    UnityEngine.Object.Destroy(clone);
                }

                PowerfulOarPlugin.LogSource?.LogError(
                    $"Could not register {definition.ItemName}: {exception}");
                return false;
            }
        }

        private static void ConfigureScaledOar(
            GameObject prefab,
            ShipItemOar oar,
            ShipItemOar vanillaOar,
            ScaledOarDefinition definition)
        {
            SaveablePrefab saveable = prefab.GetComponent<SaveablePrefab>();
            if (saveable == null)
            {
                throw new InvalidOperationException(
                    $"{definition.ItemName} is missing SaveablePrefab.");
            }

            prefab.name = definition.ItemName;
            prefab.transform.localScale = Vector3.one * definition.Scale;
            UnityEngine.Object.DontDestroyOnLoad(prefab);

            saveable.prefabIndex = definition.PrefabIndex;
            oar.name = definition.ItemName;
            oar.big = false;
            oar.wallAttachment = false;
            oar.mass = vanillaOar.mass * definition.MassMultiplier;
            oar.value = vanillaOar.value * definition.ValueMultiplier;
            oar.waterPosDistance = definition.WaterPosDistance;
            oar.waterPosHeight = definition.WaterPosHeight;
            oar.rowDipHeight = definition.RowDipHeight;
            OarStats.Apply(oar);
            HookHangMoreCompatibility.Exclude(oar);
        }

        private static void EnsureDirectoryCapacity(PrefabsDirectory directory)
        {
            if (directory.directory.Length <= Bfo5000PrefabIndex)
            {
                Array.Resize(ref directory.directory, Bfo5000PrefabIndex + 1);
            }
        }

        private static void EnsureShipItemCacheCapacity(PrefabsDirectory directory)
        {
            if (directory.shipItems == null)
            {
                directory.shipItems = new ShipItem[directory.directory.Length];
                for (int i = 0; i < directory.directory.Length; i++)
                {
                    GameObject directoryEntry = directory.directory[i];
                    if (directoryEntry != null)
                    {
                        directory.shipItems[i] = directoryEntry.GetComponent<ShipItem>();
                    }
                }
            }
            else if (directory.shipItems.Length < directory.directory.Length)
            {
                Array.Resize(ref directory.shipItems, directory.directory.Length);
            }
        }
    }

    [DefaultExecutionOrder(10000)]
    internal sealed class ScaledOarController : MonoBehaviour
    {
        private static readonly HashSet<ScaledOarController> ActiveControllers =
            new HashSet<ScaledOarController>();
        private static readonly FieldInfo CollidedCollidersField =
            AccessTools.Field(typeof(PickupableItemCollisionChecker), "collidedCols");
        private static readonly FieldInfo CurrentDecolDistanceField =
            AccessTools.Field(typeof(PickupableItemCollisionChecker), "currentDecolDistance");

        [SerializeField]
        private int prefabIndex;

        [SerializeField]
        private float targetScale = 1f;

        private ShipItemOar item;
        private ItemRigidbody itemRigidbody;
        private Outline outline;
        private bool wasInSleepOrLoadTransition;

        internal int PrefabIndex => prefabIndex;

        internal void Initialize(int newPrefabIndex, float newTargetScale)
        {
            prefabIndex = newPrefabIndex;
            targetScale = newTargetScale;
            item = GetComponent<ShipItemOar>();
            ApplyScale(transform);
            HookHangMoreCompatibility.Exclude(item);
        }

        internal static void SuppressTransitionOutlines()
        {
            foreach (ScaledOarController controller in ActiveControllers)
            {
                if (controller != null)
                {
                    controller.SuppressStaleOutline();
                }
            }
        }

        private void Awake()
        {
            item = GetComponent<ShipItemOar>();
            HookHangMoreCompatibility.Exclude(item);
        }

        private void OnEnable()
        {
            ActiveControllers.Add(this);
        }

        private void OnDisable()
        {
            ActiveControllers.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveControllers.Remove(this);
        }

        private void LateUpdate()
        {
            if (item == null)
            {
                item = GetComponent<ShipItemOar>();
                if (item == null)
                {
                    return;
                }
            }

            if (itemRigidbody == null)
            {
                itemRigidbody = item.itemRigidbodyC;
            }

            if (itemRigidbody == null || itemRigidbody.GetCurrentInventorySlot() == null)
            {
                ApplyScale(transform);
                if (itemRigidbody != null)
                {
                    ApplyScale(itemRigidbody.transform);
                }
            }

            bool inSleepOrLoadTransition =
                GameState.sleeping ||
                GameState.recovering ||
                GameState.currentlyLoading ||
                GameState.justWokeUp;

            if (inSleepOrLoadTransition)
            {
                if (!GameState.sleeping && !GameState.currentlyLoading)
                {
                    ReconcileCollisionState();
                }

                SuppressStaleOutline();
            }
            else if (wasInSleepOrLoadTransition)
            {
                ReconcileCollisionState();
                SuppressStaleOutline();
            }

            wasInSleepOrLoadTransition = inSleepOrLoadTransition;
        }

        private void ApplyScale(Transform target)
        {
            Vector3 desiredScale = Vector3.one * targetScale;
            if (target.localScale != desiredScale)
            {
                target.localScale = desiredScale;
            }
        }

        private void SuppressStaleOutline()
        {
            if (item == null)
            {
                item = GetComponent<ShipItemOar>();
            }

            if (item != null)
            {
                item.enableRedOutline = false;
            }

            if (outline == null)
            {
                outline = GetComponent<Outline>();
            }

            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private void ReconcileCollisionState()
        {
            PickupableItemCollisionChecker checker = item?.colChecker;
            if (checker == null || CollidedCollidersField == null ||
                CurrentDecolDistanceField == null)
            {
                return;
            }

            List<Collider> collidedColliders =
                CollidedCollidersField.GetValue(checker) as List<Collider>;
            Collider itemCollider = checker.GetComponent<Collider>();
            int actualCollisions = 0;
            float deepestPenetration = 0f;

            bool shouldCheckCollisions =
                item.held &&
                item.GetCurrentInventorySlot() < 0 &&
                itemCollider != null &&
                itemCollider.enabled &&
                collidedColliders != null;

            if (shouldCheckCollisions)
            {
                foreach (Collider other in collidedColliders)
                {
                    if (other == null || !other.enabled ||
                        !other.gameObject.activeInHierarchy || other.isTrigger ||
                        other.gameObject == item.gameObject || other.CompareTag("Boat"))
                    {
                        continue;
                    }

                    Vector3 direction;
                    float distance;
                    bool penetrating = Physics.ComputePenetration(
                        itemCollider,
                        itemCollider.transform.position,
                        itemCollider.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out direction,
                        out distance);

                    if (penetrating && distance > 0f)
                    {
                        actualCollisions++;
                        deepestPenetration = Mathf.Max(deepestPenetration, distance);
                    }
                }
            }

            checker.collisions = actualCollisions;
            CurrentDecolDistanceField.SetValue(checker, deepestPenetration);
            checker.allowObstructedDropping = deepestPenetration < 0.06f;
            item.enableRedOutline = false;
        }
    }

    [HarmonyPatch(typeof(PrefabsDirectory), "Start")]
    internal static class ScaledOarPrefabRegistrationPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(PrefabsDirectory __instance)
        {
            ScaledOarFactory.EnsureRegistered(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PrefabsDirectory __instance)
        {
            ScaledOarFactory.ValidateRuntimeCache(__instance);
            BoneIslandBfoPlacement.OnPrefabsReady();
        }
    }

    [HarmonyPatch(typeof(OutlineEffect), "OnEnable")]
    internal static class ScaledOarOutlineEffectPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            ScaledOarController.SuppressTransitionOutlines();
        }
    }

    [HarmonyPatch(typeof(ShipItemHammer), nameof(ShipItemHammer.CanNail))]
    internal static class ScaledOarNailingPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ShipItem item, ref bool __result)
        {
            if (HookHangMoreCompatibility.IsExcluded(item))
            {
                __result = item.sold;
            }
        }
    }

    [HarmonyPatch(typeof(ShipItem), "Awake")]
    [HarmonyAfter(HookHangMoreCompatibility.PluginGuid)]
    internal static class HookHangMoreExclusionPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ShipItem __instance)
        {
            HookHangMoreCompatibility.Exclude(__instance);
        }
    }
}
