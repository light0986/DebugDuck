using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DebugDuck
{
    /// <summary>「關於」視窗：沒有系統標題列，用自訂的「確認」按鈕關閉。</summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            Icon = App.DuckIcon;   // 工作列縮圖用黃小鴨
        }

        private void OnConfirm(object sender, RoutedEventArgs e) => Close();

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            // 點在按鈕上時不要當成拖曳
            var node = e.OriginalSource as DependencyObject;
            while (node != null && !(node is Button) && !(node is Window))
                node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);

            if (node is Button) return;
            DragMove();
        }
    }
}
