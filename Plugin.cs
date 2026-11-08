// PowerfulOar: the plugin (start-up and patching) and compatibility with other mods.
// (The user, 2026-11-08: "Plugin.cs: all other code"; the items and rowing are in Oars.cs, placement in Placement.cs.)
using System.Collections.Generic;
using System.Reflection;
using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using cakeslice;
using Object = UnityEngine.Object;

namespace PowerfulOar
{
	// ---- Plugin: start-up and patching ----

	[BepInPlugin(PowerfulOarPlugin.PluginGuid, PowerfulOarPlugin.PluginName, PowerfulOarPlugin.PluginVersion)]
	[BepInDependency("com.raddude.hookshangmore", BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency("com.raddude.radrefinements", BepInDependency.DependencyFlags.SoftDependency)]
	public sealed class PowerfulOarPlugin : BaseUnityPlugin
	{
		internal static PowerfulOarPlugin Instance { get; private set; }

		internal static ManualLogSource LogSource { get; private set; }

		private void Awake()
		{
			PowerfulOarPlugin.Instance = this;
			PowerfulOarPlugin.LogSource = base.Logger;
			this.harmony = new Harmony("DogEggz.PowerfulOar");
			RadRefinementCompatibility.Initialize(this.harmony);
			bool flag = this.ApplyPatchClass(typeof(OarStatsPatch), typeof(ShipItemOar), "OnAltHeld", null);
			bool flag2 = this.ApplyPatchClass(typeof(OarLateUpdatePatch), typeof(ShipItemOar), "ExtraLateUpdate", null);
			bool flag3 = this.ApplyPatchGroup("runtime_lifecycle", new PowerfulOarPlugin.PatchTarget[]
			{
				new PowerfulOarPlugin.PatchTarget(typeof(ScaledOarRigidbodyScalePatch), typeof(ItemRigidbody), "LateUpdate", null),
				new PowerfulOarPlugin.PatchTarget(typeof(ScaledOarSavedStatePatch), typeof(SaveablePrefab), "Load", new Type[]
				{
					typeof(SavePrefabData)
				}),
				new PowerfulOarPlugin.PatchTarget(typeof(ScaledOarInventoryEnterPatch), typeof(ItemRigidbody), "EnterInventorySlot", new Type[]
				{
					typeof(Transform)
				}),
				new PowerfulOarPlugin.PatchTarget(typeof(ScaledOarInventoryExitPatch), typeof(ItemRigidbody), "ExitInventorySlot", null)
			});
			bool flag4 = flag3 && this.ApplyPatchClass(typeof(ScaledOarPrefabRegistrationPatch), typeof(PrefabsDirectory), "Start", null);
			bool flag5 = this.ApplyPatchClass(typeof(ScaledOarOutlineEffectPatch), typeof(OutlineEffect), "OnEnable", null);
			bool flag6 = this.ApplyPatchClass(typeof(ReverseOarInputPatch), typeof(GoPointer), "LateUpdate", null);
			bool flag7 = this.ApplyPatchClass(typeof(ScaledOarNailingPatch), typeof(ShipItemHammer), "CanNail", new Type[]
			{
				typeof(ShipItem)
			});
			bool flag8 = flag4 && this.ApplyPatchGroup("vendor_display", new PowerfulOarPlugin.PatchTarget[]
			{
				new PowerfulOarPlugin.PatchTarget(typeof(BigOarVendorPatch), typeof(ShopItemSpawner), "Start", null),
				new PowerfulOarPlugin.PatchTarget(typeof(ScaledOarVendorSpawnPatch), typeof(ShopItemSpawner), "SpawnItem", null),
				new PowerfulOarPlugin.PatchTarget(typeof(HugeOarVendorPatch), typeof(Shopkeeper), "Start", null)
			});
			bool flag9 = flag8 && this.ApplyPatchGroup("huge_oar_dialogue", new PowerfulOarPlugin.PatchTarget[]
			{
				new PowerfulOarPlugin.PatchTarget(typeof(HugeOarDialogueCapturePatch), typeof(TavernRumorsDude), "Awake", null),
				new PowerfulOarPlugin.PatchTarget(typeof(HugeOarPurchasePatch), typeof(Shopkeeper), "SellItem", new Type[]
				{
					typeof(ShipItem),
					typeof(int),
					typeof(int)
				}),
				new PowerfulOarPlugin.PatchTarget(typeof(HugeOarPurchaseSessionPatch), typeof(SaveLoadManager), "Awake", null),
				new PowerfulOarPlugin.PatchTarget(typeof(HugeOarPurchaseLoadGamePatch), typeof(SaveLoadManager), "LoadGame", new Type[]
				{
					typeof(int)
				}),
				new PowerfulOarPlugin.PatchTarget(typeof(HugeOarPurchaseLoadPatch), typeof(SaveLoadManager), "LoadModData", null),
				new PowerfulOarPlugin.PatchTarget(typeof(HugeOarPurchaseStorePatch), typeof(SaveLoadManager), "SaveModData", null)
			});
			bool flag10 = this.ApplyPatchClass(typeof(HookHangMoreExclusionPatch), typeof(ShipItem), "Awake", null);
			bool flag11 = flag4 && this.ApplyPatchGroup("crate_blocker", new PowerfulOarPlugin.PatchTarget[]
			{
				new PowerfulOarPlugin.PatchTarget(typeof(ScaledOarCrateInsertPatch), typeof(CrateInventory), "InsertItem", new Type[]
				{
					typeof(ShipItem)
				}),
				new PowerfulOarPlugin.PatchTarget(typeof(ScaledOarCrateWithdrawPatch), typeof(CrateInventory), "WithdrawItem", new Type[]
				{
					typeof(ShipItem)
				}),
				new PowerfulOarPlugin.PatchTarget(typeof(ScaledOarCrateUiPatch), typeof(CrateInventoryButton), "OnActivate", new Type[]
				{
					typeof(GoPointer)
				})
			});
			if (flag4)
			{
				BoneIslandBfoPlacement.Initialize();
			}
			base.Logger.LogInfo(string.Concat(new string[]
			{
				"PowerfulOar " + PowerfulOarPlugin.PluginVersion + " loaded. Fixed per-oar stats and needs costs enabled. Hold Q to row opposite vanilla. Bone Island BFO placement, scaled-oar vendor displays, and Kicia Bay Huge Oar enabled. ",
				string.Format("Patches: stats={0}, late update={1}, ", flag, flag2),
				string.Format("prefab={0}, outline wake fix={1}, ", flag4, flag5),
				string.Format("reverse input={0}, nailing={1}, ", flag6, flag7),
				string.Format("vendor display group={0}, ", flag8),
				string.Format("Huge Oar dialogue group={0}, ", flag9),
				string.Format("HookHangMore exclusion={0}, ", flag10),
				string.Format("scaled-oar lifecycle group={0}, ", flag3),
				string.Format("crate blocker group={0}, ", flag11),
				string.Format("RadRefinement needs={0}, ", RadRefinementCompatibility.NeedsReductionEnabled),
				string.Format("continual row={0}.", RadRefinementCompatibility.ContinualRowAvailable)
			}));
		}

		private bool ApplyPatchClass(Type patchType, Type targetType, string targetMethodName, Type[] argumentTypes = null)
		{
			if (AccessTools.DeclaredMethod(targetType, targetMethodName, argumentTypes ?? Type.EmptyTypes, null) == null)
			{
				base.Logger.LogError(string.Concat(new string[]
				{
					"Skipped ",
					patchType.Name,
					": target method ",
					targetType.FullName,
					".",
					targetMethodName,
					" was not found."
				}));
				return false;
			}
			bool result;
			try
			{
				this.harmony.PatchAll(patchType);
				result = true;
			}
			catch (Exception arg)
			{
				base.Logger.LogError("Failed to apply " + patchType.Name + " to " + string.Format("{0}.{1}: {2}", targetType.FullName, targetMethodName, arg));
				result = false;
			}
			return result;
		}

		private bool ApplyPatchGroup(string groupName, params PowerfulOarPlugin.PatchTarget[] targets)
		{
			foreach (PowerfulOarPlugin.PatchTarget patchTarget in targets)
			{
				if (AccessTools.DeclaredMethod(patchTarget.TargetType, patchTarget.TargetMethodName, patchTarget.ArgumentTypes ?? Type.EmptyTypes, null) == null)
				{
					base.Logger.LogError(string.Concat(new string[]
					{
						"Skipped patch group '",
						groupName,
						"': target method ",
						patchTarget.TargetType.FullName,
						".",
						patchTarget.TargetMethodName,
						" was not found."
					}));
					return false;
				}
			}
			Harmony harmony = new Harmony("DogEggz.PowerfulOar." + groupName);
			bool result;
			try
			{
				foreach (PowerfulOarPlugin.PatchTarget patchTarget2 in targets)
				{
					harmony.PatchAll(patchTarget2.PatchType);
				}
				this.featureHarmonies.Add(harmony);
				result = true;
			}
			catch (Exception arg)
			{
				harmony.UnpatchSelf();
				base.Logger.LogError("Failed to apply patch group '" + groupName + "'; all patches in " + string.Format("the group were rolled back: {0}", arg));
				result = false;
			}
			return result;
		}

		private void OnDestroy()
		{
			BoneIslandBfoPlacement.Shutdown();
			HugeOarDialogue.Shutdown();
			HugeOarPurchaseState.Reset();
			foreach (Harmony harmony in this.featureHarmonies)
			{
				harmony.UnpatchSelf();
			}
			this.featureHarmonies.Clear();
			Harmony harmony2 = this.harmony;
			if (harmony2 != null)
			{
				harmony2.UnpatchSelf();
			}
			if (PowerfulOarPlugin.Instance == this)
			{
				PowerfulOarPlugin.Instance = null;
				PowerfulOarPlugin.LogSource = null;
			}
		}

		public const string PluginGuid = "DogEggz.PowerfulOar";

		public const string PluginName = "PowerfulOar";

		public const string PluginVersion = "1.1.2";

		internal const string GoPointerInputLoopMethod = "LateUpdate";

		private Harmony harmony;

		private readonly List<Harmony> featureHarmonies = new List<Harmony>();

		private sealed class PatchTarget
		{
			internal PatchTarget(Type patchType, Type targetType, string targetMethodName, Type[] argumentTypes = null)
			{
				this.PatchType = patchType;
				this.TargetType = targetType;
				this.TargetMethodName = targetMethodName;
				this.ArgumentTypes = argumentTypes;
			}

			internal Type PatchType { get; }

			internal Type TargetType { get; }

			internal string TargetMethodName { get; }

			internal Type[] ArgumentTypes { get; }
		}
	}

	// ---- Compatibility with other mods ----

	internal static class RadRefinementCompatibility
	{
		internal static bool NeedsReductionEnabled
		{
			get
			{
				return RadRefinementCompatibility.needsReductionEnabled;
			}
		}

		internal static bool ContinualRowAvailable
		{
			get
			{
				return RadRefinementCompatibility.continualRowAvailable;
			}
		}

		internal static float NeedsCostMultiplier
		{
			get
			{
				if (!RadRefinementCompatibility.needsReductionEnabled || RadRefinementCompatibility.rowingNeedsReduction == null)
				{
					return 1f;
				}
				return 1f - Mathf.Clamp01(RadRefinementCompatibility.rowingNeedsReduction.Value);
			}
		}

		internal static bool ContinualRowEnabled
		{
			get
			{
				return RadRefinementCompatibility.continualRowAvailable && RadRefinementCompatibility.continualRow != null && RadRefinementCompatibility.continualRow.Value;
			}
		}

		internal static void Initialize(Harmony harmony)
		{
			RadRefinementCompatibility.rowingNeedsReduction = null;
			RadRefinementCompatibility.continualRow = null;
			RadRefinementCompatibility.needsReductionEnabled = false;
			RadRefinementCompatibility.continualRowAvailable = false;
			Type type = AccessTools.TypeByName("RadRefinements.Configs");
			if (type == null)
			{
				return;
			}
			FieldInfo fieldInfo = AccessTools.Field(type, "rowingNeedsMult");
			RadRefinementCompatibility.rowingNeedsReduction = (((fieldInfo != null) ? fieldInfo.GetValue(null) : null) as ConfigEntry<float>);
			FieldInfo fieldInfo2 = AccessTools.Field(type, "continualRow");
			RadRefinementCompatibility.continualRow = (((fieldInfo2 != null) ? fieldInfo2.GetValue(null) : null) as ConfigEntry<bool>);
			MethodBase methodBase = AccessTools.DeclaredMethod(typeof(ShipItemOar), "ExtraLateUpdate", null, null);
			MethodBase methodBase2 = AccessTools.DeclaredMethod(typeof(ShipItemOar), "OnAltHeld", null, null);
			Patches patches = (methodBase != null) ? Harmony.GetPatchInfo(methodBase) : null;
			Patches patches2 = (methodBase2 != null) ? Harmony.GetPatchInfo(methodBase2) : null;
			MethodInfo methodInfo = RadRefinementCompatibility.FindOwnedPatch((patches != null) ? patches.Prefixes : null, new string[]
			{
				"ReduceNeedsPrefix"
			});
			MethodInfo methodInfo2 = RadRefinementCompatibility.FindOwnedPatch((patches != null) ? patches.Postfixes : null, new string[]
			{
				"ReduceNeedsPostfix"
			});
			MethodInfo patchMethod = RadRefinementCompatibility.FindOwnedPatch((patches2 != null) ? patches2.Postfixes : null, new string[]
			{
				"ContinualRow",
				"Postfix"
			});
			RadRefinementCompatibility.continualRowAvailable = (RadRefinementCompatibility.continualRow != null && RadRefinementCompatibility.ContainsPatch(methodBase2, patchMethod));
			if (harmony == null || RadRefinementCompatibility.rowingNeedsReduction == null || !RadRefinementCompatibility.ContainsPatch(methodBase, methodInfo) || !RadRefinementCompatibility.ContainsPatch(methodBase, methodInfo2))
			{
				ManualLogSource logSource = PowerfulOarPlugin.LogSource;
				if (logSource != null)
				{
					logSource.LogWarning("RadRefinement was detected, but its rowing-needs patches could not be safely replaced. Set RadRefinement's Rowing needs reduction to 0 to avoid incorrect needs costs.");
				}
				RadRefinementCompatibility.LogContinualRowStatus();
				return;
			}
			try
			{
				harmony.Unpatch(methodBase, methodInfo);
				harmony.Unpatch(methodBase, methodInfo2);
				RadRefinementCompatibility.needsReductionEnabled = (!RadRefinementCompatibility.ContainsPatch(methodBase, methodInfo) && !RadRefinementCompatibility.ContainsPatch(methodBase, methodInfo2));
				if (RadRefinementCompatibility.needsReductionEnabled)
				{
					ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
					if (logSource2 != null)
					{
						logSource2.LogInfo("RadRefinement rowing-needs compatibility enabled: its reduction slider now scales PowerfulOar baselines.");
					}
				}
				else
				{
					ManualLogSource logSource3 = PowerfulOarPlugin.LogSource;
					if (logSource3 != null)
					{
						logSource3.LogError("RadRefinement rowing-needs patches remained installed; slider compatibility was disabled.");
					}
				}
			}
			catch (Exception ex)
			{
				RadRefinementCompatibility.needsReductionEnabled = false;
				ManualLogSource logSource4 = PowerfulOarPlugin.LogSource;
				if (logSource4 != null)
				{
					string str = "Could not install RadRefinement rowing-needs compatibility: ";
					Exception ex2 = ex;
					logSource4.LogError(str + ((ex2 != null) ? ex2.ToString() : null));
				}
			}
			RadRefinementCompatibility.LogContinualRowStatus();
		}

		private static void LogContinualRowStatus()
		{
			if (!RadRefinementCompatibility.continualRowAvailable)
			{
				if (RadRefinementCompatibility.continualRow != null)
				{
					ManualLogSource logSource = PowerfulOarPlugin.LogSource;
					if (logSource == null)
					{
						return;
					}
					logSource.LogWarning("RadRefinement's continual-row patch could not be safely identified. Disable its Row continually option to avoid incorrect rowing force.");
				}
				return;
			}
			ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
			if (logSource2 == null)
			{
				return;
			}
			logSource2.LogInfo("RadRefinement continual-row force compatibility enabled.");
		}

		private static MethodInfo FindOwnedPatch(IEnumerable<Patch> patches, params string[] acceptedMethodNames)
		{
			if (patches == null)
			{
				return null;
			}
			MethodInfo methodInfo = null;
			foreach (Patch patch in patches)
			{
				MethodInfo patchMethod = patch.PatchMethod;
				if (!(patch.owner != "com.raddude.radrefinements") && !(patchMethod == null) && RadRefinementCompatibility.HasAcceptedName(patchMethod, acceptedMethodNames))
				{
					if (methodInfo != null && !object.Equals(methodInfo, patchMethod))
					{
						ManualLogSource logSource = PowerfulOarPlugin.LogSource;
						if (logSource != null)
						{
							logSource.LogWarning("Found multiple RadRefinement rowing patches where exactly one was expected; compatibility was disabled.");
						}
						return null;
					}
					methodInfo = patchMethod;
				}
			}
			return methodInfo;
		}

		private static bool HasAcceptedName(MethodInfo method, IEnumerable<string> acceptedMethodNames)
		{
			foreach (string b in acceptedMethodNames)
			{
				if (string.Equals(method.Name, b, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private static bool ContainsPatch(MethodBase target, MethodInfo patchMethod)
		{
			if (target == null || patchMethod == null)
			{
				return false;
			}
			Patches patchInfo = Harmony.GetPatchInfo(target);
			return patchInfo != null && (RadRefinementCompatibility.ContainsPatch(patchInfo.Prefixes, patchMethod) || RadRefinementCompatibility.ContainsPatch(patchInfo.Postfixes, patchMethod) || RadRefinementCompatibility.ContainsPatch(patchInfo.Transpilers, patchMethod) || RadRefinementCompatibility.ContainsPatch(patchInfo.Finalizers, patchMethod));
		}

		private static bool ContainsPatch(IEnumerable<Patch> patches, MethodInfo patchMethod)
		{
			using (IEnumerator<Patch> enumerator = patches.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (enumerator.Current.PatchMethod == patchMethod)
					{
						return true;
					}
				}
			}
			return false;
		}

		internal const string PluginGuid = "com.raddude.radrefinements";

		private const string ConfigsTypeName = "RadRefinements.Configs";

		private static ConfigEntry<float> rowingNeedsReduction;

		private static ConfigEntry<bool> continualRow;

		private static bool needsReductionEnabled;

		private static bool continualRowAvailable;
	}

	internal static class HookHangMoreCompatibility
	{
		internal static bool IsExcluded(ShipItem item)
		{
			return OarStats.IsScaledOar(item);
		}

		internal static void Exclude(ShipItem item)
		{
			if (!HookHangMoreCompatibility.IsExcluded(item))
			{
				return;
			}
			HangableItem component = item.GetComponent<HangableItem>();
			if (component != null && component.enabled)
			{
				component.DisconnectJoint();
				component.enabled = false;
				ManualLogSource logSource = PowerfulOarPlugin.LogSource;
				if (logSource != null)
				{
					logSource.LogInfo("Disabled vanilla hook hanging for " + item.name + ".");
				}
			}
			Type type = HookHangMoreCompatibility.AttachableItemType;
			Component component2 = (type != null) ? item.GetComponent(type) : null;
			if (component2 == null)
			{
				return;
			}
			Behaviour behaviour = component2 as Behaviour;
			// A component already disabled is usually this one waiting for its Destroy at the end of the frame (a scaled oar's
			// ShipItem.Awake postfix, its ScaledOarController.Awake and Initialize all call Exclude in one frame): destroy it again
			// as before, but log it once.
			bool firstTime = behaviour == null || behaviour.enabled;
			if (behaviour != null)
			{
				behaviour.enabled = false;
			}
			Object.Destroy(component2);
			if (!firstTime)
			{
				return;
			}
			ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
			if (logSource2 == null)
			{
				return;
			}
			logSource2.LogInfo("Removed HooksHangMore attachment support from " + item.name + ".");
		}

		// Looked up once: AccessTools.TypeByName searches every type of every loaded assembly (measured 2026-11-08 with the
		// game's and the workspace mod list's 158 assemblies: 7 ms a call when found, 32 ms and a HarmonyX warning when not),
		// and Exclude runs several times for each scaled oar spawned or each template refreshed. The first call comes from
		// PrefabsDirectory.Start in the game scene, after every plugin's assembly has loaded.
		private static Type AttachableItemType
		{
			get
			{
				if (!HookHangMoreCompatibility.attachableItemTypeResolved)
				{
					HookHangMoreCompatibility.attachableItemType = AccessTools.TypeByName(HookHangMoreCompatibility.AttachableItemTypeName);
					HookHangMoreCompatibility.attachableItemTypeResolved = true;
				}
				return HookHangMoreCompatibility.attachableItemType;
			}
		}

		internal const string PluginGuid = "com.raddude.hookshangmore";

		private const string AttachableItemTypeName = "HooksHangMore.AttachableItem";

		private static Type attachableItemType;

		private static bool attachableItemTypeResolved;
	}

	[HarmonyPatch(typeof(ShipItem), "Awake")]
	[HarmonyAfter(new string[]
	{
		"com.raddude.hookshangmore"
	})]
	internal static class HookHangMoreExclusionPatch
	{
		[HarmonyPostfix]
		[HarmonyPriority(0)]
		private static void Postfix(ShipItem __instance)
		{
			HookHangMoreCompatibility.Exclude(__instance);
		}
	}
}
