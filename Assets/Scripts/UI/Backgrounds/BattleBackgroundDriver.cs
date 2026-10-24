using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // Instance-owned UI material: aspect-correct rotation, unscaled time and background cover.
    [DisallowMultipleComponent]
    public sealed class BattleBackgroundDriver : MonoBehaviour
    {
        [SerializeField] private RawImage background;
        [SerializeField] private RawImage overlay;
        [SerializeField] private bool useUnscaledTime = true;
        private Material originalMaterial;
        private Material instanceMaterial;
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int ClockId = Shader.PropertyToID("_Clock");
        private static readonly int ExternalClockId = Shader.PropertyToID("_UseExternalClock");

        public void Configure(RawImage backgroundImage, RawImage overlayImage)
        {
            ReleaseMaterial();
            background = backgroundImage;
            overlay = overlayImage;
            if (Application.isPlaying && isActiveAndEnabled) AcquireMaterial();
        }

        // Overlay-only use (UI screens without a static background image): swap the ambient material.
        public void SetOverlayMaterial(Material material)
        {
            if (overlay == null || material == null) return;
            ReleaseMaterial();
            overlay.material = material;
            if (Application.isPlaying && isActiveAndEnabled) AcquireMaterial();
        }

        // Can also be called by the game's settings/UI code at runtime.
        public void SetTheme(Texture texture, Material material)
        {
            if (background == null || overlay == null || texture == null || material == null) return;
            ReleaseMaterial();
            background.texture = texture;
            overlay.material = material;
            if (Application.isPlaying && isActiveAndEnabled) AcquireMaterial();
        }

        private void OnEnable() => AcquireMaterial();
        private void OnDisable() => ReleaseMaterial();

        private void AcquireMaterial()
        {
            if (instanceMaterial != null || overlay == null || overlay.material == null) return;
            originalMaterial = overlay.material;
            instanceMaterial = new Material(originalMaterial) { name = originalMaterial.name + " (Instance)" };
            overlay.material = instanceMaterial;
            instanceMaterial.SetFloat(ExternalClockId, 1f);
            UpdateVisuals();
        }

        private void LateUpdate() => UpdateVisuals();

        private void UpdateVisuals()
        {
            if (overlay == null || instanceMaterial == null) return;
            Rect rect = overlay.rectTransform.rect;
            float aspect = rect.width / Mathf.Max(1f, rect.height);
            instanceMaterial.SetFloat(AspectId, aspect);
            instanceMaterial.SetFloat(ClockId, useUnscaledTime ? Time.unscaledTime : Time.time);
            if (background == null || background.texture == null) return;
            Rect bgRect = background.rectTransform.rect;
            float targetAspect = bgRect.width / Mathf.Max(1f, bgRect.height);
            float textureAspect = (float)background.texture.width / background.texture.height;
            Vector2 uvSize = targetAspect > textureAspect
                ? new Vector2(1f, textureAspect / targetAspect)
                : new Vector2(targetAspect / textureAspect, 1f);
            background.uvRect = new Rect((Vector2.one - uvSize) * 0.5f, uvSize);
        }

        private void ReleaseMaterial()
        {
            if (instanceMaterial == null) return;
            if (overlay != null && overlay.material == instanceMaterial) overlay.material = originalMaterial;
            if (Application.isPlaying) Destroy(instanceMaterial);
            else DestroyImmediate(instanceMaterial);
            instanceMaterial = null;
            originalMaterial = null;
        }
    }
}
