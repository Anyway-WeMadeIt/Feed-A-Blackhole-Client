using System;
using BlackHole.ContentExport;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 게임 콘텐츠 세트로 서버 콘텐츠 묶음(계약 1.2~1.3)을 만든다. 게임 실행 중과 에디터 내보내기가 같은 입력을 쓴다.
    // - 실행 중: 콘텐츠를 불러올 때 묶음의 버전만 계산한다(GameContentLoader → LoadedContent.ServerContentVersion).
    //   빌드 단계 없이 에디터 Play 모드와 빌드에서 언제나 지금 콘텐츠의 버전이 된다.
    // - 에디터: 내보내기 도구(BlackHole > Server Content)가 묶음 파일을 쓴다.
    // 같은 콘텐츠면 언제나 같은 바이트, 같은 버전이다. 서버 저장소에 내보낸 묶음의 목차 버전과 같아야 동기화 로그가 받아들여진다.
    //
    // 원본 콘텐츠(시트 CSV, 노드 배치, 판 콘텐츠 에셋)로 만든다. 밸런스 프로필의 패치는 들지 않는다 — 프로필을 적용한 실행은 오프라인이다(계약 C10).
    internal static class ServerContent
    {
        // 묶음의 입력. 콘텐츠 세트가 게임에서 불러올 수 있는 상태라고 본다(검사는 부르는 쪽이 먼저 한다).
        public static ServerContentSource SourceOf(GameContentSetup setup)
        {
            NodeCatalog nodes = setup.Nodes;
            NodeContentSource sheets = nodes.Content;

            return new ServerContentSource(
                sheets.UpgradeStatsCsv.text,
                sheets.NodesCsv.text,
                sheets.NodeCostCsv.text,
                sheets.NodeEffectsCsv.text,
                nodes.ToData(),
                setup.ToData(),
                sheets.Load().Content);
        }

        // 지금 콘텐츠의 묶음 버전. 만들지 못하면 오류를 남기고 ""를 돌려준다(게임은 그대로 시작하고, 서버가 로그를 거부한다).
        public static string VersionOf(GameContentSetup setup)
        {
            try
            {
                return ServerContentExporter.Export(SourceOf(setup)).ContentVersion;
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            {
                Debug.LogError($"[콘텐츠] 서버 콘텐츠 버전을 계산하지 못했다: {error.Message}", setup);
                return "";
            }
        }
    }
}
