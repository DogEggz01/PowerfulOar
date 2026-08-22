using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PowerfulOar
{
    internal static class BoneIslandBfoPlacement
    {
        private const string BoneIslandSceneName = "island 36 ()";
        private const float SafeSpawnDistance = 500f;

        private static readonly Vector3 LocalPosition =
            new Vector3(232.5f, 17.88f, 431.6406f);
        private static readonly Quaternion LocalRotation =
            Quaternion.Euler(-0.415f, 116.637f, 81.422f);
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
                Vector3 placementPoint = scenery != null
                    ? scenery.TransformPoint(LocalPosition)
                    : Vector3.zero;
                if (GameState.playing && !GameState.currentlyLoading &&
                    scenery != null && camera != null &&
                    Vector3.Distance(camera.transform.position, placementPoint) <=
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
            if (item == null)
            {
                PowerfulOarPlugin.LogSource?.LogError(
                    "Could not place Bone Island BFO 5000: cloned ShipItem missing.");
                instance.SetActive(false);
                return;
            }

            instance.name = "BFO 5000 (Bone Island)";
            instance.transform.SetParent(scenery, false);
            instance.transform.localPosition = LocalPosition;
            instance.transform.localRotation = LocalRotation;

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
                "Placed one fixed BFO 5000 at the configured Bone Island " +
                "_scenery-local pose. It will enter normal item/save control when picked up. " +
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
}
