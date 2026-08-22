using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using cakeslice;
using HarmonyLib;
using UnityEngine;

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
        public const string PluginVersion = "1.0.3";

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
                $"Patches: stats={statsPatched}, late update={lateUpdatePatched}, " +
                $"prefab={prefabPatched}, outline wake fix={outlinePatched}, " +
                $"reverse input={reverseInputPatched}, nailing={nailingPatched}, " +
                $"vendors={vendorPatched}, vendor displays={vendorDisplayPatched}, " +
                $"HookHangMore exclusion={hookCompatibilityPatched}, " +
                $"RadRefinement needs={RadRefinementCompatibility.NeedsReductionEnabled}, " +
                $"continual row={RadRefinementCompatibility.ContinualRowAvailable}.");
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
}
