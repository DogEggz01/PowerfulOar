using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PowerfulOar
{
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

    internal static class HookHangMoreCompatibility
    {
        internal const string PluginGuid = "com.raddude.hookshangmore";

        private const string AttachableItemTypeName = "HooksHangMore.AttachableItem";

        internal static bool IsExcluded(ShipItem item)
        {
            return OarStats.IsScaledOar(item);
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
}
