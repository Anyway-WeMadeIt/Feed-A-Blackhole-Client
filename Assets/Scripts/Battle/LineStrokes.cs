using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 전투 화면(스킬 화면, 사망 효과 화면)이 규칙 평면 위에 선과 원을 그리는 부품.
    // - 오래 두는 선(Line): 범위 원·예고선처럼 부르는 쪽이 매 프레임 모양을 맞춘다.
    // - 잠깐 보였다가 옅어지는 선(Flash): Tick 원·발사선·번개·폭발. 다 옅어진 선은 다음 Flash에 다시 쓴다.
    // 만든 선은 모두 이 부품의 뿌리 아래에 있고 Reset이 모두 지운다. 규칙 평면은 장면의 z = 0이고 x·y는 같다.
    internal sealed class LineStrokes : IDisposable
    {
        private const int CircleSegments = 48;
        private const int SortingOrder = 10;

        private readonly Transform _root;
        private readonly Material _material;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly List<Fading> _flashes = new List<Fading>();

        private sealed class Fading
        {
            public LineRenderer Line;
            public Color Color;
            public float Duration;
            public float Remaining;
        }

        public LineStrokes(Transform parent, string name)
        {
            _root = new GameObject(name).transform;
            _root.SetParent(parent, false);
            _material = new Material(Shader.Find("Sprites/Default")) { name = name + " Lines" };
        }

        public LineRenderer Line(string name, float width, Color color)
        {
            LineRenderer line = Create(name, width, color);
            _lines.Add(line);
            return line;
        }

        // 위치와 모양은 부르는 쪽이 SetCircle·SetSegment로 정한다.
        public LineRenderer Flash(string name, float width, Color color, float duration)
        {
            Fading flash = null;

            foreach (Fading candidate in _flashes)
            {
                if (candidate.Remaining <= 0)
                {
                    flash = candidate;
                    break;
                }
            }

            if (flash == null)
            {
                flash = new Fading { Line = Create(name, width, color) };
                _flashes.Add(flash);
            }

            flash.Line.gameObject.name = name;
            flash.Line.widthMultiplier = width;
            flash.Line.enabled = true;
            flash.Color = color;
            flash.Duration = duration;
            flash.Remaining = duration;
            SetColor(flash.Line, color);
            return flash.Line;
        }

        public void Age(float delta)
        {
            foreach (Fading flash in _flashes)
            {
                if (flash.Remaining <= 0)
                    continue;

                flash.Remaining -= delta;

                if (flash.Remaining <= 0)
                {
                    flash.Line.enabled = false;
                    continue;
                }

                Color color = flash.Color;
                color.a *= flash.Remaining / flash.Duration;
                SetColor(flash.Line, color);
            }
        }

        public void Reset()
        {
            foreach (LineRenderer line in _lines)
                Object.Destroy(line.gameObject);

            foreach (Fading flash in _flashes)
                Object.Destroy(flash.Line.gameObject);

            _lines.Clear();
            _flashes.Clear();
        }

        public void Dispose()
        {
            Object.Destroy(_root.gameObject);
            Object.Destroy(_material);
        }

        // center를 중심으로 한 원.
        public static void SetCircle(LineRenderer line, Point2 center, float radius)
        {
            line.loop = true;
            line.positionCount = CircleSegments;
            line.transform.localPosition = ToScene(center);

            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * 2 * Mathf.PI / CircleSegments;
                line.SetPosition(i, new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0));
            }
        }

        // 원을 옮기기만 한다(반지름은 그대로).
        public static void MoveTo(LineRenderer line, Point2 center) => line.transform.localPosition = ToScene(center);

        public static void SetSegment(LineRenderer line, Point2 start, Point2 end)
        {
            line.loop = false;
            line.positionCount = 2;
            line.transform.localPosition = Vector3.zero;
            line.SetPosition(0, ToScene(start));
            line.SetPosition(1, ToScene(end));
        }

        public static void SetColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        private LineRenderer Create(string name, float width, Color color)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(_root, false);
            line.sharedMaterial = _material;
            line.useWorldSpace = false;
            line.widthMultiplier = width;
            line.sortingOrder = SortingOrder;
            SetColor(line, color);
            return line;
        }

        private static Vector3 ToScene(Point2 point) => new Vector3(point.X, point.Y, 0);
    }
}
