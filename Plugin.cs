using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PracticalOar
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class PracticalOarPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "DogEggz.PracticalOar";
        public const string PluginName = "Practical Oar";
        public const string PluginVersion = "0.1.0";

        internal const float DefaultMaxBoatSpeed = 2.06f;
        internal const int DefaultRowForce = 34000;
        internal const int MinimumRowForce = 34000;
        internal const int MaximumRowForce = 102000;
        internal const int RowForceStep = 8500;
        internal const float NeedsCostPerSecond = 0.025f;

        private Harmony harmony;
        private ConfigEntry<float> maxBoatSpeed;
        private ConfigEntry<int> rowForce;
        private bool normalizingRowForce;

        internal static PracticalOarPlugin Instance { get; private set; }
        internal static ManualLogSource LogSource { get; private set; }

        internal float SelectedMaxBoatSpeed =>
            maxBoatSpeed?.Value ?? DefaultMaxBoatSpeed;

        internal float SelectedRowForce =>
            SnapRowForce(rowForce?.Value ?? DefaultRowForce);

        private void Awake()
        {
            Instance = this;
            LogSource = Logger;

            maxBoatSpeed = Config.Bind(
                "Rowing",
                "Maximum Rowing Speed",
                DefaultMaxBoatSpeed,
                new ConfigDescription(
                    "Boat speed where vanilla rowing force reaches zero. " +
                    "2.06 is about 4 knots, 2.57 about 5 knots, and 5.14 about 10 knots.",
                    new AcceptableValueList<float>(2.06f, 2.57f, 5.14f)));

            rowForce = Config.Bind(
                "Rowing",
                "Row Force",
                DefaultRowForce,
                new ConfigDescription(
                    "Vanilla oar force. The slider snaps from 34,000 (1x) to " +
                    "102,000 (3x) in steps of 8,500 (0.25x).",
                    new AcceptableValueRange<int>(MinimumRowForce, MaximumRowForce),
                    new ConfigurationManagerAttributes
                    {
                        CustomDrawer = DrawRowForce
                    }));

            NormalizeRowForceSetting();
            rowForce.SettingChanged += OnRowForceSettingChanged;

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(PracticalOarPlugin).Assembly);

            Logger.LogInfo(
                $"{PluginName} {PluginVersion} loaded. " +
                $"Max speed={SelectedMaxBoatSpeed:0.00}, " +
                $"force={SelectedRowForce:0}, " +
                $"water/food cost={NeedsCostPerSecond:0.000}/s.");
        }

        private void OnDestroy()
        {
            if (rowForce != null)
                rowForce.SettingChanged -= OnRowForceSettingChanged;

            harmony?.UnpatchSelf();

            if (Instance == this)
            {
                Instance = null;
                LogSource = null;
            }
        }

        private void OnRowForceSettingChanged(object sender, EventArgs args)
        {
            NormalizeRowForceSetting();
        }

        private void NormalizeRowForceSetting()
        {
            if (rowForce == null || normalizingRowForce)
                return;

            int snapped = SnapRowForce(rowForce.Value);
            if (snapped == rowForce.Value)
                return;

            normalizingRowForce = true;
            try
            {
                rowForce.Value = snapped;
            }
            finally
            {
                normalizingRowForce = false;
            }
        }

        internal static int SnapRowForce(int value)
        {
            int clamped = Math.Max(MinimumRowForce, Math.Min(MaximumRowForce, value));
            int steps = (int)Math.Round(
                (clamped - MinimumRowForce) / (double)RowForceStep,
                MidpointRounding.AwayFromZero);

            return MinimumRowForce + steps * RowForceStep;
        }

        private static void DrawRowForce(ConfigEntryBase setting)
        {
            int current = SnapRowForce(Convert.ToInt32(setting.BoxedValue));

            GUILayout.BeginHorizontal();
            float rawValue = GUILayout.HorizontalSlider(
                current,
                MinimumRowForce,
                MaximumRowForce);
            int snapped = SnapRowForce(Mathf.RoundToInt(rawValue));

            if (snapped != current)
                setting.BoxedValue = snapped;

            GUILayout.Label($"{snapped:N0} ({snapped / (float)DefaultRowForce:0.00}x)");
            GUILayout.EndHorizontal();
        }
    }

    internal static class BigOarFactory
    {
        internal const int VanillaOarPrefabIndex = 168;
        internal const int BigOarPrefabIndex = 823;
        internal const string BigOarName = "Big Oar";
        internal const float BigOarScale = 2f;
        internal const float BigOarWaterPosDistance = 1.5f;
        internal const float BigOarWaterPosHeight = 2.8f;
        internal const float BigOarRowDipHeight = 1f;

        internal static GameObject BigOarPrefab { get; private set; }

        internal static bool EnsureRegistered(PrefabsDirectory prefabs)
        {
            if (prefabs == null || prefabs.directory == null)
            {
                PracticalOarPlugin.LogSource?.LogError(
                    "Could not register Big Oar: PrefabsDirectory is unavailable.");
                return false;
            }

            if (prefabs.directory.Length > BigOarPrefabIndex &&
                prefabs.directory[BigOarPrefabIndex] != null)
            {
                GameObject occupied = prefabs.directory[BigOarPrefabIndex];
                if (occupied == BigOarPrefab ||
                    occupied.GetComponent<BigOarScaleController>() != null)
                {
                    BigOarPrefab = occupied;
                    return true;
                }

                PracticalOarPlugin.LogSource?.LogError(
                    $"Could not register Big Oar: prefab index {BigOarPrefabIndex} " +
                    $"is already occupied by '{occupied.name}'.");
                return false;
            }

            if (BigOarPrefab != null)
            {
                EnsureDirectoryCapacity(prefabs);
                prefabs.directory[BigOarPrefabIndex] = BigOarPrefab;
                return true;
            }

            if (prefabs.directory.Length <= VanillaOarPrefabIndex ||
                prefabs.directory[VanillaOarPrefabIndex] == null)
            {
                PracticalOarPlugin.LogSource?.LogError(
                    $"Could not register Big Oar: vanilla oar prefab " +
                    $"{VanillaOarPrefabIndex} was not found.");
                return false;
            }

            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(
                    prefabs.directory[VanillaOarPrefabIndex]);

                SaveablePrefab saveable = clone.GetComponent<SaveablePrefab>();
                ShipItemOar oar = clone.GetComponent<ShipItemOar>();
                if (saveable == null || oar == null)
                {
                    PracticalOarPlugin.LogSource?.LogError(
                        "Could not register Big Oar: the cloned vanilla prefab " +
                        "is missing SaveablePrefab or ShipItemOar.");
                    UnityEngine.Object.Destroy(clone);
                    return false;
                }

                clone.name = BigOarName;
                clone.transform.localScale = Vector3.one * BigOarScale;
                UnityEngine.Object.DontDestroyOnLoad(clone);

                saveable.prefabIndex = BigOarPrefabIndex;
                ((ShipItem)oar).name = BigOarName;
                oar.waterPosDistance = BigOarWaterPosDistance;
                oar.waterPosHeight = BigOarWaterPosHeight;
                oar.rowDipHeight = BigOarRowDipHeight;

                if (clone.GetComponent<BigOarScaleController>() == null)
                    clone.AddComponent<BigOarScaleController>();

                EnsureDirectoryCapacity(prefabs);

                prefabs.directory[BigOarPrefabIndex] = clone;
                BigOarPrefab = clone;

                PracticalOarPlugin.LogSource?.LogInfo(
                    $"Registered {BigOarName} at prefab index " +
                    $"{BigOarPrefabIndex}. Scale={BigOarScale:0.0}x, " +
                    $"water distance={BigOarWaterPosDistance:0.00}, " +
                    $"water height={BigOarWaterPosHeight:0.00}, " +
                    $"dip={BigOarRowDipHeight:0.00}.");
                return true;
            }
            catch (Exception exception)
            {
                if (clone != null)
                    UnityEngine.Object.Destroy(clone);

                PracticalOarPlugin.LogSource?.LogError(
                    $"Could not register Big Oar: {exception}");
                return false;
            }
        }

        private static void EnsureDirectoryCapacity(PrefabsDirectory prefabs)
        {
            if (prefabs.directory.Length <= BigOarPrefabIndex)
            {
                Array.Resize(
                    ref prefabs.directory,
                    BigOarPrefabIndex + 1);
            }
        }
    }

    [DefaultExecutionOrder(10000)]
    internal sealed class BigOarScaleController : MonoBehaviour
    {
        private static readonly Vector3 WorldScale =
            Vector3.one * BigOarFactory.BigOarScale;

        private ShipItem item;
        private ItemRigidbody itemRigidbody;

        private void Awake()
        {
            item = GetComponent<ShipItem>();
        }

        private void LateUpdate()
        {
            if (item == null)
                return;

            if (itemRigidbody == null)
                itemRigidbody = item.itemRigidbodyC;

            if (itemRigidbody != null &&
                itemRigidbody.GetCurrentInventorySlot() != null)
            {
                return;
            }

            ApplyScale(transform);

            if (itemRigidbody != null)
                ApplyScale(itemRigidbody.transform);
        }

        private static void ApplyScale(Transform target)
        {
            if (target.localScale != WorldScale)
                target.localScale = WorldScale;
        }
    }

    [HarmonyPatch(typeof(PrefabsDirectory), "Start")]
    internal static class BigOarPrefabRegistrationPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(PrefabsDirectory __instance)
        {
            BigOarFactory.EnsureRegistered(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipItemOar), nameof(ShipItemOar.OnAltHeld))]
    internal static class OarTuningPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ShipItemOar __instance)
        {
            PracticalOarPlugin plugin = PracticalOarPlugin.Instance;
            if (plugin == null)
                return;

            __instance.maxBoatSpeed = plugin.SelectedMaxBoatSpeed;
            __instance.rowForce = plugin.SelectedRowForce;
        }
    }

    [HarmonyPatch(typeof(ShipItemOar), nameof(ShipItemOar.ExtraLateUpdate))]
    internal static class OarNeedsCostPatch
    {
        private const float VanillaWaterCost = 0.4f;
        private const float VanillaFoodCost = 0.6f;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            FieldInfo waterField = AccessTools.Field(typeof(PlayerNeeds), nameof(PlayerNeeds.water));
            FieldInfo foodField = AccessTools.Field(typeof(PlayerNeeds), nameof(PlayerNeeds.food));

            List<int> waterCosts = FindNeedCostConstants(codes, waterField, VanillaWaterCost);
            List<int> foodCosts = FindNeedCostConstants(codes, foodField, VanillaFoodCost);

            if (waterCosts.Count == 1 && foodCosts.Count == 1)
            {
                codes[waterCosts[0]].operand = PracticalOarPlugin.NeedsCostPerSecond;
                codes[foodCosts[0]].operand = PracticalOarPlugin.NeedsCostPerSecond;

                PracticalOarPlugin.LogSource?.LogInfo(
                    "Patched rowing water and food costs to " +
                    $"{PracticalOarPlugin.NeedsCostPerSecond:0.000}/s.");
            }
            else
            {
                PracticalOarPlugin.LogSource?.LogError(
                    "Could not safely patch rowing needs costs: " +
                    $"water matches={waterCosts.Count}, food matches={foodCosts.Count}. " +
                    "Vanilla needs costs were left unchanged.");
            }

            return codes;
        }

        private static List<int> FindNeedCostConstants(
            IList<CodeInstruction> codes,
            FieldInfo needsField,
            float vanillaCost)
        {
            List<int> matches = new List<int>();

            for (int i = 0; i <= codes.Count - 6; i++)
            {
                if (codes[i].opcode != OpCodes.Ldsfld ||
                    !Equals(codes[i].operand, needsField) ||
                    codes[i + 2].opcode != OpCodes.Ldc_R4 ||
                    !(codes[i + 2].operand is float foundCost) ||
                    !Mathf.Approximately(foundCost, vanillaCost) ||
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
    }

    // Configuration Manager discovers this optional tag by name and reflection.
    // Keeping the type local means the mod does not require Configuration Manager.
    internal sealed class ConfigurationManagerAttributes
    {
        public Action<ConfigEntryBase> CustomDrawer;
    }
}
