using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Dragablz.Core;
using Dragablz.Dockablz;

namespace Dragablz
{
    public class DefaultInterTabClient : IInterTabClient
    {        
        public virtual INewTabHost<Window> GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            if (source == null) throw new ArgumentNullException("source");
            var sourceWindow = Window.GetWindow(source);
            if (sourceWindow == null) throw new ApplicationException("Unable to ascertain source window.");
            var newWindow = (Window)Activator.CreateInstance(sourceWindow.GetType());
            
             newWindow.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.DataBind);
            var newTabablzControl = newWindow.LogicalTreeDepthFirstTraversal().OfType<TabablzControl>().FirstOrDefault();
            newTabablzControl.Name = $"T{newTabablzControl.GetHashCode()}";
            newTabablzControl.IsHeaderOverTab = source.IsHeaderOverTab;
            if (newTabablzControl == null) throw new ApplicationException("Unable to ascertain tab control.");

            if (newTabablzControl.ItemsSource == null)
                newTabablzControl.Items.Clear();

            newTabablzControl.InterTabController.Partition = (partition as string);
            newWindow.Content = new UserControl() { Content = newTabablzControl};
            var tabWindowStyle = Application.Current.FindResource("TabWindow") as Style;
            newWindow.Style = tabWindowStyle;
            return new NewTabHost<Window>(newWindow, newTabablzControl);            
        }

        public virtual INewTabHost<Window> CreateNewHost(IInterTabClient interTabClient, object partition, TabablzControl tabablzControl)
        {
            var newWindow = new Window { AllowsTransparency = true, Width = tabablzControl.Width, Height = tabablzControl.Height };

            newWindow.Content = new UserControl() { Content = tabablzControl};
            var tabWindowStyle = Application.Current.FindResource("TabWindow") as Style;
            newWindow.Style = tabWindowStyle;
            return new NewTabHost<Window>(newWindow, tabablzControl);
        }


        public virtual TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }
    }
}