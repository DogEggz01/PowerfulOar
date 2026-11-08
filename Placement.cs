// PowerfulOar: where the oars appear: the Big Oar and Huge Oar vendor placement, the Huge Oar purchase dialogue at
// Kicia Bay (with its saved purchase count) and the BFO 5000's placement at Bone Island.
// (The user, 2026-11-08: "Placement.cs: include vendor placement, dialogue and BFO placement".)
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PowerfulOar
{
	// ---- Shop placement: Big Oar at the oar sellers, Huge Oar at Kicia Bay ----

	[HarmonyPatch(typeof(ShopItemSpawner), "Start")]
	internal static class BigOarVendorPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(0)]
		private static void Prefix(ShopItemSpawner __instance)
		{
			Scene scene = __instance.gameObject.scene;
			BigOarVendorPatch.VendorPlacement vendorPlacement;
			if (!scene.IsValid() || !BigOarVendorPatch.TargetPlacements.TryGetValue(scene.name, out vendorPlacement) || __instance.gameObject.name != vendorPlacement.SpawnerName)
			{
				return;
			}
			PrefabsDirectory instance = PrefabsDirectory.instance;
			if (instance == null || !ScaledOarFactory.ValidateRuntimeCache(instance))
			{
				ManualLogSource logSource = PowerfulOarPlugin.LogSource;
				if (logSource == null)
				{
					return;
				}
				logSource.LogError("Could not add Big Oar to vendor in " + scene.name + ": prefab directory unavailable.");
				return;
			}
			else
			{
				int prefabIndex = OarStats.GetPrefabIndex((__instance.itemPrefab != null) ? __instance.itemPrefab.GetComponent<ShipItem>() : null);
				if (prefabIndex == 602)
				{
					return;
				}
				if (prefabIndex != 168)
				{
					ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
					if (logSource2 == null)
					{
						return;
					}
					logSource2.LogWarning(string.Concat(new string[]
					{
						"Configured Big Oar vendor slot '",
						vendorPlacement.SpawnerName,
						"' in ",
						scene.name,
						" does not contain vanilla oar prefab ",
						string.Format("{0}; found {1}.", 168, prefabIndex)
					}));
					return;
				}
				else
				{
					__instance.itemPrefab = instance.directory[602];
					ScaledOarVendorDisplaySpawner scaledOarVendorDisplaySpawner = __instance.GetComponent<ScaledOarVendorDisplaySpawner>();
					if (scaledOarVendorDisplaySpawner == null)
					{
						scaledOarVendorDisplaySpawner = __instance.gameObject.AddComponent<ScaledOarVendorDisplaySpawner>();
					}
					scaledOarVendorDisplaySpawner.Initialize(scene.name, "Big Oar", 602, vendorPlacement.LocalPosition, vendorPlacement.LocalEuler, 2f);
					ManualLogSource logSource3 = PowerfulOarPlugin.LogSource;
					if (logSource3 == null)
					{
						return;
					}
					logSource3.LogInfo(string.Concat(new string[]
					{
						"Replaced one vanilla-oar vendor slot with Big Oar in ",
						scene.name,
						" at '",
						vendorPlacement.SpawnerName,
						"' using its configured local pose."
					}));
					return;
				}
			}
		}

		private static readonly Dictionary<string, BigOarVendorPatch.VendorPlacement> TargetPlacements = new Dictionary<string, BigOarVendorPatch.VendorPlacement>
		{
			{
				"island 9 E Dragon Cliffs",
				new BigOarVendorPatch.VendorPlacement("shop item spawner (236)", new Vector3(0.198f, 1.5f, -0.049f), new Vector3(0.625f, -176.864f, 0.7f))
			},
			{
				"island 1 A Gold Rock",
				new BigOarVendorPatch.VendorPlacement("shop item (224)", new Vector3(0.145f, 1.467f, -0.004f), new Vector3(0f, -12.273f, 0f))
			},
			{
				"island 15 M (Fort)",
				new BigOarVendorPatch.VendorPlacement("shop item (284)", new Vector3(0.287f, 1.46f, -0.251f), new Vector3(-2.876f, -2.876f, -1.02f))
			}
		};

		private sealed class VendorPlacement
		{
			internal VendorPlacement(string spawnerName, Vector3 localPosition, Vector3 localEuler)
			{
				this.SpawnerName = spawnerName;
				this.LocalPosition = localPosition;
				this.LocalEuler = localEuler;
			}

			internal readonly string SpawnerName;

			internal readonly Vector3 LocalPosition;

			internal readonly Vector3 LocalEuler;
		}
	}

	[HarmonyPatch(typeof(ShopItemSpawner), "SpawnItem")]
	internal static class ScaledOarVendorSpawnPatch
	{
		[HarmonyPostfix]
		[HarmonyPriority(0)]
		private static void Postfix(ShopItemSpawner __instance, ShipItem ___item)
		{
			ScaledOarVendorDisplaySpawner component = __instance.GetComponent<ScaledOarVendorDisplaySpawner>();
			if (component != null)
			{
				component.ConfigureSpawnedItem(___item);
			}
		}
	}

	internal sealed class ScaledOarVendorDisplaySpawner : MonoBehaviour
	{
		internal void Initialize(string newSceneName, string newItemName, int newPrefabIndex, Vector3 newLocalPosition, Vector3 newLocalEuler, float newTargetWorldScale)
		{
			this.sceneName = newSceneName;
			this.itemName = newItemName;
			this.prefabIndex = newPrefabIndex;
			this.localPosition = newLocalPosition;
			this.localRotation = Quaternion.Euler(newLocalEuler);
			this.targetWorldScale = newTargetWorldScale;
		}

		internal void ConfigureSpawnedItem(ShipItem item)
		{
			if (item == null || OarStats.GetPrefabIndex(item) != this.prefabIndex)
			{
				return;
			}
			ScaledOarVendorDisplayItem scaledOarVendorDisplayItem = item.GetComponent<ScaledOarVendorDisplayItem>();
			if (scaledOarVendorDisplayItem == null)
			{
				scaledOarVendorDisplayItem = item.gameObject.AddComponent<ScaledOarVendorDisplayItem>();
			}
			scaledOarVendorDisplayItem.Initialize(item, this.localPosition, this.localRotation, this.targetWorldScale);
			ManualLogSource logSource = PowerfulOarPlugin.LogSource;
			if (logSource == null)
			{
				return;
			}
			logSource.LogInfo(string.Concat(new string[]
			{
				"Positioned ",
				this.itemName,
				" vendor display in ",
				this.sceneName,
				" at the configured spawner-local pose."
			}));
		}

		private string sceneName;

		private string itemName;

		private int prefabIndex;

		private Vector3 localPosition;

		private Quaternion localRotation;

		private float targetWorldScale;
	}

	[DefaultExecutionOrder(10001)]
	internal sealed class ScaledOarVendorDisplayItem : MonoBehaviour
	{
		internal void Initialize(ShipItem newItem, Vector3 newLocalPosition, Quaternion newLocalRotation, float newTargetWorldScale)
		{
			this.item = newItem;
			this.localPosition = newLocalPosition;
			this.localRotation = newLocalRotation;
			this.targetWorldScale = newTargetWorldScale;
			this.ApplyPlacement();
		}

		private void LateUpdate()
		{
			if (this.item == null || this.item.sold || this.item.held)
			{
				base.enabled = false;
				return;
			}
			this.ApplyPlacement();
		}

		private void ApplyPlacement()
		{
			Transform parent = base.transform.parent;
			if (parent == null)
			{
				return;
			}
			Vector3 lossyScale = parent.lossyScale;
			base.transform.localScale = new Vector3(this.SafeLocalScale(lossyScale.x), this.SafeLocalScale(lossyScale.y), this.SafeLocalScale(lossyScale.z));
			base.transform.localPosition = this.localPosition;
			base.transform.localRotation = this.localRotation;
		}

		private float SafeLocalScale(float parentScale)
		{
			if (Mathf.Abs(parentScale) <= 0.0001f)
			{
				return this.targetWorldScale;
			}
			return this.targetWorldScale / Mathf.Abs(parentScale);
		}

		private ShipItem item;

		private Vector3 localPosition;

		private Quaternion localRotation;

		private float targetWorldScale;
	}

	[HarmonyPatch(typeof(Shopkeeper), "Start")]
	internal static class HugeOarVendorPatch
	{
		[HarmonyPostfix]
		private static void Postfix(Shopkeeper __instance)
		{
			if (!HugeOarVendorPatch.IsTarget(__instance))
			{
				return;
			}
			HugeOarVendorInstaller hugeOarVendorInstaller = __instance.GetComponent<HugeOarVendorInstaller>();
			if (hugeOarVendorInstaller == null)
			{
				hugeOarVendorInstaller = __instance.gameObject.AddComponent<HugeOarVendorInstaller>();
			}
			hugeOarVendorInstaller.Initialize(__instance);
		}

		internal static bool IsTarget(Shopkeeper shopkeeper)
		{
			if (shopkeeper == null || shopkeeper.gameObject.name != "shopkeeper (16)")
			{
				return false;
			}
			Scene scene = shopkeeper.gameObject.scene;
			return scene.IsValid() && scene.name == "island 27 Lagoon SwampShipyard";
		}

		internal const string SceneName = "island 27 Lagoon SwampShipyard";

		internal const string ShopkeeperName = "shopkeeper (16)";

		internal const string NpcName = "Modular NPC";
	}

	internal sealed class HugeOarVendorInstaller : MonoBehaviour
	{
		internal void Initialize(Shopkeeper newShopkeeper)
		{
			this.shopkeeper = newShopkeeper;
			this.nextAttempt = 0f;
		}

		internal void ShowPurchaseReaction(int purchaseCount)
		{
			if (this.dialogueArea != null)
			{
				this.dialogueArea.ShowPurchase(HugeOarPurchaseState.GetDialogue(purchaseCount));
			}
		}

		private void Update()
		{
			if (this.complete || Time.unscaledTime < this.nextAttempt)
			{
				return;
			}
			if (this.shopkeeper == null)
			{
				base.enabled = false;
				return;
			}
			this.nextAttempt = Time.unscaledTime + 1f;
			Transform transform = this.shopkeeper.transform.Find("Modular NPC");
			if (transform == null)
			{
				if (!this.reportedMissingNpc)
				{
					this.reportedMissingNpc = true;
					ManualLogSource logSource = PowerfulOarPlugin.LogSource;
					if (logSource != null)
					{
						logSource.LogError("Could not add Huge Oar to Kicia Bay: shopkeeper (16)/Modular NPC was not found; retrying.");
					}
				}
				this.nextAttempt = Time.unscaledTime + 5f;
				return;
			}
			PrefabsDirectory instance = PrefabsDirectory.instance;
			if (instance == null)
			{
				return;
			}
			if (!ScaledOarFactory.ValidateRuntimeCache(instance))
			{
				ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
				if (logSource2 != null)
				{
					logSource2.LogError("Could not add Huge Oar to Kicia Bay: scaled-oar prefab validation failed.");
				}
				base.enabled = false;
				return;
			}
			if (transform.Find("PowerfulOar Huge Oar shop spawner") != null)
			{
				ManualLogSource logSource3 = PowerfulOarPlugin.LogSource;
				if (logSource3 != null)
				{
					logSource3.LogWarning("Skipped duplicate Kicia Bay Huge Oar shop spawner.");
				}
				this.complete = true;
				base.enabled = false;
				return;
			}
			GameObject gameObject = null;
			try
			{
				gameObject = new GameObject("PowerfulOar Huge Oar shop spawner");
				gameObject.SetActive(false);
				gameObject.transform.SetParent(transform, false);
				gameObject.transform.localPosition = HugeOarVendorInstaller.LocalPosition;
				gameObject.transform.localRotation = Quaternion.Euler(HugeOarVendorInstaller.LocalEuler);
				gameObject.transform.localScale = Vector3.one;
				ShopItemSpawner shopItemSpawner = gameObject.AddComponent<ShopItemSpawner>();
				shopItemSpawner.itemPrefab = instance.directory[603];
				shopItemSpawner.availableAtNight = false;
				shopItemSpawner.priceMult = 1f;
				gameObject.AddComponent<ScaledOarVendorDisplaySpawner>().Initialize("island 27 Lagoon SwampShipyard", "Huge Oar", 603, Vector3.zero, Vector3.zero, 3f);
				this.dialogueArea = HugeOarPurchaseDialogueArea.Create(transform);
				gameObject.SetActive(true);
				this.complete = true;
				base.enabled = false;
				ManualLogSource logSource4 = PowerfulOarPlugin.LogSource;
				if (logSource4 != null)
				{
					logSource4.LogInfo(string.Concat(new string[]
					{
						"Added Huge Oar prefab 603 beneath Kicia Bay shopkeeper (16)/Modular NPC at local position ",
						HugeOarVendorInstaller.LocalPosition.ToString(),
						" and local rotation ",
						HugeOarVendorInstaller.LocalEuler.ToString(),
						"."
					}));
				}
			}
			catch (Exception ex)
			{
				if (this.dialogueArea != null)
				{
					Object.Destroy(this.dialogueArea.gameObject);
					this.dialogueArea = null;
				}
				if (gameObject != null)
				{
					Object.Destroy(gameObject);
				}
				this.nextAttempt = Time.unscaledTime + 10f;
				ManualLogSource logSource5 = PowerfulOarPlugin.LogSource;
				if (logSource5 != null)
				{
					string str = "Could not install the Kicia Bay Huge Oar vendor; retrying: ";
					Exception ex2 = ex;
					logSource5.LogError(str + ((ex2 != null) ? ex2.ToString() : null));
				}
			}
		}

		private const string SpawnerName = "PowerfulOar Huge Oar shop spawner";

		private static readonly Vector3 LocalPosition = new Vector3(-1.444f, 4.292f, 0.854f);

		private static readonly Vector3 LocalEuler = new Vector3(8.857f, -110.947f, 1.63f);

		private Shopkeeper shopkeeper;

		private HugeOarPurchaseDialogueArea dialogueArea;

		private float nextAttempt;

		private bool complete;

		private bool reportedMissingNpc;
	}

	// ---- BFO 5000 placement at Bone Island ----

	internal static class BoneIslandBfoPlacement
	{
		internal static void Initialize()
		{
			if (BoneIslandBfoPlacement.initialized)
			{
				return;
			}
			BoneIslandBfoPlacement.initialized = true;
			SceneManager.sceneLoaded += BoneIslandBfoPlacement.OnSceneLoaded;
			BoneIslandBfoPlacement.QueueLoadedBoneIslandScenes();
		}

		internal static void Shutdown()
		{
			if (!BoneIslandBfoPlacement.initialized)
			{
				return;
			}
			SceneManager.sceneLoaded -= BoneIslandBfoPlacement.OnSceneLoaded;
			BoneIslandBfoPlacement.PendingScenes.Clear();
			BoneIslandBfoPlacement.initialized = false;
		}

		internal static void OnPrefabsReady()
		{
			BoneIslandBfoPlacement.QueueLoadedBoneIslandScenes();
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			BoneIslandBfoPlacement.QueueScene(scene);
		}

		internal static void QueueAfterDisplayRemoved(Scene scene)
		{
			if (!BoneIslandBfoPlacement.initialized)
			{
				return;
			}
			BoneIslandBfoPlacement.QueueScene(scene);
		}

		private static void QueueLoadedBoneIslandScenes()
		{
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				BoneIslandBfoPlacement.QueueScene(SceneManager.GetSceneAt(i));
			}
		}

		private static void QueueScene(Scene scene)
		{
			PowerfulOarPlugin instance = PowerfulOarPlugin.Instance;
			if (instance == null || !scene.IsValid() || !scene.isLoaded || scene.name != "island 36 ()" || !BoneIslandBfoPlacement.PendingScenes.Add(scene.handle))
			{
				return;
			}
			instance.StartCoroutine(BoneIslandBfoPlacement.PlaceWhenReady(scene));
		}

		// Rebuilt by hand from 1.1.1's compiled state machine (<PlaceWhenReady>d__12), which the decompiler left
		// unreconstructed.
		private static IEnumerator PlaceWhenReady(Scene scene)
		{
			int sceneHandle = scene.handle;
			Transform scenery = null;
			for (;;)
			{
				if (PowerfulOarPlugin.Instance == null || !scene.IsValid() || !scene.isLoaded)
				{
					BoneIslandBfoPlacement.PendingScenes.Remove(sceneHandle);
					yield break;
				}
				if (scenery == null)
				{
					scenery = BoneIslandBfoPlacement.FindSceneryRoot(scene);
				}
				PrefabsDirectory instance = PrefabsDirectory.instance;
				Camera main = Camera.main;
				Vector3 b = (scenery != null) ? scenery.TransformPoint(BoneIslandBfoPlacement.LocalPosition) : Vector3.zero;
				if (GameState.playing && !GameState.currentlyLoading && scenery != null && main != null && Vector3.Distance(main.transform.position, b) <= 500f && instance != null && instance.directory != null && instance.directory.Length > 666 && instance.directory[666] != null)
				{
					if (ScaledOarFactory.ValidateRuntimeCache(instance))
					{
						BoneIslandBfoPlacement.TryPlace(instance, scenery);
					}
					BoneIslandBfoPlacement.PendingScenes.Remove(sceneHandle);
					yield break;
				}
				yield return null;
			}
		}

		private static void TryPlace(PrefabsDirectory directory, Transform scenery)
		{
			GameObject gameObject = directory.directory[666];
			if (BoneIslandBfoPlacement.HasExistingRuntimeBfo(gameObject))
			{
				ManualLogSource logSource = PowerfulOarPlugin.LogSource;
				if (logSource == null)
				{
					return;
				}
				logSource.LogInfo("Skipped Bone Island BFO display: an existing collected or display BFO 5000 is already loaded.");
				return;
			}
			else
			{
				GameObject gameObject2 = Object.Instantiate<GameObject>(gameObject);
				ShipItem component = gameObject2.GetComponent<ShipItem>();
				if (component == null)
				{
					ManualLogSource logSource2 = PowerfulOarPlugin.LogSource;
					if (logSource2 != null)
					{
						logSource2.LogError("Could not place Bone Island BFO 5000: cloned ShipItem missing.");
					}
					gameObject2.SetActive(false);
					return;
				}
				gameObject2.name = "BFO 5000 (Bone Island)";
				gameObject2.transform.SetParent(scenery, false);
				gameObject2.transform.localPosition = BoneIslandBfoPlacement.LocalPosition;
				gameObject2.transform.localRotation = BoneIslandBfoPlacement.LocalRotation;
				component.sold = true;
				Good component2 = gameObject2.GetComponent<Good>();
				if (component2 != null)
				{
					component2.RegisterAsMissionless();
				}
				ItemRigidbody itemRigidbody = component.GetItemRigidbody();
				ScaledOarController component3 = gameObject2.GetComponent<ScaledOarController>();
				if (component3 != null)
				{
					component3.SetPersistentKinematic(true);
				}
				if (itemRigidbody != null)
				{
					itemRigidbody.debugForceKinematic = true;
					Rigidbody component4 = itemRigidbody.GetComponent<Rigidbody>();
					if (component4 != null)
					{
						component4.isKinematic = true;
						component4.useGravity = false;
						component4.velocity = Vector3.zero;
						component4.angularVelocity = Vector3.zero;
					}
				}
				BoneIslandBfoDisplay boneIslandBfoDisplay = gameObject2.GetComponent<BoneIslandBfoDisplay>();
				if (boneIslandBfoDisplay == null)
				{
					boneIslandBfoDisplay = gameObject2.AddComponent<BoneIslandBfoDisplay>();
				}
				boneIslandBfoDisplay.Initialize(component, scenery.gameObject.scene);
				ManualLogSource logSource3 = PowerfulOarPlugin.LogSource;
				if (logSource3 == null)
				{
					return;
				}
				logSource3.LogInfo("Placed one fixed BFO 5000 at the configured Bone Island _scenery-local pose. It will enter normal item/save control when picked up. " + string.Format("Camera was within {0:0} m of the placement.", 500f));
				return;
			}
		}

		private static bool HasExistingRuntimeBfo(GameObject registeredPrefab)
		{
			foreach (SaveablePrefab saveablePrefab in Resources.FindObjectsOfTypeAll<SaveablePrefab>())
			{
				if (saveablePrefab != null && saveablePrefab.prefabIndex == 666 && saveablePrefab.gameObject != registeredPrefab)
				{
					return true;
				}
			}
			return false;
		}

		private static Transform FindSceneryRoot(Scene scene)
		{
			foreach (GameObject gameObject in scene.GetRootGameObjects())
			{
				if (gameObject.name == "_scenery")
				{
					return gameObject.transform;
				}
			}
			return null;
		}

		private const string BoneIslandSceneName = "island 36 ()";

		private const float SafeSpawnDistance = 500f;

		private static readonly Vector3 LocalPosition = new Vector3(232.5f, 17.88f, 431.6406f);

		private static readonly Quaternion LocalRotation = Quaternion.Euler(-0.415f, 116.637f, 81.422f);

		private static readonly HashSet<int> PendingScenes = new HashSet<int>();

		private static bool initialized;
	}

	internal sealed class BoneIslandBfoDisplay : MonoBehaviour
	{
		internal void Initialize(ShipItem newItem, Scene newHomeScene)
		{
			this.homeScene = newHomeScene;
			this.item = newItem;
			this.itemBody = ((this.item != null) ? this.item.GetItemRigidbody() : null);
			this.saveable = ((this.item != null) ? this.item.GetComponent<SaveablePrefab>() : null);
		}

		private void Update()
		{
			if (this.item == null || !this.item.held)
			{
				return;
			}
			SaveablePrefab saveablePrefab = this.saveable;
			if (saveablePrefab != null)
			{
				saveablePrefab.RegisterToSave();
			}
			if (this.itemBody != null)
			{
				ScaledOarController component = this.item.GetComponent<ScaledOarController>();
				if (component != null)
				{
					component.SetPersistentKinematic(false);
				}
				else
				{
					this.itemBody.debugForceKinematic = false;
				}
				Rigidbody component2 = this.itemBody.GetComponent<Rigidbody>();
				if (component2 != null)
				{
					component2.useGravity = true;
				}
			}
			if (FloatingOriginManager.instance != null)
			{
				base.transform.SetParent(FloatingOriginManager.instance.transform, true);
			}
			this.released = true;
			base.enabled = false;
			ManualLogSource logSource = PowerfulOarPlugin.LogSource;
			if (logSource == null)
			{
				return;
			}
			logSource.LogInfo("Released the Bone Island BFO 5000 to normal held-item physics and saving.");
		}

		// The display is an owned item (sold) lying loose, so the game's ItemRigidbody.FixedUpdate destroys it once the camera
		// is more than 600 m away, while Bone Island's scene can stay loaded much farther out. Without this the BFO 5000 was
		// gone until the scene unloaded and loaded again. Queue the placement again: it waits for the camera within 500 m.
		// On a scene unload the queued placement finds the scene gone and stops. The scene is the one it was placed in: the game's
		// ShipItem.DestroyItem first calls ShipItem.ExitBoat, which parents the item to the world (another scene), so by now
		// gameObject.scene is no longer Bone Island's (po-bfo-final2-s3: not placed again).
		private void OnDestroy()
		{
			if (this.released)
			{
				return;
			}
			Scene scene = this.homeScene;
			if (!scene.IsValid() || !scene.isLoaded)
			{
				return;
			}
			ManualLogSource logSource = PowerfulOarPlugin.LogSource;
			if (logSource != null)
			{
				logSource.LogInfo("The Bone Island BFO 5000 display was removed while Bone Island stays loaded (the game removes loose owned items more than 600 m from the camera); it will be placed again when the camera is within 500 m.");
			}
			BoneIslandBfoPlacement.QueueAfterDisplayRemoved(scene);
		}

		private bool released;

		private Scene homeScene;

		private ShipItem item;

		private ItemRigidbody itemBody;

		private SaveablePrefab saveable;
	}

	// ---- Huge Oar purchase dialogue at Kicia Bay ----

	internal sealed class HugeOarDialogue
	{
		private HugeOarDialogue(GameObject newPanel, Transform newBubble)
		{
			this.panel = newPanel;
			this.bubble = newBubble;
		}

		internal static void Capture(TavernRumorsDude source)
		{
			if (HugeOarDialogue.template != null || source == null || source.speechUI == null || source.text == null || source.drinkButton == null)
			{
				return;
			}
			HugeOarDialogue.textPath = HugeOarDialogue.Path(source.speechUI.transform, source.text.transform);
			HugeOarDialogue.buttonPath = HugeOarDialogue.Path(source.speechUI.transform, source.drinkButton.transform);
			HugeOarDialogue.template = Object.Instantiate<GameObject>(source.speechUI);
			HugeOarDialogue.template.name = "PowerfulOar Huge Oar dialogue template";
			HugeOarDialogue.template.SetActive(false);
			HugeOarDialogue.template.transform.SetParent(null, false);
			HugeOarDialogue.template.transform.localScale = Vector3.one;
			MonoBehaviour[] componentsInChildren = HugeOarDialogue.template.GetComponentsInChildren<MonoBehaviour>(true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				Object.DestroyImmediate(componentsInChildren[i]);
			}
			if (Application.isPlaying)
			{
				Object.DontDestroyOnLoad(HugeOarDialogue.template);
			}
		}

		internal static HugeOarDialogue Create(Transform parent, string content)
		{
			if (HugeOarDialogue.template == null)
			{
				foreach (TavernRumorsDude tavernRumorsDude in Resources.FindObjectsOfTypeAll<TavernRumorsDude>())
				{
					if (tavernRumorsDude.gameObject.scene.IsValid())
					{
						HugeOarDialogue.Capture(tavernRumorsDude);
						if (HugeOarDialogue.template != null)
						{
							break;
						}
					}
				}
			}
			if (HugeOarDialogue.template == null)
			{
				return null;
			}
			GameObject gameObject = Object.Instantiate<GameObject>(HugeOarDialogue.template, parent, false);
			gameObject.name = "Huge Oar purchase dialogue";
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localRotation = Quaternion.identity;
			gameObject.transform.localScale = Vector3.one;
			TextMesh component = gameObject.transform.Find(HugeOarDialogue.textPath).GetComponent<TextMesh>();
			Transform parent2 = component.transform.parent;
			Transform transform = parent2.Find("gfx");
			Transform transform2 = gameObject.transform.Find(HugeOarDialogue.buttonPath);
			gameObject.SetActive(true);
			transform2.gameObject.SetActive(false);
			Renderer component2 = component.GetComponent<Renderer>();
			Vector3 size = component2.bounds.size;
			Vector3 size2 = transform.GetComponent<Renderer>().bounds.size;
			string text = HugeOarDialogue.Wrap(content, 40);
			component.font.RequestCharactersInTexture(text, component.fontSize, component.fontStyle);
			component.text = text;
			Vector3 size3 = component2.bounds.size;
			Vector3 localScale = transform.localScale;
			localScale.x *= Mathf.Max(0.35f, (size2.x + size3.x - size.x) / Mathf.Max(0.001f, size2.x));
			localScale.y *= Mathf.Max(1f, (size2.y + size3.y - size.y) / Mathf.Max(0.001f, size2.y));
			transform.localScale = localScale;
			Collider[] componentsInChildren = gameObject.GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
			gameObject.SetActive(false);
			return new HugeOarDialogue(gameObject, parent2);
		}

		internal static void Shutdown()
		{
			if (HugeOarDialogue.template != null)
			{
				Object.Destroy(HugeOarDialogue.template);
				HugeOarDialogue.template = null;
			}
			HugeOarDialogue.textPath = null;
			HugeOarDialogue.buttonPath = null;
		}

		internal void Show(Transform anchor)
		{
			if (this.panel == null || anchor == null || Refs.observerMirror == null)
			{
				return;
			}
			Transform transform = Refs.observerMirror.transform;
			Vector3 normalized = Vector3.ProjectOnPlane(transform.position - anchor.position, Vector3.up).normalized;
			Vector3 position = anchor.position + normalized * 0.75f;
			position.y = transform.position.y + 1.2f - this.bubble.localPosition.y;
			this.panel.transform.SetPositionAndRotation(position, transform.rotation);
			this.panel.SetActive(true);
		}

		internal void Hide()
		{
			if (this.panel != null)
			{
				this.panel.SetActive(false);
			}
		}

		internal void Dispose()
		{
			if (this.panel != null)
			{
				Object.Destroy(this.panel);
			}
		}

		private static string Path(Transform root, Transform child)
		{
			List<string> list = new List<string>();
			while (child != null && child != root)
			{
				list.Add(child.name);
				child = child.parent;
			}
			if (child != root)
			{
				throw new InvalidOperationException("Dialogue element is outside the speech UI.");
			}
			list.Reverse();
			return string.Join("/", list.ToArray());
		}

		private static string Wrap(string value, int width)
		{
			value = value.TrimStart(Array.Empty<char>());
			if (value.Length <= width)
			{
				return value;
			}
			int num = value.LastIndexOf(' ', width);
			if (num < 1)
			{
				num = width;
			}
			return value.Substring(0, num) + "\n" + HugeOarDialogue.Wrap(value.Substring(num), width);
		}

		private static GameObject template;

		private static string textPath;

		private static string buttonPath;

		private readonly GameObject panel;

		private readonly Transform bubble;
	}

	[HarmonyPatch(typeof(TavernRumorsDude), "Awake")]
	internal static class HugeOarDialogueCapturePatch
	{
		[HarmonyPostfix]
		private static void Postfix(TavernRumorsDude __instance)
		{
			HugeOarDialogue.Capture(__instance);
		}
	}

	internal sealed class HugeOarPurchaseDialogueArea : MonoBehaviour
	{
		internal static HugeOarPurchaseDialogueArea Create(Transform source)
		{
			GameObject gameObject = new GameObject("PowerfulOar Huge Oar purchase dialogue trigger");
			HugeOarPurchaseDialogueArea result;
			try
			{
				gameObject.SetActive(false);
				SceneManager.MoveGameObjectToScene(gameObject, source.gameObject.scene);
				gameObject.layer = 2;
				gameObject.transform.position = source.position;
				gameObject.transform.rotation = Quaternion.identity;
				gameObject.transform.localScale = Vector3.one;
				Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
				rigidbody.isKinematic = true;
				rigidbody.useGravity = false;
				SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
				sphereCollider.radius = 4f;
				sphereCollider.isTrigger = true;
				HugeOarPurchaseDialogueArea hugeOarPurchaseDialogueArea = gameObject.AddComponent<HugeOarPurchaseDialogueArea>();
				hugeOarPurchaseDialogueArea.source = source;
				gameObject.SetActive(true);
				result = hugeOarPurchaseDialogueArea;
			}
			catch
			{
				gameObject.SetActive(false);
				Object.Destroy(gameObject);
				throw;
			}
			return result;
		}

		internal void ShowPurchase(string newContent)
		{
			this.content = newContent;
			this.pending = true;
			this.retry = 0f;
			this.missingDialogueLogged = false;
			HugeOarDialogue hugeOarDialogue = this.dialogue;
			if (hugeOarDialogue != null)
			{
				hugeOarDialogue.Dispose();
			}
			this.dialogue = null;
			float num = this.ObserverDistance();
			this.open = (num <= 4f);
		}

		private void Awake()
		{
			this.body = base.GetComponent<Rigidbody>();
			this.trigger = base.GetComponent<Collider>();
		}

		private void FixedUpdate()
		{
			if (this.source == null)
			{
				Object.Destroy(base.gameObject);
				return;
			}
			this.trigger.enabled = this.source.gameObject.activeInHierarchy;
			if (!this.trigger.enabled || GameState.currentlyLoading)
			{
				this.players.Clear();
			}
			this.body.position = this.source.position;
			this.body.rotation = Quaternion.identity;
		}

		private void Update()
		{
			this.players.RemoveWhere((Collider player) => player == null || !player.gameObject.activeInHierarchy);
			if (!this.pending || !this.Available())
			{
				HugeOarDialogue hugeOarDialogue = this.dialogue;
				if (hugeOarDialogue == null)
				{
					return;
				}
				hugeOarDialogue.Hide();
				return;
			}
			else
			{
				float num = this.ObserverDistance();
				if (!this.open && this.players.Count > 0 && num <= 4f)
				{
					this.open = true;
				}
				if (this.open && num > 5f)
				{
					this.ClosePurchase();
					return;
				}
				if (!this.open)
				{
					HugeOarDialogue hugeOarDialogue2 = this.dialogue;
					if (hugeOarDialogue2 == null)
					{
						return;
					}
					hugeOarDialogue2.Hide();
					return;
				}
				else
				{
					if (this.dialogue == null && Time.unscaledTime >= this.retry)
					{
						this.retry = Time.unscaledTime + 0.5f;
						this.dialogue = HugeOarDialogue.Create(base.transform, this.content);
						if (this.dialogue == null && !this.missingDialogueLogged)
						{
							this.missingDialogueLogged = true;
							ManualLogSource logSource = PowerfulOarPlugin.LogSource;
							if (logSource != null)
							{
								logSource.LogError("Huge Oar was purchased, but the vanilla dialogue template is not available yet; retrying.");
							}
						}
					}
					HugeOarDialogue hugeOarDialogue3 = this.dialogue;
					if (hugeOarDialogue3 == null)
					{
						return;
					}
					hugeOarDialogue3.Show(this.source);
					return;
				}
			}
		}

		private void OnTriggerEnter(Collider other)
		{
			this.Observe(other);
		}

		private void OnTriggerStay(Collider other)
		{
			this.Observe(other);
		}

		private void OnTriggerExit(Collider other)
		{
			this.players.Remove(other);
		}

		private void OnDisable()
		{
			HugeOarDialogue hugeOarDialogue = this.dialogue;
			if (hugeOarDialogue == null)
			{
				return;
			}
			hugeOarDialogue.Hide();
		}

		private void OnDestroy()
		{
			HugeOarDialogue hugeOarDialogue = this.dialogue;
			if (hugeOarDialogue != null)
			{
				hugeOarDialogue.Dispose();
			}
			this.dialogue = null;
		}

		private void Observe(Collider other)
		{
			if (other.CompareTag("Player"))
			{
				this.players.Add(other);
			}
		}

		private bool Available()
		{
			return this.source != null && this.source.gameObject.activeInHierarchy && Refs.observerMirror != null && !GameState.currentlyLoading && !GameState.justStarted && !GameState.recovering && !GameState.sleeping;
		}

		private float ObserverDistance()
		{
			if (!(Refs.observerMirror != null) || !(this.source != null))
			{
				return float.PositiveInfinity;
			}
			return Vector3.Distance(Refs.observerMirror.transform.position, this.source.position);
		}

		private void ClosePurchase()
		{
			this.pending = false;
			this.open = false;
			this.content = null;
			HugeOarDialogue hugeOarDialogue = this.dialogue;
			if (hugeOarDialogue != null)
			{
				hugeOarDialogue.Dispose();
			}
			this.dialogue = null;
		}

		private const float OpenDistance = 4f;

		private const float CloseDistance = 5f;

		private readonly HashSet<Collider> players = new HashSet<Collider>();

		private Transform source;

		private Rigidbody body;

		private Collider trigger;

		private HugeOarDialogue dialogue;

		private string content;

		private float retry;

		private bool pending;

		private bool open;

		private bool missingDialogueLogged;
	}

	[HarmonyPatch(typeof(Shopkeeper), "SellItem")]
	internal static class HugeOarPurchasePatch
	{
		[HarmonyPostfix]
		private static void Postfix(Shopkeeper __instance, ShipItem item)
		{
			if (!HugeOarVendorPatch.IsTarget(__instance) || OarStats.GetPrefabIndex(item) != 603)
			{
				return;
			}
			int purchaseCount = HugeOarPurchaseState.RecordPurchase();
			HugeOarVendorInstaller component = __instance.GetComponent<HugeOarVendorInstaller>();
			if (component != null)
			{
				component.ShowPurchaseReaction(purchaseCount);
			}
			ManualLogSource logSource = PowerfulOarPlugin.LogSource;
			if (logSource == null)
			{
				return;
			}
			logSource.LogInfo("Kicia Bay Huge Oar purchase count is now " + purchaseCount.ToString() + ".");
		}
	}

	internal static class HugeOarPurchaseState
	{
		internal static void Reset()
		{
			HugeOarPurchaseState.purchaseCount = 0;
		}

		internal static void Load()
		{
			HugeOarPurchaseState.Reset();
			string s;
			if (GameState.modData == null || !GameState.modData.TryGetValue("DogEggz.PowerfulOar.HugeOarPurchases.v1", out s))
			{
				return;
			}
			int num;
			if (int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out num) && num >= 0)
			{
				HugeOarPurchaseState.purchaseCount = num;
				return;
			}
			ManualLogSource logSource = PowerfulOarPlugin.LogSource;
			if (logSource == null)
			{
				return;
			}
			logSource.LogWarning("Ignored invalid Huge Oar purchase count in this save.");
		}

		internal static int RecordPurchase()
		{
			if (HugeOarPurchaseState.purchaseCount < 2147483647)
			{
				HugeOarPurchaseState.purchaseCount++;
			}
			HugeOarPurchaseState.Store();
			return HugeOarPurchaseState.purchaseCount;
		}

		internal static string GetDialogue(int count)
		{
			if (count <= 1)
			{
				return "...What do you even need this for?";
			}
			if (count == 2)
			{
				return "These things actually sell? I'm a genius!";
			}
			return "I'm gonna need a lot more logs...";
		}

		internal static void Store()
		{
			if (GameState.modData == null)
			{
				GameState.modData = new Dictionary<string, string>();
			}
			GameState.modData["DogEggz.PowerfulOar.HugeOarPurchases.v1"] = HugeOarPurchaseState.purchaseCount.ToString(CultureInfo.InvariantCulture);
		}

		internal static void ClearBeforeLoad()
		{
			HugeOarPurchaseState.Reset();
			Dictionary<string, string> modData = GameState.modData;
			if (modData == null)
			{
				return;
			}
			modData.Remove("DogEggz.PowerfulOar.HugeOarPurchases.v1");
		}

		private const string SaveKey = "DogEggz.PowerfulOar.HugeOarPurchases.v1";

		private static int purchaseCount;
	}

	[HarmonyPatch(typeof(SaveLoadManager), "Awake")]
	internal static class HugeOarPurchaseSessionPatch
	{
		[HarmonyPrefix]
		private static void Prefix()
		{
			HugeOarPurchaseState.Reset();
		}
	}

	[HarmonyPatch(typeof(SaveLoadManager), "LoadGame")]
	internal static class HugeOarPurchaseLoadGamePatch
	{
		[HarmonyPrefix]
		private static void Prefix()
		{
			HugeOarPurchaseState.ClearBeforeLoad();
		}
	}

	[HarmonyPatch(typeof(SaveLoadManager), "LoadModData")]
	internal static class HugeOarPurchaseLoadPatch
	{
		[HarmonyPostfix]
		private static void Postfix()
		{
			HugeOarPurchaseState.Load();
		}
	}

	[HarmonyPatch(typeof(SaveLoadManager), "SaveModData")]
	internal static class HugeOarPurchaseStorePatch
	{
		[HarmonyPrefix]
		private static void Prefix()
		{
			HugeOarPurchaseState.Store();
		}
	}
}
