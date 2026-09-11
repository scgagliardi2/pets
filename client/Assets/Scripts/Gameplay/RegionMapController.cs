using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Visual-only prototype of a Region/Location's branching node-map (design doc §5,
    /// PLAN.md Phase 1 item 5). Generates a random map on Start and lays it out in a scrollable
    /// Canvas with a generic background. Nodes aren't clickable/resolvable yet — only Forest's
    /// linear map (ForestLocationFactory + LocationFlowController) actually runs battles today.</summary>
    public sealed class RegionMapController : MonoBehaviour
    {
        private const float HorizontalSpacing = 160f;
        private const float VerticalSpacing = 160f;
        private const float EdgePadding = 100f;
        private const float NodeSize = 64f;

        private static readonly Dictionary<NodeType, Color> NodeColors = new Dictionary<NodeType, Color>
        {
            { NodeType.PvE, new Color(0.55f, 0.75f, 0.45f) },
            { NodeType.Event, new Color(0.85f, 0.75f, 0.35f) },
            { NodeType.PvP, new Color(0.8f, 0.4f, 0.4f) },
            { NodeType.Camp, new Color(0.45f, 0.65f, 0.85f) },
            { NodeType.Gym, new Color(0.75f, 0.45f, 0.85f) }
        };

        [SerializeField] private RectTransform content;
        [SerializeField] private Image background;

        [Tooltip("0 = a random seed each time this scene runs.")]
        [SerializeField] private int seed;
        [SerializeField] private int layerCount = 7;

        public IReadOnlyList<RegionMapNode> LastGeneratedNodes { get; private set; }

        private void Start()
        {
            int usedSeed = seed != 0 ? seed : System.Environment.TickCount;
            Build(RegionMapGenerator.Generate(usedSeed, layerCount));
        }

        private void Build(RegionMap map)
        {
            LastGeneratedNodes = map.Nodes;

            var nodesByLayer = map.Nodes.GroupBy(n => n.Layer).ToDictionary(g => g.Key, g => g.ToList());
            int maxPerLayer = nodesByLayer.Values.Max(l => l.Count);

            float contentWidth = maxPerLayer * HorizontalSpacing + EdgePadding;
            float contentHeight = map.LayerCount * VerticalSpacing + EdgePadding;
            content.sizeDelta = new Vector2(contentWidth, contentHeight);
            background.rectTransform.sizeDelta = new Vector2(contentWidth, contentHeight);
            background.rectTransform.anchoredPosition = Vector2.zero;

            var positions = new Dictionary<string, Vector2>();
            foreach (var kvp in nodesByLayer)
            {
                var nodes = kvp.Value;
                float y = -EdgePadding - kvp.Key * VerticalSpacing;
                float totalWidth = (nodes.Count - 1) * HorizontalSpacing;
                for (int i = 0; i < nodes.Count; i++)
                {
                    positions[nodes[i].Id] = new Vector2(-totalWidth / 2f + i * HorizontalSpacing, y);
                }
            }

            // Lines first so nodes render on top of them.
            foreach (var node in map.Nodes)
            {
                foreach (var nextId in node.NextIds)
                {
                    CreateLine(positions[node.Id], positions[nextId]);
                }
            }

            foreach (var node in map.Nodes)
            {
                CreateNode(node, positions[node.Id]);
            }
        }

        private void CreateLine(Vector2 from, Vector2 to)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(content, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.5f);
            image.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);

            Vector2 diff = to - from;
            rect.sizeDelta = new Vector2(diff.magnitude, 4f);
            rect.anchoredPosition = from;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
        }

        private void CreateNode(RegionMapNode node, Vector2 position)
        {
            var go = new GameObject($"Node_{node.Id}_{node.Type}", typeof(RectTransform));
            go.transform.SetParent(content, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(NodeSize, NodeSize);
            rect.anchoredPosition = position;

            var image = go.AddComponent<Image>();
            image.color = NodeColors.TryGetValue(node.Type, out var color) ? color : Color.gray;

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.text = Abbreviate(node.Type);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private static string Abbreviate(NodeType type)
        {
            switch (type)
            {
                case NodeType.PvE: return "PvE";
                case NodeType.Event: return "Evt";
                case NodeType.PvP: return "PvP";
                case NodeType.Camp: return "Camp";
                case NodeType.Gym: return "GYM";
                default: return type.ToString();
            }
        }
    }
}
