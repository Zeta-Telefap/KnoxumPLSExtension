using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using PlusLevelStudio.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnoxumPLSExtension.Features
{
    public class KnoxumRampSelectionUI : MonoBehaviour
    {
        private static KnoxumRampSelectionUI instance;

        private GameObject internalPanel;
        private (int x, int z)? selectedRampOwner;

        public Image heightUpImg;
        public Image heightDownImg;
        private TextMeshProUGUI heightValueText;

        public Sprite spUpNormal;
        public Sprite spUpHover;
        public Sprite spUpLimit;
        public Sprite spDownNormal;
        public Sprite spDownHover;
        public Sprite spDownLimit;

        public bool isUpLimit;
        public bool isDownLimit;

        public static KnoxumRampSelectionUI Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("KnoxumRampUI_Global");
                    instance = go.AddComponent<KnoxumRampSelectionUI>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        public bool IsVisible
        {
            get
            {
                return internalPanel != null && internalPanel.activeSelf;
            }
        }

        public void ShowForRampCell(int x, int z)
        {
            (int x, int z) ownerKey;
            KnoxumRampData ramp;
            if (!HighWallsObjects.TryGetRampOwnerAtCell(x, z, out ownerKey, out ramp))
            {
                Hide();
                return;
            }

            selectedRampOwner = ownerKey;
            EnsurePanel();

            if (internalPanel == null)
                return;

            internalPanel.SetActive(true);
            RefreshSprites();
        }

        public void Hide()
        {
            selectedRampOwner = null;
            if (internalPanel != null)
                internalPanel.SetActive(false);
        }

        private bool TryResolveSelectedRamp(out int ownerX, out int ownerZ, out KnoxumRampData ramp)
        {
            ownerX = 0;
            ownerZ = 0;
            ramp = default(KnoxumRampData);

            if (!selectedRampOwner.HasValue)
                return false;

            ownerX = selectedRampOwner.Value.x;
            ownerZ = selectedRampOwner.Value.z;
            return HighWallsObjects.TryGetRamp(ownerX, ownerZ, out ramp);
        }

        private void EnsurePanel()
        {
            EditorController editor = Singleton<EditorController>.Instance;
            if (editor == null || editor.canvas == null)
                return;

            if (internalPanel != null)
            {
                internalPanel.transform.SetParent(editor.canvas.transform, false);
                return;
            }

            string baseDir = Path.Combine(Application.streamingAssetsPath, "Modded", "knoxum.moddinghq.plsextension", "UI", "Editor");

            spUpNormal = LoadSprite(Path.Combine(baseDir, "UpArrow.png"));
            spUpHover = LoadSprite(Path.Combine(baseDir, "UpArrow_Hover.png"));
            spUpLimit = LoadSprite(Path.Combine(baseDir, "UpArrow_Limit.png"));
            spDownNormal = LoadSprite(Path.Combine(baseDir, "DownArrow.png"));
            spDownHover = LoadSprite(Path.Combine(baseDir, "DownArrow_Hover.png"));
            spDownLimit = LoadSprite(Path.Combine(baseDir, "DownArrow_Limit.png"));

            internalPanel = new GameObject("KnoxumRampInjectedPanel");
            internalPanel.transform.SetParent(editor.canvas.transform, false);

            RectTransform pRt = internalPanel.AddComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.anchoredPosition = new Vector2(-285f, 0f);
            pRt.sizeDelta = new Vector2(130f, 110f);

            CreateTip(internalPanel.transform, "RampH_Tip", LoadSprite(Path.Combine(baseDir, "RampHeightTip.png")), 0f);
            heightValueText = CreateText(internalPanel.transform, "RampH_Val", 0f);
            heightUpImg = CreateArrow(internalPanel.transform, "RampH_Up", 0f, 24f, true);
            heightDownImg = CreateArrow(internalPanel.transform, "RampH_Down", 0f, -24f, false);
        }

        private Sprite LoadSprite(string path)
        {
            if (!File.Exists(path))
                return Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            byte[] fileBytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (ImageConversion.LoadImage(tex, fileBytes))
            {
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }

            return Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        }

        private void CreateTip(Transform parent, string name, Sprite sprite, float y)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.rectTransform.sizeDelta = new Vector2(32f, 32f);
            img.rectTransform.anchoredPosition = new Vector2(-10f, y);
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, float y)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(40f, 30f);
            text.rectTransform.anchoredPosition = new Vector2(30f, y);
            text.color = Color.black;
            return text;
        }

        private Image CreateArrow(Transform parent, string name, float yBase, float yOff, bool isUp)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<CanvasRenderer>();
            go.tag = "Button";

            Image img = go.AddComponent<Image>();
            img.rectTransform.sizeDelta = new Vector2(20f, 20f);
            img.rectTransform.anchoredPosition = new Vector2(30f, yBase + yOff);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.transition = Button.Transition.None;
            button.onClick.AddListener(delegate
            {
                int ownerX;
                int ownerZ;
                KnoxumRampData ramp;
                if (!TryResolveSelectedRamp(out ownerX, out ownerZ, out ramp))
                {
                    Hide();
                    return;
                }

                int newHeight = Mathf.Clamp(ramp.riseSteps + (isUp ? 1 : -1), 1, 10);
                HighWallsController.currentEditorRampHeightSelection = newHeight;
                HighWallsObjects.SetRamp(ownerX, ownerZ, ramp.upDirection, ramp.length, newHeight, true);
                RefreshSprites();
            });

            return img;
        }

        public void RefreshSprites()
        {
            int ownerX;
            int ownerZ;
            KnoxumRampData ramp;
            if (!TryResolveSelectedRamp(out ownerX, out ownerZ, out ramp))
            {
                Hide();
                return;
            }

            HighWallsController.currentEditorRampHeightSelection = ramp.riseSteps;

            if (heightValueText != null)
                heightValueText.text = ramp.riseSteps.ToString();

            isUpLimit = ramp.riseSteps >= 10;
            isDownLimit = ramp.riseSteps <= 1;

            if (heightUpImg != null)
                heightUpImg.sprite = isUpLimit ? spUpLimit : spUpNormal;

            if (heightDownImg != null)
                heightDownImg.sprite = isDownLimit ? spDownLimit : spDownNormal;
        }
    }

    [HarmonyPatch(typeof(CursorController), "Update")]
    internal static class KnoxumRampCursorFixPatch
    {
        private static GameObject lastHovered;

        private static void Postfix(CursorController __instance)
        {
            var ui = KnoxumRampSelectionUI.Instance;
            if (ui == null || !ui.IsVisible || ui.heightUpImg == null)
                return;

            var results = (List<UnityEngine.EventSystems.RaycastResult>)AccessTools.Field(typeof(CursorController), "results").GetValue(__instance);
            if (results == null || results.Count == 0)
            {
                ResetHover(ui);
                return;
            }

            GameObject currentHit = results[0].gameObject;
            if (currentHit == null)
            {
                ResetHover(ui);
                return;
            }

            if (currentHit != lastHovered)
            {
                ResetHover(ui);
                lastHovered = currentHit;

                if (currentHit == ui.heightUpImg.gameObject && !ui.isUpLimit)
                    ui.heightUpImg.sprite = ui.spUpHover;
                else if (currentHit == ui.heightDownImg.gameObject && !ui.isDownLimit)
                    ui.heightDownImg.sprite = ui.spDownHover;
            }

            string clickId = (string)AccessTools.Field(typeof(CursorController), "clickId").GetValue(__instance);
            if (Singleton<InputManager>.Instance.GetDigitalInput(clickId, true))
            {
                Button b = currentHit.GetComponent<Button>();
                if (b != null)
                {
                    b.onClick.Invoke();
                    var audConfirm = (SoundObject)AccessTools.Field(typeof(CursorController), "audConfirm").GetValue(__instance);
                    if (audConfirm != null)
                        Singleton<MusicManager>.Instance.PlaySoundEffect(audConfirm);
                }
            }
        }

        private static void ResetHover(KnoxumRampSelectionUI ui)
        {
            if (lastHovered != null)
            {
                lastHovered = null;
                ui.RefreshSprites();
            }
        }
    }

    [HarmonyPatch(typeof(Selector), "DisableSelection")]
    internal static class KnoxumRampSelectionHidePatch
    {
        private static void Postfix()
        {
            KnoxumRampSelectionUI.Instance.Hide();
        }
    }

    [HarmonyPatch(typeof(EditorController), "RefreshCells")]
    internal static class KnoxumRampSelectionRefreshPatch
    {
        private static void Postfix(EditorController __instance)
        {
            if (__instance == null)
                return;

            if (KnoxumRampSelectionUI.Instance.IsVisible)
                KnoxumRampSelectionUI.Instance.RefreshSprites();
        }
    }
}
