using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace PowerfulOar
{
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
