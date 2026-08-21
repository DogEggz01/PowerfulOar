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
        public const string PluginVersion = "1.0.0";

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
