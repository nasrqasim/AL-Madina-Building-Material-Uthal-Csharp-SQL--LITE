using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AlMadinaERP.Wpf.Helpers
{
    public static class UniversalScrollHelper
    {
        public static void RegisterGlobalScrollHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(UIElement),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnGlobalPreviewMouseWheel),
                true);
        }

        private static void OnGlobalPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Do not intercept if event originated from a focused or open drop-down control
            var source = e.OriginalSource as DependencyObject ?? sender as DependencyObject;
            if (source == null) return;

            if (IsInsideOpenComboBox(source)) return;

            // Find the immediate target ScrollViewer (direct or within DataGrid/ListBox)
            var targetScrollViewer = FindNearestScrollViewer(source);
            if (targetScrollViewer == null) return;

            int delta = e.Delta;
            if (delta == 0) return;

            bool isScrollingDown = delta < 0;

            // Check if the current ScrollViewer has room to scroll in this direction
            bool canTargetScroll = isScrollingDown
                ? targetScrollViewer.VerticalOffset < targetScrollViewer.ScrollableHeight - 0.5
                : targetScrollViewer.VerticalOffset > 0.5;

            if (canTargetScroll)
            {
                double step = Math.Abs(delta) >= 120 ? 48.0 : 24.0;
                double amount = isScrollingDown ? step : -step;
                double targetOffset = Math.Clamp(targetScrollViewer.VerticalOffset + amount, 0, targetScrollViewer.ScrollableHeight);

                targetScrollViewer.ScrollToVerticalOffset(targetOffset);
                e.Handled = true;
            }
            else
            {
                // Boundary reached: forward scrolling to outer parent ScrollViewer if available
                var parentScrollViewer = FindAncestorScrollViewer(targetScrollViewer);
                if (parentScrollViewer != null)
                {
                    bool canParentScroll = isScrollingDown
                        ? parentScrollViewer.VerticalOffset < parentScrollViewer.ScrollableHeight - 0.5
                        : parentScrollViewer.VerticalOffset > 0.5;

                    if (canParentScroll)
                    {
                        double step = Math.Abs(delta) >= 120 ? 48.0 : 24.0;
                        double amount = isScrollingDown ? step : -step;
                        double targetOffset = Math.Clamp(parentScrollViewer.VerticalOffset + amount, 0, parentScrollViewer.ScrollableHeight);

                        parentScrollViewer.ScrollToVerticalOffset(targetOffset);
                        e.Handled = true;
                    }
                }
            }
        }

        private static bool IsInsideOpenComboBox(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current is ComboBox combo && combo.IsDropDownOpen)
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private static ScrollViewer? FindNearestScrollViewer(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current is ScrollViewer sv)
                    return sv;

                if (current is DataGrid dg)
                {
                    var internalSv = GetVisualChild<ScrollViewer>(dg);
                    if (internalSv != null) return internalSv;
                }

                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static ScrollViewer? FindAncestorScrollViewer(DependencyObject element)
        {
            if (element == null) return null;
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is ScrollViewer sv && sv != element)
                    return sv;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private static T? GetVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int numChildren = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numChildren; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                    return typed;
                var childOfChild = GetVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}
