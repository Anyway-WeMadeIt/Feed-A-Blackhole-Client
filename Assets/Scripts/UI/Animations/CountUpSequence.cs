using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BlackHole.Unity
{
    // 글자 여러 개의 숫자를 차례로 시작값에서 목표까지 올리는 연출(결산 화면의 처치 수·Gold). 순수 C#: 화면이 Tick으로 시간을 넘긴다.
    // - 단계는 Add한 순서로 하나씩 돈다. 단계마다 StepSeconds 동안 SmoothStep으로 오른다. 연출 중에도 뒤에 단계를 더 붙일 수 있다.
    // - 글자가 바뀔 때마다 ValueChanged(강조 효과용), 마지막 단계가 끝나면 Completed를 알린다.
    // - Finish는 남은 단계를 모두 최종값으로 쓰고 끝낸다(Completed는 알리지 않는다 — 건너뛴 쪽이 마무리를 정한다).
    // - Rewrite는 지금 보이는 값을 같은 서식으로 다시 쓴다(숫자 표기 설정이 바뀌었을 때).
    internal sealed class CountUpSequence
    {
        private sealed class Step
        {
            public TMP_Text Text;
            public long From;
            public long To;
            public Func<long, string> Format;
            public long Shown;
        }

        private readonly List<Step> _steps = new();
        private int _index;
        private float _elapsed;

        // 단계 하나가 걸리는 시간(초).
        public float StepSeconds { get; set; } = 1f;

        public bool IsRunning => _index < _steps.Count;

        public event Action<TMP_Text> ValueChanged;
        public event Action Completed;

        public void Clear()
        {
            _steps.Clear();
            _index = 0;
            _elapsed = 0f;
        }

        // 글자를 from으로 바로 쓰고, 차례가 오면 to까지 올린다. 글자가 없으면 넣지 않는다.
        public void Add(TMP_Text text, long from, long to, Func<long, string> format)
        {
            if (text == null)
                return;

            var step = new Step { Text = text, From = from, To = to, Format = format, Shown = from };
            text.text = format(from);
            _steps.Add(step);
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning)
                return;

            _elapsed += deltaTime;
            Step step = _steps[_index];
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_elapsed / Mathf.Max(StepSeconds, 0.0001f)));
            Write(step, step.From + (long)Math.Round((step.To - step.From) * (double)t));

            if (t < 1f)
                return;

            _index++;
            _elapsed = 0f;

            if (!IsRunning)
                Completed?.Invoke();
        }

        // 남은 단계를 모두 최종값으로 쓰고 끝낸다.
        public void Finish()
        {
            for (; _index < _steps.Count; _index++)
                Write(_steps[_index], _steps[_index].To);

            _elapsed = 0f;
        }

        // 보이는 값을 지금 서식으로 다시 쓴다(서식이 읽는 설정이 바뀌었을 때).
        public void Rewrite()
        {
            foreach (Step step in _steps)
            {
                if (step.Text != null)
                    step.Text.text = step.Format(step.Shown);
            }
        }

        private void Write(Step step, long value)
        {
            if (step.Text == null || value == step.Shown)
                return;

            step.Shown = value;
            step.Text.text = step.Format(value);
            ValueChanged?.Invoke(step.Text);
        }
    }
}
