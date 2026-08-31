using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DebugDuck
{
    /// <summary>
    /// 「查看歷史訊息」跳出的完整聊天室。
    /// 沒有系統標題列(WindowStyle=None);用自訂的「關閉」按鈕關閉,拖標題區可移動視窗。
    /// 只讀:列出 <see cref="ChatHistory"/> 裡每一則玩家訊息,以時間戳記斷句。
    /// </summary>
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            Icon = App.DuckIcon;   // 工作列縮圖用黃小鴨
        }

        public void LoadEntries()
        {
            List.Children.Clear();

            var entries = ChatHistory.Entries;
            HeaderText.Text = $"完整聊天記錄 · 共 {entries.Count} 則";

            if (entries.Count == 0)
            {
                List.Children.Add(new TextBlock
                {
                    Text = "還沒有任何訊息。",
                    Foreground = Brush("#999999"),
                    FontSize = 13,
                    Margin = new Thickness(2)
                });
                return;
            }

            foreach (var entry in entries)
            {
                var block = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

                // 時間戳記(當地時間)——用來斷句
                block.Children.Add(new TextBlock
                {
                    Text = entry.TimestampUtc.ToLocalTime().ToString("yyyy/MM/dd  HH:mm:ss"),
                    Foreground = Brush("#8A8A8A"),
                    FontSize = 11,
                    Margin = new Thickness(2, 0, 0, 3)
                });

                block.Children.Add(new Border
                {
                    Background = Brush("#CDF5CD"),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 7, 10, 7),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock
                    {
                        Text = entry.Text,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush("#12521A"),
                        FontSize = 13
                    }
                });

                List.Children.Add(block);
            }

            Scroll.ScrollToEnd();
        }

        private void OnRefresh(object sender, RoutedEventArgs e) => LoadEntries();

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            // 點在按鈕上時不要當成拖曳
            var node = e.OriginalSource as DependencyObject;
            while (node != null && !(node is Button) && !(node is Window))
                node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);

            if (node is Button) return;
            DragMove();
        }

        private static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
