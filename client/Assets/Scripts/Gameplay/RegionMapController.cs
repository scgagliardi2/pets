using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>Renders a generated Location node-map (design doc §5) and lets the player walk it:
    /// a random branching graph from a start node, through five layers of choices, to the mandatory
    /// Gym. The map flows left to right — one column of branches per layer — at spacings that fit a
    /// default run end to end on screen. Only nodes connected forward from where the player stands
    /// are clickable; clicking one slides the player token along the edge and opens up that node's
    /// own options.
    ///
    /// What a node *does* on arrival is deliberately not handled here (PLAN.md Phase 1) — stepping
    /// onto a node doesn't start its fight/event/center visit yet. This is the map and the movement
    /// on it; node resolution hooks onto the end of a step, in WalkTo, once it exists.</summary>
    public sealed class RegionMapController : MonoBehaviour
    {
        /// <summary>Gap between one layer and the next, along the map's left-to-right flow.</summary>
        private const float LayerSpacing = 120f;

        /// <summary>Gap between the branching options within a single layer, which stack
        /// vertically into a column.</summary>
        private const float BranchSpacing = 118f;

        private const float EdgePadding = 70f;
        private const float NodeSize = 52f;

        /// <summary>The Gym is every path's terminus, so it reads as the visually bigger "boss"
        /// node even before it has a bespoke icon.</summary>
        private const float GymNodeScale = 1.5f;

        /// <summary>Nudges nodes off their exact grid column so a generated map looks hand-drawn
        /// rather than like a spreadsheet. Applied along the flow axis (x) rather than across it,
        /// so it can never eat into the vertical gap a column's captions and player token need.</summary>
        private const float MaxLayerJitter = 12f;

        private const float PlayerTokenSize = 28f;
        private const float MoveDuration = 0.4f;

        /// <summary>Every node, edge, caption and the player token is anchored to the content's
        /// middle-left, matching the left-to-right flow LayOutNodes lays out against: x grows with
        /// the layer, y is signed off the vertical center of the column.</summary>
        private static readonly Vector2 MapOrigin = new Vector2(0f, 0.5f);

        private const float CaptionWidth = 116f;
        private const float CaptionHeight = 22f;
        private const float CaptionGap = 4f;

        /// <summary>What each node actually represents to the player (this is a flavor/label
        /// concern only — NodeType itself stays the shared enum other Meta code keys off of, see
        /// NodeType.cs).</summary>
        private static readonly Dictionary<NodeType, string> NodeDisplayNames = new Dictionary<NodeType, string>
        {
            { NodeType.PvE, "Battle" },
            { NodeType.Event, "Encounter" },
            { NodeType.PvP, "Mystery Trainer" },
            { NodeType.Camp, "Pokémon Center" },
            { NodeType.Gym, "Gym" }
        };

        /// <summary>Resources-relative file name (under Sprites/Nodes/, no extension) for each
        /// node's real icon art. Drop the matching PNG into
        /// Assets/Resources/Sprites/Nodes/&lt;name&gt;.png and it's picked up automatically on the
        /// next scene rebuild/Play — no code change needed. See NodeFallbackColors for what renders
        /// until then.</summary>
        private static readonly Dictionary<NodeType, string> NodeIconFileNames = new Dictionary<NodeType, string>
        {
            { NodeType.PvE, "battle" },
            { NodeType.Event, "encounter" },
            { NodeType.PvP, "mystery_trainer" },
            { NodeType.Camp, "pokemon_center" },
            { NodeType.Gym, "gym" }
        };

        /// <summary>Flat-color stand-in for a node whose icon (see NodeIconFileNames) hasn't been
        /// dropped into Resources yet, reusing Theme's semantic accents so the map still reads at a
        /// glance without real art.</summary>
        private static readonly Dictionary<NodeType, Color> NodeFallbackColors = new Dictionary<NodeType, Color>
        {
            { NodeType.PvE, Theme.Positive },
            { NodeType.Event, Theme.ButtonConfirmBg },
            { NodeType.PvP, Theme.Danger },
            { NodeType.Camp, Theme.ButtonPrimaryBg },
            { NodeType.Gym, Theme.Special }
        };

        private static readonly Dictionary<NodeType, Sprite> IconCache = new Dictionary<NodeType, Sprite>();

        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Text statusText;
        [SerializeField] private Button newMapButton;

        [Tooltip("0 = a fresh random seed each time this scene runs.")]
        [SerializeField] private int seed;

        [Tooltip("Counts the start layer and the Gym layer, so 7 = start + 5 choice layers + Gym.")]
        [SerializeField] private int layerCount = RegionMapGenerator.DefaultLayerCount;

        private readonly Dictionary<string, NodeView> nodeViews = new Dictionary<string, NodeView>();
        private readonly List<EdgeView> edgeViews = new List<EdgeView>();

        private RectTransform edgeRoot;
        private RectTransform nodeRoot;
        private RectTransform playerToken;
        private Coroutine moveRoutine;

        /// <summary>The walk state over the currently displayed map. Rebuilt by <see cref="Regenerate"/>.</summary>
        public RegionMapTraversal Traversal { get; private set; }

        public IReadOnlyList<RegionMapNode> LastGeneratedNodes => Traversal?.Map.Nodes;

        /// <summary>True while the player token is sliding between two nodes; input is ignored
        /// until it lands so a fast double-click can't skip a layer.</summary>
        public bool IsMoving => moveRoutine != null;

        private void Start()
        {
            if (newMapButton != null)
            {
                newMapButton.onClick.AddListener(Regenerate);
            }
            Regenerate();
        }

        /// <summary>Throws away the current map and walks a brand new one. Uses the serialized seed
        /// when it's set (so a specific map can be pinned while iterating on layout) and a fresh
        /// random one otherwise.</summary>
        public void Regenerate()
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }

            int usedSeed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
            Build(RegionMapGenerator.Generate(usedSeed, layerCount));
        }

        private void Build(RegionMap map)
        {
            Traversal = new RegionMapTraversal(map);

            EnsureContainers();
            ClearContainer(edgeRoot);
            ClearContainer(nodeRoot);
            nodeViews.Clear();
            edgeViews.Clear();

            var positions = LayOutNodes(map);

            // Edges first so the node icons render on top of the lines joining them.
            foreach (var node in map.Nodes)
            {
                foreach (var nextId in node.NextIds)
                {
                    edgeViews.Add(CreateEdge(node.Id, nextId, positions[node.Id], positions[nextId]));
                }
            }

            foreach (var node in map.Nodes)
            {
                nodeViews[node.Id] = CreateNode(node, positions[node.Id]);
            }

            EnsurePlayerToken();
            playerToken.anchoredPosition = PlayerPositionFor(Traversal.CurrentNodeId);

            Refresh();
            Canvas.ForceUpdateCanvases();
            SetScroll(ScrollPositionFor(playerToken.anchoredPosition.x));
        }

        /// <summary>Places layer 0 at the left and the Gym at the right, so the map reads as a
        /// journey across the Location and a whole run's worth of layers fits on screen at once
        /// (the scroll view still exists for a longer map or a narrower window, and starts where
        /// the player starts).</summary>
        private Dictionary<string, Vector2> LayOutNodes(RegionMap map)
        {
            // Seeded separately from the generator so nudging the layout can never change which
            // graph a given seed produces.
            var jitterRng = new DeterministicRandom(map.Seed ^ 0x5EED);

            var positions = new Dictionary<string, Vector2>();
            int widestLayer = 1;

            for (int layer = 0; layer < map.LayerCount; layer++)
            {
                var nodes = map.NodesInLayer(layer);
                widestLayer = Mathf.Max(widestLayer, nodes.Count);

                float x = EdgePadding + layer * LayerSpacing;
                float columnHeight = (nodes.Count - 1) * BranchSpacing;
                for (int i = 0; i < nodes.Count; i++)
                {
                    // The start and Gym nodes stay dead-on their column — they're the map's two
                    // anchors.
                    bool isAnchorLayer = layer == 0 || layer == map.LayerCount - 1;
                    float jitter = isAnchorLayer
                        ? 0f
                        : (jitterRng.NextInt(2001) / 1000f - 1f) * MaxLayerJitter;
                    positions[nodes[i].Id] = new Vector2(x + jitter, columnHeight / 2f - i * BranchSpacing);
                }
            }

            // Content is centered vertically on y = 0 (see the scene builder's middle-left anchor),
            // so the height only has to cover the tallest column plus what hangs below its lowest
            // node — its caption.
            content.sizeDelta = new Vector2(
                (map.LayerCount - 1) * LayerSpacing + EdgePadding * 2f,
                (widestLayer - 1) * BranchSpacing + NodeSize + CaptionGap + CaptionHeight + EdgePadding * 2f);

            return positions;
        }

        private void EnsureContainers()
        {
            if (edgeRoot == null)
            {
                edgeRoot = CreateContainer("Edges");
            }
            if (nodeRoot == null)
            {
                nodeRoot = CreateContainer("Nodes");
            }
        }

        private RectTransform CreateContainer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(content, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void ClearContainer(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }

        private EdgeView CreateEdge(string fromId, string toId, Vector2 from, Vector2 to)
        {
            var go = new GameObject($"Edge_{fromId}_to_{toId}", typeof(RectTransform));
            go.transform.SetParent(edgeRoot, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = MapOrigin;
            rect.pivot = new Vector2(0f, 0.5f);

            Vector2 diff = to - from;
            rect.sizeDelta = new Vector2(diff.magnitude, EdgeView.NormalThickness);
            rect.anchoredPosition = from;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);

            return new EdgeView { FromId = fromId, ToId = toId, Rect = rect, Image = image };
        }

        private NodeView CreateNode(RegionMapNode node, Vector2 position)
        {
            float size = node.Type == NodeType.Gym ? NodeSize * GymNodeScale : NodeSize;

            // The icon is the whole node — nothing is drawn behind it. (Earlier versions put a
            // color panel back there as a "you are here"/"you can go here" highlight; it read as a
            // stray orange/white square around the art rather than as a highlight. Where the
            // player is, is what the player token says; what's reachable is carried by Refresh's
            // brightness and by the thicker, brighter edges leading out of the current node.)
            var go = new GameObject($"Node_{node.Id}_{node.Type}", typeof(RectTransform));
            go.transform.SetParent(nodeRoot, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = MapOrigin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;

            var image = go.AddComponent<Image>();
            var icon = LoadIcon(node.Type);
            Color baseTint;
            if (icon != null)
            {
                // Real art: no color box behind it, just tint (white = full color, dimmed by
                // Refresh for a visited/unreachable node exactly like the fallback swatch below).
                image.sprite = icon;
                image.preserveAspect = true;
                baseTint = Color.white;
            }
            else
            {
                // No icon dropped into Resources/Sprites/Nodes yet — flat color keeps the map
                // legible in the meantime.
                baseTint = NodeFallbackColors.TryGetValue(node.Type, out var color) ? color : Theme.TextMuted;
                image.color = baseTint;
            }

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            string nodeId = node.Id;
            button.onClick.AddListener(() => OnNodeClicked(nodeId));

            var caption = CreateCaption(node, position, size);

            return new NodeView
            {
                Node = node,
                Rect = rect,
                Image = image,
                Button = button,
                Label = caption,
                BaseColor = baseTint
            };
        }

        /// <summary>A short label below the node's icon — "Start" for the entry node, otherwise the
        /// node's flavor name (Battle/Encounter/Mystery Trainer/Pokémon Center/Gym). Kept as its own
        /// object below the icon, rather than overlaid on top of it, so real artwork isn't covered
        /// by text.</summary>
        private Text CreateCaption(RegionMapNode node, Vector2 nodePosition, float nodeSize)
        {
            var go = new GameObject($"Caption_{node.Id}", typeof(RectTransform));
            go.transform.SetParent(nodeRoot, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = MapOrigin;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(CaptionWidth, CaptionHeight);
            rect.anchoredPosition = nodePosition + new Vector2(0f, -(nodeSize * 0.5f + CaptionGap));

            var text = go.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = 11;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperCenter;
            text.color = Theme.TextLight;
            text.raycastTarget = false;
            text.text = node.Layer == 0 ? "Start" : NodeDisplayNames[node.Type];
            return text;
        }

        private void EnsurePlayerToken()
        {
            if (playerToken != null)
            {
                // Keep it last in the hierarchy so it always draws over the nodes it walks between.
                playerToken.SetAsLastSibling();
                return;
            }

            var go = new GameObject("PlayerToken", typeof(RectTransform));
            go.transform.SetParent(content, false);
            var image = go.AddComponent<Image>();
            image.color = Theme.TabSelectedBg;
            image.raycastTarget = false;

            playerToken = go.GetComponent<RectTransform>();
            playerToken.anchorMin = playerToken.anchorMax = MapOrigin;
            playerToken.pivot = new Vector2(0.5f, 0.5f);
            playerToken.sizeDelta = new Vector2(PlayerTokenSize, PlayerTokenSize);

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var text = label.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = 10;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Theme.TextDark;
            text.raycastTarget = false;
            text.text = "YOU";
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            playerToken.SetAsLastSibling();
        }

        /// <summary>The token stands just above its node rather than on top of it, so it never hides
        /// the node's own icon.</summary>
        private Vector2 PlayerPositionFor(string nodeId)
        {
            var view = nodeViews[nodeId];
            return view.Rect.anchoredPosition + new Vector2(0f, view.Rect.sizeDelta.y * 0.5f + PlayerTokenSize * 0.55f);
        }

        private void OnNodeClicked(string nodeId)
        {
            if (IsMoving || !Traversal.CanMoveTo(nodeId))
            {
                return;
            }

            Traversal.MoveTo(nodeId);
            moveRoutine = StartCoroutine(WalkTo(nodeId));
        }

        private IEnumerator WalkTo(string nodeId)
        {
            // Nothing is clickable mid-step — the map only accepts input from a settled position.
            foreach (var view in nodeViews.Values)
            {
                view.Button.interactable = false;
            }

            Vector2 from = playerToken.anchoredPosition;
            Vector2 to = PlayerPositionFor(nodeId);
            float fromScroll = scrollRect != null ? scrollRect.horizontalNormalizedPosition : 0f;
            float toScroll = ScrollPositionFor(to.x);

            float elapsed = 0f;
            while (elapsed < MoveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / MoveDuration));
                playerToken.anchoredPosition = Vector2.Lerp(from, to, t);
                SetScroll(Mathf.Lerp(fromScroll, toScroll, t));
                yield return null;
            }

            playerToken.anchoredPosition = to;
            SetScroll(toScroll);

            moveRoutine = null;
            Refresh();
        }

        /// <summary>Repaints every node and edge for the player's current position: where they've
        /// been, where they can go, and everything still out of reach.</summary>
        private void Refresh()
        {
            foreach (var view in nodeViews.Values)
            {
                bool isCurrent = view.Node.Id == Traversal.CurrentNodeId;
                bool isAvailable = Traversal.CanMoveTo(view.Node.Id);
                bool isVisited = Traversal.HasVisited(view.Node.Id);

                view.Button.interactable = isAvailable;
                view.Image.color = isCurrent || isAvailable ? view.BaseColor
                    : isVisited ? Dim(view.BaseColor, 0.75f)
                    : Dim(view.BaseColor, 0.4f);

                view.Label.color = isCurrent || isAvailable || isVisited ? Theme.TextLight : Theme.TextMuted;
            }

            foreach (var edge in edgeViews)
            {
                bool isWalked = WasWalked(edge);
                bool isOffered = edge.FromId == Traversal.CurrentNodeId;

                edge.Image.color = isWalked ? Theme.TabSelectedBg
                    : isOffered ? new Color(1f, 1f, 1f, 0.85f)
                    : new Color(1f, 1f, 1f, 0.22f);
                edge.Rect.sizeDelta = new Vector2(
                    edge.Rect.sizeDelta.x,
                    isWalked || isOffered ? EdgeView.HighlightThickness : EdgeView.NormalThickness);
            }

            UpdateStatusText();
        }

        /// <summary>An edge counts as walked only if its two ends are consecutive steps on the path
        /// actually taken — two visited nodes can be joined by an edge the player never used.</summary>
        private bool WasWalked(EdgeView edge)
        {
            var visited = Traversal.VisitedNodeIds;
            for (int i = 1; i < visited.Count; i++)
            {
                if (visited[i - 1] == edge.FromId && visited[i] == edge.ToId)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateStatusText()
        {
            if (statusText == null)
            {
                return;
            }

            var map = Traversal.Map;
            statusText.text = Traversal.IsComplete
                ? $"Gym reached — the Location's mandatory finale.   (seed {map.Seed})"
                : $"Step {Traversal.CurrentNode.Layer} / {map.LayerCount - 1} — pick one of {Traversal.AvailableNextNodes.Count} paths.   (seed {map.Seed})";
        }

        private float ScrollPositionFor(float nodeX)
        {
            if (scrollRect == null || scrollRect.viewport == null)
            {
                return 0f;
            }

            float viewportWidth = scrollRect.viewport.rect.width;
            float scrollable = content.rect.width - viewportWidth;
            if (scrollable <= 0f)
            {
                // A map that already fits edge to edge — the common case at these spacings — has
                // nothing to scroll to.
                return 0f;
            }

            // Centers the player's node in the viewport where there's room to do so.
            return Mathf.Clamp01((nodeX - viewportWidth * 0.5f) / scrollable);
        }

        private void SetScroll(float normalized)
        {
            if (scrollRect != null)
            {
                scrollRect.horizontalNormalizedPosition = normalized;
            }
        }

        /// <summary>Loads a node type's real icon from Resources/Sprites/Nodes (see
        /// NodeIconFileNames), cached per type — including a miss, so a missing file doesn't retry
        /// Resources.Load on every node of that type.</summary>
        private static Sprite LoadIcon(NodeType type)
        {
            if (IconCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            Sprite sprite = null;
            if (NodeIconFileNames.TryGetValue(type, out var fileName))
            {
                sprite = Resources.Load<Sprite>($"Sprites/Nodes/{fileName}");
            }

            IconCache[type] = sprite;
            return sprite;
        }

        private static Color Dim(Color color, float factor) =>
            new Color(color.r * factor, color.g * factor, color.b * factor, color.a);

        private sealed class NodeView
        {
            public RegionMapNode Node;
            public RectTransform Rect;
            public Image Image;
            public Button Button;
            public Text Label;
            public Color BaseColor;
        }

        private sealed class EdgeView
        {
            public const float NormalThickness = 4f;
            public const float HighlightThickness = 7f;

            public string FromId;
            public string ToId;
            public RectTransform Rect;
            public Image Image;
        }
    }
}
