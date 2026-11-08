// PowerfulOar: the scaled oar items (definitions, templates, per-oar stats, behaviour, item patches) and how an oar
// behaves when rowed (force, needs, stroke, reverse input).
// (The user, 2026-11-08: "Oars.cs: include newly added item, how oar behave"; placement is in Placement.cs, the rest in
// Plugin.cs.)
using System.Collections.Generic;
using System.Collections;
using System.Reflection.Emit;
using System.Reflection;
using System;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using cakeslice;
using Object = UnityEngine.Object;

namespace PowerfulOar
{
	// ---- Scaled oar items: definitions, templates, per-oar stats, behaviour ----

	internal sealed class ScaledOarDefinition
	{
		internal ScaledOarDefinition(int prefabIndex, string itemName, float scale, float waterPosDistance, float waterPosHeight, float rowDipHeight, float massMultiplier, int valueMultiplier, int fixedValue = 0)
		{
			this.PrefabIndex = prefabIndex;
			this.ItemName = itemName;
			this.Scale = scale;
			this.WaterPosDistance = waterPosDistance;
			this.WaterPosHeight = waterPosHeight;
			this.RowDipHeight = rowDipHeight;
			this.MassMultiplier = massMultiplier;
			this.ValueMultiplier = valueMultiplier;
			this.FixedValue = fixedValue;
		}

		// The item's value when set (above 0), in place of the game's oar's value times ValueMultiplier.
		internal int FixedValue { get; }

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
		internal static bool EnsureRegistered(PrefabsDirectory directory)
		{
			if (directory == null || directory.directory == null)
			{
				ManualLogSource logSource = PowerfulOarPlugin.LogSource;
				if (logSource != null)
				{
					logSource.LogError("Could not register scaled oars: PrefabsDirectory is unavailable.");
				}
				return false;
			}
			if (directory.directory.Length <= 168 || directory.directory[168] == null)
			{
				ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
				if (logSource2 != null)
				{
					logSource2.LogError("Could not register scaled oars: vanilla oar prefab " + string.Format("{0} was not found.", 168));
				}
				return false;
			}
			ShipItemOar component = directory.directory[168].GetComponent<ShipItemOar>();
			if (component == null)
			{
				ManualLogSource logSource3 = PowerfulOarPlugin.LogSource;
				if (logSource3 != null)
				{
					logSource3.LogError("Could not register scaled oars: prefab 168 is not a ShipItemOar.");
				}
				return false;
			}
			OarStats.Apply(component);
			ScaledOarFactory.EnsureDirectoryCapacity(directory);
			bool flag = true;
			foreach (ScaledOarDefinition definition in ScaledOarFactory.Definitions)
			{
				flag &= ScaledOarFactory.EnsureRegistered(directory, definition, component);
			}
			return flag & ScaledOarFactory.EnsureLegacyBigOarRegistered(directory, component);
		}

		internal static bool ValidateRuntimeCache(PrefabsDirectory directory)
		{
			if (!ScaledOarFactory.EnsureRegistered(directory))
			{
				return false;
			}
			ScaledOarFactory.EnsureDirectoryCapacity(directory);
			ScaledOarFactory.EnsureShipItemCacheCapacity(directory);
			bool result = true;
			foreach (ScaledOarDefinition scaledOarDefinition in ScaledOarFactory.Definitions)
			{
				GameObject gameObject = directory.directory[scaledOarDefinition.PrefabIndex];
				ShipItem shipItem = (gameObject != null) ? gameObject.GetComponent<ShipItem>() : null;
				SaveablePrefab saveablePrefab = (gameObject != null) ? gameObject.GetComponent<SaveablePrefab>() : null;
				ScaledOarController scaledOarController = (gameObject != null) ? gameObject.GetComponent<ScaledOarController>() : null;
				if (shipItem == null || saveablePrefab == null || scaledOarController == null || saveablePrefab.prefabIndex != scaledOarDefinition.PrefabIndex || scaledOarController.PrefabIndex != scaledOarDefinition.PrefabIndex)
				{
					ManualLogSource logSource = PowerfulOarPlugin.LogSource;
					if (logSource != null)
					{
						logSource.LogError(scaledOarDefinition.ItemName + " registration failed validation: directory entry is missing ShipItem, ScaledOarController, or the expected prefab index.");
					}
					result = false;
				}
				else
				{
					directory.shipItems[scaledOarDefinition.PrefabIndex] = shipItem;
					if (!scaledOarDefinition.CacheValidationLogged)
					{
						scaledOarDefinition.CacheValidationLogged = true;
						ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
						if (logSource2 != null)
						{
							logSource2.LogInfo("Validated " + scaledOarDefinition.ItemName + " prefab and item cache at index " + string.Format("{0}.", scaledOarDefinition.PrefabIndex));
						}
					}
				}
			}
			GameObject gameObject2 = directory.directory[169];
			ShipItem shipItem2 = (gameObject2 != null) ? gameObject2.GetComponent<ShipItem>() : null;
			SaveablePrefab saveablePrefab2 = (gameObject2 != null) ? gameObject2.GetComponent<SaveablePrefab>() : null;
			ScaledOarController scaledOarController2 = (gameObject2 != null) ? gameObject2.GetComponent<ScaledOarController>() : null;
			if (shipItem2 == null || saveablePrefab2 == null || scaledOarController2 == null || saveablePrefab2.prefabIndex != 169 || scaledOarController2.PrefabIndex != 169)
			{
				ManualLogSource logSource3 = PowerfulOarPlugin.LogSource;
				if (logSource3 != null)
				{
					logSource3.LogError("Legacy Big Oar compatibility failed validation at prefab index " + string.Format("{0}.", 169));
				}
				result = false;
			}
			else
			{
				directory.shipItems[169] = shipItem2;
			}
			return result;
		}

		internal static bool MigrateLegacyBigOar(SaveablePrefab saveable, SavePrefabData data)
		{
			if (saveable == null || data == null || data.prefabIndex != 169)
			{
				return false;
			}
			ScaledOarController component = saveable.GetComponent<ScaledOarController>();
			ShipItemOar component2 = saveable.GetComponent<ShipItemOar>();
			if (component == null || component2 == null || saveable.prefabIndex != 169)
			{
				return false;
			}
			saveable.prefabIndex = 602;
			component.Initialize(602, 2f);
			OarStats.Apply(component2);
			ManualLogSource logSource = PowerfulOarPlugin.LogSource;
			if (logSource != null)
			{
				logSource.LogInfo("Migrated saved Big Oar from legacy prefab " + string.Format("{0} to {1}; ", 169, 602) + "the new index will be written on the next save.");
			}
			return true;
		}

		private static bool EnsureLegacyBigOarRegistered(PrefabsDirectory directory, ShipItemOar vanillaOar)
		{
			GameObject gameObject = directory.directory[169];
			if (gameObject != null)
			{
				ScaledOarController component = gameObject.GetComponent<ScaledOarController>();
				SaveablePrefab component2 = gameObject.GetComponent<SaveablePrefab>();
				ShipItemOar component3 = gameObject.GetComponent<ShipItemOar>();
				if (component != null && component2 != null && component3 != null && component.PrefabIndex == 169 && component2.prefabIndex == 169)
				{
					ScaledOarFactory.ConfigureScaledOar(gameObject, component3, vanillaOar, ScaledOarFactory.LegacyBigOarDefinition);
					component.Initialize(169, 2f);
					return true;
				}
				ManualLogSource logSource = PowerfulOarPlugin.LogSource;
				if (logSource != null)
				{
					logSource.LogError(string.Concat(new string[]
					{
						"Could not reserve legacy Big Oar prefab index ",
						string.Format("{0}: it is already occupied by ", 169),
						"'",
						gameObject.name,
						"'. Old PowerfulOar saves using that index cannot be migrated safely."
					}));
				}
				return false;
			}
			else
			{
				GameObject gameObject2 = directory.directory[602];
				if (((gameObject2 != null) ? gameObject2.GetComponent<ShipItemOar>() : null) == null)
				{
					ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
					if (logSource2 != null)
					{
						logSource2.LogError("Could not create legacy Big Oar compatibility prefab: " + string.Format("current prefab {0} is unavailable.", 602));
					}
					return false;
				}
				GameObject gameObject3 = null;
				bool result;
				try
				{
					gameObject3 = Object.Instantiate<GameObject>(gameObject2, ScaledOarFactory.TemplateHolder(), false);
					ShipItemOar component4 = gameObject3.GetComponent<ShipItemOar>();
					ScaledOarController component5 = gameObject3.GetComponent<ScaledOarController>();
					if (component4 == null || component5 == null)
					{
						ManualLogSource logSource3 = PowerfulOarPlugin.LogSource;
						if (logSource3 != null)
						{
							logSource3.LogError("Could not create legacy Big Oar compatibility prefab: required components are missing.");
						}
						Object.Destroy(gameObject3);
						result = false;
					}
					else
					{
						ScaledOarFactory.ConfigureScaledOar(gameObject3, component4, vanillaOar, ScaledOarFactory.LegacyBigOarDefinition);
						component5.Initialize(169, 2f);
						directory.directory[169] = gameObject3;
						ManualLogSource logSource4 = PowerfulOarPlugin.LogSource;
						if (logSource4 != null)
						{
							logSource4.LogInfo(string.Format("Reserved prefab {0} for legacy ", 169) + string.Format("Big Oar saves; new Big Oars use prefab {0}.", 602));
						}
						result = true;
					}
				}
				catch (Exception ex)
				{
					if (gameObject3 != null)
					{
						Object.Destroy(gameObject3);
					}
					ManualLogSource logSource5 = PowerfulOarPlugin.LogSource;
					if (logSource5 != null)
					{
						string str = "Could not create legacy Big Oar compatibility prefab: ";
						Exception ex2 = ex;
						logSource5.LogError(str + ((ex2 != null) ? ex2.ToString() : null));
					}
					result = false;
				}
				return result;
			}
		}

		private static bool EnsureRegistered(PrefabsDirectory directory, ScaledOarDefinition definition, ShipItemOar vanillaOar)
		{
			GameObject gameObject = directory.directory[definition.PrefabIndex];
			if (gameObject != null)
			{
				ScaledOarController scaledOarController = gameObject.GetComponent<ScaledOarController>();
				if (!(gameObject == definition.Prefab) && (!(scaledOarController != null) || scaledOarController.PrefabIndex != definition.PrefabIndex))
				{
					ManualLogSource logSource = PowerfulOarPlugin.LogSource;
					if (logSource != null)
					{
						logSource.LogError("Could not register " + definition.ItemName + ": prefab index " + string.Format("{0} is already occupied by '{1}'.", definition.PrefabIndex, gameObject.name));
					}
					return false;
				}
				ShipItemOar component = gameObject.GetComponent<ShipItemOar>();
				if (component == null)
				{
					ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
					if (logSource2 != null)
					{
						logSource2.LogError("Could not configure " + definition.ItemName + ": ShipItemOar is missing.");
					}
					return false;
				}
				if (scaledOarController == null)
				{
					scaledOarController = gameObject.AddComponent<ScaledOarController>();
				}
				ScaledOarFactory.ConfigureScaledOar(gameObject, component, vanillaOar, definition);
				definition.Prefab = gameObject;
				scaledOarController.Initialize(definition.PrefabIndex, definition.Scale);
				return true;
			}
			else
			{
				if (!(definition.Prefab != null))
				{
					GameObject gameObject2 = null;
					bool result;
					try
					{
						gameObject2 = Object.Instantiate<GameObject>(directory.directory[168], ScaledOarFactory.TemplateHolder(), false);
						Object component2 = gameObject2.GetComponent<SaveablePrefab>();
						ShipItemOar component3 = gameObject2.GetComponent<ShipItemOar>();
						if (component2 == null || component3 == null)
						{
							ManualLogSource logSource3 = PowerfulOarPlugin.LogSource;
							if (logSource3 != null)
							{
								logSource3.LogError("Could not register " + definition.ItemName + ": the cloned vanilla prefab is missing SaveablePrefab or ShipItemOar.");
							}
							Object.Destroy(gameObject2);
							result = false;
						}
						else
						{
							ScaledOarFactory.ConfigureScaledOar(gameObject2, component3, vanillaOar, definition);
							ScaledOarController scaledOarController2 = gameObject2.GetComponent<ScaledOarController>();
							if (scaledOarController2 == null)
							{
								scaledOarController2 = gameObject2.AddComponent<ScaledOarController>();
							}
							scaledOarController2.Initialize(definition.PrefabIndex, definition.Scale);
							directory.directory[definition.PrefabIndex] = gameObject2;
							definition.Prefab = gameObject2;
							ManualLogSource logSource4 = PowerfulOarPlugin.LogSource;
							if (logSource4 != null)
							{
								logSource4.LogInfo(string.Concat(new string[]
								{
									"Registered ",
									definition.ItemName,
									" at prefab index ",
									string.Format("{0}. Scale={1:0.0}x, ", definition.PrefabIndex, definition.Scale),
									string.Format("mass={0:0.0}, value={1}, ", component3.mass, component3.value),
									string.Format("water distance={0:0.00}, ", definition.WaterPosDistance),
									string.Format("water height={0:0.00}, ", definition.WaterPosHeight),
									string.Format("dip={0:0.00}, big=false.", definition.RowDipHeight)
								}));
							}
							result = true;
						}
					}
					catch (Exception arg)
					{
						if (gameObject2 != null)
						{
							Object.Destroy(gameObject2);
						}
						ManualLogSource logSource5 = PowerfulOarPlugin.LogSource;
						if (logSource5 != null)
						{
							logSource5.LogError(string.Format("Could not register {0}: {1}", definition.ItemName, arg));
						}
						result = false;
					}
					return result;
				}
				ShipItemOar component4 = definition.Prefab.GetComponent<ShipItemOar>();
				ScaledOarController component5 = definition.Prefab.GetComponent<ScaledOarController>();
				if (component4 == null || component5 == null)
				{
					ManualLogSource logSource6 = PowerfulOarPlugin.LogSource;
					if (logSource6 != null)
					{
						logSource6.LogError("Could not restore " + definition.ItemName + ": cached components are missing.");
					}
					return false;
				}
				ScaledOarFactory.ConfigureScaledOar(definition.Prefab, component4, vanillaOar, definition);
				component5.Initialize(definition.PrefabIndex, definition.Scale);
				directory.directory[definition.PrefabIndex] = definition.Prefab;
				return true;
			}
		}

		private static void ConfigureScaledOar(GameObject prefab, ShipItemOar oar, ShipItemOar vanillaOar, ScaledOarDefinition definition)
		{
			SaveablePrefab component = prefab.GetComponent<SaveablePrefab>();
			if (component == null)
			{
				throw new InvalidOperationException(definition.ItemName + " is missing SaveablePrefab.");
			}
			ScaledOarFactory.RemoveTemplateLodGroup(prefab);
			prefab.name = definition.ItemName;
			prefab.transform.localScale = Vector3.one * definition.Scale;
			if (prefab.transform.parent == null)
			{
				Object.DontDestroyOnLoad(prefab);
			}
			component.prefabIndex = definition.PrefabIndex;
			oar.name = definition.ItemName;
			oar.big = false;
			oar.wallAttachment = false;
			oar.mass = vanillaOar.mass * definition.MassMultiplier;
			oar.value = (definition.FixedValue > 0) ? definition.FixedValue : (vanillaOar.value * definition.ValueMultiplier);
			oar.waterPosDistance = definition.WaterPosDistance;
			oar.waterPosHeight = definition.WaterPosHeight;
			oar.rowDipHeight = definition.RowDipHeight;
			OarStats.Apply(oar);
			HookHangMoreCompatibility.Exclude(oar);
		}

		// The templates are made under this inactive object, so they never wake (no ShipItem.Awake: no physics copy, no
		// LODGroup, no updates), like the game's own prefab assets in the PrefabsDirectory. A copy of a template is made at the
		// scene's root, active, and wakes the normal way. (Until the 2026-11-08 review the templates were live items kept
		// 38.5 km off a new game's start.)
		internal static Transform TemplateHolder()
		{
			if (ScaledOarFactory.templateHolder == null)
			{
				GameObject gameObject = new GameObject("PowerfulOar templates");
				gameObject.SetActive(false);
				Object.DontDestroyOnLoad(gameObject);
				ScaledOarFactory.templateHolder = gameObject.transform;
			}
			return ScaledOarFactory.templateHolder;
		}

		// A template that has woken (a live object put in the directory, as all templates were before the inactive holder) has a
		// LODGroup from the game's ShipItem.AddLODGroup. Instantiate would copy it into every oar spawned from the template; the
		// spawned oar's own ShipItem.AddLODGroup then gets null from AddComponent<LODGroup>() and throws, which ends its
		// ShipItem.LoadAfterDelay early: an oar saved in a crate is then never put back in it. A dormant template has none, so
		// this is a guard. DestroyImmediate, not Destroy: Destroy waits for the end of the frame, and the legacy prefab 169 is
		// copied from the 602 template in the same frame.
		private static void RemoveTemplateLodGroup(GameObject prefab)
		{
			LODGroup component = prefab.GetComponent<LODGroup>();
			if (component != null)
			{
				Object.DestroyImmediate(component);
			}
		}

		private static void EnsureDirectoryCapacity(PrefabsDirectory directory)
		{
			if (directory.directory.Length <= 666)
			{
				Array.Resize<GameObject>(ref directory.directory, 667);
			}
		}

		private static void EnsureShipItemCacheCapacity(PrefabsDirectory directory)
		{
			if (directory.shipItems == null)
			{
				directory.shipItems = new ShipItem[directory.directory.Length];
				for (int i = 0; i < directory.directory.Length; i++)
				{
					GameObject gameObject = directory.directory[i];
					if (gameObject != null)
					{
						directory.shipItems[i] = gameObject.GetComponent<ShipItem>();
					}
				}
				return;
			}
			if (directory.shipItems.Length < directory.directory.Length)
			{
				Array.Resize<ShipItem>(ref directory.shipItems, directory.directory.Length);
			}
		}

		internal const int VanillaOarPrefabIndex = 168;

		internal const int LegacyBigOarPrefabIndex = 169;

		internal const int BigOarPrefabIndex = 602;

		internal const int HugeOarPrefabIndex = 603;

		internal const int Bfo5000PrefabIndex = 666;

		internal const float BigOarScale = 2f;

		internal const float HugeOarScale = 3f;

		internal const float Bfo5000Scale = 5f;

		private static readonly ScaledOarDefinition[] Definitions = new ScaledOarDefinition[]
		{
			new ScaledOarDefinition(602, "Big Oar", 2f, 1.5f, 2.8f, 1f, 8f, 4),
			new ScaledOarDefinition(603, "Huge Oar", 3f, 2.25f, 4.2f, 1.5f, 27f, 12),
			// Value 2000: a shop pays half the value of an owned oar (Shopkeeper.GetPrice), so it sells for 1000 (the user, 2026-11-08:
			// "make it sell 1000").
			new ScaledOarDefinition(666, "BFO 5000", 5f, 3.75f, 7f, 2.5f, 125f, 10, 2000)
		};

		private static readonly ScaledOarDefinition LegacyBigOarDefinition = new ScaledOarDefinition(169, "Big Oar", 2f, 1.5f, 2.8f, 1f, 8f, 4);

		private static Transform templateHolder;
	}

	[DefaultExecutionOrder(10000)]
	internal sealed class ScaledOarController : MonoBehaviour
	{
		internal int PrefabIndex
		{
			get
			{
				return this.prefabIndex;
			}
		}

		internal void Initialize(int newPrefabIndex, float newTargetScale)
		{
			this.prefabIndex = newPrefabIndex;
			this.targetScale = newTargetScale;
			this.CacheItemComponents();
			this.BeginPhysicsInitialization();
			HookHangMoreCompatibility.Exclude(this.item);
		}

		internal bool CanRestoreSavedCrate(int crateId)
		{
			return this.waitingForSavedCrate && this.savedCrateId > 0 && this.savedCrateId == crateId;
		}

		internal void PrepareForSavedState(SavePrefabData data)
		{
			if (data == null)
			{
				return;
			}
			this.CacheItemComponents();
			this.BeginPhysicsInitialization();
			this.waitingForSavedCrate = (data.crateId > 0);
			this.waitingForSavedInventory = (!this.waitingForSavedCrate && data.inventorySlot > -1);
			this.savedCrateId = (this.waitingForSavedCrate ? data.crateId : 0);
			if (!this.waitingForSavedCrate || this.item == null || this.itemRigidbody == null)
			{
				return;
			}
			this.itemRigidbody.attached = true;
			this.itemRigidbody.disableCol = true;
			this.itemRigidbody.inStove = true;
			base.transform.localScale = Vector3.one * this.item.inventoryScale * 0.33f;
			Collider component = this.item.GetComponent<Collider>();
			if (component != null)
			{
				component.enabled = false;
			}
		}

		internal void CompleteSavedCrateRestore(int crateId)
		{
			if (!this.CanRestoreSavedCrate(crateId))
			{
				return;
			}
			this.waitingForSavedCrate = false;
			this.savedCrateId = 0;
		}

		internal void OnEnteredInventory()
		{
			this.waitingForSavedInventory = false;
		}

		internal void RestoreWorldScaleAfterStorage()
		{
			this.waitingForSavedCrate = false;
			this.waitingForSavedInventory = false;
			this.savedCrateId = 0;
			this.CacheItemComponents();
			Collider collider = (this.item != null) ? this.item.GetComponent<Collider>() : null;
			if (collider != null)
			{
				collider.enabled = true;
			}
			this.BeginPhysicsInitialization();
		}

		internal void SetPersistentKinematic(bool state)
		{
			this.persistentKinematic = state;
			this.CacheItemComponents();
			if (this.itemRigidbody == null)
			{
				return;
			}
			if (state)
			{
				this.itemRigidbody.debugForceKinematic = true;
			}
			else if (!this.initializationGuardActive)
			{
				this.itemRigidbody.debugForceKinematic = false;
			}
			Rigidbody body = this.itemRigidbody.GetBody();
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
			foreach (ScaledOarController scaledOarController in ScaledOarController.ActiveControllers)
			{
				if (scaledOarController != null)
				{
					scaledOarController.SuppressStaleOutline();
				}
			}
		}

		private void Awake()
		{
			this.CacheItemComponents();
			this.BeginPhysicsInitialization();
			HookHangMoreCompatibility.Exclude(this.item);
		}

		private void OnEnable()
		{
			ScaledOarController.ActiveControllers.Add(this);
			this.StartInitializationRoutineIfNeeded();
		}

		private void OnDisable()
		{
			ScaledOarController.ActiveControllers.Remove(this);
			if (this.initializationRoutine != null)
			{
				base.StopCoroutine(this.initializationRoutine);
				this.initializationRoutine = null;
			}
		}

		private void OnDestroy()
		{
			ScaledOarController.ActiveControllers.Remove(this);
		}

		private void LateUpdate()
		{
			this.CacheItemComponents();
			if (this.item == null)
			{
				return;
			}
			bool flag = GameState.sleeping || GameState.recovering || GameState.currentlyLoading || GameState.justWokeUp;
			if (flag)
			{
				if (!GameState.sleeping && !GameState.currentlyLoading)
				{
					this.ReconcileCollisionState();
				}
				this.SuppressStaleOutline();
			}
			else if (this.wasInSleepOrLoadTransition)
			{
				this.ReconcileCollisionState();
				this.SuppressStaleOutline();
			}
			this.wasInSleepOrLoadTransition = flag;
		}

		private void ApplyScale(Transform target)
		{
			Vector3 vector = Vector3.one * this.targetScale;
			if (target.localScale != vector)
			{
				target.localScale = vector;
			}
		}

		private void CacheItemComponents()
		{
			if (this.item == null)
			{
				this.item = base.GetComponent<ShipItemOar>();
			}
			if (this.itemRigidbody == null && this.item != null)
			{
				this.itemRigidbody = this.item.itemRigidbodyC;
			}
		}

		private void BeginPhysicsInitialization()
		{
			this.CacheItemComponents();
			if (this.item == null || this.itemRigidbody == null)
			{
				return;
			}
			this.ApplyScale(base.transform);
			this.ApplyScale(this.itemRigidbody.transform);
			if (!this.initializationGuardActive)
			{
				this.initializationGuardActive = true;
				if (!this.itemRigidbody.debugForceKinematic)
				{
					this.itemRigidbody.debugForceKinematic = true;
					this.ownsKinematicGuard = true;
				}
			}
			this.StartInitializationRoutineIfNeeded();
		}

		private void StartInitializationRoutineIfNeeded()
		{
			if (!this.initializationGuardActive || this.initializationRoutine != null || !base.isActiveAndEnabled || !base.gameObject.activeInHierarchy)
			{
				return;
			}
			this.initializationRoutine = base.StartCoroutine(this.CompletePhysicsInitialization());
		}

		// Rebuilt by hand from 1.1.1's compiled state machine (<CompletePhysicsInitialization>d__35), which the
		// decompiler left unreconstructed.
		private IEnumerator CompletePhysicsInitialization()
		{
			while (this.itemRigidbody == null || this.itemRigidbody.GetBody() == null || this.itemRigidbody.GetComponent<Collider>() == null)
			{
				this.CacheItemComponents();
				yield return null;
			}
			int storageRestoreGraceFrames = 0;
			while (GameState.currentlyLoading || GameState.loadingBoatLocalItems || GameState.recovering || this.waitingForSavedCrate || this.waitingForSavedInventory)
			{
				if (!GameState.currentlyLoading && !GameState.loadingBoatLocalItems && !GameState.recovering && (this.waitingForSavedCrate || this.waitingForSavedInventory))
				{
					storageRestoreGraceFrames++;
					if (storageRestoreGraceFrames > 30)
					{
						this.RecoverFailedStorageRestore();
					}
				}
				else
				{
					storageRestoreGraceFrames = 0;
				}
				yield return null;
			}
			Rigidbody body = this.itemRigidbody.GetBody();
			if (body != null)
			{
				body.isKinematic = true;
				body.velocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
				if (this.persistentKinematic)
				{
					body.useGravity = false;
				}
			}
			Physics.SyncTransforms();
			yield return new WaitForFixedUpdate();
			this.ReleaseInitializationGuard();
		}

		private void RecoverFailedStorageRestore()
		{
			bool flag = this.waitingForSavedCrate;
			this.waitingForSavedCrate = false;
			this.waitingForSavedInventory = false;
			this.savedCrateId = 0;
			SaveablePrefab saveablePrefab = (this.item != null) ? this.item.GetComponent<SaveablePrefab>() : null;
			if (flag && saveablePrefab != null)
			{
				saveablePrefab.currentCrateId = 0;
			}
			if (this.itemRigidbody != null)
			{
				this.itemRigidbody.attached = false;
				this.itemRigidbody.disableCol = false;
				this.itemRigidbody.inStove = false;
			}
			Collider collider = (this.item != null) ? this.item.GetComponent<Collider>() : null;
			if (collider != null)
			{
				collider.enabled = true;
			}
			this.ApplyScale(base.transform);
			if (this.itemRigidbody != null)
			{
				this.ApplyScale(this.itemRigidbody.transform);
			}
			ManualLogSource logSource = PowerfulOarPlugin.LogSource;
			if (logSource == null)
			{
				return;
			}
			string str = "Recovered ";
			ShipItemOar shipItemOar = this.item;
			logSource.LogWarning(str + (((shipItemOar != null) ? shipItemOar.name : null) ?? "scaled oar") + " after its saved inventory or crate location could not be restored.");
		}

		private void ReleaseInitializationGuard()
		{
			this.initializationRoutine = null;
			this.initializationGuardActive = false;
			if (this.ownsKinematicGuard && !this.persistentKinematic && this.itemRigidbody != null)
			{
				this.itemRigidbody.debugForceKinematic = false;
			}
			this.ownsKinematicGuard = false;
		}

		private void SuppressStaleOutline()
		{
			if (this.item == null)
			{
				this.item = base.GetComponent<ShipItemOar>();
			}
			if (this.item != null)
			{
				this.item.enableRedOutline = false;
			}
			if (this.outline == null)
			{
				this.outline = base.GetComponent<Outline>();
			}
			if (this.outline != null)
			{
				this.outline.enabled = false;
			}
		}

		private void ReconcileCollisionState()
		{
			ShipItemOar shipItemOar = this.item;
			PickupableItemCollisionChecker pickupableItemCollisionChecker = (shipItemOar != null) ? shipItemOar.colChecker : null;
			if (pickupableItemCollisionChecker == null || ScaledOarController.CollidedCollidersField == null || ScaledOarController.CurrentDecolDistanceField == null)
			{
				return;
			}
			List<Collider> list = ScaledOarController.CollidedCollidersField.GetValue(pickupableItemCollisionChecker) as List<Collider>;
			Collider component = pickupableItemCollisionChecker.GetComponent<Collider>();
			int num = 0;
			float num2 = 0f;
			if (this.item.held && this.item.GetCurrentInventorySlot() < 0 && component != null && component.enabled && list != null)
			{
				foreach (Collider collider in list)
				{
					Vector3 vector;
					float num3;
					if (!(collider == null) && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger && !(collider.gameObject == this.item.gameObject) && !collider.CompareTag("Boat") && Physics.ComputePenetration(component, component.transform.position, component.transform.rotation, collider, collider.transform.position, collider.transform.rotation, out vector, out num3) && num3 > 0f)
					{
						num++;
						num2 = Mathf.Max(num2, num3);
					}
				}
			}
			pickupableItemCollisionChecker.collisions = num;
			ScaledOarController.CurrentDecolDistanceField.SetValue(pickupableItemCollisionChecker, num2);
			pickupableItemCollisionChecker.allowObstructedDropping = (num2 < 0.06f);
			this.item.enableRedOutline = false;
		}

		private static readonly HashSet<ScaledOarController> ActiveControllers = new HashSet<ScaledOarController>();

		private static readonly FieldInfo CollidedCollidersField = AccessTools.Field(typeof(PickupableItemCollisionChecker), "collidedCols");

		private static readonly FieldInfo CurrentDecolDistanceField = AccessTools.Field(typeof(PickupableItemCollisionChecker), "currentDecolDistance");

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
	}

	internal static class OarStats
	{
		internal static bool Apply(ShipItemOar oar)
		{
			int prefabIndex = OarStats.GetPrefabIndex(oar);
			if (prefabIndex <= 169)
			{
				if (prefabIndex == 168)
				{
					oar.rowForce = 36000f;
					oar.maxBoatSpeed = 2.06f;
					return true;
				}
				if (prefabIndex == 169)
				{
					if (!OarStats.IsLegacyBigOar(oar))
					{
						return false;
					}
					oar.rowForce = 93500f;
					oar.maxBoatSpeed = 3.1f;
					return true;
				}
			}
			else
			{
				if (prefabIndex == 602)
				{
					oar.rowForce = 93500f;
					oar.maxBoatSpeed = 3.1f;
					return true;
				}
				if (prefabIndex == 603)
				{
					oar.rowForce = 204000f;
					oar.maxBoatSpeed = 5.14f;
					return true;
				}
				if (prefabIndex == 666)
				{
					oar.rowForce = 510000f;
					oar.maxBoatSpeed = 10.29f;
					return true;
				}
			}
			return false;
		}

		internal static float GetWaterCostPerSecond(ShipItemOar oar)
		{
			return OarStats.GetNeedsCostPerSecond(oar, 0.4f) * RadRefinementCompatibility.NeedsCostMultiplier;
		}

		internal static float GetFoodCostPerSecond(ShipItemOar oar)
		{
			return OarStats.GetNeedsCostPerSecond(oar, 0.6f) * RadRefinementCompatibility.NeedsCostMultiplier;
		}

		private static float GetNeedsCostPerSecond(ShipItemOar oar, float fallback)
		{
			int prefabIndex = OarStats.GetPrefabIndex(oar);
			if (prefabIndex <= 169)
			{
				if (prefabIndex == 168)
				{
					return 0.1f;
				}
				if (prefabIndex == 169)
				{
					if (!OarStats.IsLegacyBigOar(oar))
					{
						return fallback;
					}
					return 0.2f;
				}
			}
			else
			{
				if (prefabIndex == 602)
				{
					return 0.2f;
				}
				if (prefabIndex == 603)
				{
					return 0.3f;
				}
				if (prefabIndex == 666)
				{
					return 1f;
				}
			}
			return fallback;
		}

		internal static int GetPrefabIndex(ShipItem item)
		{
			SaveablePrefab saveablePrefab = (item != null) ? item.GetComponent<SaveablePrefab>() : null;
			if (!(saveablePrefab != null))
			{
				return -1;
			}
			return saveablePrefab.prefabIndex;
		}

		internal static bool IsScaledOar(ShipItem item)
		{
			// Runs for every item in the world every frame (ScaledOarRigidbodyScalePatch on ItemRigidbody.LateUpdate), so it
			// reads the SaveablePrefab once; the same answer as IsLegacyBigOar for prefab 169.
			int prefabIndex = OarStats.GetPrefabIndex(item);
			return prefabIndex == 602 || prefabIndex == 603 || prefabIndex == 666 || (prefabIndex == 169 && item.GetComponent<ScaledOarController>() != null);
		}

		private static bool IsLegacyBigOar(ShipItem item)
		{
			return item != null && OarStats.GetPrefabIndex(item) == 169 && item.GetComponent<ScaledOarController>() != null;
		}

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
	}

	// ---- Scaled oar items: game patches ----

	[HarmonyPatch(typeof(PrefabsDirectory), "Start")]
	internal static class ScaledOarPrefabRegistrationPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(0)]
		private static void Prefix(PrefabsDirectory __instance)
		{
			ScaledOarFactory.EnsureRegistered(__instance);
		}

		[HarmonyPostfix]
		[HarmonyPriority(0)]
		private static void Postfix(PrefabsDirectory __instance)
		{
			ScaledOarFactory.ValidateRuntimeCache(__instance);
			BoneIslandBfoPlacement.OnPrefabsReady();
		}
	}

	[HarmonyPatch(typeof(SaveablePrefab), "Load")]
	internal static class ScaledOarSavedStatePatch
	{
		[HarmonyPostfix]
		private static void Postfix(SaveablePrefab __instance, SavePrefabData data)
		{
			ScaledOarFactory.MigrateLegacyBigOar(__instance, data);
			ScaledOarController component = __instance.GetComponent<ScaledOarController>();
			if (component == null)
			{
				return;
			}
			component.PrepareForSavedState(data);
		}
	}

	[HarmonyPatch(typeof(ItemRigidbody), "LateUpdate")]
	internal static class ScaledOarRigidbodyScalePatch
	{
		[HarmonyPrefix]
		private static bool Prefix(ItemRigidbody __instance)
		{
			return !OarStats.IsScaledOar(__instance.GetShipItem()) || __instance.GetCurrentInventorySlot() != null;
		}
	}

	[HarmonyPatch(typeof(ItemRigidbody), "EnterInventorySlot")]
	internal static class ScaledOarInventoryEnterPatch
	{
		[HarmonyPostfix]
		private static void Postfix(ItemRigidbody __instance)
		{
			ShipItem shipItem = __instance.GetShipItem();
			if (shipItem == null)
			{
				return;
			}
			ScaledOarController component = shipItem.GetComponent<ScaledOarController>();
			if (component == null)
			{
				return;
			}
			component.OnEnteredInventory();
		}
	}

	[HarmonyPatch(typeof(ItemRigidbody), "ExitInventorySlot")]
	internal static class ScaledOarInventoryExitPatch
	{
		[HarmonyPostfix]
		private static void Postfix(ItemRigidbody __instance)
		{
			ShipItem shipItem = __instance.GetShipItem();
			if (shipItem == null)
			{
				return;
			}
			ScaledOarController component = shipItem.GetComponent<ScaledOarController>();
			if (component == null)
			{
				return;
			}
			component.RestoreWorldScaleAfterStorage();
		}
	}

	[HarmonyPatch(typeof(CrateInventory), "InsertItem")]
	internal static class ScaledOarCrateInsertPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(CrateInventory __instance, ShipItem item)
		{
			if (!OarStats.IsScaledOar(item))
			{
				return true;
			}
			SaveablePrefab component = item.GetComponent<SaveablePrefab>();
			SaveablePrefab component2 = __instance.GetComponent<SaveablePrefab>();
			int num = (component2 != null) ? component2.instanceId : 0;
			ScaledOarController component3 = item.GetComponent<ScaledOarController>();
			if (num > 0 && __instance.containedItems.Contains(item))
			{
				if (component != null)
				{
					component.currentCrateId = num;
				}
				if (component3 != null)
				{
					component3.CompleteSavedCrateRestore(num);
				}
				return false;
			}
			if ((component3 != null) ? component3.CanRestoreSavedCrate(num) : (num > 0 && component != null && component.currentCrateId == num))
			{
				if (component3 != null)
				{
					component3.CompleteSavedCrateRestore(num);
				}
				return true;
			}
			if (component != null && component.currentCrateId == num)
			{
				component.currentCrateId = 0;
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(CrateInventory), "WithdrawItem")]
	internal static class ScaledOarCrateWithdrawPatch
	{
		[HarmonyPostfix]
		private static void Postfix(ShipItem item)
		{
			if (item != null)
			{
				ScaledOarController component = item.GetComponent<ScaledOarController>();
				if (component == null)
				{
					return;
				}
				component.RestoreWorldScaleAfterStorage();
			}
		}
	}

	[HarmonyPatch(typeof(CrateInventoryButton), "OnActivate")]
	internal static class ScaledOarCrateUiPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(GoPointer activatingPointer)
		{
			PickupableItem pickupableItem = (activatingPointer != null) ? activatingPointer.GetHeldItem() : null;
			if (!OarStats.IsScaledOar((pickupableItem != null) ? pickupableItem.GetComponent<ShipItem>() : null))
			{
				return true;
			}
			if (NotificationUi.instance != null)
			{
				NotificationUi.instance.ShowNotification("Scaled oars cannot be stored in crates.");
			}
			CrateInventoryUI instance = CrateInventoryUI.instance;
			if (instance != null)
			{
				instance.RefreshButtons();
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(ShipItemHammer), "CanNail")]
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

	[HarmonyPatch(typeof(OutlineEffect), "OnEnable")]
	internal static class ScaledOarOutlineEffectPatch
	{
		[HarmonyPostfix]
		[HarmonyPriority(0)]
		private static void Postfix()
		{
			ScaledOarController.SuppressTransitionOutlines();
		}
	}

	// ---- Rowing: force, needs, stroke, reverse input ----

	[HarmonyPatch(typeof(ShipItemOar), "OnAltHeld")]
	internal static class OarStatsPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(ShipItemOar __instance, ref bool ___isHoldingButton, bool ___isOverWater, ref bool ___isRowing, ref float ___rowProgress, out OarStatsPatch.RowCallState __state)
		{
			__state = new OarStatsPatch.RowCallState
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
					Rigidbody component = GameState.currentBoat.parent.GetComponent<Rigidbody>();
					float d = Mathf.InverseLerp(__instance.maxBoatSpeed, 0f, component.velocity.magnitude);
					component.AddForceAtPosition(__instance.waterPos.forward * __instance.rowForce * Time.deltaTime * d, __instance.waterPos.position);
				}
				else
				{
					___isRowing = false;
				}
			}
			return false;
		}

		[HarmonyPostfix]
		[HarmonyAfter(new string[]
		{
			"com.raddude.radrefinements"
		})]
		private static void Postfix(ShipItemOar __instance, bool ___isOverWater, OarStatsPatch.RowCallState __state)
		{
			if (!RadRefinementCompatibility.ContinualRowEnabled || !__instance.held || !___isOverWater || !GameState.currentBoat || __state.ProgressBefore > 1f)
			{
				return;
			}
			if (__state.ProgressBefore + Time.deltaTime > 1f)
			{
				return;
			}
			Rigidbody component = GameState.currentBoat.parent.GetComponent<Rigidbody>();
			Vector3 a = __instance.waterPos.forward * -__instance.rowForce * Time.deltaTime;
			component.AddForceAtPosition(-a, __instance.waterPos.position);
			float d = Mathf.InverseLerp(__instance.maxBoatSpeed, 0f, component.velocity.magnitude);
			float d2 = __state.Reverse ? 1f : -1f;
			component.AddForceAtPosition(__instance.waterPos.forward * __instance.rowForce * Time.deltaTime * d * d2, __instance.waterPos.position);
		}

		private struct RowCallState
		{
			internal float ProgressBefore;

			internal bool Reverse;
		}
	}

	[HarmonyPatch(typeof(ShipItemOar), "ExtraLateUpdate")]
	internal static class OarLateUpdatePatch
	{
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> list = new List<CodeInstruction>(instructions);
			FieldInfo needsField = AccessTools.Field(typeof(PlayerNeeds), "water");
			FieldInfo needsField2 = AccessTools.Field(typeof(PlayerNeeds), "food");
			FieldInfo rowProgressField = AccessTools.Field(typeof(ShipItemOar), "rowProgress");
			List<int> list2 = OarLateUpdatePatch.FindNeedCostConstants(list, needsField, 0.4f);
			List<int> list3 = OarLateUpdatePatch.FindNeedCostConstants(list, needsField2, 0.6f);
			if (list2.Count == 1 && list3.Count == 1)
			{
				MethodInfo selector = AccessTools.Method(typeof(OarLateUpdatePatch), "SelectWaterCost", null, null);
				MethodInfo selector2 = AccessTools.Method(typeof(OarLateUpdatePatch), "SelectFoodCost", null, null);
				OarLateUpdatePatch.ReplaceNeedCostConstant(list, list3[0], selector2);
				OarLateUpdatePatch.ReplaceNeedCostConstant(list, list2[0], selector);
				ManualLogSource logSource = PowerfulOarPlugin.LogSource;
				if (logSource != null)
				{
					logSource.LogInfo("Patched per-oar rowing water and food costs: Original=0.1/s, Big Oar=0.2/s, Huge Oar=0.3/s, BFO 5000=1/s.");
				}
			}
			else
			{
				ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
				if (logSource2 != null)
				{
					logSource2.LogError("Could not safely patch rowing needs costs: " + string.Format("water matches={0}, food matches={1}. ", list2.Count, list3.Count) + "Vanilla needs costs were left unchanged.");
				}
			}
			List<int> list4 = OarLateUpdatePatch.FindStrokeProgressLoads(list, rowProgressField);
			if (list4.Count == 1)
			{
				MethodInfo methodInfo = AccessTools.Method(typeof(OarLateUpdatePatch), "SelectStrokeProgress", null, null);
				list.Insert(list4[0] + 1, new CodeInstruction(OpCodes.Call, methodInfo));
				ManualLogSource logSource3 = PowerfulOarPlugin.LogSource;
				if (logSource3 != null)
				{
					logSource3.LogInfo("Patched reverse oar stroke animation for the Q key.");
				}
			}
			else
			{
				ManualLogSource logSource4 = PowerfulOarPlugin.LogSource;
				if (logSource4 != null)
				{
					logSource4.LogError("Could not safely patch reverse oar stroke animation: " + string.Format("matches={0}. Reverse thrust remains available on Q.", list4.Count));
				}
			}
			return list;
		}

		private static float SelectStrokeProgress(float progress)
		{
			if (!PowerfulOarInput.ReverseRowHeld)
			{
				return progress;
			}
			return 1f - progress;
		}

		private static float SelectWaterCost(ShipItemOar oar)
		{
			return OarStats.GetWaterCostPerSecond(oar);
		}

		private static float SelectFoodCost(ShipItemOar oar)
		{
			return OarStats.GetFoodCostPerSecond(oar);
		}

		private static void ReplaceNeedCostConstant(IList<CodeInstruction> codes, int constantIndex, MethodInfo selector)
		{
			CodeInstruction codeInstruction = codes[constantIndex];
			codeInstruction.opcode = OpCodes.Ldarg_0;
			codeInstruction.operand = null;
			codes.Insert(constantIndex + 1, new CodeInstruction(OpCodes.Call, selector));
		}

		private static List<int> FindNeedCostConstants(IList<CodeInstruction> codes, FieldInfo needsField, float expectedConstant)
		{
			List<int> list = new List<int>();
			for (int i = 0; i <= codes.Count - 6; i++)
			{
				if (!(codes[i].opcode != OpCodes.Ldsfld) && object.Equals(codes[i].operand, needsField) && !(codes[i + 2].opcode != OpCodes.Ldc_R4))
				{
					object operand = codes[i + 2].operand;
					if (operand is float)
					{
						float a = (float)operand;
						if (Mathf.Approximately(a, expectedConstant) && !(codes[i + 3].opcode != OpCodes.Mul) && !(codes[i + 4].opcode != OpCodes.Sub) && !(codes[i + 5].opcode != OpCodes.Stsfld) && object.Equals(codes[i + 5].operand, needsField))
						{
							list.Add(i + 2);
						}
					}
				}
			}
			return list;
		}

		private static List<int> FindStrokeProgressLoads(IList<CodeInstruction> codes, FieldInfo rowProgressField)
		{
			List<int> list = new List<int>();
			for (int i = 0; i <= codes.Count - 6; i++)
			{
				if (OarLateUpdatePatch.LoadsFloat(codes[i], -1f) && OarLateUpdatePatch.LoadsFloat(codes[i + 1], 2f) && !(codes[i + 2].opcode != OpCodes.Ldarg_0) && !(codes[i + 3].opcode != OpCodes.Ldfld) && object.Equals(codes[i + 3].operand, rowProgressField) && !(codes[i + 4].opcode != OpCodes.Mul) && !(codes[i + 5].opcode != OpCodes.Add))
				{
					list.Add(i + 3);
				}
			}
			return list;
		}

		private static bool LoadsFloat(CodeInstruction instruction, float expected)
		{
			if (instruction.opcode == OpCodes.Ldc_R4)
			{
				object operand = instruction.operand;
				if (operand is float)
				{
					float a = (float)operand;
					return Mathf.Approximately(a, expected);
				}
			}
			return false;
		}

		private const float VanillaWaterCost = 0.4f;

		private const float VanillaFoodCost = 0.6f;
	}

	[HarmonyPatch(typeof(GoPointer), "LateUpdate")]
	internal static class ReverseOarInputPatch
	{
		[HarmonyPostfix]
		private static void Postfix(GoPointer __instance)
		{
			if (!PowerfulOarInput.ReverseRowHeld)
			{
				return;
			}
			ShipItemOar shipItemOar = __instance.GetHeldItem() as ShipItemOar;
			if (shipItemOar == null || GameInput.GetKey(InputName.Activate))
			{
				return;
			}
			if (Input.GetKeyDown(KeyCode.Q))
			{
				shipItemOar.OnAltActivate();
			}
			shipItemOar.OnAltHeld();
		}
	}

	internal static class PowerfulOarInput
	{
		internal static bool ReverseRowHeld
		{
			get
			{
				return GameState.playing && !GameState.inCursorMenu && !GameState.wasInSettingsMenu && Input.GetKey(KeyCode.Q);
			}
		}
	}
}
