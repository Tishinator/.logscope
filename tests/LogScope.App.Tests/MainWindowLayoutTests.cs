using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;

namespace LogScope.App.Tests;

/// <summary>
/// Visual / layout-consistency regression tests for MainWindow.
///
/// These guard against the v0.4.6 regression where the summary bar was given
/// DockPanel.Dock="Bottom" but placed inside a Grid (which ignores the attached
/// property). The opaque Border then stretched to fill the content cell and painted
/// over the entire landing screen — the window looked blank.
///
/// The plain "does not throw" smoke test cannot catch this: an opaque overlay is still
/// a *valid* layout. These assert structural + geometric invariants, all fully headless
/// (no window is shown, no pixels rendered), so they are deterministic in CI.
///
/// Note: a Window's own chrome template is not applied until it is shown, so we measure
/// and walk the window's Content (the root DockPanel) directly, and resolve named elements
/// through the namescope via FindName rather than by walking a not-yet-realized tree.
/// </summary>
[Xunit.Collection(WpfCollection.Name)]
public class MainWindowLayoutTests
{
    private readonly WpfTestFixture _wpf;
    public MainWindowLayoutTests(WpfTestFixture wpf) => _wpf = wpf;

    private static MainWindow BuildArrangedWindow()
    {
        var window = new MainWindow { DataContext = new LogScope.App.ViewModels.MainViewModel() };
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(1280, 800));
        root.Arrange(new Rect(0, 0, 1280, 800));
        root.UpdateLayout();
        return window;
    }

    private static T Named<T>(MainWindow window, string name) where T : FrameworkElement
    {
        var found = window.FindName(name) as T;
        found.Should().NotBeNull($"element '{name}' should exist in the MainWindow namescope");
        return found!;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }

    /// <summary>The opaque fill color of an element's background, or null if it is not fully opaque.</summary>
    private static Color? OpaqueBackground(DependencyObject o)
    {
        Brush? brush = o switch
        {
            Border b => b.Background,
            Panel p => p.Background,
            Control c => c.Background,
            _ => null,
        };
        return brush is SolidColorBrush s && s.Color.A == 255 ? s.Color : null;
    }

    /// <summary>
    /// Every element that carries a local DockPanel.Dock value must be a direct visual child
    /// of a DockPanel. Setting Dock on a child of a Grid/Canvas is a silent no-op that lets an
    /// opaque element stretch across the whole cell — the exact v0.4.6 bug.
    /// </summary>
    [Fact]
    public void DockedElements_MustBeDirectChildrenOfDockPanel()
    {
        _wpf.Run(() =>
        {
            var window = BuildArrangedWindow();
            var root = (FrameworkElement)window.Content;

            var walked = Descendants(root).ToList();
            walked.Should().NotBeEmpty("the content visual tree must be realized after Arrange");

            var offenders = new List<string>();
            foreach (var obj in walked)
            {
                if (obj.ReadLocalValue(DockPanel.DockProperty) == DependencyProperty.UnsetValue)
                    continue; // Dock not explicitly set on this element.

                if (VisualTreeHelper.GetParent(obj) is not DockPanel)
                {
                    var actualParent = VisualTreeHelper.GetParent(obj);
                    var desc = obj is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name)
                        ? $"{obj.GetType().Name} '{fe.Name}'"
                        : obj.GetType().Name;
                    offenders.Add($"{desc} has DockPanel.Dock set but its parent is {actualParent?.GetType().Name ?? "null"}");
                }
            }

            offenders.Should().BeEmpty(
                "DockPanel.Dock only has effect on direct children of a DockPanel; otherwise the " +
                "element ignores the dock and an opaque one overlays sibling content");
        });
    }

    /// <summary>The landing screen must be present, marked visible, and arranged with real size when no workspace is open.</summary>
    [Fact]
    public void LandingScreen_IsShownAndSized_WhenNoWorkspace()
    {
        _wpf.Run(() =>
        {
            var window = BuildArrangedWindow();
            var landing = Named<Grid>(window, "LandingPanel");

            landing.Visibility.Should().Be(Visibility.Visible, "no workspace is open, so the landing screen should show");
            landing.ActualWidth.Should().BeGreaterThan(0);
            landing.ActualHeight.Should().BeGreaterThan(0);
        });
    }

    /// <summary>
    /// No opaque sibling rendered after the landing screen may cover it. This catches any
    /// opaque overlay filling the content root (the v0.4.6 summary-bar-in-Grid symptom),
    /// using arranged geometry only — no rendering required.
    /// </summary>
    [Fact]
    public void LandingScreen_IsNotCoveredByOpaqueSibling()
    {
        _wpf.Run(() =>
        {
            var window = BuildArrangedWindow();
            var contentRoot = Named<Grid>(window, "ContentRoot");
            var landing = Named<Grid>(window, "LandingPanel");

            var children = contentRoot.Children.Cast<UIElement>().ToList();
            int landingIndex = children.IndexOf(landing);
            landingIndex.Should().BeGreaterThanOrEqualTo(0);

            var landingBounds = landing.TransformToAncestor(contentRoot)
                .TransformBounds(new Rect(landing.RenderSize));

            for (int i = landingIndex + 1; i < children.Count; i++)
            {
                var sibling = children[i];
                if (sibling.Visibility != Visibility.Visible) continue;
                if (OpaqueBackground(sibling) is not { } color) continue;

                var siblingBounds = sibling.TransformToAncestor(contentRoot)
                    .TransformBounds(new Rect(sibling.RenderSize));
                siblingBounds.Inflate(0.5, 0.5);

                siblingBounds.Contains(landingBounds).Should().BeFalse(
                    $"an opaque #{color.R:X2}{color.G:X2}{color.B:X2} sibling ({sibling.GetType().Name}) rendered " +
                    "after the landing screen fully covers it — the landing content would be invisible");
            }
        });
    }
}
