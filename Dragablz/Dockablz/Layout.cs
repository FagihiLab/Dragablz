using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Dragablz.Core;
using Dragablz.Themes;

namespace Dragablz.Dockablz
{
    [TemplatePart(Name = TopDropZonePartName, Type = typeof(DropZone))]
    [TemplatePart(Name = RightDropZonePartName, Type = typeof(DropZone))]
    [TemplatePart(Name = BottomDropZonePartName, Type = typeof(DropZone))]
    [TemplatePart(Name = LeftDropZonePartName, Type = typeof(DropZone))]
    public class Layout : ContentControl
    {

        private static readonly HashSet<Layout> LoadedLayouts = new HashSet<Layout>();
        private const string TopDropZonePartName = "PART_TopDropZone";
        private const string RightDropZonePartName = "PART_RightDropZone";
        private const string BottomDropZonePartName = "PART_BottomDropZone";
        private const string LeftDropZonePartName = "PART_LeftDropZone";

        private readonly IDictionary<DropZoneLocation, DropZone> _dropZones = new Dictionary<DropZoneLocation, DropZone>();
        private static Tuple<Layout, DropZone> _currentlyOfferedDropZone;

        private static bool _isDragOpWireUpPending;

        static Layout()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Layout), new FrameworkPropertyMetadata(typeof(Layout)));

            EventManager.RegisterClassHandler(typeof(DragablzItem), DragablzItem.DragStarted, new DragablzDragStartedEventHandler(ItemDragStarted));
            EventManager.RegisterClassHandler(typeof(DragablzItem), DragablzItem.PreviewDragDelta, new DragablzDragDeltaEventHandler(PreviewItemDragDelta), true);
            EventManager.RegisterClassHandler(typeof(DragablzItem), DragablzItem.DragCompleted, new DragablzDragCompletedEventHandler(ItemDragCompleted));
        }

        public Layout()
        {
            Loaded += (sender, args) =>
            {
                LoadedLayouts.Add(this);
                if (IsInitiated)
                {
                    MarkTopLeftItem(this.Query());
                }
                else
                {
                    MarkItem(this);
                }
            };
            Unloaded += (sender, args) => LoadedLayouts.Remove(this);

            TabablzStyle = Application.Current.FindResource("TabablzControlStyle") as Style;
        }

        /// <summary>
        /// Helper method to get all the currently loaded layouts.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Layout> GetLoadedInstances()
        {
            return LoadedLayouts.ToList();
        }

        /// <summary>
        /// Finds the location of a tab control withing a layout.
        /// </summary>
        /// <param name="tabablzControl"></param>
        /// <returns></returns>
        public static LocationReport Find(TabablzControl tabablzControl)
        {
            if (tabablzControl == null) throw new ArgumentNullException("tabablzControl");

            return Finder.Find(tabablzControl);
        }

        /// <summary>
        /// Creates a split in a layout, at the location of a specified <see cref="TabablzControl"/>.
        /// </summary>
        /// <para></para>
        /// <param name="tabablzControl">Tab control to be split.</param>
        /// <param name="orientation">Direction of split.</param>
        /// <param name="makeSecond">Set to <c>true</c> to make the current tab control push into the right hand or bottom of the split.</param>
        /// <remarks>The tab control to be split must be hosted in a layout control.</remarks>
        public static BranchResult Branch(TabablzControl tabablzControl, Orientation orientation, bool makeSecond)
        {
            return Branch(tabablzControl, orientation, makeSecond, .5);
        }

        /// <summary>
        /// Creates a split in a layout, at the location of a specified <see cref="TabablzControl"/>.
        /// </summary>
        /// <para></para>
        /// <param name="tabablzControl">Tab control to be split.</param>
        /// <param name="orientation">Direction of split.</param>
        /// <param name="makeSecond">Set to <c>true</c> to make the current tab control push into the right hand or bottom of the split.</param>
        /// <param name="firstItemProportion">Sets the proportion of the first tab control, with 0.5 being 50% of available space.</param>
        /// <remarks>The tab control to be split must be hosted in a layout control.  <see cref="Layout.BranchTemplate" /> should be set (typically via XAML).</remarks>
        public static BranchResult Branch(TabablzControl tabablzControl, Orientation orientation, bool makeSecond, double firstItemProportion)
        {
            return Branch(tabablzControl, null, orientation, makeSecond, firstItemProportion);
        }

        /// <summary>
        /// Creates a split in a layout, at the location of a specified <see cref="TabablzControl"/>.
        /// </summary>
        /// <para></para>
        /// <param name="tabablzControl">Tab control to be split.</param>
        /// <param name="newSiblingTabablzControl">New sibling tab control (otherwise <see cref="Layout.BranchTemplate"/> will be used).</param>
        /// <param name="orientation">Direction of split.</param>
        /// <param name="makeCurrentSecond">Set to <c>true</c> to make the current tab control push into the right hand or bottom of the split.</param>
        /// <param name="firstItemProportion">Sets the proportion of the first tab control, with 0.5 being 50% of available space.</param>
        /// <remarks>The tab control to be split must be hosted in a layout control. </remarks>
        public static BranchResult Branch(TabablzControl tabablzControl, TabablzControl newSiblingTabablzControl, Orientation orientation, bool makeCurrentSecond,
            double firstItemProportion)
        {
            if (firstItemProportion < 0.0 || firstItemProportion > 1.0) throw new ArgumentOutOfRangeException("firstItemProportion", "Must be >= 0.0 and <= 1.0");

            var locationReport = Find(tabablzControl);

            Action<Branch> applier;
            object existingContent;
            if (!locationReport.IsLeaf)
            {
                existingContent = locationReport.RootLayout.Content;
                applier = branch => locationReport.RootLayout.Content = branch;
            }
            else if (!locationReport.IsSecondLeaf)
            {
                existingContent = locationReport.ParentBranch.FirstItem;
                applier = branch => locationReport.ParentBranch.FirstItem = branch;
            }
            else
            {
                existingContent = locationReport.ParentBranch.SecondItem;
                applier = branch => locationReport.ParentBranch.SecondItem = branch;
            }

            var selectedItem = tabablzControl.SelectedItem;
            var branchResult = Branch(orientation, firstItemProportion, makeCurrentSecond, locationReport.RootLayout.BranchTemplate, newSiblingTabablzControl, existingContent, applier);
            tabablzControl.SelectedItem = selectedItem;
            tabablzControl.Dispatcher.BeginInvoke(new Action(() =>
            {
                tabablzControl.SetCurrentValue(Selector.SelectedItemProperty, selectedItem);
                MarkTopLeftItem(locationReport.RootLayout.Query());
            }),
                DispatcherPriority.Loaded);

            return branchResult;
        }

        /// <summary>
        /// Use in conjuction with the <see cref="InterTabController.Partition"/> on a <see cref="TabablzControl"/>
        /// to isolate drag and drop spaces/control instances.
        /// </summary>
        public string Partition { get; set; }

        public static readonly DependencyProperty InterLayoutClientProperty = DependencyProperty.Register(
            "InterLayoutClient", typeof(IInterLayoutClient), typeof(Layout), new PropertyMetadata(new DefaultInterLayoutClient()));

        public IInterLayoutClient InterLayoutClient
        {
            get { return (IInterLayoutClient)GetValue(InterLayoutClientProperty); }
            set { SetValue(InterLayoutClientProperty, value); }
        }

        internal static bool IsContainedWithinBranch(DependencyObject dependencyObject)
        {
            do
            {
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
                if (dependencyObject is Branch)
                    return true;
            } while (dependencyObject != null);
            return false;
        }

        private static readonly DependencyPropertyKey IsParticipatingInDragPropertyKey =
            DependencyProperty.RegisterReadOnly(
                "IsParticipatingInDrag", typeof(bool), typeof(Layout),
                new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty IsParticipatingInDragProperty =
            IsParticipatingInDragPropertyKey.DependencyProperty;

        public bool IsParticipatingInDrag
        {
            get { return (bool)GetValue(IsParticipatingInDragProperty); }
            private set { SetValue(IsParticipatingInDragPropertyKey, value); }
        }

        public static readonly DependencyProperty BranchTemplateProperty = DependencyProperty.Register(
            "BranchTemplate", typeof(DataTemplate), typeof(Layout), new PropertyMetadata(default(DataTemplate)));

        public DataTemplate BranchTemplate
        {
            get { return (DataTemplate)GetValue(BranchTemplateProperty); }
            set { SetValue(BranchTemplateProperty, value); }
        }

        public static Style TabablzStyle { get; private set; }
        public static bool IsInitiated { get; set; }

        private static readonly DependencyPropertyKey IsTopLeftItemPropertyKey =
            DependencyProperty.RegisterReadOnly(
                "IsTopLeftItem", typeof(bool), typeof(Layout),
                new PropertyMetadata(default(bool)));

        /// <summary>
        /// Indicates if an item/tab control within a layout is contained at the top most and left most branch item.
        /// </summary>
        public static readonly DependencyProperty IsTopLeftItemProperty = IsTopLeftItemPropertyKey.DependencyProperty;

        /// <summary>
        /// Indicates if an item/tab control within a layout is contained at the top most and left most branch item.
        /// </summary>
        private static void SetIsTopLeftItem(DependencyObject element, bool value)
        {
            element.SetValue(IsTopLeftItemPropertyKey, value);
        }

        /// <summary>
        /// Indicates if an item/tab control within a layout is contained at the top most and left most branch item.
        /// </summary>
        public static bool GetIsTopLeftItem(DependencyObject element)
        {
            return (bool)element.GetValue(IsTopLeftItemProperty);
        }

        /// <summary>When overridden in a derived class, is invoked whenever application code or internal processes call <see cref="M:System.Windows.FrameworkElement.ApplyTemplate" />.</summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _dropZones[DropZoneLocation.Top] = GetTemplateChild(TopDropZonePartName) as DropZone;
            _dropZones[DropZoneLocation.Right] = GetTemplateChild(RightDropZonePartName) as DropZone;
            _dropZones[DropZoneLocation.Bottom] = GetTemplateChild(BottomDropZonePartName) as DropZone;
            _dropZones[DropZoneLocation.Left] = GetTemplateChild(LeftDropZonePartName) as DropZone;
        }

        private static void ItemDragStarted(object sender, DragablzDragStartedEventArgs e)
        {
            //we wait until drag is in full flow so we know the partition has been setup by the owning tab control
            _isDragOpWireUpPending = true;
        }

        private static void SetupParticipatingLayouts(DragablzItem dragablzItem)
        {
            var sourceOfDragItemsControl = ItemsControl.ItemsControlFromItemContainer(dragablzItem) as DragablzItemsControl;
            if (sourceOfDragItemsControl == null || sourceOfDragItemsControl.Items.Count != 1) return;

            var draggingWindow = Window.GetWindow(dragablzItem);
            if (draggingWindow == null) return;

            foreach (var loadedLayout in LoadedLayouts.Where(l =>
                l.Partition == dragablzItem.PartitionAtDragStart &&
                !Equals(Window.GetWindow(l), draggingWindow)))

            {
                loadedLayout.IsParticipatingInDrag = true;
            }
        }

        private void MonitorDropZones(Point cursorPos)
        {
            var myWindow = Window.GetWindow(this);
            if (myWindow == null) return;

            foreach (var dropZone in _dropZones.Values.Where(dz => dz != null))
            {
                var pointFromScreen = myWindow.PointFromScreen(cursorPos);
                var pointRelativeToDropZone = myWindow.TranslatePoint(pointFromScreen, dropZone);
                var inputHitTest = dropZone.InputHitTest(pointRelativeToDropZone);
                //TODO better halding when windows are layered over each other
                if (inputHitTest != null)
                {
                    if (_currentlyOfferedDropZone != null)
                        _currentlyOfferedDropZone.Item2.IsOffered = false;
                    dropZone.IsOffered = true;
                    _currentlyOfferedDropZone = new Tuple<Layout, DropZone>(this, dropZone);
                }
                else
                {
                    dropZone.IsOffered = false;
                    if (_currentlyOfferedDropZone != null && _currentlyOfferedDropZone.Item2 == dropZone)
                        _currentlyOfferedDropZone = null;
                }
            }
        }

        private static bool TryGetSourceTabControl(DragablzItem dragablzItem, out TabablzControl tabablzControl)
        {
            var sourceOfDragItemsControl = ItemsControl.ItemsControlFromItemContainer(dragablzItem) as DragablzItemsControl;
            if (sourceOfDragItemsControl == null) throw new ApplicationException("Unable to determine source items control.");

            tabablzControl = TabablzControl.GetOwnerOfHeaderItems(sourceOfDragItemsControl);

            return tabablzControl != null;
        }

        private void Branch(DropZoneLocation location, DragablzItem sourceDragablzItem)
        {
            if (InterLayoutClient == null)
                throw new InvalidOperationException("InterLayoutClient is not set.");

            var sourceOfDragItemsControl = ItemsControl.ItemsControlFromItemContainer(sourceDragablzItem) as DragablzItemsControl;
            if (sourceOfDragItemsControl == null) throw new ApplicationException("Unable to determin source items control.");

            var sourceTabControl = TabablzControl.GetOwnerOfHeaderItems(sourceOfDragItemsControl);
            if (sourceTabControl == null) throw new ApplicationException("Unable to determin source tab control.");

            var sourceItem = sourceOfDragItemsControl.ItemContainerGenerator.ItemFromContainer(sourceDragablzItem);

            if (sourceItem is DragableTabItem dragableTabItem)
            {
                dragableTabItem.Location = location;
                dragableTabItem.LayoutName = Name;

                if (Window.GetWindow(this) == Application.Current.MainWindow)
                {
                    dragableTabItem.IsMainWindow = true;
                }

            }

            sourceTabControl.RemoveItem(sourceDragablzItem);

            var branchItem = new Branch
            {
                Orientation = (location == DropZoneLocation.Right || location == DropZoneLocation.Left) ? Orientation.Horizontal : Orientation.Vertical
            };

            object newContent;
            if (BranchTemplate == null)
            {
                var newTabHost = InterLayoutClient.GetNewHost(Partition, sourceTabControl);
                if (newTabHost == null)
                    throw new ApplicationException("InterLayoutClient did not provide a new tab host.");
                newTabHost.TabablzControl.AddToSource(sourceItem);
                newTabHost.TabablzControl.SelectedItem = sourceItem;
                if (sourceTabControl.IsHeaderOverTab)
                {
                    newTabHost.TabablzControl.IsHeaderOverTab = true;
                    newTabHost.TabablzControl.Style = TabablzStyle;
                }
                newContent = newTabHost.Container;
            }
            else
            {
                newContent = new ContentControl
                {
                    Content = new object(),
                    ContentTemplate = BranchTemplate,
                };
                ((ContentControl)newContent).Dispatcher.BeginInvoke(new Action(() =>
               {
                   //TODO might need to improve this a bit, make it a bit more declarative for complex trees
                   var newTabControl = ((ContentControl)newContent).VisualTreeDepthFirstTraversal().OfType<TabablzControl>().FirstOrDefault();
                   if (newTabControl == null) return;

                   if (sourceTabControl.IsHeaderOverTab)
                   {
                       newTabControl.IsHeaderOverTab = true;
                       newTabControl.Style = TabablzStyle;
                   }

                   newTabControl.DataContext = sourceTabControl.DataContext;
                   newTabControl.AddToSource(sourceItem);
                   newTabControl.SelectedItem = sourceItem;
               }), DispatcherPriority.Loaded);
            }
            if ((Content as TabablzControl)?.Items.Count == 0)
            {
                SetCurrentValue(ContentProperty, newContent);
                Dispatcher.BeginInvoke(new Action(() => MarkTopLeftItem(this.Query())), DispatcherPriority.Loaded);
                return;
            }

            if (location == DropZoneLocation.Right || location == DropZoneLocation.Bottom)
            {
                branchItem.FirstItem = Content;
                branchItem.SecondItem = newContent;
            }
            else
            {
                branchItem.FirstItem = newContent;
                branchItem.SecondItem = Content;
            }

            SetCurrentValue(ContentProperty, branchItem);

            Dispatcher.BeginInvoke(new Action(() => MarkTopLeftItem(this.Query())), DispatcherPriority.Loaded);
        }

        internal static bool ConsolidateBranch(DependencyObject redundantNode)
        {
            bool isSecondLineageWhenOwnerIsBranch;
            var ownerBranch = FindLayoutOrBranchOwner(redundantNode, out isSecondLineageWhenOwnerIsBranch) as Branch;
            if (ownerBranch == null) return false;

            var survivingItem = isSecondLineageWhenOwnerIsBranch ? ownerBranch.FirstItem : ownerBranch.SecondItem;

            var grandParent = FindLayoutOrBranchOwner(ownerBranch, out isSecondLineageWhenOwnerIsBranch);
            if (grandParent == null) throw new ApplicationException("Unexpected structure, grandparent Layout or Branch not found");

            var layout = grandParent as Layout;
            if (layout != null)
            {
                layout.Content = survivingItem;
                MarkTopLeftItem(layout.Query());
                return true;
            }

            var branch = (Branch)grandParent;
            if (isSecondLineageWhenOwnerIsBranch)
                branch.SecondItem = survivingItem;
            else
                branch.FirstItem = survivingItem;
            var rootLayout = branch.VisualTreeAncestory().OfType<Layout>().FirstOrDefault();
            if (rootLayout != null)
                MarkTopLeftItem(rootLayout.Query());

            return true;
        }

        private static object FindLayoutOrBranchOwner(DependencyObject node, out bool isSecondLineageWhenOwnerIsBranch)
        {
            isSecondLineageWhenOwnerIsBranch = false;

            var ancestoryStack = new Stack<DependencyObject>();
            do
            {
                ancestoryStack.Push(node);
                node = VisualTreeHelper.GetParent(node);
                if (node is Layout)
                    return node;

                var branch = node as Branch;
                if (branch == null) continue;

                isSecondLineageWhenOwnerIsBranch = ancestoryStack.Contains(branch.SecondContentPresenter);
                return branch;

            } while (node != null);

            return null;
        }

        private static BranchResult Branch(Orientation orientation, double proportion, bool makeSecond, DataTemplate branchTemplate, TabablzControl newSibling, object existingContent, Action<Branch> applier)
        {
            var branchItem = new Branch
            {
                Orientation = orientation
            };

            var newContent = new ContentControl
            {
                Content = newSibling ?? new object(),
                ContentTemplate = branchTemplate,
            };

            if (!makeSecond)
            {
                branchItem.FirstItem = existingContent;
                branchItem.SecondItem = newContent;
            }
            else
            {
                branchItem.FirstItem = newContent;
                branchItem.SecondItem = existingContent;
            }

            branchItem.SetCurrentValue(Dockablz.Branch.FirstItemLengthProperty, new GridLength(proportion, GridUnitType.Star));
            branchItem.SetCurrentValue(Dockablz.Branch.SecondItemLengthProperty, new GridLength(1 - proportion, GridUnitType.Star));

            applier(branchItem);

            newContent.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Loaded);
            var newTabablzControl = newContent.VisualTreeDepthFirstTraversal().OfType<TabablzControl>().FirstOrDefault();
            if (newTabablzControl != null) return new BranchResult(branchItem, newTabablzControl);

            //let#s be kinf and give WPF an extra change to gen the controls
            newContent.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Background);
            newTabablzControl = newContent.VisualTreeDepthFirstTraversal().OfType<TabablzControl>().FirstOrDefault();

            if (newTabablzControl == null)
                throw new ApplicationException("New TabablzControl was not generated inside branch.");

            return new BranchResult(branchItem, newTabablzControl);
        }

        private static void ItemDragCompleted(object sender, DragablzDragCompletedEventArgs e)
        {
            _isDragOpWireUpPending = false;

            foreach (var loadedLayout in LoadedLayouts)
                loadedLayout.IsParticipatingInDrag = false;

            if (_currentlyOfferedDropZone == null || e.DragablzItem.IsDropTargetFound) return;

            TabablzControl tabablzControl;
            if (TryGetSourceTabControl(e.DragablzItem, out tabablzControl))
            {
                if (tabablzControl.Items.Count > 1) return;

                _currentlyOfferedDropZone.Item1.Branch(_currentlyOfferedDropZone.Item2.Location, e.DragablzItem);
            }

            _currentlyOfferedDropZone = null;
        }

        private static void PreviewItemDragDelta(object sender, DragablzDragDeltaEventArgs e)
        {
            if (e.Cancel) return;

            if (_isDragOpWireUpPending)
            {
                SetupParticipatingLayouts(e.DragablzItem);
                _isDragOpWireUpPending = false;
            }

            foreach (var layout in LoadedLayouts.Where(l => l.IsParticipatingInDrag))
            {
                var cursorPos = Native.GetCursorPos();
                layout.MonitorDropZones(cursorPos);
            }
        }

        internal static readonly DependencyProperty LocationSnapShotProperty = DependencyProperty.RegisterAttached(
            "LocationSnapShot", typeof(LocationSnapShot), typeof(Layout), new PropertyMetadata(default(LocationSnapShot)));

        internal static void SetLocationSnapShot(FrameworkElement element, LocationSnapShot value)
        {
            element.SetValue(LocationSnapShotProperty, value);
        }

        internal static LocationSnapShot GetLocationSnapShot(FrameworkElement element)
        {
            return (LocationSnapShot)element.GetValue(LocationSnapShotProperty);
        }

        private bool IsHostingTab()
        {
            return this.VisualTreeDepthFirstTraversal().OfType<TabablzControl>()
                .FirstOrDefault(t => t.InterTabController != null && t.InterTabController.Partition == Partition)
                != null;
        }

        private static void MarkTopLeftItem(LayoutAccessor layoutAccessor)
        {
            if (layoutAccessor.TabablzControl != null)
            {
                SetIsTopLeftItem(layoutAccessor.TabablzControl, true);
                return;
            }
            var branchAccessor = layoutAccessor.BranchAccessor;
            while (branchAccessor != null && branchAccessor.FirstItemTabablzControl == null)
            {
                branchAccessor = branchAccessor.FirstItemBranchAccessor;
            }

            foreach (var tabablzControl in layoutAccessor.TabablzControls())
            {
                SetIsTopLeftItem(tabablzControl, branchAccessor != null && Equals(tabablzControl, branchAccessor.FirstItemTabablzControl));
            }
        }

        private async static void MarkItem(Layout layout)
        {
            var layoutAccessor = layout.Query();
            //MarkTopLeftItem(layoutAccessor);
            if (layoutAccessor.TabablzControl is null)
                return;

            var TabItems = layoutAccessor.TabablzControl.GetOrderedHeaders();
            var groupedDragablzItems = TabItems.Where(x => x.Content is DragableTabItem).GroupBy(x => (x.Content as DragableTabItem).TabControlName);

            if (groupedDragablzItems is null)
                return;

            var currentItems = groupedDragablzItems;
            if (currentItems != null)
            {
                var tabControl = layoutAccessor.TabablzControl;
                var hasUnsetLocation = TabItems.Where(x => (x.Content as DragableTabItem).Location == DropZoneLocation.Unset).Any();
                var location = DropZoneLocation.Unset;
                List<Window> windows = new List<Window>();
                foreach (var items in currentItems)
                {
                    foreach (var item in items)
                    {

                        if (item.Content is DragableTabItem dragableTabItem)
                        {
                            if (!dragableTabItem.IsMainWindow)
                            {
                                windows.Add(tabControl.GetWindow(item));
                            }
                        }
                    }

                    var newItems = items.Where(x => (x.Content as DragableTabItem).Location != DropZoneLocation.Unset).ToList();
                    foreach (var item in newItems)
                    {
                        if (item.Content is not DragableTabItem)
                            continue;

                        IEnumerable<DragablzItem> relatedItems = null;
                        relatedItems = items.Where(x => (x.Content is DragableTabItem)).Where(x => (x.Content as DragableTabItem).TabControlName == (item.Content as DragableTabItem).TabControlName
                           && (x.Content as DragableTabItem).Location == (item.Content as DragableTabItem).Location).ToList();

                        location = (item.Content as DragableTabItem).Location;

                        var branchItem = new Branch
                        {
                            Orientation = (location == DropZoneLocation.Right || location == DropZoneLocation.Left) ? Orientation.Horizontal : Orientation.Vertical
                        };
                        var newTabHost = layoutAccessor.Layout.InterLayoutClient.GetNewHost(layoutAccessor.Layout.Partition, tabControl);
                        if (newTabHost == null)
                            throw new ApplicationException("InterLayoutClient did not provide a new tab host.");

                        var sourceOfDragItemsControl = ItemsControl.ItemsControlFromItemContainer(item) as DragablzItemsControl;
                        if (sourceOfDragItemsControl == null) throw new ApplicationException("Unable to determin source items control.");

                        var sourceItem = sourceOfDragItemsControl.ItemContainerGenerator.ItemFromContainer(item);

                        if (!(item.Content as DragableTabItem).IsMainWindow)
                        {
                            continue;
                        }

                        newTabHost.TabablzControl.Name = (item.Content as DragableTabItem).TabControlName;
                        tabControl.RemoveItem(item);
                        newTabHost.TabablzControl.AddToSource(sourceItem);
                        newTabHost.TabablzControl.SelectedItem = sourceItem;

                        if (relatedItems is not null)
                        {
                            if (relatedItems.Count() > 1)
                            {
                                foreach (var relatedItem in relatedItems.Skip(1))
                                {
                                    var sourceRelatedItem = sourceOfDragItemsControl.ItemContainerGenerator.ItemFromContainer(relatedItem);
                                    if (sourceRelatedItem is DragableTabItem relatedDragableTabItem)
                                    {
                                        if (!relatedDragableTabItem.IsMainWindow)
                                        {
                                            continue;
                                            //newTabHost.TabablzControl.InterTabController.InterTabClient.CreateNewHost(tabControl.InterTabController.InterTabClient, layoutAccessor.Layout.Partition, tabControl).Container.Show();
                                            //continue;
                                        }

                                    }
                                    tabControl.RemoveItem(relatedItem);
                                    newTabHost.TabablzControl.AddToSource(sourceRelatedItem);

                                }
                            }
                        }


                        if (tabControl.IsHeaderOverTab)
                        {
                            newTabHost.TabablzControl.IsHeaderOverTab = true;
                            newTabHost.TabablzControl.Style = TabablzStyle;
                        }
                        object newContent = newTabHost.Container;

                        if (!hasUnsetLocation)
                        {
                            layoutAccessor.Layout.SetCurrentValue(ContentProperty, newContent);
                            hasUnsetLocation = true;
                            continue;
                        }

                        if (location == DropZoneLocation.Right || location == DropZoneLocation.Bottom)
                        {
                            branchItem.FirstItem = layoutAccessor.Layout.Content;
                            branchItem.SecondItem = newContent;
                        }
                        else
                        {
                            branchItem.FirstItem = newContent;
                            branchItem.SecondItem = layoutAccessor.Layout.Content;
                        }
                        layoutAccessor.Layout.SetCurrentValue(ContentProperty, branchItem);
                    }

                    foreach (var window in windows)
                    {
                        window.Show();
                    }
                }

            }
        }
    }
}