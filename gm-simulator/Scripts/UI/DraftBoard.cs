using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class DraftBoard : Control
{
    private OptionButton _posFilter = null!;
    private VBoxContainer _boardList = null!;
    private PackedScene _prospectCardScene = null!;

    // Tag labels for tagging prospects
    private static readonly string[] Tags = { "Must Have", "Good Value", "Reach", "Do Not Draft" };
    private static readonly Color[] TagColors = {
        ThemeColors.Success,    // Must Have - green
        ThemeColors.Info,       // Good Value - blue
        ThemeColors.Warning,    // Reach - yellow
        ThemeColors.Danger,     // Do Not Draft - red
    };

    public override void _Ready()
    {
        _posFilter = GetNode<OptionButton>("MarginContainer/VBox/HeaderHBox/PosFilter");
        _boardList = GetNode<VBoxContainer>("MarginContainer/VBox/ScrollContainer/BoardList");
        _prospectCardScene = GD.Load<PackedScene>("res://Scenes/Scouting/ProspectCard.tscn");

        SetupFilters();
        RefreshBoard();
    }

    private void SetupFilters()
    {
        _posFilter.AddItem("All Positions", 0);
        int idx = 1;
        foreach (Pos pos in Enum.GetValues<Pos>())
        {
            _posFilter.AddItem(pos.ToString(), idx++);
        }
    }

    private void RefreshBoard()
    {
        foreach (var child in _boardList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        var prospects = GetOrderedProspects(gm);

        if (prospects.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "No prospects on your board.\nUse Scouting to add prospects.",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            emptyLabel.AddThemeColorOverride("font_color", ThemeColors.TextTertiary);
            _boardList.AddChild(emptyLabel);
            return;
        }

        int rank = 1;
        foreach (var prospect in prospects)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);

            // Rank number
            var rankLabel = new Label
            {
                Text = $"{rank}.",
                CustomMinimumSize = new Vector2(30, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            rankLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(rankLabel);

            // Name
            var nameLabel = new Label
            {
                Text = prospect.FullName,
                CustomMinimumSize = new Vector2(140, 0),
            };
            nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(nameLabel);

            // Position
            var posLabel = new Label
            {
                Text = prospect.Position.ToString(),
                CustomMinimumSize = new Vector2(50, 0),
            };
            posLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(posLabel);

            // College
            var collegeLabel = new Label
            {
                Text = prospect.College,
                CustomMinimumSize = new Vector2(100, 0),
                ClipText = true,
            };
            collegeLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(collegeLabel);

            // Scouting grade
            var gradeLabel = new Label
            {
                Text = prospect.ScoutGrade.ToString(),
                CustomMinimumSize = new Vector2(80, 0),
            };
            gradeLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(gradeLabel);

            // Projected round
            var projLabel = new Label
            {
                Text = $"Rd {prospect.ProjectedRound}",
                CustomMinimumSize = new Vector2(40, 0),
            };
            projLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(projLabel);

            // Tag indicator
            if (gm.DraftBoardTags.TryGetValue(prospect.Id, out int tagIdx) && tagIdx >= 0 && tagIdx < Tags.Length)
            {
                var tagLabel = new Label { Text = Tags[tagIdx] };
                tagLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Caption);
                tagLabel.AddThemeColorOverride("font_color", TagColors[tagIdx]);
                hbox.AddChild(tagLabel);
            }

            // Move up button
            string pid = prospect.Id;
            var upBtn = new Button { Text = "^", CustomMinimumSize = new Vector2(30, 0) };
            upBtn.Pressed += () => MoveUp(pid);
            hbox.AddChild(upBtn);

            // Move down button
            var downBtn = new Button { Text = "v", CustomMinimumSize = new Vector2(30, 0) };
            downBtn.Pressed += () => MoveDown(pid);
            hbox.AddChild(downBtn);

            // Tag cycle button
            var tagBtn = new Button { Text = "Tag", CustomMinimumSize = new Vector2(40, 0) };
            tagBtn.Pressed += () => CycleTag(pid);
            hbox.AddChild(tagBtn);

            // View button
            var viewBtn = new Button { Text = "View", CustomMinimumSize = new Vector2(50, 0) };
            viewBtn.Pressed += () => OpenProspectCard(pid);
            hbox.AddChild(viewBtn);

            // Remove from board button
            var removeBtn = new Button { Text = "X", CustomMinimumSize = new Vector2(30, 0) };
            removeBtn.Pressed += () => RemoveFromBoard(pid);
            hbox.AddChild(removeBtn);

            _boardList.AddChild(hbox);
            rank++;
        }
    }

    private List<Prospect> GetOrderedProspects(GameManager gm)
    {
        // Only show prospects the player has explicitly added to their board
        if (gm.DraftBoardOrder.Count == 0)
            return new List<Prospect>();

        var lookup = gm.CurrentDraftClass.ToDictionary(p => p.Id);
        var ordered = new List<Prospect>();
        foreach (var id in gm.DraftBoardOrder)
        {
            if (lookup.TryGetValue(id, out var prospect))
                ordered.Add(prospect);
        }

        // Apply position filter
        int posIdx = _posFilter.Selected;
        if (posIdx > 0)
        {
            var positions = Enum.GetValues<Pos>();
            if (posIdx - 1 < positions.Length)
            {
                var targetPos = positions[posIdx - 1];
                ordered = ordered.Where(p => p.Position == targetPos).ToList();
            }
        }

        return ordered;
    }

    private void MoveUp(string prospectId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int idx = gm.DraftBoardOrder.IndexOf(prospectId);
        if (idx <= 0) return;

        gm.DraftBoardOrder.RemoveAt(idx);
        gm.DraftBoardOrder.Insert(idx - 1, prospectId);
        RefreshBoard();
    }

    private void MoveDown(string prospectId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int idx = gm.DraftBoardOrder.IndexOf(prospectId);
        if (idx < 0 || idx >= gm.DraftBoardOrder.Count - 1) return;

        gm.DraftBoardOrder.RemoveAt(idx);
        gm.DraftBoardOrder.Insert(idx + 1, prospectId);
        RefreshBoard();
    }

    private void RemoveFromBoard(string prospectId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.DraftBoardOrder.Remove(prospectId);
        gm.DraftBoardTags.Remove(prospectId);
        RefreshBoard();
    }

    private void CycleTag(string prospectId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (!gm.DraftBoardTags.TryGetValue(prospectId, out int current))
            current = -1;

        current = (current + 1) % (Tags.Length + 1) - 1; // -1, 0, 1, 2, 3, -1, ...
        gm.DraftBoardTags[prospectId] = current;
        RefreshBoard();
    }

    private void OpenProspectCard(string prospectId)
    {
        var card = _prospectCardScene.Instantiate<ProspectCard>();
        card.Initialize(prospectId);
        GetTree().Root.AddChild(card);
    }

    private void OnFilterChanged(int _idx) => RefreshBoard();
}
