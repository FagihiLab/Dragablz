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
            DependencyProperty.Register("Location", typeof(DropZoneLocation), typeof(TabItem), new PropertyMetadata(DropZoneLocation.Left));
    
    
    }
}
