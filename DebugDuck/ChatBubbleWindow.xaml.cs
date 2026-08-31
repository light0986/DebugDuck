using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DebugDuck.Duck;

namespace DebugDuck
{
    public partial class ChatBubbleWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // 訊息底色
        private const string PlayerBg = "#CDF5CD";   // 淡亮綠
        private const string PlayerFg = "#12521A";
        private const string DuckBg = "#CDE8FF";     // 淡亮藍
        private const string DuckFg = "#0B3D91";

        private static readonly TimeSpan ThinkDelay = TimeSpan.FromSeconds(3);

        private readonly DuckBrain _brain;
        private readonly Action<DuckState> _onReact;

        private DispatcherTimer _think;
        private TextBlock _duckText;
        private bool _busy;
        private bool _greeted;
        private DateTime _guardUntil;

        public ChatBubbleWindow(DuckBrain brain, Action<DuckState> onReact)
        {
            InitializeComponent();
            _brain = brain;
            _onReact = onReact;

            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
            };

            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible && !_greeted)
                {
                    _greeted = true;
                    AddBubble("嘎", DuckBg, DuckFg, HorizontalAlignment.Left);
                }
            };

            // 點到聊天室以外的地方（失去作用中狀態）就關閉。
            // 剛開啟的短暫期間忽略（右鍵選單關閉會造成一次假失焦）。
            Deactivated += (s, e) =>
            {
                if (DateTime.UtcNow < _guardUntil)
                {
                    Dispatcher.BeginInvoke(new Action(() => { if (IsVisible) Activate(); }));
                    return;
                }
                Hide();
            };
        }

        /// <summary>開啟聊天室（只由 PetWindow 的右鍵選單呼叫）。</summary>
        public void ShowChat()
        {
            _guardUntil = DateTime.UtcNow.AddMilliseconds(500);
            if (!IsVisible) Show();
            Reposition();
            Activate();
            FocusInput();
        }

        public void FocusInput()
        {
            if (_busy) return;
            InputBox.Focus();
            Keyboard.Focus(InputBox);
        }

        // ---------- 位置：貼在小鴨上方 ----------

        public void Reposition()
        {
            if (Owner == null) return;

            var wa = SystemParameters.WorkArea;
            var h = ActualHeight > 0 ? ActualHeight : 140;

            var left = Owner.Left + Owner.Width / 2 - Width / 2;
            left = Math.Max(wa.Left + 4, Math.Min(left, wa.Right - Width - 4));

            var above = Owner.Top - h + 16;
            Top = above >= wa.Top + 4 ? above : Owner.Top + Owner.Height - 16;
            Left = left;
        }

        // ---------- 輸入 ----------

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                Send();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Hide();
            }
        }

        private void OnSend(object sender, RoutedEventArgs e) => Send();

        private void Send()
        {
            if (_busy) return;

            var text = InputBox.Text.Trim();
            if (text.Length == 0) return;

            InputBox.Clear();

            // 存進背景歷史（只存玩家訊息），然後清空聊天室畫面
            ChatHistory.AddPlayerMessage(text);
            Log.Children.Clear();

            // 1) 玩家訊息（淡綠）
            AddBubble(text, PlayerBg, PlayerFg, HorizontalAlignment.Right);

            // 2) 鴨子「思考中..」（淡藍）
            _duckText = AddBubble("思考中..", DuckBg, DuckFg, HorizontalAlignment.Left);

            // 思考期間鎖住輸入，3 秒後給隨機回應
            SetBusy(true);
            var reply = _brain.Respond(text);

            _think = new DispatcherTimer { Interval = ThinkDelay };
            _think.Tick += (s, e) =>
            {
                _think.Stop();
                _think = null;
                _duckText.Text = reply.Text;
                DuckSound.Speak(reply.Text);         // 台詞出現的同時叫（嘎嘎→2 聲、嘎嘎嘎→3 聲）
                _onReact?.Invoke(reply.Reaction);
                SetBusy(false);
            };
            _think.Start();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            InputBox.IsEnabled = !busy;
            SendButton.IsEnabled = !busy;
            InputArea.Opacity = busy ? 0.5 : 1.0;
            if (!busy)
            {
                InputBox.Focus();
                Keyboard.Focus(InputBox);
            }
        }

        // ---------- 訊息氣泡 ----------

        private TextBlock AddBubble(string text, string bg, string fg, HorizontalAlignment align)
        {
            var tb = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = Brush(fg)
            };

            Log.Children.Add(new Border
            {
                Background = Brush(bg),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(11, 8, 11, 8),
                Margin = align == HorizontalAlignment.Right
                    ? new Thickness(44, 4, 0, 4)
                    : new Thickness(0, 4, 44, 4),
                HorizontalAlignment = align,
                Child = tb
            });

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Reposition));
            return tb;
        }

        private static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
