using Cob.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Cob.Presentation.CardHouse
{
    [DisallowMultipleComponent]
    public sealed class CobCardHouseHud : MonoBehaviour
    {
        private CobGameEngine _engine;
        private Canvas _canvas;
        private Text _status;
        private Button _endTurn;
        private Button _restart;

        public void Bind(CobGameEngine engine)
        {
            _engine = engine;
            EnsureUi();
            Refresh();
        }

        public void Refresh()
        {
            if (_engine == null || _status == null) return;

            var p = _engine.State.player;
            var ai = _engine.State.ai;

            _status.text =
                $"Turn: {_engine.State.turn}  Current: {_engine.State.currentPlayer}\n" +
                $"Player HP: {p.hp}  Blessing: {p.blessingThisTurn - p.spentBlessing}  Mana: {p.manaThisTurn - p.spentMana}\n" +
                $"Hand: {p.hand.Count}  Deck: {p.deck.Count}  Discard: {p.discard.Count}  Played: {p.played.Count}  Attire: {p.attire.Count}\n" +
                $"AI HP: {ai.hp}  Hand: {ai.hand.Count}  Deck: {ai.deck.Count}  Discard: {ai.discard.Count}";
        }

        private void EnsureUi()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("CoB_HUD");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = CreatePanel(canvasGo.transform);
            _status = CreateText(panel, "Status", 18, TextAnchor.UpperLeft);

            var row = CreateRow(panel, "ButtonsRow");
            _endTurn = CreateButton(row, "End Turn");
            _restart = CreateButton(row, "Restart");
        }

        private static RectTransform CreatePanel(Transform parent)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(12f, -12f);
            rt.sizeDelta = new Vector2(520f, 210f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        private static RectTransform CreateRow(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, 36f);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return rt;
        }

        private static Text CreateText(RectTransform parent, string name, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, 140f);
            return t;
        }

        private static Button CreateButton(RectTransform parent, string title)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.9f);

            var btn = go.GetComponent<Button>();

            var labelGo = new GameObject("Text", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = title;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.black;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(160f, 36f);

            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return btn;
        }

        public void WireButtons(System.Action onEndTurn, System.Action onRestart)
        {
            EnsureUi();
            _endTurn.onClick.RemoveAllListeners();
            _restart.onClick.RemoveAllListeners();
            if (onEndTurn != null) _endTurn.onClick.AddListener(() => onEndTurn());
            if (onRestart != null) _restart.onClick.AddListener(() => onRestart());
        }
    }
}

