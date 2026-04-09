using Shared.Profiling;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ONI_MP.Networking.Components
{
	public class PingMarker : MonoBehaviour
	{
		private const float Duration = 5f;
		private const float FadeStart = 3.5f;
		private const float MarkerSize = 28f;
		private const float PulseSpeed = 3f;
		private const float PulseMin = 0.8f;
		private const float PulseMax = 1.2f;

		private static Sprite cachedRingSprite;

		private Image ringImage;
		private TextMeshProUGUI nameLabel;
		private Color baseColor;
		private float spawnTime;
		private Vector3 worldPosition;
		private Camera uiCamera;

		public void Init(Vector3 worldPos, Color color, string playerName)
		{
			using var _ = Profiler.Scope();

			worldPosition = worldPos;
			baseColor = color;
			spawnTime = Time.unscaledTime;
			uiCamera = GameScreenManager.Instance.GetCamera(GameScreenManager.UIRenderTarget.ScreenSpaceCamera);

			var rectTransform = gameObject.AddComponent<RectTransform>();
			rectTransform.sizeDelta = new Vector2(MarkerSize, MarkerSize);

			var ringGO = new GameObject("Ring");
			ringGO.transform.SetParent(transform, false);
			var ringRect = ringGO.AddComponent<RectTransform>();
			ringRect.sizeDelta = new Vector2(MarkerSize, MarkerSize);
			ringImage = ringGO.AddComponent<Image>();
			if (cachedRingSprite == null)
				cachedRingSprite = CreateRingSprite();
			ringImage.sprite = cachedRingSprite;
			ringImage.color = baseColor;
			ringImage.raycastTarget = false;

			var labelGO = new GameObject("Label");
			labelGO.transform.SetParent(transform, false);
			var labelRect = labelGO.AddComponent<RectTransform>();
			labelRect.sizeDelta = new Vector2(100, 20);
			labelRect.anchoredPosition = new Vector2(0, MarkerSize * 0.5f + 4f);
			nameLabel = labelGO.AddComponent<TextMeshProUGUI>();
			nameLabel.text = playerName;
			nameLabel.fontSize = 12;
			nameLabel.font = Localization.FontAsset;
			nameLabel.color = baseColor;
			nameLabel.alignment = TextAlignmentOptions.Center;
			nameLabel.raycastTarget = false;
		}

		private void Update()
		{
			using var _ = Profiler.Scope();

			float elapsed = Time.unscaledTime - spawnTime;
			if (elapsed >= Duration)
			{
				Destroy(gameObject);
				return;
			}

			UpdateScreenPosition();

			float pulse = Mathf.Lerp(PulseMin, PulseMax, (Mathf.Sin(elapsed * PulseSpeed) + 1f) * 0.5f);
			ringImage.rectTransform.localScale = new Vector3(pulse, pulse, 1f);

			if (elapsed > FadeStart)
			{
				float alpha = 1f - (elapsed - FadeStart) / (Duration - FadeStart);
				ringImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
				nameLabel.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
			}
		}

		private void UpdateScreenPosition()
		{
			using var _ = Profiler.Scope();

			if (uiCamera == null)
				return;

			var canvas = GameScreenManager.Instance.ssCameraCanvas?.GetComponent<Canvas>();
			float planeZ = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera
				? canvas.planeDistance
				: 10f;

			if (Camera.main == null)
				return;

			Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
			screenPos.z = planeZ;
			Vector3 uiWorldPos = uiCamera.ScreenToWorldPoint(screenPos);
			transform.position = uiWorldPos;
		}

		private static Sprite CreateRingSprite()
		{
			using var _ = Profiler.Scope();

			int size = 64;
			var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
			float center = size * 0.5f;
			float outerRadius = center - 1f;
			float innerRadius = outerRadius - 4f;

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
					if (dist <= outerRadius && dist >= innerRadius)
					{
						float edgeSoft = 1f - Mathf.Clamp01(Mathf.Abs(dist - (innerRadius + outerRadius) * 0.5f) / 2.5f);
						tex.SetPixel(x, y, new Color(1f, 1f, 1f, edgeSoft));
					}
					else
					{
						tex.SetPixel(x, y, Color.clear);
					}
				}
			}

			tex.Apply();
			return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
		}
	}
}
