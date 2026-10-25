using System;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적의 체력 음영(BlackHole/Enemy Shade 셰이더). 몸체(와 성질 속 채움)는 '남은 체력 비율'만큼의 면적에 음영(검정 α ShadeStrength를 덮은 색)이
    // 씌워져 있고, 피해를 입은 만큼 몸체의 한쪽(로컬 위)부터 기본색이 드러나며, 경계선은 자전과 함께 돈다. 체력 50%면 절반은 기본색, 절반은 음영.
    // 음영 상태(마지막 체력 비율)는 EnemyVisual이 갖고, 이 처리기 하나가 모든 적을 같은 식으로 다룬다. 셰이더를 못 찾으면 음영 없이 그린다.
    internal sealed class EnemyShade : IDisposable
    {
        // 셰이더를 못 찾으면(빌드에 셰이더가 빠진 경우 등) 음영 없이 기존처럼 그린다.
        private const string ShaderName = "BlackHole/Enemy Shade";
        private static readonly int _centerId = Shader.PropertyToID("_ShadeCenter");
        private static readonly int _radiusId = Shader.PropertyToID("_ShadeRadius");
        private static readonly int _thresholdId = Shader.PropertyToID("_ShadeThreshold");
        // 문턱값이 이 값이면 경계(부드러움 포함)가 몸체 밖이라 전부 음영 / 전부 기본색이다.
        private const float AllThreshold = 2f;

        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly Material _material;

        public EnemyShade()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader != null)
                _material = new Material(shader) { name = "Enemy Shade" };
            else
                Debug.LogWarning($"[적 화면] '{ShaderName}' 셰이더를 찾지 못해 체력 음영 없이 그린다. 빌드라면 Project Settings > Graphics > Always Included Shaders에 추가할 것.");
        }

        // 출현 때 한 번: 색을 내는 몸체(은하는 실루엣 팔)와 성질 속 채움에 음영 셰이더를 쓴다.
        // 중심·반지름은 스프라이트의 텍스처 좌표(UV)라 여기서 한 번만 넣고, 이후엔 체력이 바뀔 때 문턱값만 고친다(Update).
        public void Attach(EnemyVisual visual)
        {
            if (_material == null)
                return;

            visual.Body.sharedMaterial = _material;
            Init(visual.Body, 1);

            // 성질 속 채움은 같은 스프라이트를 TraitFillScale로 줄인 자식이다. 반지름을 그만큼 키워 잡아야 경계선이 몸체와 같은 높이에 온다.
            if (visual.Fill != null)
            {
                visual.Fill.sharedMaterial = _material;
                Init(visual.Fill, 1 / EnemyLooks.TraitFillScale);
            }
        }

        // 체력이 바뀐 프레임에만 문턱값을 다시 넣는다(평소 프레임에는 아무것도 하지 않는다).
        // 음영 면적 = 남은 체력 비율, 기본색 면적 = 잃은 체력 비율. 기본색 쪽은 몸체의 로컬 위쪽이고,
        // 셰이더가 스프라이트의 UV로 재므로 경계선은 자전·피격 흔들림·출현 크기 변화를 저절로 따라간다.
        public void Update(EnemyVisual visual, Enemy enemy)
        {
            if (_material == null)
                return;

            float ratio = Mathf.Clamp01(enemy.Health / enemy.Stats.MaxHealth);
            if (ratio == visual.ShadeRatio)
                return;

            visual.ShadeRatio = ratio;
            float threshold = ThresholdOf(1 - ratio);
            SetThreshold(visual.Body, threshold);

            if (visual.Fill != null)
                SetThreshold(visual.Fill, threshold);
        }

        public void Dispose()
        {
            if (_material != null)
                Object.Destroy(_material);
        }

        // 출현 때 한 번: 스프라이트 중심(bounds.center)과 반지름(긴 변의 절반 — EnemyView.Create가 크기를 맞추는 기준과 같다)을
        // 셰이더가 쓰는 UV 단위로 바꿔 넣는다. 로컬 y(단위) → 텍스처 픽셀 y = 스프라이트 원점 + pivot + y × PPU → ÷ 텍스처 높이.
        // 문턱값은 '전부 음영'으로 시작하고, 첫 Synchronize의 Update가 실제 체력으로 고친다.
        private void Init(SpriteRenderer renderer, float radiusScale)
        {
            Sprite sprite = renderer.sprite;
            Bounds bounds = sprite.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y) * radiusScale;
            float pixelsPerUnit = sprite.pixelsPerUnit;
            float textureHeight = sprite.texture.height;

            float centerPixelY = SpriteOriginY(sprite) + sprite.pivot.y + bounds.center.y * pixelsPerUnit;
            float centerUv = centerPixelY / textureHeight;
            float radiusUv = radius * pixelsPerUnit / textureHeight;

            renderer.GetPropertyBlock(_block);
            _block.SetVector(_centerId, new Vector4(0, centerUv, 0, 0));
            _block.SetFloat(_radiusId, Mathf.Max(radiusUv, 1e-6f));
            _block.SetFloat(_thresholdId, AllThreshold);
            renderer.SetPropertyBlock(_block);
        }

        // 스프라이트 사각형의 아래쪽 끝이 텍스처에서 몇 번째 픽셀(y)인가. 아틀라스에 사각형으로 묶였으면 아틀라스 안 위치를 쓴다.
        // 아틀라스에 Tight(모양대로)로 묶인 스프라이트는 textureRect를 읽을 수 없어(예외) 원본 사각형으로 대신한다 —
        // 그 경우 경계 높이가 어긋날 수 있다(지금 적 스프라이트는 모두 코드로 만든 단독 텍스처라 해당 없음).
        private static float SpriteOriginY(Sprite sprite)
        {
            if (sprite.packed && sprite.packingMode == SpritePackingMode.Tight)
                return sprite.rect.y;

            return sprite.textureRect.y - sprite.textureRectOffset.y;
        }

        private void SetThreshold(SpriteRenderer renderer, float threshold)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetFloat(_thresholdId, threshold);
            renderer.SetPropertyBlock(_block);
        }

        // 기본색이 드러날 면적 비율(0 ~ 1) → 셰이더 문턱값 t(−1 ~ 1, 축 방향 거리 ÷ 반지름).
        // 단순히 t = 1 − 2·비율로 두면 원 모양에서 '높이' 비율이 되어 50%일 때만 맞고 10%·90%에서 면적이 크게 어긋난다.
        // 그래서 원에서 축 방향 t 바깥 활꼴의 면적 비율 (acos t − t·√(1 − t²)) / π 가 목표 비율이 되는 t를 이분법으로 구한다
        // (체력이 바뀐 프레임에만 계산한다). 적 도형은 대부분 원에 가까운 다각형이라 체감상 면적 비율과 맞는다.
        private static float ThresholdOf(float brightFraction)
        {
            if (brightFraction <= 0)
                return AllThreshold;

            if (brightFraction >= 1)
                return -AllThreshold;

            float low = -1;
            float high = 1;

            for (int i = 0; i < 20; i++)
            {
                float t = (low + high) * 0.5f;
                float segment = (Mathf.Acos(t) - t * Mathf.Sqrt(1 - t * t)) / Mathf.PI;

                // 활꼴 면적은 t가 커질수록 줄어든다.
                if (segment > brightFraction)
                    low = t;
                else
                    high = t;
            }

            return (low + high) * 0.5f;
        }
    }
}
