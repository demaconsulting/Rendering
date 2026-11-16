// <copyright file="GallerySysmlNotation.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Builds the gallery diagrams for the SysML-agnostic notation primitives: relationship end markers
///     (hollow triangle with two or four dots, circled plus, diamonds, circle, crossbar), outline and text
///     styles, centered compartment titles, folder name-in-tab, thick fork/join bars, the terminate badge,
///     send/accept corner icons, dashed bands and rounded lifelines. The two sheets are hand-built
///     <see cref="LayoutTree"/>s (they belong to the <c>custom-rendering</c> group, which is the only group
///     allowed to bypass the layout engine), while the graph-driven diagrams go through
///     <see cref="LayoutEngine"/> to prove the appearance reaches the placed boxes.
/// </summary>
internal static class GallerySysmlNotation
{
    /// <summary>Vertical distance between two rows of the end-marker sheet.</summary>
    private const double EndMarkerRowHeight = 54.0;

    /// <summary>Height of a node box on the end-marker sheet.</summary>
    private const double EndMarkerBoxHeight = 32.0;

    /// <summary>Corner radius used by every rounded box on the sheets (the Light theme's corner radius is small).</summary>
    private const double RoundedRadius = 10.0;

    /// <summary>
    ///     One row of the end-marker sheet: the relationship name, the marker, whether the marker sits at
    ///     the line's target end (the usage-side line starts at the right node and ends at the left one) or
    ///     at its source end (the line starts at the left node), and the rule the row demonstrates.
    /// </summary>
    /// <param name="Name">Relationship name, drawn on the connector.</param>
    /// <param name="Style">The end marker.</param>
    /// <param name="AtSource">Whether the marker is the line's <see cref="LayoutLine.SourceEnd"/>.</param>
    /// <param name="LeftNode">Label of the node the marker touches.</param>
    /// <param name="RightNode">Label of the node at the plain end of the line.</param>
    /// <param name="Rule">Caption of the SysML v2 notation rule.</param>
    private sealed record EndMarkerRow(
        string Name,
        EndMarkerStyle Style,
        bool AtSource,
        string LeftNode,
        string RightNode,
        string Rule);

    /// <summary>
    ///     The SysML relationship end markers, one labeled row per relationship, each end labeled with its
    ///     <see cref="EndMarkerStyle"/> name. Triangles are drawn at the line's target end and the
    ///     membership glyphs (diamonds, circled plus, circle) at its source end, so the sheet proves both
    ///     ends of a line decorate identically.
    /// </summary>
    /// <param name="theme">The theme the sheet will be rendered with.</param>
    /// <returns>A hand-built tree of two-box edges, one per end-marker glyph.</returns>
    public static LayoutTree EndMarkers(Theme theme)
    {
        EndMarkerRow[] rows =
        [
            new("specialization", EndMarkerStyle.HollowTriangle, false, "definition-node", "definition-node", "HollowTriangle (target end)"),
            new("feature typing", EndMarkerStyle.HollowTriangleTwoDots, false, "definition-node", "usage-node", "HollowTriangleTwoDots (target end)"),
            new("subsetting", EndMarkerStyle.HollowTriangle, false, "usage-node", "usage-node", "HollowTriangle (target end)"),
            new("reference subsetting", EndMarkerStyle.HollowTriangleFourDots, false, "usage-node", "usage-node", "HollowTriangleFourDots (target end)"),
            new("redefinition", EndMarkerStyle.HollowTriangleCrossbar, false, "usage-node", "usage-node", "HollowTriangleCrossbar (target end)"),
            new("composite membership", EndMarkerStyle.FilledDiamond, true, "type-node", "usage-node", "FilledDiamond (source end)"),
            new("reference membership", EndMarkerStyle.HollowDiamond, true, "type-node", "usage-node", "HollowDiamond (source end)"),
            new("owned membership", EndMarkerStyle.CircledPlus, true, "namespace-node", "element-node", "CircledPlus (source end)"),
            new("unowned membership", EndMarkerStyle.Circle, true, "namespace-node", "element-node", "Circle (source end)"),
        ];

        const double LeftX = 20.0;
        const double BoxWidth = 140.0;
        const double RightX = 420.0;
        const double CaptionX = 590.0;
        const double Top = 50.0;

        var nodes = new List<LayoutNode>
        {
            Text(20, 24, 560, "SysML v2 relationship end markers", theme.FontSizeTitle, bold: true),
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var centreY = Top + (i * EndMarkerRowHeight) + (EndMarkerBoxHeight / 2.0);
            var boxY = centreY - (EndMarkerBoxHeight / 2.0);

            nodes.Add(NodeBox(LeftX, boxY, BoxWidth, EndMarkerBoxHeight, row.LeftNode));
            nodes.Add(NodeBox(RightX, boxY, BoxWidth, EndMarkerBoxHeight, row.RightNode));

            var start = row.AtSource
                ? new Point2D(LeftX + BoxWidth, centreY)
                : new Point2D(RightX, centreY);
            var end = row.AtSource
                ? new Point2D(RightX, centreY)
                : new Point2D(LeftX + BoxWidth, centreY);
            nodes.Add(new LayoutLine(
                [start, end],
                row.AtSource ? row.Style : EndMarkerStyle.None,
                row.AtSource ? EndMarkerStyle.None : row.Style,
                LineStyle.Solid,
                row.Name));

            nodes.Add(Text(CaptionX, centreY + 4, 300, row.Rule, theme.FontSizeBody, bold: false));
        }

        var height = Top + (rows.Length * EndMarkerRowHeight);
        return new LayoutTree(CaptionX + 270, height, nodes);
    }

    /// <summary>
    ///     One sheet showing every notation primitive next to its legacy counterpart: dashed and solid
    ///     rounded boxes with regular, italic (abstract) and legacy bold names, compartments with centered
    ///     italic titles, folders with the name in the tab (solid and dashed) and in the body, thick
    ///     fork/join bars, the terminate badge, send/accept corner icons, a dashed swim lane and rounded
    ///     lifelines with a keyword line.
    /// </summary>
    /// <param name="theme">The theme the sheet will be rendered with.</param>
    /// <returns>A hand-built tree exercising each primitive.</returns>
    public static LayoutTree NotationPrimitives(Theme theme)
    {
        var nodes = new List<LayoutNode>();
        AddOutlineAndTextStyles(nodes, theme);
        AddCompartmentsAndFolders(nodes, theme);
        AddActionNotation(nodes, theme);
        AddLifelines(nodes, theme);
        return new LayoutTree(1010, 740, nodes);
    }

    /// <summary>
    ///     A graph-driven diagram proving <see cref="LayoutGraphNode.Appearance"/> reaches the placed
    ///     boxes through the hierarchical and layered algorithms: a package folder with its name in the
    ///     tab holding a dashed rounded box and a regular-named box, beside a dashed imported folder.
    /// </summary>
    /// <returns>A compound graph carrying <see cref="BoxAppearance"/> on several nodes.</returns>
    public static LayoutGraph BoxAppearanceGraph()
    {
        var theme = Themes.Dark;
        var titleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);

        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);

        var pkg = graph.AddNode("powertrain", 10, 10);
        pkg.Label = "Powertrain";
        pkg.Shape = BoxShape.Folder;
        pkg.Appearance = new BoxAppearance(
            NameStyle: TextStyle.Regular,
            FolderTitle: FolderTitlePlacement.Tab);

        var engine = pkg.Children.AddNode("engine", 150, titleHeight);
        engine.Label = "Engine";
        engine.Keyword = "part def";
        engine.Shape = BoxShape.RoundedRectangle;
        engine.RoundedCornerRadius = RoundedRadius;
        engine.Appearance = new BoxAppearance(
            LineStyle.Dashed,
            NameStyle: TextStyle.Regular,
            KeywordStyle: TextStyle.Regular);

        var motor = pkg.Children.AddNode("motor", 150, titleHeight);
        motor.Label = "Motor";
        motor.Keyword = "part def";
        motor.Shape = BoxShape.RoundedRectangle;
        motor.RoundedCornerRadius = RoundedRadius;
        motor.Appearance = new BoxAppearance(
            NameStyle: TextStyle.Italic,
            KeywordStyle: TextStyle.Regular);

        var edge = pkg.Children.AddEdge("motor-engine", motor, engine);
        edge.TargetEnd = EndMarkerStyle.HollowTriangle;

        var imported = graph.AddNode("imported", 10, 10);
        imported.Label = "ISQ";
        imported.Shape = BoxShape.Folder;
        imported.Appearance = new BoxAppearance(
            LineStyle.Dashed,
            NameStyle: TextStyle.Regular,
            FolderTitle: FolderTitlePlacement.Tab);
        var quantities = imported.Children.AddNode("quantities", 110, 40);
        quantities.Label = "Quantities";

        var importEdge = graph.AddEdge("powertrain-imports-isq", pkg, imported);
        importEdge.TargetEnd = EndMarkerStyle.OpenChevron;
        importEdge.LineStyle = LineStyle.Dashed;
        importEdge.Label = "import";

        return graph;
    }

    /// <summary>
    ///     A state-machine style graph whose reversed (back) edges end in a wrap-around corridor and whose
    ///     every edge ends in an open chevron. Permanent evidence that the final approach of a back edge
    ///     is long enough for the rendered chevron plus the rounded corner before it (no collapsed corner
    ///     inside the arrowhead) and that no connector label's opaque background cuts a node border.
    /// </summary>
    /// <returns>A seven-transition, six-state graph laid out top to bottom.</returns>
    public static LayoutGraph BackEdgeEntryApproach()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        graph.Set(CoreOptions.MergeParallelEdges, false);

        var idle = StateNode(graph, "idle", "Idle");
        var closing = StateNode(graph, "doorsClosing", "DoorsClosing");
        var moving = StateNode(graph, "moving", "Moving");
        var arrived = StateNode(graph, "atFloor", "AtFloor");
        var open = StateNode(graph, "doorsOpen", "DoorsOpen");
        var waiting = StateNode(graph, "waiting", "Waiting");

        Transition(graph, "callReceived", idle, closing, "callReceived");
        Transition(graph, "doorsClosed", closing, moving, "doorsClosed");
        Transition(graph, "arrive", moving, arrived, "atFloor");
        Transition(graph, "openDoors", arrived, open, "doorsAreOpen");
        Transition(graph, "linger", open, waiting, "linger");
        Transition(graph, "timeout", waiting, closing, "timeout");
        Transition(graph, "idleTimeout", waiting, idle, "idleTimeout");

        return graph;
    }

    /// <summary>Adds a plain state box of the standard size.</summary>
    private static LayoutGraphNode StateNode(LayoutGraph graph, string id, string label)
    {
        var node = graph.AddNode(id, 130, 44);
        node.Label = label;
        node.Shape = BoxShape.RoundedRectangle;
        node.RoundedCornerRadius = RoundedRadius;
        return node;
    }

    /// <summary>Adds a labeled transition ending in an open chevron.</summary>
    private static void Transition(
        LayoutGraph graph,
        string id,
        LayoutGraphNode source,
        LayoutGraphNode target,
        string label)
    {
        var edge = graph.AddEdge(id, source, target);
        edge.TargetEnd = EndMarkerStyle.OpenChevron;
        edge.Label = label;
    }

    /// <summary>Row 1 of the primitives sheet: outline style and name/keyword text style.</summary>
    private static void AddOutlineAndTextStyles(List<LayoutNode> nodes, Theme theme)
    {
        nodes.Add(Text(20, 22, 400, "Outline and text style", theme.FontSizeBody, bold: true));
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + 8;
        const double Y = 36.0;

        var variants = new (double X, string Label, string Caption, BoxAppearance? Appearance)[]
        {
            (20, "Vehicle", "solid, regular name and keyword", new BoxAppearance(NameStyle: TextStyle.Regular, KeywordStyle: TextStyle.Regular)),
            (230, "Imported", "dashed outline", new BoxAppearance(LineStyle.Dashed, NameStyle: TextStyle.Regular, KeywordStyle: TextStyle.Regular)),
            (440, "Abstract", "italic (abstract) name", new BoxAppearance(NameStyle: TextStyle.Italic, KeywordStyle: TextStyle.Regular)),
            (650, "Legacy", "default: bold name, italic keyword", null),
        };

        foreach (var (x, label, caption, appearance) in variants)
        {
            nodes.Add(new LayoutBox(
                x,
                Y,
                180,
                height,
                label,
                0,
                BoxShape.RoundedRectangle,
                [],
                [],
                Keyword: "part def",
                RoundedCornerRadius: RoundedRadius,
                CenterTitle: true,
                Appearance: appearance));
            nodes.Add(Text(x, Y + height + 18, 200, caption, theme.FontSizeBody - 1, bold: false));
        }
    }

    /// <summary>Row 2 of the primitives sheet: compartment titles and folders.</summary>
    private static void AddCompartmentsAndFolders(List<LayoutNode> nodes, Theme theme)
    {
        const double Y = 170.0;
        nodes.Add(Text(20, Y - 14, 400, "Compartments: centered italic regular titles (left) and legacy (right)", theme.FontSizeBody, bold: true));

        LayoutCompartment[] centered =
        [
            new("attributes", ["mass : Real", "speed : Real"], TextAlign.Center, TextStyle.Italic),
            new("parts", ["engine : Engine"], TextAlign.Center, TextStyle.Italic),
        ];
        LayoutCompartment[] legacy =
        [
            new("attributes", ["mass : Real", "speed : Real"]),
            new("parts", ["engine : Engine"]),
        ];
        var compartmentHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true)
            + centered.Sum(c => CompartmentHeight(theme, c));

        nodes.Add(new LayoutBox(
            20, Y, 220, compartmentHeight, "Vehicle", 0, BoxShape.Rectangle, centered, [],
            Keyword: "part def",
            Appearance: new BoxAppearance(NameStyle: TextStyle.Regular, KeywordStyle: TextStyle.Regular)));
        nodes.Add(new LayoutBox(
            260, Y, 220, compartmentHeight, "Vehicle", 0, BoxShape.Rectangle, legacy, [],
            Keyword: "part def"));

        // Folders: name in tab with a nested child, dashed imported folder, and name in body.
        nodes.Add(Text(520, Y - 14, 460, "Folders: name in tab, dashed imported, name in body", theme.FontSizeBody, bold: true));
        var tabbed = new BoxAppearance(NameStyle: TextStyle.Regular, FolderTitle: FolderTitlePlacement.Tab);
        var tabHeight = BoxMetrics.FolderTabHeight(theme);
        var child = new LayoutBox(536, Y + tabHeight + 16, 110, 34, "Child", 0, BoxShape.Rectangle, [], [], CenterTitle: true);
        nodes.Add(new LayoutBox(
            520, Y, 150, tabHeight + 76, "Package", 0, BoxShape.Folder, [], [child],
            Appearance: tabbed));

        nodes.Add(new LayoutBox(
            700, Y, 130, tabHeight + 76, "Imported", 0, BoxShape.Folder, [], [],
            Appearance: new BoxAppearance(LineStyle.Dashed, NameStyle: TextStyle.Regular, FolderTitle: FolderTitlePlacement.Tab)));

        nodes.Add(new LayoutBox(
            860, Y, 130, tabHeight + 76, "Identification", 0, BoxShape.Folder, [], [],
            CenterTitle: true,
            Appearance: new BoxAppearance(NameStyle: TextStyle.Regular)));
    }

    /// <summary>Row 3 of the primitives sheet: fork/join, terminate, send/accept and a dashed swim lane.</summary>
    private static void AddActionNotation(List<LayoutNode> nodes, Theme theme)
    {
        const double Y = 390.0;
        nodes.Add(Text(20, Y, 400, "Fork / join bars and terminate", theme.FontSizeBody, bold: true));
        nodes.Add(new LayoutBadge(90, Y + 40, 90, BadgeShape.ThickHorizontalBar, "fork"));
        nodes.Add(new LayoutBadge(250, Y + 40, 60, BadgeShape.ThickVerticalBar, "join"));
        nodes.Add(new LayoutBadge(360, Y + 40, 24, BadgeShape.CrossedCircle, "terminate"));

        nodes.Add(Text(500, Y, 300, "Send / accept corner icons", theme.FontSizeBody, bold: true));
        var iconBoxHeight = 46.0;
        nodes.Add(new LayoutBox(
            500, Y + 16, 140, iconBoxHeight, "send msg", 0, BoxShape.RoundedRectangle, [], [],
            RoundedCornerRadius: RoundedRadius,
            CenterTitle: true,
            Appearance: new BoxAppearance(NameStyle: TextStyle.Regular, CornerIcon: BoxCornerIcon.Send)));
        nodes.Add(new LayoutBox(
            660, Y + 16, 140, iconBoxHeight, "accept sig", 0, BoxShape.RoundedRectangle, [], [],
            RoundedCornerRadius: RoundedRadius,
            CenterTitle: true,
            Appearance: new BoxAppearance(NameStyle: TextStyle.Regular, CornerIcon: BoxCornerIcon.Accept)));

        nodes.Add(Text(830, Y, 160, "Dashed swim lane", theme.FontSizeBody, bold: true));
        var task = new LayoutBox(884, Y + 28, 90, 34, "task", 0, BoxShape.Rectangle, [], [], CenterTitle: true);
        nodes.Add(new LayoutBand(
            830, Y + 16, 160, 70, BandOrientation.Horizontal, "Lane", [task], LineStyle.Dashed));
    }

    /// <summary>Row 4 of the primitives sheet: rounded lifelines with a keyword line beside a legacy one.</summary>
    private static void AddLifelines(List<LayoutNode> nodes, Theme theme)
    {
        const double Y = 520.0;
        nodes.Add(Text(20, Y, 500, "Lifelines: rounded header with keyword, dashed header, legacy", theme.FontSizeBody, bold: true));

        var keywordHeader = BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: true);
        nodes.Add(new LayoutLifeline(
            110, Y + 16, Y + 190, "engine : Engine", 150, keywordHeader, "part",
            BoxShape.RoundedRectangle,
            new BoxAppearance(NameStyle: TextStyle.Regular, KeywordStyle: TextStyle.Regular)));
        nodes.Add(new LayoutLifeline(
            300, Y + 16, Y + 190, "wheel : Wheel", 150, keywordHeader, "part",
            BoxShape.RoundedRectangle,
            new BoxAppearance(LineStyle.Dashed, NameStyle: TextStyle.Regular, KeywordStyle: TextStyle.Regular)));
        nodes.Add(new LayoutLifeline(
            490, Y + 16, Y + 190, "legacy", 150, BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: false)));
    }

    /// <summary>Computes the height of a titled compartment with the renderer's own layout formula.</summary>
    private static double CompartmentHeight(Theme theme, LayoutCompartment compartment) =>
        theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding
        + (compartment.Rows.Count * (theme.LabelPadding + theme.FontSizeBody))
        + theme.LabelPadding;

    /// <summary>Creates a plain node box centered on its label.</summary>
    private static LayoutBox NodeBox(double x, double y, double width, double height, string label) =>
        new(x, y, width, height, label, 0, BoxShape.Rectangle, [], [], CenterTitle: true);

    /// <summary>Creates a left-aligned text label whose Y is the vertical text position.</summary>
    private static LayoutLabel Text(double x, double y, double maxWidth, string text, double fontSize, bool bold) =>
        new(
            x,
            y,
            maxWidth,
            text,
            TextAlign.Left,
            bold ? FontWeight.Bold : FontWeight.Regular,
            FontStyle.Normal,
            fontSize);
}
