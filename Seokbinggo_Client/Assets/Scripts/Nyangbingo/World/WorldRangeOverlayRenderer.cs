using System.Collections.Generic;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// A-26: 등불/체/해태 반경·밀폐 창 표시 전용 오버레이.
    /// 입력을 읽지 않으며 Collider·Seal·저장에 영향을 주지 않는다.
    /// 좌표 계약: 논리 셀 (x,y) AABB [x,x+1]×[y,y+1], 중심 (x+0.5,y+0.5) — Center는 월드 좌표(보통 셀 중심).
    /// Circle Radius = 타일 반경. Rect Radius = half-extent(SealSystem seal_window_rx/ry와 동일 단위).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldRangeOverlayRenderer : MonoBehaviour, IWorldRangeOverlayRenderer
    {
        private const int CircleSegments = 48;
        private const float LineWidth = 0.08f;

        [SerializeField] private Color circleColor = new Color(0.2f, 0.85f, 1f, 0.85f);
        [SerializeField] private Color rectColor = new Color(1f, 0.75f, 0.2f, 0.85f);
        [SerializeField] private int sortingOrder = 50;

        private readonly List<LineRenderer> pool = new List<LineRenderer>(8);
        private bool visible = true;
        private Material sharedLineMaterial;
        private int activeCount;

        public void SetVisible(bool visible)
        {
            this.visible = visible;
            for (var i = 0; i < activeCount; i++)
            {
                if (pool[i] != null) pool[i].enabled = visible;
            }
        }

        public void Render(IReadOnlyList<WorldRangeOverlay> overlays)
        {
            if (overlays == null || overlays.Count == 0)
            {
                Clear();
                return;
            }

            EnsureMaterial();
            activeCount = 0;
            for (var i = 0; i < overlays.Count; i++)
            {
                var overlay = overlays[i];
                if (overlay.Radius <= 0f) continue;
                var line = Acquire(activeCount++);
                line.enabled = visible;
                if (overlay.Shape == WorldRangeShape.AxisAlignedRect)
                {
                    var halfY = overlay.SecondaryRadius > 0f ? overlay.SecondaryRadius : overlay.Radius;
                    DrawRect(line, overlay.Center, overlay.Radius, halfY, rectColor);
                }
                else
                    DrawCircle(line, overlay.Center, overlay.Radius, circleColor);
            }

            for (var i = activeCount; i < pool.Count; i++)
            {
                if (pool[i] != null) pool[i].enabled = false;
            }
        }

        public void Clear()
        {
            activeCount = 0;
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null) pool[i].enabled = false;
            }
        }

        private void OnDestroy()
        {
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] == null) continue;
                if (Application.isPlaying) Destroy(pool[i].gameObject);
                else DestroyImmediate(pool[i].gameObject);
            }
            pool.Clear();
            if (sharedLineMaterial != null)
            {
                if (Application.isPlaying) Destroy(sharedLineMaterial);
                else DestroyImmediate(sharedLineMaterial);
                sharedLineMaterial = null;
            }
        }

        private LineRenderer Acquire(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject($"WorldRangeOverlay_{pool.Count}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.sharedMaterial = sharedLineMaterial;
                lr.textureMode = LineTextureMode.Stretch;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 2;
                lr.sortingOrder = sortingOrder;
                lr.enabled = false;
                pool.Add(lr);
            }

            var line = pool[index];
            if (line.sharedMaterial == null) line.sharedMaterial = sharedLineMaterial;
            return line;
        }

        private void EnsureMaterial()
        {
            if (sharedLineMaterial != null) return;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            sharedLineMaterial = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
            sharedLineMaterial.name = "WorldRangeOverlayLine";
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null) pool[i].sharedMaterial = sharedLineMaterial;
            }
        }

        private static void DrawCircle(LineRenderer line, Vector2 center, float radiusTiles, Color color)
        {
            line.startColor = color;
            line.endColor = color;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.positionCount = CircleSegments;
            for (var i = 0; i < CircleSegments; i++)
            {
                var t = (i / (float)CircleSegments) * Mathf.PI * 2f;
                var x = center.x + Mathf.Cos(t) * radiusTiles;
                var y = center.y + Mathf.Sin(t) * radiusTiles;
                line.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        private static void DrawRect(LineRenderer line, Vector2 center, float halfX, float halfY, Color color)
        {
            line.startColor = color;
            line.endColor = color;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.positionCount = 4;
            var min = new Vector2(center.x - halfX, center.y - halfY);
            var max = new Vector2(center.x + halfX, center.y + halfY);
            line.SetPosition(0, new Vector3(min.x, min.y, 0f));
            line.SetPosition(1, new Vector3(max.x, min.y, 0f));
            line.SetPosition(2, new Vector3(max.x, max.y, 0f));
            line.SetPosition(3, new Vector3(min.x, max.y, 0f));
        }
    }
}
