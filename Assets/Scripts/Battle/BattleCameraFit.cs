using System;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 전투 카메라의 크기: 저작한 카메라 크기(세로 반 폭) × 판의 전장 배율을 화면비에 맞춘다.
    // - 전장 배율(Hq.FieldScale): 이정표에 닿을 때마다 커진다(원작 실측 1 → 1.68 → 2.27 → 2.83). 판을 시작할 때 BattleSystem이 정한다.
    //   적·Breaker·블랙홀은 월드 크기라 그만큼 작아 보이고, 화면 크기가 고정인 표시(달·떠오르는 숫자)는 각 화면이 같은 배율을 곱한다.
    // - 화면비: 기준 화면비(16:9)가 보여 주는 가로 폭을 좁은 화면에서도 보장한다.
    //   기준보다 넓은 화면(20:9 등)은 크기 그대로(세로는 같고 옆이 더 보인다), 좁은 화면(4:3 등)은 가로 폭이 기준과 같아지도록 키운다(세로가 더 보인다).
    // 화면 크기나 전장 배율이 바뀐 때만 크기를 고친다. 카메라 위치는 건드리지 않는다(카메라 흔들림 등과 함께 쓸 수 있다).
    // 전투 카메라(Main Camera)에 붙인다. 씬에 없으면 GameBootstrap이 붙인다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class BattleCameraFit : MonoBehaviour
    {
        private const float ReferenceAspect = 16f / 9f;

        private Camera _camera;
        // 저작한 크기(전장 배율 1). 붙는 순간(Awake)의 크기다.
        private float _authoredSize;
        // 판의 전장 배율. 첫 판 전에는 1이다.
        private float _fieldScale = HqGrowthDefinition.StartFieldScale;
        private int _width;
        private int _height;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _authoredSize = _camera.orthographicSize;
            Fit();
        }

        private void Update() => Fit();

        // 판의 전장 배율을 정한다(양수). 바뀌면 곧바로 크기를 다시 맞춘다.
        public void SetFieldScale(float fieldScale)
        {
            if (float.IsNaN(fieldScale) || float.IsInfinity(fieldScale) || fieldScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(fieldScale), $"양의 유한한 값이 필요하다. 받은 값: {fieldScale}.");

            if (fieldScale == _fieldScale)
                return;

            _fieldScale = fieldScale;

            // 화면 크기가 그대로여도 다음 맞춤이 크기를 다시 계산하게 한다. Awake 전이면 Awake의 맞춤이 이 배율을 쓴다.
            _width = 0;

            if (_camera != null)
                Fit();
        }

        private void Fit()
        {
            if (!_camera.orthographic)
                return;

            int width = _camera.pixelWidth;
            int height = _camera.pixelHeight;

            if (width <= 0 || height <= 0 || width == _width && height == _height)
                return;

            _width = width;
            _height = height;
            _camera.orthographicSize = SizeFor(_authoredSize * _fieldScale, (float)width / height);
        }

        // 화면비 aspect(가로/세로)에서의 카메라 크기.
        internal static float SizeFor(float authoredSize, float aspect) =>
            aspect >= ReferenceAspect ? authoredSize : authoredSize * ReferenceAspect / aspect;
    }
}
