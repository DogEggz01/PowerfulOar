using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using cakeslice;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PowerfulOar
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        HookHangMoreCompatibility.PluginGuid,
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        RadRefinementCompatibility.PluginGuid,
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class PowerfulOarPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "DogEggz.PowerfulOar";
        public const string PluginName = "PowerfulOar";
        public const string PluginVersion = "1.0.1";

        internal const string GoPointerInputLoopMethod = "LateUpdate";

        private Harmony harmony;

        internal static PowerfulOarPlugin Instance { get; private set; }
        internal static ManualLogSource LogSource { get; private set; }

        private void Awake()
        {
            Instance = this;
            LogSource = Logger;

            harmony = new Harmony(PluginGuid);
            RadRefinementCompatibility.Initialize(harmony);
            bool statsPatched = ApplyPatchClass(
                typeof(OarStatsPatch),
                typeof(ShipItemOar),
                nameof(ShipItemOar.OnAltHeld));
            bool lateUpdatePatched = ApplyPatchClass(
                typeof(OarLateUpdatePatch),
                typeof(ShipItemOar),
                nameof(ShipItemOar.ExtraLateUpdate));
            bool prefabPatched = ApplyPatchClass(
                typeof(ScaledOarPrefabRegistrationPatch),
                typeof(PrefabsDirectory),
                "Start");
            bool outlinePatched = ApplyPatchClass(
                typeof(ScaledOarOutlineEffectPatch),
                typeof(OutlineEffect),
                "OnEnable");
            bool reverseInputPatched = ApplyPatchClass(
                typeof(ReverseOarInputPatch),
                typeof(GoPointer),
                GoPointerInputLoopMethod);
            bool nailingPatched = ApplyPatchClass(
                typeof(ScaledOarNailingPatch),
                typeof(ShipItemHammer),
                nameof(ShipItemHammer.CanNail),
                new[] { typeof(ShipItem) });
            bool vendorPatched = ApplyPatchClass(
                typeof(BigOarVendorPatch),
                typeof(ShopItemSpawner),
                "Start");
            bool vendorDisplayPatched = ApplyPatchClass(
                typeof(BigOarVendorSpawnPatch),
                typeof(ShopItemSpawner),
                "SpawnItem");
            bool hookCompatibilityPatched = ApplyPatchClass(
                typeof(HookHangMoreExclusionPatch),
                typeof(ShipItem),
                "Awake");

            BoneIslandBfoPlacement.Initialize();

            Logger.LogInfo(
                $"{PluginName} {PluginVersion} loaded. " +
                "Fixed per-oar stats and needs costs enabled. Hold Q to row opposite vanilla. " +
                "Bone Island BFO placement and upright Big Oar vendor displays enabled. " +
                "Press F7 while aiming at Bone Island to capture replacement coordinates. " +
                $"Patches: stats={statsPatched}, late update={lateUpdatePatched}, " +
                $"prefab={prefabPatched}, outline wake fix={outlinePatched}, " +
                $"reverse input={reverseInputPatched}, nailing={nailingPatched}, " +
                $"vendors={vendorPatched}, vendor displays={vendorDisplayPatched}, " +
                $"HookHangMore exclusion={hookCompatibilityPatched}, " +
                $"RadRefinement needs={RadRefinementCompatibility.NeedsReductionEnabled}, " +
                $"continual row={RadRefinementCompatibility.ContinualRowAvailable}.");
        }

        private void Update()
        {
            BoneIslandPlacementCapture.CheckForCapture();
        }

        private bool ApplyPatchClass(
            Type patchType,
            Type targetType,
            string targetMethodName,
            Type[] argumentTypes = null)
        {
            MethodInfo target = AccessTools.DeclaredMethod(
                targetType,
                targetMethodName,
                argumentTypes ?? Type.EmptyTypes);
            if (target == null)
            {
                Logger.LogError(
                    $"Skipped {patchType.Name}: target method " +
                    $"{targetType.FullName}.{targetMethodName} was not found.");
                return false;
            }

            try
            {
                harmony.PatchAll(patchType);
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    $"Failed to apply {patchType.Name} to " +
                    $"{targetType.FullName}.{targetMethodName}: {exception}");
                return false;
            }
        }

        private void OnDestroy()
        {
            BoneIslandBfoPlacement.Shutdown();
            harmony?.UnpatchSelf();

            if (Instance == this)
            {
                Instance = null;
                LogSource = null;
            }
        }
    }

    internal static class PowerfulOarInput
    {
        internal static bool ReverseRowHeld =>
            GameState.playing &&
            !GameState.inCursorMenu &&
            !GameState.wasInSettingsMenu &&
            Input.GetKey(KeyCode.Q);
    }

    internal static class RadRefinementCompatibility
    {
        internal const string PluginGuid = "com.raddude.radrefinements";

        private const string ConfigsTypeName = "RadRefinements.Configs";
        private const string OarPatchTypeName =
            "RadRefinements.Patches.OarPatches+ShipItemOarPatches";

        private static ConfigEntry<float> rowingNeedsReduction;
        private static ConfigEntry<bool> continualRow;
        private static bool needsReductionEnabled;
        private static bool continualRowAvailable;

        internal static bool NeedsReductionEnabled => needsReductionEnabled;
        internal static bool ContinualRowAvailable => continualRowAvailable;

        internal static float NeedsCostMultiplier
        {
            get
            {
                if (!needsReductionEnabled || rowingNeedsReduction == null)
                {
                    return 1f;
                }

                return 1f - Mathf.Clamp01(rowingNeedsReduction.Value);
            }
        }

        internal static bool ContinualRowEnabled =>
            continualRowAvailable && continualRow != null && continualRow.Value;

        internal static void Initialize(Harmony harmony)
        {
            Type configsType = AccessTools.TypeByName(ConfigsTypeName);
            Type oarPatchType = AccessTools.TypeByName(OarPatchTypeName);
            if (configsType == null || oarPatchType == null)
            {
                return;
            }

            rowingNeedsReduction =
                AccessTools.Field(configsType, "rowingNeedsMult")
                    ?.GetValue(null) as ConfigEntry<float>;
            continualRow =
                AccessTools.Field(configsType, "continualRow")
                    ?.GetValue(null) as ConfigEntry<bool>;

            MethodBase extraLateUpdate = AccessTools.DeclaredMethod(
                typeof(ShipItemOar),
                nameof(ShipItemOar.ExtraLateUpdate));
            MethodBase onAltHeld = AccessTools.DeclaredMethod(
                typeof(ShipItemOar),
                nameof(ShipItemOar.OnAltHeld));
            MethodInfo reduceNeedsPrefix = AccessTools.Method(
                oarPatchType,
                "ReduceNeedsPrefix");
            MethodInfo reduceNeedsPostfix = AccessTools.Method(
                oarPatchType,
                "ReduceNeedsPostfix");
            MethodInfo continualRowPostfix = AccessTools.Method(
                oarPatchType,
                "ContinualRow");

            continualRowAvailable =
                continualRow != null &&
                ContainsPatch(onAltHeld, continualRowPostfix);

            bool canReplaceNeedsPatches =
                harmony != null &&
                rowingNeedsReduction != null &&
                ContainsPatch(extraLateUpdate, reduceNeedsPrefix) &&
                ContainsPatch(extraLateUpdate, reduceNeedsPostfix);
            if (!canReplaceNeedsPatches)
            {
                PowerfulOarPlugin.LogSource?.LogWarning(
                    "RadRefinement was detected, but its rowing-needs patches " +
                    "could not be safely replaced. Its slider will not alter " +
                    "PowerfulOar needs costs.");
                return;
            }

            try
            {
                // RadRefinement refunds the vanilla 0.4/0.6 rates. Remove only
                // those two patches, then apply its slider to each oar's actual
                // PowerfulOar baseline inside OarStats.
                harmony.Unpatch(extraLateUpdate, reduceNeedsPrefix);
                harmony.Unpatch(extraLateUpdate, reduceNeedsPostfix);

                needsReductionEnabled =
                    !ContainsPatch(extraLateUpdate, reduceNeedsPrefix) &&
                    !ContainsPatch(extraLateUpdate, reduceNeedsPostfix);

                if (needsReductionEnabled)
                {
                    PowerfulOarPlugin.LogSource?.LogInfo(
                        "RadRefinement rowing-needs compatibility enabled: " +
                        "its reduction slider now scales PowerfulOar baselines.");
                }
                else
                {
                    PowerfulOarPlugin.LogSource?.LogError(
                        "RadRefinement rowing-needs patches remained installed; " +
                        "slider compatibility was disabled.");
                }
            }
            catch (Exception exception)
            {
                needsReductionEnabled = false;
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not install RadRefinement rowing-needs compatibility: " +
                    exception);
            }

            if (continualRowAvailable)
            {
                PowerfulOarPlugin.LogSource?.LogInfo(
                    "RadRefinement continual-row force compatibility enabled.");
            }
        }

        private static bool ContainsPatch(
            MethodBase target,
            MethodInfo patchMethod)
        {
            if (target == null || patchMethod == null)
            {
                return false;
            }

            Patches patches = Harmony.GetPatchInfo(target);
            if (patches == null)
            {
                return false;
            }

            return ContainsPatch(patches.Prefixes, patchMethod) ||
                   ContainsPatch(patches.Postfixes, patchMethod) ||
                   ContainsPatch(patches.Transpilers, patchMethod) ||
                   ContainsPatch(patches.Finalizers, patchMethod);
        }

        private static bool ContainsPatch(
            IEnumerable<Patch> patches,
            MethodInfo patchMethod)
        {
            foreach (Patch patch in patches)
            {
                if (patch.PatchMethod == patchMethod)
                {
                    return true;
                }
            }

            return false;
        }
    }

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

    internal static class HookHangMoreCompatibility
    {
        internal const string PluginGuid = "com.raddude.hookshangmore";

        private const string AttachableItemTypeName = "HooksHangMore.AttachableItem";

        internal static bool IsExcluded(ShipItem item)
        {
            int prefabIndex = OarStats.GetPrefabIndex(item);
            return prefabIndex == ScaledOarFactory.BigOarPrefabIndex ||
                   prefabIndex == ScaledOarFactory.Bfo5000PrefabIndex;
        }

        internal static void Exclude(ShipItem item)
        {
            if (!IsExcluded(item))
            {
                return;
            }

            HangableItem vanillaHangable = item.GetComponent<HangableItem>();
            if (vanillaHangable != null && vanillaHangable.enabled)
            {
                vanillaHangable.DisconnectJoint();
                vanillaHangable.enabled = false;
                PowerfulOarPlugin.LogSource?.LogInfo(
                    $"Disabled vanilla hook hanging for {item.name}.");
            }

            Type attachableType = AccessTools.TypeByName(AttachableItemTypeName);
            Component attachable =
                attachableType != null ? item.GetComponent(attachableType) : null;
            if (attachable == null)
            {
                return;
            }

            Behaviour attachableBehaviour = attachable as Behaviour;
            if (attachableBehaviour != null)
            {
                attachableBehaviour.enabled = false;
            }

            // HooksHangMore's click patches test only whether AttachableItem
            // exists; they do not respect Behaviour.enabled. Destroying the
            // component also lets its OnDestroy detach any live holder safely.
            UnityEngine.Object.Destroy(attachable);
            PowerfulOarPlugin.LogSource?.LogInfo(
                $"Removed HooksHangMore attachment support from {item.name}.");
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

    [HarmonyPatch(typeof(ShopItemSpawner), "Start")]
    internal static class BigOarVendorPatch
    {
        private static readonly Dictionary<string, float> TargetSceneOffsets =
            new Dictionary<string, float>
        {
            { "island 9 E Dragon Cliffs", 0f },
            { "island 1 A Gold Rock", 0f },
            { "island 15 M (Fort)", 0.40f }
        };

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ShopItemSpawner __instance)
        {
            Scene scene = __instance.gameObject.scene;
            float outwardOffset;
            if (!scene.IsValid() ||
                !TargetSceneOffsets.TryGetValue(scene.name, out outwardOffset))
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

            ShopItemSpawner candidate = null;
            string candidatePath = null;
            ShopItemSpawner leftCandidate = null;
            string leftCandidatePath = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ShopItemSpawner spawner in
                         root.GetComponentsInChildren<ShopItemSpawner>(true))
                {
                    int prefabIndex = GetPrefabIndex(spawner.itemPrefab);
                    if (prefabIndex == ScaledOarFactory.BigOarPrefabIndex)
                    {
                        return;
                    }

                    if (prefabIndex != ScaledOarFactory.VanillaOarPrefabIndex)
                    {
                        continue;
                    }

                    string hierarchyPath = GetHierarchyPath(spawner.transform);
                    // The paired vanilla-oar slots are authored as adjacent
                    // siblings from left to right. Replace the later/right slot
                    // and leave the earlier/left slot untouched.
                    if (leftCandidate == null ||
                        string.CompareOrdinal(hierarchyPath, leftCandidatePath) < 0)
                    {
                        leftCandidate = spawner;
                        leftCandidatePath = hierarchyPath;
                    }

                    if (candidate == null ||
                        string.CompareOrdinal(hierarchyPath, candidatePath) > 0)
                    {
                        candidate = spawner;
                        candidatePath = hierarchyPath;
                    }
                }
            }

            if (candidate == null)
            {
                PowerfulOarPlugin.LogSource?.LogWarning(
                    $"No vanilla-oar vendor slot was found in {scene.name}.");
                return;
            }

            Vector3 outwardDirection = leftCandidate != null && leftCandidate != candidate
                ? Vector3.ProjectOnPlane(
                    candidate.transform.position - leftCandidate.transform.position,
                    Vector3.up)
                : Vector3.ProjectOnPlane(candidate.transform.right, Vector3.up);
            if (outwardDirection.sqrMagnitude < 0.0001f)
            {
                outwardDirection = Vector3.right;
            }

            candidate.itemPrefab = directory.directory[ScaledOarFactory.BigOarPrefabIndex];
            BigOarVendorDisplaySpawner display =
                candidate.GetComponent<BigOarVendorDisplaySpawner>();
            if (display == null)
            {
                display = candidate.gameObject.AddComponent<BigOarVendorDisplaySpawner>();
            }

            display.Initialize(scene.name, outwardDirection.normalized, outwardOffset);
            PowerfulOarPlugin.LogSource?.LogInfo(
                $"Replaced one vanilla-oar vendor slot with Big Oar in {scene.name} " +
                $"at {candidatePath}; configured outward display offset is " +
                $"{outwardOffset:0.00} m.");
        }

        private static int GetPrefabIndex(GameObject prefab)
        {
            SaveablePrefab saveable =
                prefab != null ? prefab.GetComponent<SaveablePrefab>() : null;
            return saveable != null ? saveable.prefabIndex : -1;
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target.GetSiblingIndex().ToString("D4", CultureInfo.InvariantCulture);
            while (target.parent != null)
            {
                target = target.parent;
                path = target.GetSiblingIndex().ToString("D4", CultureInfo.InvariantCulture) +
                       "/" + path;
            }

            return path;
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
        private const float SurfaceClearance = 0.03f;
        private const float MinimumGroundDrop = 0.5f;
        private const float GroundProbeHeight = 1.5f;
        private const float GroundProbeDistance = 8f;

        private string sceneName;
        private Vector3 outwardDirection;
        private float outwardOffset;

        internal void Initialize(
            string newSceneName,
            Vector3 newOutwardDirection,
            float newOutwardOffset)
        {
            sceneName = newSceneName;
            outwardDirection = newOutwardDirection;
            outwardOffset = newOutwardOffset;
        }

        internal void ConfigureSpawnedItem(ShipItem item)
        {
            if (item == null)
            {
                return;
            }

            Transform placementFrame = transform.parent != null ? transform.parent : transform;
            Vector3 requestedSurface =
                transform.position + outwardDirection * outwardOffset;
            Vector3 surfacePoint = FindSurfacePoint(requestedSurface, item.transform);

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            // The oar mesh's blade is its negative local-Y end. Keep local up
            // pointing upward so the blade rests at the bottom like the vanilla
            // vendor oar while retaining the authored stall-facing yaw.
            Quaternion worldRotation =
                Quaternion.LookRotation(forward.normalized, Vector3.up);

            BigOarVendorDisplayItem controller =
                item.GetComponent<BigOarVendorDisplayItem>();
            if (controller == null)
            {
                controller = item.gameObject.AddComponent<BigOarVendorDisplayItem>();
            }

            controller.Initialize(
                item,
                placementFrame,
                placementFrame.InverseTransformPoint(surfacePoint),
                Quaternion.Inverse(placementFrame.rotation) * worldRotation,
                2f,
                SurfaceClearance);

            PowerfulOarPlugin.LogSource?.LogInfo(
                $"Positioned upright Big Oar vendor display in {sceneName}. " +
                $"Surface={FormatVector(surfacePoint)}, " +
                $"outwardOffset={outwardOffset:0.00} m.");
        }

        private static Vector3 FindSurfacePoint(
            Vector3 requestedSurface,
            Transform displayedItem)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                requestedSurface + Vector3.up * GroundProbeHeight,
                Vector3.down,
                GroundProbeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            bool foundGround = false;
            RaycastHit selectedGround = default;
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider != null
                    ? hit.collider.transform
                    : null;
                if (hitTransform == null || displayedItem == hitTransform ||
                    hitTransform.IsChildOf(displayedItem) ||
                    Vector3.Dot(hit.normal, Vector3.up) < 0.5f ||
                    hit.point.y > requestedSurface.y - MinimumGroundDrop)
                {
                    continue;
                }

                // Choose the highest upward-facing surface clearly below the
                // authored shop slot. This passes through its tabletop but
                // stops at the walkable ground directly underneath.
                if (!foundGround || hit.point.y > selectedGround.point.y)
                {
                    foundGround = true;
                    selectedGround = hit;
                }
            }

            return foundGround ? selectedGround.point : requestedSurface;
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.000}, {1:0.000}, {2:0.000})",
                value.x,
                value.y,
                value.z);
        }
    }

    [DefaultExecutionOrder(10001)]
    internal sealed class BigOarVendorDisplayItem : MonoBehaviour
    {
        private ShipItem item;
        private BoxCollider itemCollider;
        private Transform placementFrame;
        private Vector3 localSurfacePoint;
        private Quaternion localRotation;
        private float targetWorldScale;
        private float surfaceClearance;

        internal void Initialize(
            ShipItem newItem,
            Transform newPlacementFrame,
            Vector3 newLocalSurfacePoint,
            Quaternion newLocalRotation,
            float newTargetWorldScale,
            float newSurfaceClearance)
        {
            item = newItem;
            itemCollider = GetComponent<BoxCollider>();
            placementFrame = newPlacementFrame;
            localSurfacePoint = newLocalSurfacePoint;
            localRotation = newLocalRotation;
            targetWorldScale = newTargetWorldScale;
            surfaceClearance = newSurfaceClearance;
            ApplyPlacement();
        }

        private void LateUpdate()
        {
            if (item == null || item.sold || item.held)
            {
                enabled = false;
                return;
            }

            // ScaledOarController restores ordinary item scale earlier in
            // LateUpdate. Correct only this shop display instance afterward so
            // parent scale (notably Gold Rock's 2x spawner) cannot double it.
            ApplyPlacement();
        }

        private void ApplyPlacement()
        {
            if (placementFrame == null)
            {
                return;
            }

            Transform itemParent = transform.parent;
            Vector3 parentScale = itemParent != null ? itemParent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(
                SafeLocalScale(parentScale.x),
                SafeLocalScale(parentScale.y),
                SafeLocalScale(parentScale.z));

            Vector3 surfacePoint = placementFrame.TransformPoint(localSurfacePoint);
            transform.SetPositionAndRotation(
                surfacePoint,
                placementFrame.rotation * localRotation);

            float minimumHeight = PlacementBounds.GetMinimumProjection(
                itemCollider,
                surfacePoint,
                Vector3.up);
            transform.position +=
                Vector3.up * (surfaceClearance - minimumHeight);
        }

        private float SafeLocalScale(float parentScale)
        {
            return Mathf.Abs(parentScale) > 0.0001f
                ? targetWorldScale / Mathf.Abs(parentScale)
                : targetWorldScale;
        }
    }

    internal static class PlacementBounds
    {
        internal static float GetMinimumProjection(
            BoxCollider box,
            Vector3 origin,
            Vector3 axis)
        {
            if (box == null)
            {
                return 0f;
            }

            Vector3 halfSize = box.size * 0.5f;
            float minimum = float.PositiveInfinity;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localCorner = box.center + Vector3.Scale(
                            halfSize,
                            new Vector3(x, y, z));
                        Vector3 worldCorner = box.transform.TransformPoint(localCorner);
                        minimum = Mathf.Min(
                            minimum,
                            Vector3.Dot(worldCorner - origin, axis));
                    }
                }
            }

            return float.IsPositiveInfinity(minimum) ? 0f : minimum;
        }
    }

    internal static class BoneIslandBfoPlacement
    {
        private const string BoneIslandSceneName = "island 36 ()";
        private const string HandObjectName = "hand";
        private const float SafeSpawnDistance = 500f;
        private const float RaiseAlongHand = 1.05f;

        private static readonly Vector3 LocalSurfacePosition =
            new Vector3(230.874f, 16.809f, 430.240f);
        private static readonly Vector3 LocalSurfaceEuler =
            new Vector3(346.106f, 11.973f, 252.471f);
        private static readonly Vector3 LocalSurfaceNormal =
            new Vector3(0.948f, -0.292f, -0.127f).normalized;
        private static readonly HashSet<int> PendingScenes = new HashSet<int>();

        private static bool initialized;

        internal static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            QueueLoadedBoneIslandScenes();
        }

        internal static void Shutdown()
        {
            if (!initialized)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            PendingScenes.Clear();
            initialized = false;
        }

        internal static void OnPrefabsReady()
        {
            QueueLoadedBoneIslandScenes();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            QueueScene(scene);
        }

        private static void QueueLoadedBoneIslandScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                QueueScene(SceneManager.GetSceneAt(index));
            }
        }

        private static void QueueScene(Scene scene)
        {
            PowerfulOarPlugin plugin = PowerfulOarPlugin.Instance;
            if (plugin == null || !scene.IsValid() || !scene.isLoaded ||
                scene.name != BoneIslandSceneName || !PendingScenes.Add(scene.handle))
            {
                return;
            }

            plugin.StartCoroutine(PlaceWhenReady(scene));
        }

        private static IEnumerator PlaceWhenReady(Scene scene)
        {
            int sceneHandle = scene.handle;
            Transform scenery = null;
            while (PowerfulOarPlugin.Instance != null && scene.IsValid() && scene.isLoaded)
            {
                if (scenery == null)
                {
                    scenery = FindSceneryRoot(scene);
                }

                PrefabsDirectory directory = PrefabsDirectory.instance;
                Camera camera = Camera.main;
                Vector3 surfacePoint = scenery != null
                    ? scenery.TransformPoint(LocalSurfacePosition)
                    : Vector3.zero;
                if (GameState.playing && !GameState.currentlyLoading &&
                    scenery != null && camera != null &&
                    Vector3.Distance(camera.transform.position, surfacePoint) <=
                        SafeSpawnDistance &&
                    directory != null && directory.directory != null &&
                    directory.directory.Length > ScaledOarFactory.Bfo5000PrefabIndex &&
                    directory.directory[ScaledOarFactory.Bfo5000PrefabIndex] != null)
                {
                    if (ScaledOarFactory.ValidateRuntimeCache(directory))
                    {
                        TryPlace(directory, scenery);
                    }

                    PendingScenes.Remove(sceneHandle);
                    yield break;
                }

                yield return null;
            }

            PendingScenes.Remove(sceneHandle);
        }

        private static void TryPlace(
            PrefabsDirectory directory,
            Transform scenery)
        {
            GameObject registeredPrefab =
                directory.directory[ScaledOarFactory.Bfo5000PrefabIndex];
            if (HasExistingRuntimeBfo(registeredPrefab))
            {
                PowerfulOarPlugin.LogSource?.LogInfo(
                    "Skipped Bone Island BFO display: an existing collected or display " +
                    "BFO 5000 is already loaded.");
                return;
            }

            GameObject instance = UnityEngine.Object.Instantiate(registeredPrefab);
            ShipItem item = instance.GetComponent<ShipItem>();
            BoxCollider itemCollider = instance.GetComponent<BoxCollider>();
            if (item == null || itemCollider == null)
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not place Bone Island BFO 5000: cloned item components missing.");
                instance.SetActive(false);
                return;
            }

            instance.name = "BFO 5000 (Bone Island)";
            instance.transform.SetParent(scenery, true);

            Vector3 surfacePoint = scenery.TransformPoint(LocalSurfacePosition);
            Vector3 worldNormal =
                scenery.TransformDirection(LocalSurfaceNormal).normalized;
            Quaternion capturedSurfaceFrame =
                scenery.rotation * Quaternion.Euler(LocalSurfaceEuler);
            Transform hand = scenery.Find(HandObjectName);

            // The hand mesh's local up axis runs from wrist to fingers. Align
            // the oar's negative local-Y blade with that direction and keep its
            // thin local-Z axis along the palm normal. Raising the transform
            // origin along the same line threads the shaft through the marked
            // opening instead of pushing the entire collider outside the palm.
            Vector3 fingerDirection = hand != null
                ? hand.up.normalized
                : (capturedSurfaceFrame * Vector3.forward).normalized;
            Vector3 thinAxis =
                Vector3.ProjectOnPlane(worldNormal, fingerDirection).normalized;
            if (thinAxis.sqrMagnitude < 0.0001f)
            {
                thinAxis = hand != null
                    ? hand.forward.normalized
                    : (capturedSurfaceFrame * Vector3.up).normalized;
            }

            Quaternion worldRotation =
                Quaternion.LookRotation(thinAxis, -fingerDirection);
            Vector3 passThroughPoint =
                surfacePoint + fingerDirection * RaiseAlongHand;
            instance.transform.SetPositionAndRotation(passThroughPoint, worldRotation);

            item.sold = true;
            Good good = instance.GetComponent<Good>();
            if (good != null)
            {
                good.RegisterAsMissionless();
            }

            ItemRigidbody itemBody = item.GetItemRigidbody();
            if (itemBody != null)
            {
                itemBody.debugForceKinematic = true;
                Rigidbody body = itemBody.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            BoneIslandBfoDisplay display =
                instance.GetComponent<BoneIslandBfoDisplay>();
            if (display == null)
            {
                display = instance.AddComponent<BoneIslandBfoDisplay>();
            }

            display.Initialize(item);
            PowerfulOarPlugin.LogSource?.LogInfo(
                "Placed one fixed BFO 5000 across the Bone Island palm. " +
                $"Raised {RaiseAlongHand:0.00} m along the hand and aligned through " +
                "the finger opening. It will enter normal item/save control when picked up. " +
                $"Camera was within {SafeSpawnDistance:0} m of the placement.");
        }

        private static bool HasExistingRuntimeBfo(GameObject registeredPrefab)
        {
            foreach (SaveablePrefab saveable in
                     Resources.FindObjectsOfTypeAll<SaveablePrefab>())
            {
                if (saveable != null &&
                    saveable.prefabIndex == ScaledOarFactory.Bfo5000PrefabIndex &&
                    saveable.gameObject != registeredPrefab)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform FindSceneryRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "_scenery")
                {
                    return root.transform;
                }
            }

            return null;
        }
    }

    internal sealed class BoneIslandBfoDisplay : MonoBehaviour
    {
        private ShipItem item;
        private ItemRigidbody itemBody;
        private SaveablePrefab saveable;

        internal void Initialize(ShipItem newItem)
        {
            item = newItem;
            itemBody = item != null ? item.GetItemRigidbody() : null;
            saveable = item != null ? item.GetComponent<SaveablePrefab>() : null;
        }

        private void Update()
        {
            if (item == null || !item.held)
            {
                return;
            }

            saveable?.RegisterToSave();
            if (itemBody != null)
            {
                itemBody.debugForceKinematic = false;
                Rigidbody body = itemBody.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.useGravity = true;
                }
            }

            if (FloatingOriginManager.instance != null)
            {
                transform.SetParent(FloatingOriginManager.instance.transform, true);
            }

            enabled = false;
            PowerfulOarPlugin.LogSource?.LogInfo(
                "Released the Bone Island BFO 5000 to normal held-item physics and saving.");
        }
    }

    internal static class BoneIslandPlacementCapture
    {
        private const string BoneIslandSceneName = "island 36 ()";
        private const float CaptureDistance = 500f;

        internal static void CheckForCapture()
        {
            if (!GameState.playing || GameState.inCursorMenu ||
                GameState.wasInSettingsMenu || !Input.GetKeyDown(KeyCode.F7))
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                ShowNotification("BFO capture: camera unavailable.");
                return;
            }

            Scene scene = SceneManager.GetSceneByName(BoneIslandSceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                ShowNotification("BFO capture: Bone Island is not loaded.");
                PowerfulOarPlugin.LogSource?.LogWarning(
                    $"F7 ground capture requires loaded scene '{BoneIslandSceneName}'.");
                return;
            }

            Transform scenery = FindSceneryRoot(scene);
            if (scenery == null)
            {
                ShowNotification("BFO capture: _scenery root not found.");
                PowerfulOarPlugin.LogSource?.LogError(
                    $"Could not capture BFO placement in {scene.name}: _scenery root missing.");
                return;
            }

            RaycastHit hit;
            int ignoredHits;
            int visualCandidates;
            int temporaryColliders;
            if (!TryFindSceneryHit(
                    camera,
                    scenery,
                    out hit,
                    out ignoredHits,
                    out visualCandidates,
                    out temporaryColliders))
            {
                ShowNotification("BFO capture: aim at Bone Island ground.");
                PowerfulOarPlugin.LogSource?.LogWarning(
                    $"F7 ground capture found no Bone Island scenery hit; " +
                    $"ignored {ignoredHits} intervening world hit(s), " +
                    $"tested {visualCandidates} collider-less visual candidate(s), " +
                    $"and created {temporaryColliders} temporary capture collider(s).");
                return;
            }

            Vector3 facing = Vector3.ProjectOnPlane(camera.transform.forward, hit.normal);
            if (facing.sqrMagnitude < 0.0001f)
            {
                facing = Vector3.ProjectOnPlane(camera.transform.up, hit.normal);
            }

            Quaternion worldRotation = Quaternion.LookRotation(facing.normalized, hit.normal);
            Vector3 localPosition = scenery.InverseTransformPoint(hit.point);
            Quaternion localRotation = Quaternion.Inverse(scenery.rotation) * worldRotation;
            Vector3 localEuler = localRotation.eulerAngles;
            Vector3 localNormal = scenery.InverseTransformDirection(hit.normal).normalized;

            PowerfulOarPlugin.LogSource?.LogInfo(
                "BFO 5000 Bone Island placement capture: " +
                $"localPosition={FormatVector(localPosition)}, " +
                $"localEuler={FormatVector(localEuler)}, " +
                $"localSurfaceNormal={FormatVector(localNormal)}, " +
                $"hitObject='{hit.collider.gameObject.name}', " +
                $"ignoredWorldHits={ignoredHits}, " +
                $"visualCandidates={visualCandidates}, " +
                $"temporaryCaptureColliders={temporaryColliders}.");
            ShowNotification("BFO ground captured. Check BepInEx log.");
        }

        private static bool TryFindSceneryHit(
            Camera camera,
            Transform scenery,
            out RaycastHit selectedHit,
            out int ignoredHits,
            out int visualCandidates,
            out int temporaryColliderCount)
        {
            selectedHit = default;
            ignoredHits = 0;
            visualCandidates = 0;
            temporaryColliderCount = 0;
            float nearestDistance = float.PositiveInfinity;
            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            List<MeshCollider> temporaryColliders =
                AddTemporaryVisualColliders(ray, scenery, out visualCandidates);
            temporaryColliderCount = temporaryColliders.Count;

            try
            {
                if (temporaryColliders.Count > 0)
                {
                    Physics.SyncTransforms();
                }

                RaycastHit[] hits = Physics.RaycastAll(
                    ray,
                    CaptureDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                foreach (RaycastHit candidate in hits)
                {
                    Collider collider = candidate.collider;
                    Transform target = collider != null ? collider.transform : null;
                    bool belongsToScenery = target != null &&
                        (target == scenery || target.IsChildOf(scenery));
                    if (!belongsToScenery)
                    {
                        ignoredHits++;
                        continue;
                    }

                    if (candidate.distance < nearestDistance)
                    {
                        nearestDistance = candidate.distance;
                        selectedHit = candidate;
                    }
                }
            }
            finally
            {
                foreach (MeshCollider temporaryCollider in temporaryColliders)
                {
                    if (temporaryCollider == null)
                    {
                        continue;
                    }

                    temporaryCollider.enabled = false;
                    UnityEngine.Object.Destroy(temporaryCollider);
                }
            }

            return !float.IsPositiveInfinity(nearestDistance);
        }

        private static List<MeshCollider> AddTemporaryVisualColliders(
            Ray ray,
            Transform scenery,
            out int visualCandidates)
        {
            visualCandidates = 0;
            List<MeshCollider> temporaryColliders = new List<MeshCollider>();

            foreach (MeshFilter meshFilter in scenery.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter == null || !meshFilter.gameObject.activeInHierarchy ||
                    meshFilter.sharedMesh == null ||
                    meshFilter.GetComponent<Collider>() != null)
                {
                    continue;
                }

                Renderer renderer = meshFilter.GetComponent<Renderer>();
                float boundsDistance;
                if (renderer == null || !renderer.enabled ||
                    !renderer.bounds.IntersectRay(ray, out boundsDistance) ||
                    boundsDistance > CaptureDistance)
                {
                    continue;
                }

                visualCandidates++;
                MeshCollider temporaryCollider = null;
                try
                {
                    temporaryCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    temporaryCollider.sharedMesh = meshFilter.sharedMesh;
                    temporaryCollider.convex = false;
                    temporaryCollider.isTrigger = false;

                    if (temporaryCollider.sharedMesh != null)
                    {
                        temporaryColliders.Add(temporaryCollider);
                    }
                    else
                    {
                        temporaryCollider.enabled = false;
                        UnityEngine.Object.Destroy(temporaryCollider);
                    }
                }
                catch (Exception exception)
                {
                    if (temporaryCollider != null)
                    {
                        temporaryCollider.enabled = false;
                        UnityEngine.Object.Destroy(temporaryCollider);
                    }

                    PowerfulOarPlugin.LogSource?.LogWarning(
                        $"Could not make Bone Island visual '{meshFilter.gameObject.name}' " +
                        $"temporarily raycastable for F7 capture: {exception.Message}");
                }
            }

            return temporaryColliders;
        }

        private static Transform FindSceneryRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "_scenery")
                {
                    return root.transform;
                }
            }

            return null;
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.000}f, {1:0.000}f, {2:0.000}f)",
                value.x,
                value.y,
                value.z);
        }

        private static void ShowNotification(string message)
        {
            if (NotificationUi.instance != null)
            {
                NotificationUi.instance.ShowNotification(message);
            }
        }
    }

    [HarmonyPatch(typeof(GoPointer), PowerfulOarPlugin.GoPointerInputLoopMethod)]
    internal static class ReverseOarInputPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GoPointer __instance)
        {
            if (!PowerfulOarInput.ReverseRowHeld)
            {
                return;
            }

            ShipItemOar oar = __instance.GetHeldItem() as ShipItemOar;
            if (oar == null || GameInput.GetKey(InputName.Activate))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                oar.OnAltActivate();
            }

            oar.OnAltHeld();
        }
    }

    [HarmonyPatch(typeof(ShipItemOar), nameof(ShipItemOar.OnAltHeld))]
    internal static class OarStatsPatch
    {
        private struct RowCallState
        {
            internal float ProgressBefore;
            internal bool Reverse;
        }

        [HarmonyPrefix]
        private static bool Prefix(
            ShipItemOar __instance,
            ref bool ___isHoldingButton,
            bool ___isOverWater,
            ref bool ___isRowing,
            ref float ___rowProgress,
            out RowCallState __state)
        {
            __state = new RowCallState
            {
                ProgressBefore = ___rowProgress,
                Reverse = PowerfulOarInput.ReverseRowHeld
            };

            OarStats.Apply(__instance);

            if (!__state.Reverse)
            {
                return true;
            }

            ___isHoldingButton = true;
            if (__instance.held && ___isOverWater && GameState.currentBoat)
            {
                if (___rowProgress <= 1f)
                {
                    ___isRowing = true;
                    ___rowProgress += Time.deltaTime;

                    Rigidbody body = GameState.currentBoat.parent.GetComponent<Rigidbody>();
                    float speedFade = Mathf.InverseLerp(
                        __instance.maxBoatSpeed,
                        0f,
                        body.velocity.magnitude);

                    body.AddForceAtPosition(
                        __instance.waterPos.forward * __instance.rowForce * Time.deltaTime * speedFade,
                        __instance.waterPos.position);
                }
                else
                {
                    ___isRowing = false;
                }
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyAfter(RadRefinementCompatibility.PluginGuid)]
        private static void Postfix(
            ShipItemOar __instance,
            bool ___isOverWater,
            RowCallState __state)
        {
            if (!RadRefinementCompatibility.ContinualRowEnabled ||
                !__instance.held ||
                !___isOverWater ||
                !GameState.currentBoat ||
                __state.ProgressBefore > 1f)
            {
                return;
            }

            // The normal and reverse paths each advance row progress once
            // before RadRefinement's postfix. Its extra continual force only
            // runs when that updated progress has not completed the stroke.
            float progressBeforeRad = __state.ProgressBefore + Time.deltaTime;
            if (progressBeforeRad > 1f)
            {
                return;
            }

            Rigidbody body = GameState.currentBoat.parent.GetComponent<Rigidbody>();

            // Cancel RadRefinement's unbounded, always-vanilla-direction force.
            Vector3 radForce =
                __instance.waterPos.forward *
                -__instance.rowForce *
                Time.deltaTime;
            body.AddForceAtPosition(
                -radForce,
                __instance.waterPos.position);

            float speedFade = Mathf.InverseLerp(
                __instance.maxBoatSpeed,
                0f,
                body.velocity.magnitude);
            float direction = __state.Reverse ? 1f : -1f;

            // Preserve RadRefinement's additional stroke and progress update,
            // but enforce PowerfulOar direction and per-prefab speed limits.
            body.AddForceAtPosition(
                __instance.waterPos.forward *
                __instance.rowForce *
                Time.deltaTime *
                speedFade *
                direction,
                __instance.waterPos.position);
        }
    }

    [HarmonyPatch(typeof(ShipItemOar), nameof(ShipItemOar.ExtraLateUpdate))]
    internal static class OarLateUpdatePatch
    {
        private const float VanillaWaterCost = 0.4f;
        private const float VanillaFoodCost = 0.6f;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            FieldInfo waterField = AccessTools.Field(typeof(PlayerNeeds), "water");
            FieldInfo foodField = AccessTools.Field(typeof(PlayerNeeds), "food");
            FieldInfo rowProgressField = AccessTools.Field(typeof(ShipItemOar), "rowProgress");

            List<int> waterMatches = FindNeedCostConstants(codes, waterField, VanillaWaterCost);
            List<int> foodMatches = FindNeedCostConstants(codes, foodField, VanillaFoodCost);

            if (waterMatches.Count == 1 && foodMatches.Count == 1)
            {
                MethodInfo waterSelector = AccessTools.Method(
                    typeof(OarLateUpdatePatch),
                    nameof(SelectWaterCost));
                MethodInfo foodSelector = AccessTools.Method(
                    typeof(OarLateUpdatePatch),
                    nameof(SelectFoodCost));

                // Replace the later instruction first so the earlier index remains valid.
                ReplaceNeedCostConstant(codes, foodMatches[0], foodSelector);
                ReplaceNeedCostConstant(codes, waterMatches[0], waterSelector);
                PowerfulOarPlugin.LogSource?.LogInfo(
                    "Patched per-oar rowing water and food costs: " +
                    "Original=0.1/s, Big Oar=0.2/s, BFO 5000=1/s.");
            }
            else
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not safely patch rowing needs costs: " +
                    $"water matches={waterMatches.Count}, food matches={foodMatches.Count}. " +
                    "Vanilla needs costs were left unchanged.");
            }

            List<int> strokeMatches = FindStrokeProgressLoads(codes, rowProgressField);
            if (strokeMatches.Count == 1)
            {
                MethodInfo selector = AccessTools.Method(
                    typeof(OarLateUpdatePatch),
                    nameof(SelectStrokeProgress));
                codes.Insert(strokeMatches[0] + 1, new CodeInstruction(OpCodes.Call, selector));
                PowerfulOarPlugin.LogSource?.LogInfo(
                    "Patched reverse oar stroke animation for the Q key.");
            }
            else
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not safely patch reverse oar stroke animation: " +
                    $"matches={strokeMatches.Count}. Reverse thrust remains available on Q.");
            }

            return codes;
        }

        private static float SelectStrokeProgress(float progress)
        {
            return PowerfulOarInput.ReverseRowHeld ? 1f - progress : progress;
        }

        private static float SelectWaterCost(ShipItemOar oar)
        {
            return OarStats.GetWaterCostPerSecond(oar);
        }

        private static float SelectFoodCost(ShipItemOar oar)
        {
            return OarStats.GetFoodCostPerSecond(oar);
        }

        private static void ReplaceNeedCostConstant(
            IList<CodeInstruction> codes,
            int constantIndex,
            MethodInfo selector)
        {
            CodeInstruction instruction = codes[constantIndex];
            instruction.opcode = OpCodes.Ldarg_0;
            instruction.operand = null;
            codes.Insert(constantIndex + 1, new CodeInstruction(OpCodes.Call, selector));
        }

        private static List<int> FindNeedCostConstants(
            IList<CodeInstruction> codes,
            FieldInfo needsField,
            float expectedConstant)
        {
            List<int> matches = new List<int>();
            for (int i = 0; i <= codes.Count - 6; i++)
            {
                if (codes[i].opcode != OpCodes.Ldsfld || !Equals(codes[i].operand, needsField))
                {
                    continue;
                }

                if (codes[i + 2].opcode != OpCodes.Ldc_R4 ||
                    !(codes[i + 2].operand is float value) ||
                    !Mathf.Approximately(value, expectedConstant) ||
                    codes[i + 3].opcode != OpCodes.Mul ||
                    codes[i + 4].opcode != OpCodes.Sub ||
                    codes[i + 5].opcode != OpCodes.Stsfld ||
                    !Equals(codes[i + 5].operand, needsField))
                {
                    continue;
                }

                matches.Add(i + 2);
            }

            return matches;
        }

        private static List<int> FindStrokeProgressLoads(
            IList<CodeInstruction> codes,
            FieldInfo rowProgressField)
        {
            List<int> matches = new List<int>();
            for (int i = 0; i <= codes.Count - 6; i++)
            {
                if (!LoadsFloat(codes[i], -1f) ||
                    !LoadsFloat(codes[i + 1], 2f) ||
                    codes[i + 2].opcode != OpCodes.Ldarg_0 ||
                    codes[i + 3].opcode != OpCodes.Ldfld ||
                    !Equals(codes[i + 3].operand, rowProgressField) ||
                    codes[i + 4].opcode != OpCodes.Mul ||
                    codes[i + 5].opcode != OpCodes.Add)
                {
                    continue;
                }

                matches.Add(i + 3);
            }

            return matches;
        }

        private static bool LoadsFloat(CodeInstruction instruction, float expected)
        {
            return instruction.opcode == OpCodes.Ldc_R4 &&
                   instruction.operand is float value &&
                   Mathf.Approximately(value, expected);
        }
    }

}
