using System;
using System.Collections.Generic;
using System.Linq;
using LandingPage.Models.Layout;
using LandingPage.Settings;

namespace LandingPage.Models
{
    // Resolves the shared toolbar: one toolbar widget (identified by its panel instance id, on any dashboard page)
    // whose settings every other toolbar shows while LandingPageSettings.UseSharedToolbar is on.
    public static class SharedToolbar
    {
        private static IEnumerable<GridNode> Nodes(GridNode node)
        {
            if (node == null) yield break;
            yield return node;
            if (node.Children != null) foreach (var child in node.Children) foreach (var nested in Nodes(child)) yield return nested;
        }

        private static IEnumerable<KeyValuePair<DashboardPage,GridNode>> Layouts(LandingPageSettings settings)
        {
            // The active page's layout is the projection (GridLayout); other pages keep theirs in DashboardPages.
            var active = settings.DashboardPages?.FirstOrDefault(page => page.Id == settings.ActiveDashboardPageId);
            if (settings.GridLayout != null) yield return new KeyValuePair<DashboardPage,GridNode>(active,settings.GridLayout);
            if (settings.DashboardPages != null)
                foreach (var page in settings.DashboardPages) if (page?.Layout != null && !ReferenceEquals(page.Layout,settings.GridLayout)) yield return new KeyValuePair<DashboardPage,GridNode>(page,page.Layout);
        }

        // The source toolbar's panel and page, or null when sharing is off or the source no longer exists.
        public static ViewProperties FindSource(LandingPageSettings settings,out DashboardPage page)
        {
            page = null;
            if (settings == null || !settings.UseSharedToolbar || settings.SharedToolbarId == Guid.Empty) return null;
            foreach (var layout in Layouts(settings))
                foreach (var node in Nodes(layout.Value))
                    if (node.ViewProperties?.ViewId == "ToolbarWidget" && node.ViewProperties.InstanceId == settings.SharedToolbarId) { page = layout.Key; return node.ViewProperties; }
            return null;
        }

        public static bool IsSource(LandingPageSettings settings,Guid instanceId) => settings != null && settings.UseSharedToolbar && instanceId != Guid.Empty && settings.SharedToolbarId == instanceId;

        // The settings a toolbar shows: the shared toolbar's, unless sharing is off, the source is missing, this toolbar is
        // the source, or it keeps its own toolbar.
        public static ToolbarWidgetSettings Effective(LandingPageSettings settings,ToolbarWidgetSettings own,Guid instanceId)
        {
            if (own == null || own.KeepOwnToolbar) return own;
            var source = FindSource(settings,out _);
            if (source == null || source.InstanceId == instanceId || ReferenceEquals(source.ToolbarSettings,own)) return own;
            return source.ToolbarSettings ?? own;
        }

        public static bool Follows(LandingPageSettings settings,ToolbarWidgetSettings own,Guid instanceId) => !ReferenceEquals(Effective(settings,own,instanceId),own);
    }
}
