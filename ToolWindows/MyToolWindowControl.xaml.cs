using System.Windows;
using System.Windows.Controls;

namespace CodexVS22
{
    public partial class MyToolWindowControl : UserControl
    {
        public MyToolWindowControl()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            VS.MessageBox.Show("CodexVS22", "Button clicked");
        }
    }
}