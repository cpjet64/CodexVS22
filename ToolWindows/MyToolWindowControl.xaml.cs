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

        public void AppendSelectionToInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var box = this.FindName("InputBox") as TextBox;
            if (box != null)
            {
                if (!string.IsNullOrEmpty(box.Text))
                    box.Text += "\n";
                box.Text += text;
                box.Focus();
                box.CaretIndex = box.Text.Length;
            }
        }
    }
}
