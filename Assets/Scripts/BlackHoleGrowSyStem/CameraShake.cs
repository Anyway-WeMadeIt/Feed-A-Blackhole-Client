using System.Collections;
using UnityEngine;

namespace BlackHole.Unity
{
    public sealed class CameraShake : MonoBehaviour
    {
        [Header("Shake settings")]
        //얼마동안 흔들기 (지속시간)
        [SerializeField] private float _duration;
        //얼마나 세게 흔들기
        [SerializeField] private float _magnitude;

        //원래 위치
        private Vector3 _originalPosition;
        private float _remainingTime;

        private void Awake()
        {
            _originalPosition = transform.localPosition;
        }

        public void Play()
        {
            _remainingTime = _duration;
        }

        private void LateUpdate()
        {
            if (_remainingTime <= 0f)
            {
                transform.localPosition = _originalPosition;
                return;
            }

            _remainingTime -= Time.deltaTime;

            Vector2 offset =
                Random.insideUnitCircle * _magnitude;

            transform.localPosition =
                _originalPosition +
                new Vector3(offset.x, offset.y, 0f);
        }
    }
}


