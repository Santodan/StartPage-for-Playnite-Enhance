using LandingPage.ViewModels.Layout;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LandingPage.Views
{
    /// <summary>
    /// Interaktionslogik für GridNodeView.xaml
    /// </summary>
    public partial class GridNodeView : UserControl
    {
        public GridNodeView()
        {
            InitializeComponent();
            PreviewMouseWheel += (sender,args) => {
                if(DataContext is GridNodeViewModel model && model.EditModeEnabled && model.GridNode.ViewProperties?.ViewId == "LibraryChart" && EditPanel.IsHitTestVisible)
                    foreach(var child in model.View.Children.OfType<LibraryStatisticsView>()) { child.ChartMouseWheel(sender,args); if(args.Handled) break; }
            };
        }

        private void OpenAppearance_Click(object sender, RoutedEventArgs e)
        {
            var model = (sender as FrameworkElement)?.DataContext as GridNodeViewModel;
            if (model?.GridNode.ViewProperties == null) return;
            DependencyObject parent = this;
            while (parent != null && !(parent is StartPageView))
                parent = VisualTreeHelper.GetParent(parent);
            (parent as StartPageView)?.ShowAppearance(model);
            e.Handled = true;
        }

        private void EditCharts_Click(object sender,RoutedEventArgs args)
        {
            DependencyObject parent = this;
            while(parent != null && !(parent is StartPageView)) parent = VisualTreeHelper.GetParent(parent);
            if(parent is StartPageView page) { page.BeginChartEditing(); if(DataContext is GridNodeViewModel model && model.GridNode.ViewProperties?.LibraryStatisticsSettings.EditCharts == false && page.DataContext is ViewModels.StartPageViewModel dashboard) dashboard.SaveCurrentPage(); }
        }
        private void EditFeaturedSections_Click(object sender, RoutedEventArgs args)
        {
            if (!(DataContext is GridNodeViewModel model) || !model.EditModeEnabled || model.GridNode.ViewProperties?.FeaturedGameSettings?.UseCustomSectionLayout != true) return;
            if ((sender as System.Windows.Controls.Primitives.ToggleButton)?.IsChecked == true)
            {
                DependencyObject parent = this;
                while (parent != null && !(parent is StartPageView)) parent = VisualTreeHelper.GetParent(parent);
                (parent as StartPageView)?.BeginFeaturedSectionEditing(model.GridNode.ViewProperties.FeaturedGameSettings);
            }
            args.Handled = true;
        }
        private void Border_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.OriginalSource == sender && DataContext is GridNodeViewModel viewModel)
            {
                viewModel.Border_PreviewDragEnter(sender, e);
            } 
        }

        private void Border_PreviewDragLeave(object sender, DragEventArgs e)
        {
            if (e.OriginalSource == sender && DataContext is GridNodeViewModel viewModel)
            {
                viewModel.Border_PreviewDragExit(sender, e);
            } 
        }

        private void Border_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource == sender && DataContext is GridNodeViewModel viewModel)
            {
                viewModel.Border_PreviewMouseMove(sender, e);
            } 
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ViewContentControl.Content is FrameworkElement ele)
            {
                ele.RaiseEvent(e);
                if (ele is Panel panel)
                {
                    foreach(FrameworkElement child in panel.Children)
                    {
                        child?.RaiseEvent(e);
                    }
                }
            }
        }
    }
}
