using System;
using System.Collections.Generic;
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
        private sealed class PatchTarget
        {
            internal PatchTarget(
                Type patchType,
                Type targetType,
                string targetMethodName,
                Type[] argumentTypes = null)
            {
                PatchType = patchType;
                TargetType = targetType;
                TargetMethodName = targetMethodName;
                ArgumentTypes = argumentTypes;
            }

            internal Type PatchType { get; }
            internal Type TargetType { get; }
            internal string TargetMethodName { get; }
            internal Type[] ArgumentTypes { get; }
        }

        public const string PluginGuid = "DogEggz.PowerfulOar";
        public const string PluginName = "PowerfulOar";
        public const string PluginVersion = "1.1.0";

        internal const string GoPointerInputLoopMethod = "LateUpdate";

        private Harmony harmony;
        private readonly List<Harmony> featureHarmonies = new List<Harmony>();

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
            bool lifecyclePatched = ApplyPatchGroup(
                "runtime_lifecycle",
                new PatchTarget(
                    typeof(ScaledOarRigidbodyScalePatch),
                    typeof(ItemRigidbody),
                    nameof(ItemRigidbody.LateUpdate)),
                new PatchTarget(
                    typeof(ScaledOarSavedStatePatch),
                    typeof(SaveablePrefab),
                    nameof(SaveablePrefab.Load),
                    new[] { typeof(SavePrefabData) }),
                new PatchTarget(
                    typeof(ScaledOarInventoryEnterPatch),
                    typeof(ItemRigidbody),
                    nameof(ItemRigidbody.EnterInventorySlot),
                    new[] { typeof(Transform) }),
                new PatchTarget(
                    typeof(ScaledOarInventoryExitPatch),
                    typeof(ItemRigidbody),
                    nameof(ItemRigidbody.ExitInventorySlot)));
            bool prefabPatched = lifecyclePatched && ApplyPatchClass(
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
            bool vendorPatched = prefabPatched && ApplyPatchGroup(
                "vendor_display",
                new PatchTarget(
                    typeof(BigOarVendorPatch),
                    typeof(ShopItemSpawner),
                    "Start"),
                new PatchTarget(
                    typeof(ScaledOarVendorSpawnPatch),
                    typeof(ShopItemSpawner),
                    "SpawnItem"),
                new PatchTarget(
                    typeof(HugeOarVendorPatch),
                    typeof(Shopkeeper),
                    "Start"));
            bool hugeDialoguePatched = vendorPatched && ApplyPatchGroup(
                "huge_oar_dialogue",
                new PatchTarget(
                    typeof(HugeOarDialogueCapturePatch),
                    typeof(TavernRumorsDude),
                    "Awake"),
                new PatchTarget(
                    typeof(HugeOarPurchasePatch),
                    typeof(Shopkeeper),
                    "SellItem",
                    new[] { typeof(ShipItem), typeof(int), typeof(int) }),
                new PatchTarget(
                    typeof(HugeOarPurchaseSessionPatch),
                    typeof(SaveLoadManager),
                    "Awake"),
                new PatchTarget(
                    typeof(HugeOarPurchaseLoadGamePatch),
                    typeof(SaveLoadManager),
                    nameof(SaveLoadManager.LoadGame),
                    new[] { typeof(int) }),
                new PatchTarget(
                    typeof(HugeOarPurchaseLoadPatch),
                    typeof(SaveLoadManager),
                    nameof(SaveLoadManager.LoadModData)),
                new PatchTarget(
                    typeof(HugeOarPurchaseStorePatch),
                    typeof(SaveLoadManager),
                    nameof(SaveLoadManager.SaveModData)));
            bool hookCompatibilityPatched = ApplyPatchClass(
                typeof(HookHangMoreExclusionPatch),
                typeof(ShipItem),
                "Awake");
            bool cratePatched = prefabPatched && ApplyPatchGroup(
                "crate_blocker",
                new PatchTarget(
                    typeof(ScaledOarCrateInsertPatch),
                    typeof(CrateInventory),
                    nameof(CrateInventory.InsertItem),
                    new[] { typeof(ShipItem) }),
                new PatchTarget(
                    typeof(ScaledOarCrateWithdrawPatch),
                    typeof(CrateInventory),
                    nameof(CrateInventory.WithdrawItem),
                    new[] { typeof(ShipItem) }),
                new PatchTarget(
                    typeof(ScaledOarCrateUiPatch),
                    typeof(CrateInventoryButton),
                    nameof(CrateInventoryButton.OnActivate),
                    new[] { typeof(GoPointer) }));

            if (prefabPatched)
            {
                BoneIslandBfoPlacement.Initialize();
            }

            Logger.LogInfo(
                $"{PluginName} {PluginVersion} loaded. " +
                "Fixed per-oar stats and needs costs enabled. Hold Q to row opposite vanilla. " +
                "Bone Island BFO placement, scaled-oar vendor displays, and " +
                "Kicia Bay Huge Oar enabled. " +
                $"Patches: stats={statsPatched}, late update={lateUpdatePatched}, " +
                $"prefab={prefabPatched}, outline wake fix={outlinePatched}, " +
                $"reverse input={reverseInputPatched}, nailing={nailingPatched}, " +
                $"vendor display group={vendorPatched}, " +
                $"Huge Oar dialogue group={hugeDialoguePatched}, " +
                $"HookHangMore exclusion={hookCompatibilityPatched}, " +
                $"scaled-oar lifecycle group={lifecyclePatched}, " +
                $"crate blocker group={cratePatched}, " +
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

        private bool ApplyPatchGroup(string groupName, params PatchTarget[] targets)
        {
            foreach (PatchTarget target in targets)
            {
                MethodInfo targetMethod = AccessTools.DeclaredMethod(
                    target.TargetType,
                    target.TargetMethodName,
                    target.ArgumentTypes ?? Type.EmptyTypes);
                if (targetMethod == null)
                {
                    Logger.LogError(
                        $"Skipped patch group '{groupName}': target method " +
                        $"{target.TargetType.FullName}.{target.TargetMethodName} was not found.");
                    return false;
                }
            }

            Harmony groupHarmony = new Harmony($"{PluginGuid}.{groupName}");
            try
            {
                foreach (PatchTarget target in targets)
                {
                    groupHarmony.PatchAll(target.PatchType);
                }

                featureHarmonies.Add(groupHarmony);
                return true;
            }
            catch (Exception exception)
            {
                groupHarmony.UnpatchSelf();
                Logger.LogError(
                    $"Failed to apply patch group '{groupName}'; all patches in " +
                    $"the group were rolled back: {exception}");
                return false;
            }
        }

        private void OnDestroy()
        {
            BoneIslandBfoPlacement.Shutdown();
            HugeOarDialogue.Shutdown();
            HugeOarPurchaseState.Reset();
            foreach (Harmony featureHarmony in featureHarmonies)
            {
                featureHarmony.UnpatchSelf();
            }

            featureHarmonies.Clear();
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
