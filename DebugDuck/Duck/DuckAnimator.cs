using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DebugDuck.Duck
{
    /// <summary>
    /// 從磁碟載入逐格 PNG 並用 DispatcherTimer 逐格播放。
    /// 綁定 <see cref="CurrentFrame"/> 到 Image.Source 即可。
    /// </summary>
    public sealed class DuckAnimator : INotifyPropertyChanged
    {
        private sealed class StateSpec { public int fps { get; set; } public bool loop { get; set; } }
        private sealed class Manifest
        {
            public Dictionary<string, StateSpec> states { get; set; }
            public string defaultImage { get; set; }
        }

        private static readonly Dictionary<DuckState, StateSpec> Defaults = new Dictionary<DuckState, StateSpec>
        {
            { DuckState.Idle,   new StateSpec { fps = 6,  loop = true  } },
            { DuckState.Blink,  new StateSpec { fps = 12, loop = false } },
            { DuckState.Listen, new StateSpec { fps = 8,  loop = true  } },
            { DuckState.Talk,   new StateSpec { fps = 10, loop = true  } },
            { DuckState.Happy,  new StateSpec { fps = 12, loop = false } },
            { DuckState.Drag,   new StateSpec { fps = 8,  loop = true  } },
        };

        private readonly Dictionary<DuckState, BitmapSource[]> _frames = new Dictionary<DuckState, BitmapSource[]>();
        private readonly Dictionary<DuckState, StateSpec> _specs = new Dictionary<DuckState, StateSpec>();
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        private DuckState _state = DuckState.Idle;
        private int _index;
        private ImageSource _currentFrame;
        private string _defaultImageName;

        public DuckAnimator()
        {
            foreach (var kv in Defaults) _specs[kv.Key] = kv.Value;
            _timer.Tick += OnTick;
            Load();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>非循環動畫（Blink/Happy）播到最後一格時觸發。</summary>
        public event EventHandler<DuckState> AnimationCompleted;

        /// <summary>目前要顯示的畫格。沒有任何 PNG 素材時為 null。</summary>
        public ImageSource CurrentFrame
        {
            get { return _currentFrame; }
            private set
            {
                if (ReferenceEquals(_currentFrame, value)) return;
                _currentFrame = value;
                var h = PropertyChanged;
                if (h != null) h(this, new PropertyChangedEventArgs(nameof(CurrentFrame)));
            }
        }

        public DuckState State { get { return _state; } }

        /// <summary>是否成功載入了任何 PNG 素材（否則畫面要退回向量小鴨）。</summary>
        public bool HasFrames { get; private set; }

        public void Play(DuckState state)
        {
            if (!_frames.ContainsKey(state)) state = DuckState.Idle;        // 缺該狀態 → 退回 idle
            if (!_frames.ContainsKey(state)) { Stop(); return; }            // 連 idle 都沒有 → 向量小鴨接手

            _state = state;
            _index = 0;
            ShowCurrent();

            if (_frames[state].Length <= 1) { _timer.Stop(); return; }   // 靜態單張圖，不用跑計時器

            var spec = _specs[state];
            _timer.Interval = TimeSpan.FromSeconds(1.0 / Math.Max(1, spec.fps));
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnTick(object sender, EventArgs e)
        {
            var seq = _frames[_state];
            var spec = _specs[_state];
            _index++;

            if (_index >= seq.Length)
            {
                if (spec.loop)
                {
                    _index = 0;
                }
                else
                {
                    _index = seq.Length - 1;
                    _timer.Stop();
                    ShowCurrent();
                    var h = AnimationCompleted;
                    if (h != null) h(this, _state);
                    return;
                }
            }

            ShowCurrent();
        }

        private void ShowCurrent()
        {
            BitmapSource[] seq;
            if (_frames.TryGetValue(_state, out seq) && seq.Length > 0)
                CurrentFrame = seq[Math.Min(_index, seq.Length - 1)];
        }

        private void Load()
        {
            // 磁碟上的 Assets/<state>/*.png：使用者要自訂逐格動畫時才會有
            var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            if (Directory.Exists(root))
            {
                ApplyManifest(Path.Combine(root, "duck.manifest.json"));

                foreach (DuckState state in Enum.GetValues(typeof(DuckState)))
                {
                    var dir = Path.Combine(root, state.ToString().ToLowerInvariant());
                    if (!Directory.Exists(dir)) continue;

                    var files = Directory.GetFiles(dir, "*.png")
                                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                         .ToArray();
                    if (files.Length == 0) continue;

                    var loaded = new List<BitmapSource>(files.Length);
                    foreach (var f in files)
                    {
                        var bmp = TryLoad(f);
                        if (bmp != null) loaded.Add(bmp);
                    }
                    if (loaded.Count > 0) _frames[state] = loaded.ToArray();
                }

                if (!_frames.ContainsKey(DuckState.Idle))
                {
                    var single = FindDefaultImage(root);
                    if (single != null)
                    {
                        var bmp = TryLoad(single);
                        if (bmp != null) _frames[DuckState.Idle] = new[] { bmp };
                    }
                }
            }

            // 預設小鴨：打包進組件的內嵌資源 Assets/黃小鴨.png
            if (!_frames.ContainsKey(DuckState.Idle))
            {
                var res = TryLoadResource("Assets/黃小鴨.png");
                if (res != null) _frames[DuckState.Idle] = new[] { res };
            }

            HasFrames = _frames.ContainsKey(DuckState.Idle);
            if (HasFrames) Play(DuckState.Idle);
        }

        private static BitmapSource TryLoadResource(string relativePath)
        {
            try
            {
                var sri = System.Windows.Application.GetResourceStream(new Uri(relativePath, UriKind.Relative));
                if (sri == null) return null;
                using (var s = sri.Stream)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = s;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        private string FindDefaultImage(string root)
        {
            try
            {
                if (!string.IsNullOrEmpty(_defaultImageName))
                {
                    var named = Path.Combine(root, _defaultImageName);
                    if (File.Exists(named)) return named;
                }

                return Directory.GetFiles(root, "*.png", SearchOption.TopDirectoryOnly)
                                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private void ApplyManifest(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var m = new JavaScriptSerializer().Deserialize<Manifest>(File.ReadAllText(path));
                if (m == null) return;

                _defaultImageName = m.defaultImage;
                if (m.states == null) return;

                foreach (var kv in m.states)
                {
                    DuckState state;
                    if (!Enum.TryParse(kv.Key, true, out state) || kv.Value == null) continue;
                    _specs[state] = new StateSpec
                    {
                        fps = kv.Value.fps > 0 ? kv.Value.fps : Defaults[state].fps,
                        loop = kv.Value.loop
                    };
                }
            }
            catch
            {
                // manifest 壞掉就當作沒有，用預設值。
            }
        }

        private static BitmapSource TryLoad(string file)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;      // 讀完就放開檔案鎖
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.UriSource = new Uri(file, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}
