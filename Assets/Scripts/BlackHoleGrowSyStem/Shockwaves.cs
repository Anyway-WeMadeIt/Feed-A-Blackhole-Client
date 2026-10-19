using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BlackHole.Unity
{
    //화면 왜곡 물결, 셰이더 Hidden/BlackHoleShockwav의 전역 값만 사용, 게임 상태 변경 X
    internal sealed class Shockwaves : IDisposable
    {
        public const int MaxWaves = 6;

        private const float Amplitude = 0.03f; // 왜곡량 0.02~0.05
        private const float Glow = 0.18f; // 링 빛
        private const float Chroma = 0.15f; //색수치, 0이면 끈다

        private static readonly int WavesId = Shader.PropertyToID("_ShockWaves");
        private static readonly int WidthsId = Shader.PropertyToID("_ShockWidths");
        private static readonly int AmpId = Shader.PropertyToID("_ShockAmp");
        private static readonly int GlowId = Shader.PropertyToID("_ShockGlow");
        private static readonly int ChromaId = Shader.PropertyToID("_ShockChroma");

        private sealed class Wave
        {
            public Vector3 Position;
            public float Age;
            public float Seconds;
            public float Strength;
            public float Width;
            public float MaxRadius;
        }

        private readonly Camera _camera;
        private readonly ScriptableRendererFeature _feature;
        private readonly List<Wave> _waves = new List<Wave>(MaxWaves);
        private readonly Vector4[] _waveData = new Vector4[MaxWaves];
        private readonly float[] _widthData = new float[MaxWaves];
        private bool _cleared;

        public Shockwaves(Camera camera, ScriptableRendererFeature feature = null)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _feature = feature;

            Shader.SetGlobalFloat(AmpId, Amplitude);
            Shader.SetGlobalFloat(GlowId, Glow);
            Shader.SetGlobalFloat(ChromaId, Chroma);

            Apply();
            //Debug.Log(_feature != null ? "[물결] Feature 연결됨" : "[물결] Feature 연결 안 됨");
        }

        public void Spawn(Vector3 worldPosition, float strength = 1f, float seconds = 1.2f, float width = 0.08f, float delay = 0f, float maxRadius = 1.3f)
        {
            if (_waves.Count >= MaxWaves)
                _waves.RemoveAt(0);

            _waves.Add(new Wave
            {
                Position = worldPosition,
                Age = -delay,
                Seconds = seconds,
                Strength = strength,
                Width = width,
                MaxRadius = maxRadius,
            });

            _cleared = false;
            SetFeatureActive(true);
        }

        public void Age(float delta)
        {
            if (_waves.Count == 0 && _cleared)
                return;
            
            for (int i = _waves.Count - 1; i >= 0; i--)
            {
                _waves[i].Age += delta;
                if (_waves[i].Age > _waves[i].Seconds)
                    _waves.RemoveAt(i);
            }

            Apply();
        }

        public bool IsClear => _waves.Count == 0;

        public void Reset()
        {
            _waves.Clear();
            Apply();
        }

        public void Dispose() => Reset();

        public void Apply()
        {
            for (int i = 0; i < MaxWaves; i++)
            {
                _waveData[i] = Vector4.zero;
                _widthData[i] = 1f;
            }

            for (int i = 0; i < _waves.Count; i++)
            {
                Wave wave = _waves[i];
                if (wave.Age < 0f)
                    continue;
                
                float t = Mathf.Clamp01(wave.Age / wave.Seconds);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                Vector3 viewport = _camera.WorldToViewportPoint(wave.Position);

                _waveData[i] = new Vector4(
                    viewport.x,
                    viewport.y,
                    wave.MaxRadius * eased,
                    wave.Strength * (1f - t) * (1f - t) * (1f - t));
                _widthData[i] = wave.Width * (0.6f + t * 0.8f);
            }

            Shader.SetGlobalVectorArray(WavesId, _waveData);
            Shader.SetGlobalFloatArray(WidthsId, _widthData);
            // Shader.SetGlobalFloat(AmpId, Amplitude);
            // Shader.SetGlobalFloat(GlowId, Glow);
            // Shader.SetGlobalFloat(ChromaId, Chroma);

            _cleared = _waves.Count == 0;
            SetFeatureActive(!_cleared);
        }

        private void SetFeatureActive(bool active)
        {
            if (_feature != null && _feature.isActive != active)
            {
                _feature.SetActive(active);
                //Debug.Log($"[물결] Full Screen Pass {(active ? "켜짐" : "꺼짐")}");
            }
        }
    }
}