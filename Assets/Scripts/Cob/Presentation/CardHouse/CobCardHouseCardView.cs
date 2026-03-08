using Cob.Runtime;
using Cob.Presentation.Art;
using TMPro;
using UnityEngine;

namespace Cob.Presentation.CardHouse
{
    [DisallowMultipleComponent]
    public sealed class CobCardHouseCardView : MonoBehaviour
    {
        [Header("Rendering")]
        [Tooltip("If true, treat sprite-sheet tiles as full card faces (TTS style).")]
        [SerializeField] private bool useFullCardSprite = true;

        [SerializeField] private string _debugName;
        [SerializeField] private int _instanceId;
        [SerializeField] private int _cardId;
        [SerializeField] private string _debugBody;

        private TextMesh _label;
        private TMP_Text _titleTmp;
        private TMP_Text _bodyTmp;
        private int _applyTmpFramesLeft;
        private SpriteRenderer _imageRenderer;
        private Transform _frontTransform;
        private SpriteRenderer _fullCardRenderer;

        public int InstanceId => _instanceId;
        public int CardId => _cardId;
        public string CardName => _debugName;

        public void Bind(CobCardInstance instance)
        {
            _instanceId = instance?.instanceId ?? 0;
            _cardId = instance?.Def?.card_id ?? 0;
            _debugName = instance?.Def?.name ?? "(null)";
            _debugBody = BuildBody(instance?.Def);

            // Clear demo visuals immediately, then load sprite from TTS sprite-sheet.
            EnsureFrontTransform();
            if (useFullCardSprite)
            {
                EnsureFullCardRenderer();
                if (_fullCardRenderer != null) _fullCardRenderer.sprite = null;
            }
            else
            {
                EnsureImageRenderer();
                if (_imageRenderer != null) _imageRenderer.sprite = null;
            }

            CobSpriteSheetCache.RequestCardSprite(instance?.Def, s =>
            {
                if (this == null) return;
                if (s == null) return;

                EnsureFrontTransform();
                if (useFullCardSprite)
                {
                    EnsureFullCardRenderer();
                    if (_fullCardRenderer != null)
                    {
                        _fullCardRenderer.sprite = s;
                        HideFrontOverlays();
                        DisableLabel();
                    }
                }
                else
                {
                    EnsureImageRenderer();
                    if (_imageRenderer != null) _imageRenderer.sprite = s;
                }
            });

            EnsureTmpBindings();
            ApplyTmpText();
            _applyTmpFramesLeft = 3;

            EnsureLabel();
            RefreshLabel();
        }

        private void EnsureFrontTransform()
        {
            if (_frontTransform != null) return;
            var all = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t != null && t.name == "Front")
                {
                    _frontTransform = t;
                    return;
                }
            }
        }

        private void EnsureFullCardRenderer()
        {
            if (_fullCardRenderer != null) return;
            if (_frontTransform == null) return;

            var existing = _frontTransform.Find("CoB_FullCard");
            if (existing != null)
            {
                _fullCardRenderer = existing.GetComponent<SpriteRenderer>();
                if (_fullCardRenderer != null) return;
            }

            // Try to copy placement from the demo "Frame" (same plane/size).
            var refSr = FindFrontSpriteRendererByName("Frame") ?? FindFrontSpriteRendererByName("ImageBackground") ?? FindFrontSpriteRendererByName("Image");

            var go = new GameObject("CoB_FullCard");
            go.transform.SetParent(_frontTransform, false);
            if (refSr != null)
            {
                go.transform.localPosition = refSr.transform.localPosition;
                go.transform.localRotation = refSr.transform.localRotation;
                go.transform.localScale = refSr.transform.localScale;
            }
            else
            {
                go.transform.localPosition = new Vector3(0f, 0f, 0.03f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            _fullCardRenderer = go.AddComponent<SpriteRenderer>();
            _fullCardRenderer.color = Color.white;
        }

        private SpriteRenderer FindFrontSpriteRendererByName(string name)
        {
            if (_frontTransform == null) return null;
            var srs = _frontTransform.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (sr != null && sr.gameObject.name == name) return sr;
            }
            return null;
        }

        private void HideFrontOverlays()
        {
            if (_frontTransform == null) return;

            // Disable all TMP text + sprite layers except our full-card renderer.
            var tmps = _frontTransform.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < tmps.Length; i++)
            {
                var t = tmps[i];
                if (t != null) t.enabled = false;
            }

            var srs = _frontTransform.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (sr == null) continue;
                if (sr == _fullCardRenderer) continue;
                sr.enabled = false;
            }
        }

        private void DisableLabel()
        {
            if (_label != null) _label.gameObject.SetActive(false);
        }

        private void EnsureImageRenderer()
        {
            if (_imageRenderer != null) return;
            var srs = GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (sr == null) continue;
                // CcgCard prefab uses a SpriteRenderer GameObject named "Image" for the main art.
                if (sr.gameObject.name == "Image")
                {
                    _imageRenderer = sr;
                    return;
                }
            }
        }

        private void LateUpdate()
        {
            // Some sample/demo scripts may apply their own card text on Start/first frames.
            // Re-apply our CoB text for a few frames to guarantee the override.
            if (_applyTmpFramesLeft <= 0) return;
            _applyTmpFramesLeft--;
            EnsureTmpBindings();
            ApplyTmpText();
        }

        private void EnsureTmpBindings()
        {
            if (_titleTmp != null && _bodyTmp != null) return;

            var tmps = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < tmps.Length; i++)
            {
                var t = tmps[i];
                if (t == null) continue;
                if (_titleTmp == null && t.gameObject.name == "TitleText") _titleTmp = t;
                if (_bodyTmp == null && t.gameObject.name == "DescriptionText") _bodyTmp = t;
            }
        }

        private void ApplyTmpText()
        {
            if (_titleTmp != null) _titleTmp.text = _debugName;
            if (_bodyTmp != null) _bodyTmp.text = _debugBody;
        }

        private static string BuildBody(Cob.Data.CobCardRecord def)
        {
            if (def == null) return string.Empty;

            var parts = new System.Collections.Generic.List<string>(4);
            Add(parts, def.effect1);
            Add(parts, def.effect1text);
            Add(parts, def.effect2);
            Add(parts, def.effect2text);

            return string.Join("\n", parts);

            static void Add(System.Collections.Generic.List<string> list, string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return;
                var t = raw.Trim();
                if (t.Equals("none", System.StringComparison.OrdinalIgnoreCase)) return;
                list.Add(t);
            }
        }

        private void EnsureLabel()
        {
            if (_label != null) return;

            var labelGo = new GameObject("CoB_Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            labelGo.transform.localRotation = Quaternion.identity;
            labelGo.transform.localScale = Vector3.one;

            _label = labelGo.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontSize = 64;
            _label.characterSize = 0.0125f;
            _label.color = Color.black;
        }

        private void RefreshLabel()
        {
            if (_label == null) return;
            _label.text = _debugName;
        }
    }
}

