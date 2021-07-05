using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dragablz.Core;

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
            if (newTabablzControl == null) throw new ApplicationException("Unable to ascertain tab control.");

            if (newTabablzControl.ItemsSource == null)
                newTabablzControl.Items.Clear();

            newTabablzControl.InterTabController.Partition = (partition as string);
            newWindow.Content = new UserControl() { Content = newTabablzControl};
            var tabWindowStyle = Application.Current.FindResource("TabWindow") as Style;
            newWindow.Style = tabWindowStyle;
            return new NewTabHost<Window>(newWindow, newTabablzControl);            
        }

        public virtual TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }
    }
}