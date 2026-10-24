using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 켜진 중력장을 그리는 부품 [임시]: 범위 테두리 원 하나와, 테두리에서 중심으로 계속 좁혀 들어가는 원 몇 개(끌어당김을 보여 준다).
    // 사망 효과 화면(DeathEffectView)이 매 프레임 판의 중력장 목록(DeathEffects.GravityFields)을 넘긴다. 목록에서 빠진 중력장의 원은 숨겨 두었다가 다시 쓴다.
    // 켜질 때와 꺼질 때 잠깐 옅어진다. 끌림을 다시 계산하지 않는다.
    internal sealed class GravityFieldRings : IDisposable
    {
        private const float EdgeWidth = 0.08f;
        private const float PulseWidth = 0.05f;
        private const int PulseCount = 3;
        // 좁혀 드는 원 하나가 테두리에서 중심까지 가는 시간(초).
        private const float PulseSeconds = 0.9f;
        private const float FadeInSeconds = 0.15f;
        private const float FadeOutSeconds = 0.3f;

        private static readonly Color FieldColor = new(0.6f, 0.45f, 1f, 1f);

        private readonly LineStrokes _strokes;
        private readonly List<Ring> _rings = new();
        private float _clock;

        private sealed class Ring
        {
            public long Sequence;
            public LineRenderer Edge;
            public LineRenderer[] Pulses;
            public bool Used;
        }

        public GravityFieldRings(Transform parent) => _strokes = new LineStrokes(parent, "Gravity Fields");

        public void Show(IReadOnlyList<GravityField> fields, bool paused, float delta)
        {
            if (!paused)
                _clock += delta;

            for (int i = 0; i < _rings.Count; i++)
                _rings[i].Used = false;

            for (int i = 0; i < fields.Count; i++)
            {
                GravityField field = fields[i];
                Ring ring = Take(field.Sequence);
                ring.Used = true;

                float elapsed = field.Duration - field.Remaining;
                float fade = Mathf.Clamp01(elapsed / FadeInSeconds) * Mathf.Clamp01(field.Remaining / FadeOutSeconds);

                Color edge = FieldColor;
                edge.a = 0.8f * fade;
                LineStrokes.SetCircle(ring.Edge, field.Center, field.Radius);
                LineStrokes.SetColor(ring.Edge, edge);
                ring.Edge.enabled = true;

                for (int k = 0; k < ring.Pulses.Length; k++)
                {
                    // 0 = 테두리, 1 = 중심. 원마다 위상을 나눠 고르게 좁혀 든다.
                    float phase = Mathf.Repeat(_clock / PulseSeconds + (float)k / ring.Pulses.Length, 1f);
                    Color pulse = FieldColor;
                    pulse.a = 0.6f * fade * Mathf.Sin(phase * Mathf.PI);

                    LineRenderer line = ring.Pulses[k];
                    LineStrokes.SetCircle(line, field.Center, Mathf.Max(0.01f, field.Radius * (1 - phase)));
                    LineStrokes.SetColor(line, pulse);
                    line.enabled = true;
                }
            }

            for (int i = 0; i < _rings.Count; i++)
            {
                Ring ring = _rings[i];

                if (ring.Used)
                    continue;

                ring.Sequence = 0;
                ring.Edge.enabled = false;

                foreach (LineRenderer line in ring.Pulses)
                    line.enabled = false;
            }
        }

        public void Reset()
        {
            _strokes.Reset();
            _rings.Clear();
        }

        public void Dispose() => _strokes.Dispose();

        // 이 중력장의 원. 처음 보는 중력장이면 쉬는 원을 쓰고, 없으면 새로 만든다.
        private Ring Take(long sequence)
        {
            Ring idle = null;

            for (int i = 0; i < _rings.Count; i++)
            {
                if (_rings[i].Sequence == sequence)
                    return _rings[i];

                if (idle == null && _rings[i].Sequence == 0 && !_rings[i].Used)
                    idle = _rings[i];
            }

            if (idle == null)
            {
                idle = new Ring
                {
                    Edge = _strokes.Line("Gravity Field Edge", EdgeWidth, FieldColor),
                    Pulses = new LineRenderer[PulseCount],
                };

                for (int k = 0; k < PulseCount; k++)
                    idle.Pulses[k] = _strokes.Line("Gravity Field Pulse", PulseWidth, FieldColor);

                _rings.Add(idle);
            }

            idle.Sequence = sequence;
            return idle;
        }
    }
}
