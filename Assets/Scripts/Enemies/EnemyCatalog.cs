using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 게임에 등록된 적 종류 전체의 목록. 공급(과 나중의 단계별 적 풀)은 이 목록의 종류만 쓸 수 있다.
    // 목록에 없는 종류를 공급·변환 대상이 가리키면 EnemyContentLoader가 목록에 없는 종류로 보고한다.
    // 같은 종류(EnemyType)가 두 번 들어가면 EnemyContentLoader가 중복으로 보고한다.
    [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "BlackHole/Enemy Catalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        [SerializeField] private List<EnemyKind> kinds = new List<EnemyKind>();

        // 비어 있는 칸은 뺀다.
        public IReadOnlyList<EnemyKind> Kinds()
        {
            var result = new List<EnemyKind>(kinds.Count);

            foreach (EnemyKind kind in kinds)
            {
                if (kind != null)
                    result.Add(kind);
            }

            return result;
        }

        // Core 저작 형식에 적 종류를 채운다. 검증은 EnemyContentLoader가 한다.
        public void WriteTo(EnemyContentData data)
        {
            foreach (EnemyKind kind in Kinds())
                data.Enemies.Add(kind.ToData());
        }
    }
}
