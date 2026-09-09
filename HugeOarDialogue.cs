using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PowerfulOar
{
    internal static class HugeOarPurchaseState
    {
        private const string SaveKey =
            "DogEggz.PowerfulOar.HugeOarPurchases.v1";

        private static int purchaseCount;

        internal static void Reset()
        {
            purchaseCount = 0;
        }

        internal static void Load()
        {
            Reset();
            string data;
            if (GameState.modData == null ||
                !GameState.modData.TryGetValue(SaveKey, out data))
            {
                return;
            }

            int loadedCount;
            if (!int.TryParse(
                    data,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out loadedCount) ||
                loadedCount < 0)
            {
                PowerfulOarPlugin.LogSource?.LogWarning(
                    "Ignored invalid Huge Oar purchase count in this save.");
                return;
            }

            purchaseCount = loadedCount;
        }

        internal static int RecordPurchase()
        {
            if (purchaseCount < int.MaxValue)
            {
                purchaseCount++;
            }

            Store();
            return purchaseCount;
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

            GameState.modData[SaveKey] =
                purchaseCount.ToString(CultureInfo.InvariantCulture);
        }

        internal static void ClearBeforeLoad()
        {
            Reset();
            GameState.modData?.Remove(SaveKey);
        }
    }

    // Uses a sanitized clone of Sailwind's TavernRumorsDude speech UI, matching
    // the Wind Totem quest dialogue layout without depending on that mod.
    internal sealed class HugeOarDialogue
    {
        private static GameObject template;
        private static string textPath;
        private static string buttonPath;

        private readonly GameObject panel;
        private readonly Transform bubble;

        private HugeOarDialogue(GameObject newPanel, Transform newBubble)
        {
            panel = newPanel;
            bubble = newBubble;
        }

        internal static void Capture(TavernRumorsDude source)
        {
            if (template != null || source == null || source.speechUI == null ||
                source.text == null || source.drinkButton == null)
            {
                return;
            }

            textPath = Path(source.speechUI.transform, source.text.transform);
            buttonPath = Path(
                source.speechUI.transform,
                source.drinkButton.transform);
            template = Object.Instantiate(source.speechUI);
            template.name = "PowerfulOar Huge Oar dialogue template";
            template.SetActive(false);
            template.transform.SetParent(null, false);
            template.transform.localScale = Vector3.one;

            // The copied GPButtonInterface still targets the tavern NPC. Remove
            // every copied behaviour so this informational bubble cannot invoke it.
            foreach (MonoBehaviour script in
                     template.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(script);
            }

            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(template);
            }
        }

        internal static HugeOarDialogue Create(
            Transform parent,
            string content)
        {
            if (template == null)
            {
                foreach (TavernRumorsDude source in
                         Resources.FindObjectsOfTypeAll<TavernRumorsDude>())
                {
                    if (source.gameObject.scene.IsValid())
                    {
                        Capture(source);
                        if (template != null)
                        {
                            break;
                        }
                    }
                }
            }

            if (template == null)
            {
                return null;
            }

            GameObject newPanel = Object.Instantiate(template, parent, false);
            newPanel.name = "Huge Oar purchase dialogue";
            newPanel.transform.localPosition = Vector3.zero;
            newPanel.transform.localRotation = Quaternion.identity;
            newPanel.transform.localScale = Vector3.one;

            TextMesh text =
                newPanel.transform.Find(textPath).GetComponent<TextMesh>();
            Transform newBubble = text.transform.parent;
            Transform graphics = newBubble.Find("gfx");
            Transform button = newPanel.transform.Find(buttonPath);

            newPanel.SetActive(true);
            button.gameObject.SetActive(false);

            Renderer textRenderer = text.GetComponent<Renderer>();
            Vector3 oldTextSize = textRenderer.bounds.size;
            Vector3 oldBubbleSize = graphics.GetComponent<Renderer>().bounds.size;
            string wrapped = Wrap(content, 40);
            text.font.RequestCharactersInTexture(
                wrapped,
                text.fontSize,
                text.fontStyle);
            text.text = wrapped;
            Vector3 newTextSize = textRenderer.bounds.size;
            Vector3 scale = graphics.localScale;
            scale.x *= Mathf.Max(
                0.35f,
                (oldBubbleSize.x + newTextSize.x - oldTextSize.x) /
                Mathf.Max(0.001f, oldBubbleSize.x));
            scale.y *= Mathf.Max(
                1f,
                (oldBubbleSize.y + newTextSize.y - oldTextSize.y) /
                Mathf.Max(0.001f, oldBubbleSize.y));
            graphics.localScale = scale;

            foreach (Collider collider in
                     newPanel.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            newPanel.SetActive(false);
            return new HugeOarDialogue(newPanel, newBubble);
        }

        internal static void Shutdown()
        {
            if (template != null)
            {
                Object.Destroy(template);
                template = null;
            }

            textPath = null;
            buttonPath = null;
        }

        internal void Show(Transform anchor)
        {
            if (panel == null || anchor == null || Refs.observerMirror == null)
            {
                return;
            }

            Transform observer = Refs.observerMirror.transform;
            Vector3 toward = Vector3.ProjectOnPlane(
                observer.position - anchor.position,
                Vector3.up).normalized;
            Vector3 position = anchor.position + toward * 0.75f;
            position.y = observer.position.y + 1.2f - bubble.localPosition.y;
            panel.transform.SetPositionAndRotation(position, observer.rotation);
            panel.SetActive(true);
        }

        internal void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        internal void Dispose()
        {
            if (panel != null)
            {
                Object.Destroy(panel);
            }
        }

        private static string Path(Transform root, Transform child)
        {
            List<string> names = new List<string>();
            while (child != null && child != root)
            {
                names.Add(child.name);
                child = child.parent;
            }

            if (child != root)
            {
                throw new InvalidOperationException(
                    "Dialogue element is outside the speech UI.");
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string Wrap(string value, int width)
        {
            value = value.TrimStart();
            if (value.Length <= width)
            {
                return value;
            }

            int cut = value.LastIndexOf(' ', width);
            if (cut < 1)
            {
                cut = width;
            }

            return value.Substring(0, cut) + "\n" +
                   Wrap(value.Substring(cut), width);
        }
    }

    internal sealed class HugeOarPurchaseDialogueArea : MonoBehaviour
    {
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

        internal static HugeOarPurchaseDialogueArea Create(Transform source)
        {
            GameObject area = new GameObject(
                "PowerfulOar Huge Oar purchase dialogue trigger");
            try
            {
                area.SetActive(false);
                SceneManager.MoveGameObjectToScene(area, source.gameObject.scene);
                area.layer = 2;
                area.transform.position = source.position;
                area.transform.rotation = Quaternion.identity;
                area.transform.localScale = Vector3.one;

                Rigidbody body = area.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                SphereCollider sphere = area.AddComponent<SphereCollider>();
                sphere.radius = OpenDistance;
                sphere.isTrigger = true;

                HugeOarPurchaseDialogueArea controller =
                    area.AddComponent<HugeOarPurchaseDialogueArea>();
                controller.source = source;
                area.SetActive(true);
                return controller;
            }
            catch
            {
                area.SetActive(false);
                Object.Destroy(area);
                throw;
            }
        }

        internal void ShowPurchase(string newContent)
        {
            content = newContent;
            pending = true;
            retry = 0f;
            missingDialogueLogged = false;

            dialogue?.Dispose();
            dialogue = null;

            float distance = ObserverDistance();
            open = distance <= OpenDistance;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            trigger = GetComponent<Collider>();
        }

        private void FixedUpdate()
        {
            if (source == null)
            {
                Destroy(gameObject);
                return;
            }

            trigger.enabled = source.gameObject.activeInHierarchy;
            if (!trigger.enabled || GameState.currentlyLoading)
            {
                players.Clear();
            }

            body.position = source.position;
            body.rotation = Quaternion.identity;
        }

        private void Update()
        {
            players.RemoveWhere(
                player => player == null || !player.gameObject.activeInHierarchy);

            if (!pending || !Available())
            {
                dialogue?.Hide();
                return;
            }

            float distance = ObserverDistance();
            if (!open && players.Count > 0 && distance <= OpenDistance)
            {
                open = true;
            }

            if (open && distance > CloseDistance)
            {
                ClosePurchase();
                return;
            }

            if (!open)
            {
                dialogue?.Hide();
                return;
            }

            if (dialogue == null && Time.unscaledTime >= retry)
            {
                retry = Time.unscaledTime + 0.5f;
                dialogue = HugeOarDialogue.Create(transform, content);
                if (dialogue == null && !missingDialogueLogged)
                {
                    missingDialogueLogged = true;
                    PowerfulOarPlugin.LogSource?.LogError(
                        "Huge Oar was purchased, but the vanilla dialogue " +
                        "template is not available yet; retrying.");
                }
            }

            dialogue?.Show(source);
        }

        private void OnTriggerEnter(Collider other)
        {
            Observe(other);
        }

        private void OnTriggerStay(Collider other)
        {
            Observe(other);
        }

        private void OnTriggerExit(Collider other)
        {
            players.Remove(other);
        }

        private void OnDisable()
        {
            dialogue?.Hide();
        }

        private void OnDestroy()
        {
            dialogue?.Dispose();
            dialogue = null;
        }

        private void Observe(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                players.Add(other);
            }
        }

        private bool Available()
        {
            return source != null && source.gameObject.activeInHierarchy &&
                   Refs.observerMirror != null && !GameState.currentlyLoading &&
                   !GameState.justStarted && !GameState.recovering &&
                   !GameState.sleeping;
        }

        private float ObserverDistance()
        {
            return Refs.observerMirror != null && source != null
                ? Vector3.Distance(
                    Refs.observerMirror.transform.position,
                    source.position)
                : float.PositiveInfinity;
        }

        private void ClosePurchase()
        {
            pending = false;
            open = false;
            content = null;
            dialogue?.Dispose();
            dialogue = null;
        }
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

    [HarmonyPatch(typeof(Shopkeeper), "SellItem")]
    internal static class HugeOarPurchasePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Shopkeeper __instance, ShipItem item)
        {
            if (!HugeOarVendorPatch.IsTarget(__instance) ||
                OarStats.GetPrefabIndex(item) !=
                    ScaledOarFactory.HugeOarPrefabIndex)
            {
                return;
            }

            int purchaseCount = HugeOarPurchaseState.RecordPurchase();
            HugeOarVendorInstaller installer =
                __instance.GetComponent<HugeOarVendorInstaller>();
            installer?.ShowPurchaseReaction(purchaseCount);

            PowerfulOarPlugin.LogSource?.LogInfo(
                "Kicia Bay Huge Oar purchase count is now " +
                purchaseCount + ".");
        }
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

    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadGame))]
    internal static class HugeOarPurchaseLoadGamePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            HugeOarPurchaseState.ClearBeforeLoad();
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadModData))]
    internal static class HugeOarPurchaseLoadPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            HugeOarPurchaseState.Load();
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.SaveModData))]
    internal static class HugeOarPurchaseStorePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            HugeOarPurchaseState.Store();
        }
    }
}
