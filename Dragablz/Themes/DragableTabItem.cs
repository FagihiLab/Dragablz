using Dragablz.Dockablz;
using System.Windows;
using System.Windows.Controls;

namespace Dragablz.Themes
{
    /// <summary>
    /// this class has been made to get the ability of preset tab location
    /// </summary>
    public class DragableTabItem : TabItem
    {

        public DropZoneLocation Location
        {
            get { return (DropZoneLocation)GetValue(LocationProperty); }
            set { SetValue(LocationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Location.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LocationProperty =
            DependencyProperty.Register("Location", typeof(DropZoneLocation), typeof(DragableTabItem), new PropertyMetadata(DropZoneLocation.Left));

        public string LayoutName
        {
            get { return (string)GetValue(LayoutNameProperty); }
            set { SetValue(LayoutNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LayoutName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LayoutNameProperty =
            DependencyProperty.Register("LayoutName", typeof(string), typeof(DragableTabItem), new PropertyMetadata(""));

        public bool IsMainWindow
        {
            get { return (bool)GetValue(IsMainWindowProperty); }
            set { SetValue(IsMainWindowProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsMainWindow.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsMainWindowProperty =
            DependencyProperty.Register("IsMainWindow", typeof(bool), typeof(DragableTabItem), new PropertyMetadata(true));

        public State CurrentState 
        {
            get => new DragableTabItem.State
            {
                LayoutName = LayoutName,
                Location = Location,
                IsMainWindow = IsMainWindow
            };
            set
            {
                LayoutName = value.LayoutName;
                Location = value.Location;
                IsMainWindow = value.IsMainWindow;
            }
        }
        public class State
        {
            public string LayoutName { get; set; }
            public DropZoneLocation Location { get; set; }
            public bool IsMainWindow { get; set; }
        }
    }
}
