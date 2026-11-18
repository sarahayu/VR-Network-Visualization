// Creates and updates temporary node highlight/preview visuals.
// Keeps interaction feedback separate from the stored node appearance, so preview
// cleanup does not need to restore graph colors.

using UnityEngine;

namespace VidiGraph
{
    // Retains the component name for existing references; highlighting now uses a temporary tint.
    public sealed class NodeHighlightHalo : MonoBehaviour
    {
        Renderer nodeRenderer;
        Color baseColor;
        Color highlightColor;
        bool baseVisible;
        bool preview;

        public static NodeHighlightHalo SetPreview(GameObject node, Renderer visual, bool active)
        {
            var effect = node.GetComponent<NodeHighlightHalo>();
            if (effect == null)
            {
                effect = node.AddComponent<NodeHighlightHalo>();
                effect.baseColor = GameObjectUtils.GetColor(visual);
            }
            effect.nodeRenderer = visual;
            effect.preview = active;
            effect.enabled = effect.baseVisible || active;
            effect.Apply();
            return effect;
        }

        // Clear the temporary box-selection preview independently of hover/selection feedback.
        public void ClearPreview()
        {
            preview = false;
            enabled = baseVisible;
            Apply();
        }

        public static void Set(GameObject node, Renderer visual, bool hovered, bool selected,
            Color hoverColor, Color selectColor, Color assignedColor)
        {
            var effect = node.GetComponent<NodeHighlightHalo>();
            if (effect == null && !hovered && !selected) return;
            if (effect == null) effect = node.AddComponent<NodeHighlightHalo>();
            effect.nodeRenderer = visual;
            effect.baseColor = assignedColor;
            effect.baseVisible = hovered || selected;
            effect.highlightColor = hovered ? hoverColor : selectColor;
            effect.enabled = effect.baseVisible || effect.preview;
            effect.Apply();
        }

        void Apply()
        {
            if (nodeRenderer == null) return;
            Color display = preview ? Color.Lerp(baseColor, Color.cyan, 0.35f)
                : baseVisible ? Color.Lerp(baseColor, highlightColor, 0.35f) : baseColor;
            GameObjectUtils.SetColor(nodeRenderer, display);
        }

        void LateUpdate() { Apply(); }
        void OnDisable() { if (nodeRenderer != null) GameObjectUtils.SetColor(nodeRenderer, baseColor); }

    }
}
