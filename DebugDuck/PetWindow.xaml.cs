using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DebugDuck.Duck;

namespace DebugDuck
{
    public partial class PetWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private readonly DuckAnimator _animator = new DuckAnimator();
        private readonly DuckBrain _brain = new DuckBrain();
        private readonly AppState _state = AppState.Load();

        private ChatBubbleWindow _chat;
        private HistoryWindow _history;

        private bool _pressed;
        private bool _dragging;
        private bool _squashing;
        private Point _pressScreen;
        private double _startLeft, _startTop;

        public PetWindow()
        {
            InitializeComponent();

            SpriteImage.SetBinding(System.Windows.Controls.Image.SourceProperty,
                new Binding(nameof(DuckAnimator.CurrentFrame)) { Source = _animator });

            _animator.AnimationCompleted += (s, completed) => _animator.Play(DuckState.Idle);

            Loaded += OnLoaded;
            SourceInitialized += OnSourceInitialized;
            LocationChanged += (s, e) => _chat?.Reposition();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            // 從 Alt-Tab 清單隱藏
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RestorePosition();
            StartBob();
        }

        // ---------- 位置 ----------

        private void RestorePosition()
        {
            var wa = SystemParameters.WorkArea;

            if (IsOnScreen(_state.PetLeft, _state.PetTop))
            {
                Left = _state.PetLeft;
                Top = _state.PetTop;
            }
            else
            {
                Left = wa.Right - Width - 24;
                Top = wa.Bottom - Height - 8;
            }
        }

        private static bool IsOnScreen(double left, double top)
        {
            if (double.IsNaN(left) || double.IsNaN(top)) return false;
            var v = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                             SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            return v.Contains(new Point(left + 40, top + 40));
        }

        private void SavePosition()
        {
            _state.PetLeft = Left;
            _state.PetTop = Top;
            _state.Save();
        }

        // ---------- 拖曳 / 點擊 ----------

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _pressed = true;
            _dragging = false;
            _pressScreen = PointToScreen(e.GetPosition(this));
            _startLeft = Left;
            _startTop = Top;
            Root.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_pressed) return;

            var now = PointToScreen(e.GetPosition(this));
            var dx = now.X - _pressScreen.X;
            var dy = now.Y - _pressScreen.Y;

            if (!_dragging && Math.Abs(dx) + Math.Abs(dy) > 4)
            {
                _dragging = true;
                Nod();
                _animator.Play(DuckState.Drag);
            }

            if (_dragging)
            {
                Left = _startLeft + dx;
                Top = _startTop + dy;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_pressed) return;
            _pressed = false;
            Root.ReleaseMouseCapture();

            if (_dragging)
            {
                _dragging = false;
                SavePosition();
                _animator.Play(DuckState.Idle);
            }
            else
            {
                // 左鍵點擊只變形；聊天室只能從右鍵選單開
                Squash();
            }
        }

        // ---------- 左鍵點擊：Q 彈壓扁 ----------

        private void Squash()
        {
            if (_squashing) return;
            _squashing = true;

            DuckSound.Play(1);   // 點擊變形時叫一聲

            var dur = TimeSpan.FromSeconds(0.5);
            var spring = new ElasticEase { Oscillations = 1, Springiness = 6, EasingMode = EasingMode.EaseOut };
            var quick = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 垂直壓扁最多到 0.9 倍，再 Q 彈回 1.0
            var sy = new DoubleAnimationUsingKeyFrames { Duration = dur };
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(0.0)));
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(0.90, KeyTime.FromPercent(0.24), quick));
            sy.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(1.0), spring));

            // 水平同時稍微撐開，維持體積感
            var sx = new DoubleAnimationUsingKeyFrames { Duration = dur };
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(0.0)));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.08, KeyTime.FromPercent(0.24), quick));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(1.0), spring));

            sy.Completed += (s, e) =>
            {
                HostScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                HostScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                HostScale.ScaleX = 1.0;
                HostScale.ScaleY = 1.0;
                _squashing = false;   // 0.5 秒後才能再次觸發
            };

            HostScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            HostScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        }

        // ---------- 對話（只能從右鍵選單開） ----------

        private void ToggleChat()
        {
            if (_chat != null && _chat.IsVisible)
            {
                _chat.Hide();
                return;
            }

            if (_chat == null)
            {
                _chat = new ChatBubbleWindow(_brain, React) { Owner = this };
            }

            _chat.ShowChat();
        }

        /// <summary>ChatBubble 依小鴨的回應叫這個來驅動動畫。</summary>
        private void React(DuckState reaction)
        {
            _animator.Play(reaction);

            switch (reaction)
            {
                case DuckState.Happy: Hop(); break;
                case DuckState.Talk: Nod(); break;
                case DuckState.Listen: Nod(); break;
            }
        }

        // ---------- 浮動 / 點頭 / 跳躍 ----------

        private void StartBob()
        {
            var bob = new DoubleAnimation
            {
                From = 0,
                To = -3,
                Duration = TimeSpan.FromSeconds(1.2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            HostBob.BeginAnimation(TranslateTransform.YProperty, bob);
        }

        private void Nod()
        {
            var nod = new DoubleAnimationUsingKeyFrames();
            nod.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            nod.KeyFrames.Add(new EasingDoubleKeyFrame(-9, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.14))));
            nod.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.32))));
            nod.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5))));
            HostRot.BeginAnimation(RotateTransform.AngleProperty, nod);
        }

        private void Hop()
        {
            var hop = new DoubleAnimationUsingKeyFrames();
            hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            hop.KeyFrames.Add(new EasingDoubleKeyFrame(-16, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.18)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
            hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4)),
                new BounceEase { Bounces = 2, Bounciness = 2, EasingMode = EasingMode.EaseOut }));
            hop.KeyFrames.Add(new EasingDoubleKeyFrame(-7, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.55))));
            hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.72))));
            hop.Completed += (s, e) => StartBob();   // 跳完把持續性的上下浮動接回去
            HostBob.BeginAnimation(TranslateTransform.YProperty, hop, HandoffBehavior.SnapshotAndReplace);
        }

        // ---------- 選單 ----------

        private void OnMenuTalk(object sender, RoutedEventArgs e) => ToggleChat();

        private void OnMenuHistory(object sender, RoutedEventArgs e)
        {
            if (_history == null)
            {
                _history = new HistoryWindow { Owner = this };
                _history.Closed += (s, ev) => _history = null;
            }

            _history.LoadEntries();

            if (_history.IsVisible) _history.Activate();
            else _history.Show();
        }

        private void OnMenuCenter(object sender, RoutedEventArgs e)
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 24;
            Top = wa.Bottom - Height - 8;
            SavePosition();
            _chat?.Reposition();
        }

        private void OnMenuExit(object sender, RoutedEventArgs e)
        {
            SavePosition();
            Application.Current.Shutdown();
        }

        private void OnMenuAbout(object sender, RoutedEventArgs e)
        {
            new AboutWindow { Owner = this }.ShowDialog();
        }
    }
}
