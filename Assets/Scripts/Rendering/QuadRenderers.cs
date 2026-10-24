using UnityEngine;

namespace BlackHole.Unity
{
    // 셰이더 메쉬를 얹는 사각형 렌더러의 공용 생성 도구. 한 변이 1인 사각형이라 그릴 크기는 transform의 배율로 맞춘다.
    // Breaker 링·구체와 적 성질 메쉬(달)가 같이 쓴다.
    internal static class QuadRenderers
    {
        public static Mesh CreateMesh()
        {
            var mesh = new Mesh
            {
                name = "Shader Quad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0),
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        // 처음에는 꺼진 상태로 만든다.
        public static MeshRenderer Create(string name, Transform parent, Mesh quad, Material material, int sortingOrder)
        {
            var view = new GameObject(name);
            view.transform.SetParent(parent, false);

            view.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = view.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }
    }
}
