using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackHole.Unity
{
    // 밝은 그림 ↔ 어두운 그림 짝. Presentation(ImageBindings)이 닿지 않는 그림(설정 행, 카드, 노드 트리 등)을 테마에 맞게 바꿀 때 쓴다.
    // ImageBindings의 Dark 바인딩과 같은 그림을 가리킨다. 짝은 Tools/UI/build_dark_sprites.py가 만든 그림에서 채운다.
    [CreateAssetMenu(fileName = "UISpriteSet", menuName = "BlackHole/UI Sprite Set")]
    public sealed class UISpriteSet : ScriptableObject
    {
        [Serializable]
        public sealed class Pair
        {
            public Sprite light;
            public Sprite dark;
        }

        [SerializeField] private List<Pair> _pairs = new();

        private Dictionary<Sprite, Sprite> _toDark;
        private Dictionary<Sprite, Sprite> _toLight;

        // sprite의 themeId 버전. 밝은 그림이면 Dark에서 어두운 짝을, 어두운 그림이면 Light에서 밝은 짝을 준다. 짝이 없으면 그대로.
        public Sprite For(Sprite sprite, string themeId)
        {
            if (sprite == null)
                return null;

            Build();
            Dictionary<Sprite, Sprite> map = themeId == UITheme.Dark.ToId() ? _toDark : _toLight;
            return map.TryGetValue(sprite, out Sprite other) ? other : sprite;
        }

        private void Build()
        {
            if (_toDark != null)
                return;

            _toDark = new Dictionary<Sprite, Sprite>();
            _toLight = new Dictionary<Sprite, Sprite>();

            foreach (Pair pair in _pairs)
            {
                if (pair == null || pair.light == null || pair.dark == null)
                    continue;

                _toDark[pair.light] = pair.dark;
                _toLight[pair.dark] = pair.light;
            }
        }
    }
}
