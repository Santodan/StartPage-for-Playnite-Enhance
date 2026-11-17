using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LandingPage.Models.Layout
{
    // Regroup just two matching leaf rectangles; retain every subtree outside their band.
    public sealed class AdjacentPanelMerge
    {
        private sealed class Band { public GridNode Node; public double Start,End; }
        private const double Epsilon = .000001;
        public GridNode Owner { get; private set; }
        public GridNode Kept { get; private set; }
        public GridNode Removed { get; private set; }
        private GridNode left,right;
        private int index;
        private Orientation cross;
        private double start,end;
        private List<Band> leftBands,rightBands;
        private static bool Container(GridNode node) => node.ViewProperties == null && node.Padding == 0 && node.HorizontalAlignment == HorizontalAlignment.Stretch && node.VerticalAlignment == VerticalAlignment.Stretch;
        private static bool Weights(GridNode node) => node.Children.All(child => child.Size.IsStar && child.Size.Value > 0);
        private static bool Near(double a,double b) => Math.Abs(a-b)<Epsilon;
        private static bool PathBands(GridNode node,GridNode target,Orientation orientation,double start,double end,List<Band> bands)
        {
            if(node == target) { bands.Add(new Band { Node = node,Start = start,End = end }); return true; }
            if(!Container(node) || (node.Children.Count > 1 && node.Orientation != orientation) || !Weights(node)) return false;
            var total = node.Children.Sum(child => child.Size.Value); if(total <= 0) return false;
            var cursor = start; var found = false;
            foreach(var child in node.Children) {
                var next = cursor+(end-start)*child.Size.Value/total;
                if(child.Enumerate().Contains(target)) { if(!PathBands(child,target,orientation,cursor,next,bands)) return false; found = true; }
                else bands.Add(new Band { Node = child,Start = cursor,End = next });
                cursor = next;
            }
            return found;
        }
        private static GridNode MatchingLeaf(GridNode node,Orientation orientation,double start,double end,double wantedStart,double wantedEnd)
        {
            if(node.IsLeaf) return Near(start,wantedStart) && Near(end,wantedEnd) ? node : null;
            if(!Container(node) || (node.Children.Count > 1 && node.Orientation != orientation) || !Weights(node)) return null;
            var total = node.Children.Sum(child => child.Size.Value); if(total <= 0) return null;
            var cursor = start;
            foreach(var child in node.Children) {
                var next = cursor+(end-start)*child.Size.Value/total;
                if(wantedStart >= cursor-Epsilon && wantedEnd <= next+Epsilon) {
                    var match = MatchingLeaf(child,orientation,cursor,next,wantedStart,wantedEnd); if(match != null) return match;
                }
                cursor = next;
            }
            return null;
        }
        public static AdjacentPanelMerge Find(GridNode owner,GridNode source,Orientation orientation,bool next)
        {
            if(owner == null || source == null || !source.IsLeaf || owner.Orientation != orientation) return null;
            var sourceIndex = owner.Children.ToList().FindIndex(child => child.Enumerate().Contains(source));
            var otherIndex = sourceIndex+(next ? 1 : -1);
            if(sourceIndex < 0 || otherIndex < 0 || otherIndex >= owner.Children.Count) return null;
            var branch = owner.Children[sourceIndex]; var other = owner.Children[otherIndex];
            if(!((branch.Size.IsStar && other.Size.IsStar) || (branch.Size.IsAbsolute && other.Size.IsAbsolute)) || branch.Size.Value <= 0 || other.Size.Value <= 0) return null;
            var cross = orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
            var bands = new List<Band>(); if(!PathBands(branch,source,cross,0,1,bands)) return null;
            var selected = bands.First(item => item.Node == source);
            var target = MatchingLeaf(other,cross,0,1,selected.Start,selected.End); if(target == null) return null;
            var otherBands = new List<Band>(); if(!PathBands(other,target,cross,0,1,otherBands)) return null;
            return new AdjacentPanelMerge { Owner = owner,Kept = source.ViewProperties == null ? target : source,Removed = source.ViewProperties == null ? source : target,
                left = sourceIndex < otherIndex ? branch : other,right = sourceIndex < otherIndex ? other : branch,index = Math.Min(sourceIndex,otherIndex),cross = cross,start = selected.Start,end = selected.End,
                leftBands = sourceIndex < otherIndex ? bands : otherBands,rightBands = sourceIndex < otherIndex ? otherBands : bands };
        }
        private GridNode Strip(List<Band> bands,double from,double to,GridLength width)
        {
            var nodes = bands.Where(band => band.Start >= from-Epsilon && band.End <= to+Epsilon).ToList();
            if(nodes.Count == 1) { nodes[0].Node.Size = width; return nodes[0].Node; }
            var result = new GridNode { Orientation = cross,Size = width };
            foreach(var band in nodes) { band.Node.Size = new GridLength(band.End-band.Start,GridUnitType.Star); result.Children.Add(band.Node); }
            return result;
        }
        private GridNode BandRow(double from,double to,GridLength leftSize,GridLength rightSize)
        {
            var row = new GridNode { Orientation = Owner.Orientation,Size = new GridLength(to-from,GridUnitType.Star) };
            row.Children.Add(Strip(leftBands,from,to,leftSize)); row.Children.Add(Strip(rightBands,from,to,rightSize)); return row;
        }
        public void Apply()
        {
            var leftSize = left.Size; var rightSize = right.Size;
            var combined = new GridLength(leftSize.Value+rightSize.Value+(leftSize.IsAbsolute ? 5 : 0),leftSize.GridUnitType);
            GridNode replacement;
            if(Near(start,0) && Near(end,1)) { Kept.Size = combined; replacement = Kept; }
            else {
                replacement = new GridNode { Orientation = cross,Size = combined };
                if(start > Epsilon) replacement.Children.Add(BandRow(0,start,leftSize,rightSize));
                Kept.Size = new GridLength(end-start,GridUnitType.Star); replacement.Children.Add(Kept);
                if(end < 1-Epsilon) replacement.Children.Add(BandRow(end,1,leftSize,rightSize));
            }
            Owner.Children = new ObservableCollection<GridNode>(Owner.Children.Take(index).Concat(new[] { replacement }).Concat(Owner.Children.Skip(index+2)));
        }
    }
}
