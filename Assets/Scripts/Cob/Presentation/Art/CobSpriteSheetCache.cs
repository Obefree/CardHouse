using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cob.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace Cob.Presentation.Art
{
    public sealed class CobSpriteSheetCache : MonoBehaviour
    {
        private const int MaxConcurrentDownloads = 4;

        [Header("Source")]
        [Tooltip("If enabled, first try to load sprite-sheets from StreamingAssets (offline, no 403/AccessDenied).")]
        [SerializeField] private bool tryLocalStreamingAssetsFirst = true;

        [Tooltip("Subfolder under StreamingAssets that contains sprite-sheet PNG files.")]
        [SerializeField] private string streamingAssetsSubfolder = "CoB/TTSFiles";

        [Tooltip("If false, remote downloads will be skipped (useful for offline testing).")]
        [SerializeField] private bool allowRemoteDownload = true;

        [Min(1)]
        [SerializeField] private int remoteTimeoutSeconds = 20;

        private readonly Dictionary<string, Texture2D> _textureByUrl = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Sprite> _spriteByKey = new Dictionary<string, Sprite>();

        private readonly Queue<IEnumerator> _queue = new Queue<IEnumerator>();
        private int _activeDownloads;

        private static CobSpriteSheetCache _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null) return;
            _instance = FindFirstObjectOfTypeCompat<CobSpriteSheetCache>();
            if (_instance != null) return;

            var go = new GameObject("CoB_SpriteSheetCache");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CobSpriteSheetCache>();
        }

        public static void RequestCardSprite(CobCardRecord def, Action<Sprite> onReady)
        {
            if (def == null)
            {
                onReady?.Invoke(null);
                return;
            }

            EnsureInstance();
            _instance.Enqueue(_instance.CoRequestCardSprite(def, onReady));
        }

        private void Enqueue(IEnumerator job)
        {
            _queue.Enqueue(job);
            TryPump();
        }

        private void TryPump()
        {
            while (_activeDownloads < MaxConcurrentDownloads && _queue.Count > 0)
            {
                var job = _queue.Dequeue();
                StartCoroutine(RunJob(job));
            }
        }

        private IEnumerator RunJob(IEnumerator job)
        {
            _activeDownloads++;
            yield return StartCoroutine(job);
            _activeDownloads--;
            TryPump();
        }

        private IEnumerator CoRequestCardSprite(CobCardRecord def, Action<Sprite> onReady)
        {
            if (string.IsNullOrWhiteSpace(def.sprite_url))
            {
                onReady?.Invoke(null);
                yield break;
            }

            var url = def.sprite_url.Trim();
            var numW = def.sprite_num_w > 0 ? def.sprite_num_w : 8;
            var numH = def.sprite_num_h > 0 ? def.sprite_num_h : 6;
            var cardId = def.card_id;

            var key = $"{url}|{cardId}|{numW}|{numH}";
            if (_spriteByKey.TryGetValue(key, out var cached) && cached != null)
            {
                onReady?.Invoke(cached);
                yield break;
            }

            if (!_textureByUrl.TryGetValue(url, out var tex) || tex == null)
            {
                // 1) Try local sprite-sheet file from StreamingAssets (offline friendly).
                if (tryLocalStreamingAssetsFirst)
                {
                    var localPath = TryResolveLocalSpriteSheetPath(url);
                    if (!string.IsNullOrWhiteSpace(localPath))
                    {
                        yield return StartCoroutine(CoLoadTextureFromFile(localPath, t =>
                        {
                            tex = t;
                        }));
                        if (tex != null)
                        {
                            tex.wrapMode = TextureWrapMode.Clamp;
                            _textureByUrl[url] = tex;
                        }
                    }
                }

                // 2) Fallback to remote download (may fail if Firebase token/ACL is invalid).
                if (tex == null)
                {
                    if (!allowRemoteDownload)
                    {
                        onReady?.Invoke(null);
                        yield break;
                    }

                    using (var req = UnityWebRequestTexture.GetTexture(url))
                    {
                        req.timeout = remoteTimeoutSeconds;
                        yield return req.SendWebRequest();

                        if (req.result != UnityWebRequest.Result.Success)
                        {
                            var code = req.responseCode;
                            var details = req.error;
                            string body = null;
                            try { body = req.downloadHandler != null ? req.downloadHandler.text : null; } catch { /* ignore */ }
                            if (!string.IsNullOrWhiteSpace(body) && body.Length > 160) body = body.Substring(0, 160) + "...";
                            Debug.LogWarning($"[CoB] Sprite download failed (HTTP {code}): {url} ({details}){(string.IsNullOrWhiteSpace(body) ? "" : $" body=`{body}`")}");
                            onReady?.Invoke(null);
                            yield break;
                        }

                        tex = DownloadHandlerTexture.GetContent(req);
                        if (tex == null)
                        {
                            onReady?.Invoke(null);
                            yield break;
                        }

                        tex.wrapMode = TextureWrapMode.Clamp;
                        _textureByUrl[url] = tex;
                    }
                }
            }

            var sprite = SliceSprite(tex, cardId, numW, numH);
            if (sprite != null)
            {
                _spriteByKey[key] = sprite;
            }

            onReady?.Invoke(sprite);
        }

        private string TryResolveLocalSpriteSheetPath(string spriteUrl)
        {
            if (string.IsNullOrWhiteSpace(spriteUrl)) return null;
            if (string.IsNullOrWhiteSpace(streamingAssetsSubfolder)) return null;

            // Extract file name from Firebase URL. Typical contains "...TTSFiles%2F<file>.png?..."
            var decoded = spriteUrl;
            try { decoded = Uri.UnescapeDataString(spriteUrl); } catch { /* ignore */ }

            var marker = "TTSFiles/";
            var idx = decoded.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var start = idx + marker.Length;
            var end = decoded.IndexOf('?', start);
            if (end < 0) end = decoded.Length;

            var file = decoded.Substring(start, end - start).Trim();
            if (file.Length == 0) return null;

            // Remove any path parts just in case.
            file = Path.GetFileName(file);
            if (file.Length == 0) return null;

            var full = Path.Combine(Application.streamingAssetsPath, streamingAssetsSubfolder, file);
            return full;
        }

        private static IEnumerator CoLoadTextureFromFile(string path, Action<Texture2D> onReady)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                onReady?.Invoke(null);
                yield break;
            }

            // StreamingAssets on Android/webgl require UnityWebRequest. On Windows/Mac we can read directly.
            var uri = path;
            if (!uri.Contains("://"))
            {
                uri = "file:///" + uri.Replace("\\", "/");
            }

            using (var req = UnityWebRequestTexture.GetTexture(uri))
            {
                req.timeout = 20;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onReady?.Invoke(null);
                    yield break;
                }
                onReady?.Invoke(DownloadHandlerTexture.GetContent(req));
            }
        }

        private static T FindFirstObjectOfTypeCompat<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }

        private static Sprite SliceSprite(Texture2D tex, int cardId, int numW, int numH)
        {
            if (tex == null) return null;
            if (numW <= 0 || numH <= 0) return null;

            var pos = GetPosition(cardId);
            var row = pos / numW;
            var col = pos % numW;
            if (row < 0 || row >= numH) row = 0;
            if (col < 0 || col >= numW) col = 0;

            var cellW = tex.width / (float)numW;
            var cellH = tex.height / (float)numH;

            // Web/TTS counts rows from TOP. Unity rect y is from BOTTOM.
            var invRow = (numH - 1) - row;
            var x = col * cellW;
            var y = invRow * cellH;

            var rect = new Rect(x, y, cellW, cellH);
            // Scale to CardHouse CCG card's art area size (~2.9 units wide).
            // Otherwise the raw sprite tile (often 512px wide) appears huge in world units.
            const float targetWorldWidth = 2.9f;
            var ppu = Mathf.Max(10f, cellW / targetWorldWidth);
            return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), ppu);
        }

        private static int GetPosition(int cardId)
        {
            // Mirrors web `sprite-utils.js`:
            // - 100-based (100,101,...) OR 1-based OR 0-based
            if (cardId >= 100) return cardId - 100;
            if (cardId >= 1) return cardId - 1;
            return Mathf.Max(0, cardId);
        }
    }
}

