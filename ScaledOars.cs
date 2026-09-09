using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using cakeslice;
using HarmonyLib;
using UnityEngine;

namespace PowerfulOar
{
    internal static class OarStats
    {
        internal const float OriginalForce = 36000f;
        internal const float OriginalMaxBoatSpeed = 2.06f;
        internal const float OriginalNeedsCostPerSecond = 0.1f;

        internal const float BigForce = 93500f;
        internal const float BigMaxBoatSpeed = 3.1f;
        internal const float BigNeedsCostPerSecond = 0.2f;

        internal const float HugeForce = 204000f;
        internal const float HugeMaxBoatSpeed = 5.14f;
        internal const float HugeNeedsCostPerSecond = 0.3f;

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

                case ScaledOarFactory.LegacyBigOarPrefabIndex:
                    if (!IsLegacyBigOar(oar))
                    {
                        return false;
                    }

                    oar.rowForce = BigForce;
                    oar.maxBoatSpeed = BigMaxBoatSpeed;
                    return true;

                case ScaledOarFactory.HugeOarPrefabIndex:
                    oar.rowForce = HugeForce;
                    oar.maxBoatSpeed = HugeMaxBoatSpeed;
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

                case ScaledOarFactory.LegacyBigOarPrefabIndex:
                    return IsLegacyBigOar(oar)
                        ? BigNeedsCostPerSecond
                        : fallback;

                case ScaledOarFactory.HugeOarPrefabIndex:
                    return HugeNeedsCostPerSecond;

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

        internal static bool IsScaledOar(ShipItem item)
        {
            int prefabIndex = GetPrefabIndex(item);
            return prefabIndex == ScaledOarFactory.BigOarPrefabIndex ||
                   IsLegacyBigOar(item) ||
                   prefabIndex == ScaledOarFactory.HugeOarPrefabIndex ||
                   prefabIndex == ScaledOarFactory.Bfo5000PrefabIndex;
        }

        private static bool IsLegacyBigOar(ShipItem item)
        {
            return item != null &&
                   GetPrefabIndex(item) ==
                       ScaledOarFactory.LegacyBigOarPrefabIndex &&
                   item.GetComponent<ScaledOarController>() != null;
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
        internal const int LegacyBigOarPrefabIndex = 169;
        internal const int BigOarPrefabIndex = 602;
        internal const int HugeOarPrefabIndex = 603;
        internal const int Bfo5000PrefabIndex = 666;
        internal const float BigOarScale = 2f;
        internal const float HugeOarScale = 3f;
        internal const float Bfo5000Scale = 5f;

        private static readonly ScaledOarDefinition[] Definitions =
        {
            new ScaledOarDefinition(
                BigOarPrefabIndex, "Big Oar", BigOarScale,
                1.50f, 2.80f, 1.00f, 8f, 4),
            new ScaledOarDefinition(
                HugeOarPrefabIndex, "Huge Oar", HugeOarScale,
                2.25f, 4.20f, 1.50f, 27f, 12),
            new ScaledOarDefinition(
                Bfo5000PrefabIndex, "BFO 5000", Bfo5000Scale,
                3.75f, 7.00f, 2.50f, 125f, 10)
        };
        private static readonly ScaledOarDefinition LegacyBigOarDefinition =
            new ScaledOarDefinition(
                LegacyBigOarPrefabIndex, "Big Oar", BigOarScale,
                1.50f, 2.80f, 1.00f, 8f, 4);

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

            success &= EnsureLegacyBigOarRegistered(directory, vanillaOar);

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

            GameObject legacyPrefab = directory.directory[LegacyBigOarPrefabIndex];
            ShipItem legacyItem =
                legacyPrefab != null ? legacyPrefab.GetComponent<ShipItem>() : null;
            SaveablePrefab legacySaveable =
                legacyPrefab != null ? legacyPrefab.GetComponent<SaveablePrefab>() : null;
            ScaledOarController legacyController = legacyPrefab != null
                ? legacyPrefab.GetComponent<ScaledOarController>()
                : null;
            if (legacyItem == null || legacySaveable == null ||
                legacyController == null ||
                legacySaveable.prefabIndex != LegacyBigOarPrefabIndex ||
                legacyController.PrefabIndex != LegacyBigOarPrefabIndex)
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    "Legacy Big Oar compatibility failed validation at prefab index " +
                    $"{LegacyBigOarPrefabIndex}.");
                success = false;
            }
            else
            {
                directory.shipItems[LegacyBigOarPrefabIndex] = legacyItem;
            }

            return success;
        }

        internal static bool MigrateLegacyBigOar(
            SaveablePrefab saveable,
            SavePrefabData data)
        {
            if (saveable == null || data == null ||
                data.prefabIndex != LegacyBigOarPrefabIndex)
            {
                return false;
            }

            ScaledOarController controller =
                saveable.GetComponent<ScaledOarController>();
            ShipItemOar oar = saveable.GetComponent<ShipItemOar>();
            if (controller == null || oar == null ||
                saveable.prefabIndex != LegacyBigOarPrefabIndex)
            {
                return false;
            }

            saveable.prefabIndex = BigOarPrefabIndex;
            controller.Initialize(BigOarPrefabIndex, BigOarScale);
            OarStats.Apply(oar);
            PowerfulOarPlugin.LogSource?.LogInfo(
                $"Migrated saved Big Oar from legacy prefab " +
                $"{LegacyBigOarPrefabIndex} to {BigOarPrefabIndex}; " +
                "the new index will be written on the next save.");
            return true;
        }

        private static bool EnsureLegacyBigOarRegistered(
            PrefabsDirectory directory,
            ShipItemOar vanillaOar)
        {
            GameObject existing = directory.directory[LegacyBigOarPrefabIndex];
            if (existing != null)
            {
                ScaledOarController controller =
                    existing.GetComponent<ScaledOarController>();
                SaveablePrefab saveable = existing.GetComponent<SaveablePrefab>();
                ShipItemOar existingOar = existing.GetComponent<ShipItemOar>();
                if (controller != null && saveable != null && existingOar != null &&
                    controller.PrefabIndex == LegacyBigOarPrefabIndex &&
                    saveable.prefabIndex == LegacyBigOarPrefabIndex)
                {
                    ConfigureScaledOar(
                        existing,
                        existingOar,
                        vanillaOar,
                        LegacyBigOarDefinition);
                    controller.Initialize(LegacyBigOarPrefabIndex, BigOarScale);
                    return true;
                }

                PowerfulOarPlugin.LogSource?.LogError(
                    $"Could not reserve legacy Big Oar prefab index " +
                    $"{LegacyBigOarPrefabIndex}: it is already occupied by " +
                    $"'{existing.name}'. Old PowerfulOar saves using that index " +
                    "cannot be migrated safely.");
                return false;
            }

            GameObject currentBigOar = directory.directory[BigOarPrefabIndex];
            ShipItemOar currentBigOarItem = currentBigOar != null
                ? currentBigOar.GetComponent<ShipItemOar>()
                : null;
            if (currentBigOarItem == null)
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not create legacy Big Oar compatibility prefab: " +
                    $"current prefab {BigOarPrefabIndex} is unavailable.");
                return false;
            }

            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(currentBigOar);
                ShipItemOar cloneOar = clone.GetComponent<ShipItemOar>();
                ScaledOarController controller =
                    clone.GetComponent<ScaledOarController>();
                if (cloneOar == null || controller == null)
                {
                    PowerfulOarPlugin.LogSource?.LogError(
                        "Could not create legacy Big Oar compatibility prefab: " +
                        "required components are missing.");
                    UnityEngine.Object.Destroy(clone);
                    return false;
                }

                ConfigureScaledOar(
                    clone,
                    cloneOar,
                    vanillaOar,
                    LegacyBigOarDefinition);
                controller.Initialize(LegacyBigOarPrefabIndex, BigOarScale);
                directory.directory[LegacyBigOarPrefabIndex] = clone;
                PowerfulOarPlugin.LogSource?.LogInfo(
                    $"Reserved prefab {LegacyBigOarPrefabIndex} for legacy " +
                    $"Big Oar saves; new Big Oars use prefab {BigOarPrefabIndex}.");
                return true;
            }
            catch (Exception exception)
            {
                if (clone != null)
                {
                    UnityEngine.Object.Destroy(clone);
                }

                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not create legacy Big Oar compatibility prefab: " +
                    exception);
                return false;
            }
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
        private Coroutine initializationRoutine;
        private bool initializationGuardActive;
        private bool ownsKinematicGuard;
        private bool persistentKinematic;
        private bool waitingForSavedCrate;
        private bool waitingForSavedInventory;
        private int savedCrateId;

        internal int PrefabIndex => prefabIndex;

        internal void Initialize(int newPrefabIndex, float newTargetScale)
        {
            prefabIndex = newPrefabIndex;
            targetScale = newTargetScale;
            CacheItemComponents();
            BeginPhysicsInitialization();
            HookHangMoreCompatibility.Exclude(item);
        }

        internal bool CanRestoreSavedCrate(int crateId)
        {
            return waitingForSavedCrate && savedCrateId > 0 &&
                   savedCrateId == crateId;
        }

        internal void PrepareForSavedState(SavePrefabData data)
        {
            if (data == null)
            {
                return;
            }

            CacheItemComponents();
            BeginPhysicsInitialization();

            waitingForSavedCrate = data.crateId > 0;
            waitingForSavedInventory = !waitingForSavedCrate && data.inventorySlot > -1;
            savedCrateId = waitingForSavedCrate ? data.crateId : 0;

            if (!waitingForSavedCrate || item == null || itemRigidbody == null)
            {
                return;
            }

            // Saved crate contents are restored by ShipItem two frames later.
            // Put the oar into a non-physical storage state immediately so its
            // full-size colliders cannot overlap a boat during that window.
            itemRigidbody.attached = true;
            itemRigidbody.disableCol = true;
            itemRigidbody.inStove = true;
            transform.localScale = Vector3.one * item.inventoryScale * 0.33f;

            Collider interactionCollider = item.GetComponent<Collider>();
            if (interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }
        }

        internal void CompleteSavedCrateRestore(int crateId)
        {
            if (!CanRestoreSavedCrate(crateId))
            {
                return;
            }

            waitingForSavedCrate = false;
            savedCrateId = 0;
        }

        internal void OnEnteredInventory()
        {
            waitingForSavedInventory = false;
        }

        internal void RestoreWorldScaleAfterStorage()
        {
            waitingForSavedCrate = false;
            waitingForSavedInventory = false;
            savedCrateId = 0;
            CacheItemComponents();

            Collider interactionCollider = item != null
                ? item.GetComponent<Collider>()
                : null;
            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
            }

            BeginPhysicsInitialization();
        }

        internal void SetPersistentKinematic(bool state)
        {
            persistentKinematic = state;
            CacheItemComponents();
            if (itemRigidbody == null)
            {
                return;
            }

            if (state)
            {
                itemRigidbody.debugForceKinematic = true;
            }
            else if (!initializationGuardActive)
            {
                itemRigidbody.debugForceKinematic = false;
            }

            Rigidbody body = itemRigidbody.GetBody();
            if (body != null)
            {
                if (state)
                {
                    body.isKinematic = true;
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.useGravity = !state;
            }
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
            CacheItemComponents();
            BeginPhysicsInitialization();
            HookHangMoreCompatibility.Exclude(item);
        }

        private void OnEnable()
        {
            ActiveControllers.Add(this);
            StartInitializationRoutineIfNeeded();
        }

        private void OnDisable()
        {
            ActiveControllers.Remove(this);
            if (initializationRoutine != null)
            {
                StopCoroutine(initializationRoutine);
                initializationRoutine = null;
            }
        }

        private void OnDestroy()
        {
            ActiveControllers.Remove(this);
        }

        private void LateUpdate()
        {
            CacheItemComponents();
            if (item == null)
            {
                return;
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

        private void CacheItemComponents()
        {
            if (item == null)
            {
                item = GetComponent<ShipItemOar>();
            }

            if (itemRigidbody == null && item != null)
            {
                itemRigidbody = item.itemRigidbodyC;
            }
        }

        private void BeginPhysicsInitialization()
        {
            CacheItemComponents();
            if (item == null || itemRigidbody == null)
            {
                return;
            }

            ApplyScale(transform);
            ApplyScale(itemRigidbody.transform);

            if (!initializationGuardActive)
            {
                initializationGuardActive = true;
                if (!itemRigidbody.debugForceKinematic)
                {
                    itemRigidbody.debugForceKinematic = true;
                    ownsKinematicGuard = true;
                }
            }

            StartInitializationRoutineIfNeeded();
        }

        private void StartInitializationRoutineIfNeeded()
        {
            if (!initializationGuardActive || initializationRoutine != null ||
                !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return;
            }

            initializationRoutine = StartCoroutine(CompletePhysicsInitialization());
        }

        private IEnumerator CompletePhysicsInitialization()
        {
            while (itemRigidbody == null || itemRigidbody.GetBody() == null ||
                   itemRigidbody.GetComponent<Collider>() == null)
            {
                CacheItemComponents();
                yield return null;
            }

            int storageRestoreGraceFrames = 0;
            while (GameState.currentlyLoading || GameState.loadingBoatLocalItems ||
                   GameState.recovering || waitingForSavedCrate ||
                   waitingForSavedInventory)
            {
                bool loading = GameState.currentlyLoading ||
                               GameState.loadingBoatLocalItems ||
                               GameState.recovering;
                if (!loading && (waitingForSavedCrate || waitingForSavedInventory))
                {
                    storageRestoreGraceFrames++;
                    if (storageRestoreGraceFrames > 30)
                    {
                        RecoverFailedStorageRestore();
                    }
                }
                else
                {
                    storageRestoreGraceFrames = 0;
                }

                yield return null;
            }

            Rigidbody body = itemRigidbody.GetBody();
            if (body != null)
            {
                body.isKinematic = true;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                if (persistentKinematic)
                {
                    body.useGravity = false;
                }
            }

            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            ReleaseInitializationGuard();
        }

        private void RecoverFailedStorageRestore()
        {
            bool failedCrateRestore = waitingForSavedCrate;
            waitingForSavedCrate = false;
            waitingForSavedInventory = false;
            savedCrateId = 0;

            SaveablePrefab saveable = item != null
                ? item.GetComponent<SaveablePrefab>()
                : null;
            if (failedCrateRestore && saveable != null)
            {
                saveable.currentCrateId = 0;
            }

            if (itemRigidbody != null)
            {
                itemRigidbody.attached = false;
                itemRigidbody.disableCol = false;
                itemRigidbody.inStove = false;
            }

            Collider interactionCollider = item != null
                ? item.GetComponent<Collider>()
                : null;
            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
            }

            ApplyScale(transform);
            if (itemRigidbody != null)
            {
                ApplyScale(itemRigidbody.transform);
            }

            PowerfulOarPlugin.LogSource?.LogWarning(
                $"Recovered {item?.name ?? "scaled oar"} after its saved " +
                "inventory or crate location could not be restored.");
        }

        private void ReleaseInitializationGuard()
        {
            initializationRoutine = null;
            initializationGuardActive = false;
            if (ownsKinematicGuard && !persistentKinematic && itemRigidbody != null)
            {
                itemRigidbody.debugForceKinematic = false;
            }

            ownsKinematicGuard = false;
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
