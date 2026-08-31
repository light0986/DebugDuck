using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

namespace DebugDuck
{
    /// <summary>
    /// Duck.mp3（已去掉 ID3 標籤與開頭 4 個靜音 frame）以 base64 內嵌成程式碼（見檔尾 Data）。
    /// 播放時解碼寫到 %TEMP%\DebugDuck\Duck.mp3（只寫一次），再用 WPF MediaPlayer 播。
    /// MediaPlayer 只吃 URI 不吃 stream，所以一定要先落地成檔案。零外部相依。
    /// </summary>
    public static class DuckSound
    {
        public static bool Enabled = true;

        /// <summary>連叫時每一聲開始的間隔。比音檔（~910ms）短就會互相重疊＝混音效果；調小更急促。</summary>
        public static TimeSpan QuackStagger = TimeSpan.FromMilliseconds(250);

        /// <summary>每個 MediaPlayer 的音量。多聲重疊時總和會變大，所以單顆壓低一點。</summary>
        public static double Volume = 0.5;

        private const int PoolSize = 4;              // 同時最多幾聲（嘎嘎嘎=3，留一點餘裕）

        private static MediaPlayer[] _pool;
        private static string _path;
        private static readonly object _lock = new object();

        private static DispatcherTimer _seq;
        private static int _started;                 // 這次已經觸發幾聲
        private static int _target;                  // 這次總共要幾聲
        private static int _cursor;                  // 下一個要用 pool 裡的哪一顆

        /// <summary>依台詞字串發出對應聲數：嘎嘎→2、嘎嘎嘎→3、其餘（嘎 / ? / !）→1。</summary>
        public static void Speak(string line)
        {
            Play(line == "嘎嘎" ? 2 : line == "嘎嘎嘎" ? 3 : 1);
        }

        /// <summary>連叫 times 聲：用一組 MediaPlayer 輪流播，每聲相隔 <see cref="QuackStagger"/>，聲音會疊在一起。</summary>
        public static void Play(int times = 1)
        {
            if (!Enabled || times < 1) return;
            var app = System.Windows.Application.Current;
            if (app == null) return;

            // 已在 UI 執行緒就直接播，讓聲音和台詞文字同時出來
            if (app.Dispatcher.CheckAccess()) PlayCore(times);
            else app.Dispatcher.BeginInvoke(new Action(() => PlayCore(times)));
        }

        private static void PlayCore(int times)
        {
            try
            {
                EnsurePool();

                _target = Math.Min(times, PoolSize);
                _started = 0;

                _seq?.Stop();
                FireOne();                           // 第一聲：現在

                if (_started < _target)
                {
                    if (_seq == null)
                    {
                        _seq = new DispatcherTimer();
                        _seq.Tick += (s, e) =>
                        {
                            FireOne();
                            if (_started >= _target) _seq.Stop();
                        };
                    }
                    _seq.Interval = QuackStagger;
                    _seq.Start();
                }
            }
            catch { /* 沒有音效裝置 / 解碼器就算了 */ }
        }

        private static void FireOne()
        {
            var p = _pool[_cursor];
            _cursor = (_cursor + 1) % PoolSize;
            _started++;

            p.Stop();                                // 這顆若還在播就重頭來
            p.Position = TimeSpan.Zero;
            p.Volume = Volume;
            p.Play();
        }

        private static void EnsurePool()
        {
            if (_pool != null) return;
            var uri = new Uri(EnsureFile(), UriKind.Absolute);
            _pool = new MediaPlayer[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _pool[i] = new MediaPlayer { Volume = Volume };
                _pool[i].Open(uri);
            }
        }

        private static string EnsureFile()
        {
            if (_path != null && File.Exists(_path)) return _path;
            lock (_lock)
            {
                var dir = Path.Combine(Path.GetTempPath(), "DebugDuck");
                Directory.CreateDirectory(dir);
                var p = Path.Combine(dir, "Duck.mp3");
                if (!File.Exists(p) || new FileInfo(p).Length == 0)
                    File.WriteAllBytes(p, Convert.FromBase64String(Data));
                _path = p;
                return p;
            }
        }

        private static readonly string Data =
            "//viBAAN1upojxHsTyDdTRHiPYnkIW4G6Aw9mowtwN0Bh7NRf0ADSMk4kCa5iGWXA3zdNI4UEahjmQaZuHcdJxIFBngZZoG+dqHHCgisJuPwes" +
            "kBMg1ISoJkLMIgD+BkBZhaQ1QSkJoLAIsIICrCABjBqRAg3REwyAg4OQIGIqJsKSJYMAjAwBMJBUJIfDEZiwKwaB2VTIsmyAblgSyoXjEtGqCa" +
            "FswK6U6PVSJWeGZwmWqlKNQfrDuFpl7m2EaiUZOe6cJ3X3Nzczc3Ny6lGTnunBNVpl7m2EaiyE0ZPmCcgLkJMZNHjZIKxAIhSJh0XHBOKBGIRU" +
            "JR4WHwwCYgEQZf0ADSMk4kCa5iGWXA3zdNI4UEahjmQaZuHcdJxIFBngZZoG+dqHHCgisJuPweskBMg1ISoJkLMIgD+BkBZhaQ1QSkJoLAIsII" +
            "CrCABjBqRAg3REwyAg4OQIGIqJsKSJYMAjAwBMJBUJIfDEZiwKwaB2VTIsmyAblgSyoXjEtGqCaFswK6U6PVSJWeGZwmWqlKNQfrDuFpl7m2Ea" +
            "iUZOe6cJ3X3Nzczc3Ny6lGTnunBNVpl7m2EaiyE0ZPmCcgLkJMZNHjZIKxAIhSJh0XHBOKBGIRUJR4WHwwCYgEQZeZLxSwKrDrt4WzdpdjQl0Q" +
            "AkQqmq4sorapyx4tIyR6Ux0MzKFJ4FEWuXvTyMkA6EPAQSDIOe0tmDuxaQvsppE5hKsDDXu3BiCVCOCE8uOy0m6MAnyPVJpj7KcTc7hwL6rbEM" +
            "Pk+xN2mBMxzl8VKFn+o0PfJkl8A31EGrRxBDIVjgnzTHfUhg4cKX04/n5UEgSBAER34TxOT198369eW17a9+9KTN70WOZTV/y2vvSlNXr16+5w" +
            "YHlFjlOvSlISWfksG5u+SDAwWGFX8fxzfodk9IDc32MwPFlGD+5XEtWkEgSBACteZn9iQTOOAbiWT15LecgEgnoRMWHES9+nnB52r8rZoEB0JA" +
            "NDyRDzJeKWBVYddvC2btLsaEuiAEiFU1XFlFbVOWPFpGSPSmOhmZQpPAoi1y96eRkgHQh4CCQZBz2lswd2LSF9lNInMJVgYa924MQSoRwQnlx2" +
            "Wk3RgE+R6pNMfZTibncOBfVbYhh8n2Ju0wJmOcvipQs/1Gh75MkvgG+og1aOIIZCscE+aY76kMHDhS+nH8/KgkCQIAiO/CeJyevvm/Xry2vbXv" +
            "3pSZveixzKav+W196Upq9evX3ODA8oscp16UpCSz8lg3N3yQYGCwwq/j+Ob9DsnpAbm+xmB4sowf3K4lq0gkCQIAVrzM/sSCZxwDcSyevJbzkA" +
            "kE9CJiw4iXv084PO1flbNAgOhIBoeSITEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/74gQADudHd8SbbB9S6O74k22D6l3dzRxtvZ" +
            "dLu7mjjbey6QAAX5D1KI1IAMHETCwF3gqAplF+Fb0UIo/i82btYYHFpxrFOlQ1CIQW9UOW1zv0l45DPWbww/CxNvU2kcnYbR7cpgkrf+RtwcFI" +
            "tl9PMYNYijP2pOpDcvrOm87sQcrCQ1VmBghph0ToRMK5bdOyePA0jM4JlDzTMEz4sQrz8GhiB9e1ESD2xxAsTqlh5VfPOH/IQMG3F9F5gvf2Nx" +
            "6CqN1Xzx589NpnJzf32Ir+/elLUssio3Zxyzic+PyehGB5ig7fpZOrEBTW5+JBwsMBzVkt7qDm+8SnCcyNE7Egh3d0nRYj6Lh6QCZ6AAC/IepR" +
            "GpABg4iYWAu8FQFMovwreihFH8XmzdrDA4tONYp0qGoRCC3qhy2ud+kvHIZ6zeGH4WJt6m0jk7DaPblMElb/yNuDgpFsvp5jBrEUZ+1J1Ibl9Z" +
            "03ndiDlYSGqswMENMOidCJhXLbp2Tx4GkZnBMoeaZgmfFiFefg0MQPr2oiQe2OIFidUsPKr55w/5CBg24vovMF7+xuPQVRuq+ePPnptM5Ob++x" +
            "Ff370pallkVG7OOWcTnx+T0IwPMUHb9LJ1YgKa3PxIOFhgOaslvdQc33iU4TmRonYkEO7uk6LEfRcPSATPQ9oYTShAkBjgUCjDggqAaghgIK1o" +
            "UBhoEUXQfcZmyzvL0JCo8P08cuTQVwwddywSVkRfO20huQ6Cts7Y1FGaROgCAyVEJGjYa0SQYDWnlaXwCORdHagJGZ6eLYS9SLI+BSF1M1E0HI" +
            "4yLJIDIbnBXox6to9gl0vm+jJIu1QwQd2NdhUwmitVr9MP2Ex3F5mWzA6ewxwHTVcJMPipMO1buw5zc/TulpybzM5FPRcww5SF48Pyvq984W3f" +
            "pXqPXND9K9d8hlo4sUDxqtC8TC3ifzQ8sQCK+P9gbi/m/bpv516Y5rCz6ZD/S2wN+y+5P1SHtDCaUIEgMcCgUYcEFQDUEMBBWtCgMNAii6D7jM" +
            "2Wd5ehIVHh+njlyaCuGDruWCSsiL522kNyHQVtnbGoozSJ0AQGSohI0bDWiSDAa08rS+ARyLo7UBIzPTxbCXqRZHwKQupmomg5HGRZJAZDc4K9" +
            "GPVtHsEul830ZJF2qGCDuxrsKmE0Vqtfph+wmO4vMy2YHT2GOA6arhJh8VJh2rd2HObn6d0tOTeZnIp6LmGHKQvHh+V9XvnC279K9R65ofpXrv" +
            "kMtHFigeNVoXiYW8T+aHliARXx/sDcX837dN/OvTHNYWfTIf6W2Bv2X3J+qUxBTUUzLjk4LjQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/++IEAA7nAXFIm2xPMuAuKRNtieZdodcibj01w7Q65E3HprgAAB6Q+3eNEC1PCMeQGp6AEAR/" +
            "TZGhJH9c7jF7ourEzBL1H5yRYNikHLUZvH3oYZPthhqOTcE23XepejyRmG4zORZ6mLQC4zD2MZwXGGvNPVM1u27VJS1YMguMs8ZaBcPVZaJIlF" +
            "VOKg/CYYHSCRY2qHumYkrya4/dAP1rwpIqWBcvaSHCJPeP4nY9Wd5fJEDAqaOT5xmVNb3Z29az09t///+7vy8Pjl1VPdR7dMwkozZNpGvKlSVi" +
            "ZOXonCorZTRAqFg6ZeK5EVgPhPNhWU08t1SZThXYyw7CBnANmPm/DgAAD0h9u8aIFqeEY8gNT0AIAj+myNCSP653GL3RdWJmCXqPzkiwbFIOWo" +
            "zePvQwyfbDDUcm4Jtuu9S9HkjMNxmciz1MWgFxmHsYzguMNeaeqZrdt2qSlqwZBcZZ4y0C4eqy0SRKKqcVB+EwwOkEixtUPdMxJXk1x+6AfrXh" +
            "SRUsC5e0kOESe8fxOx6s7y+SIGBU0cnzjMqa3uzt61np7b///93fl4fHLqqe6j26ZhJRmybSNeVKkrEycvROFRWymiBULB0y8VyIrAfCebCspp" +
            "5bqkynCuxlh2EDOAbMfN+HDPocmRpjQDBAqGgkIgUzMtUMgJGdx0LUk1MGyLvdomASg4CDKjO1JwHOum/yVuUsVsUMMI0iSlWiiAlxLgqEaKWX" +
            "Mv6nSZf1CLYXFUpmCdx/Swz2JKpzfMoQlnLiX5vgLlWwFApIjM5RHNCU8pyDHKj2c3mjd3Sp27hK5DITOm2dCznJ+uoCcTyWcUspmVxRioplTV" +
            "Z8OZ35SY+kLSOYc0SHJm82vr6xJ/7+fc6029xpVAFBHcWWCAugZtrcipA0hjnGGUaNNKEhdDtNhcOo2hOCAkJAHJQilOMoVtbDZyuG3Fic51VJ" +
            "QlWJufGGzPocmRpjQDBAqGgkIgUzMtUMgJGdx0LUk1MGyLvdomASg4CDKjO1JwHOum/yVuUsVsUMMI0iSlWiiAlxLgqEaKWXMv6nSZf1CLYXFU" +
            "pmCdx/Swz2JKpzfMoQlnLiX5vgLlWwFApIjM5RHNCU8pyDHKj2c3mjd3Sp27hK5DITOm2dCznJ+uoCcTyWcUspmVxRioplTVZ8OZ35SY+kLSOY" +
            "c0SHJm82vr6xJ/7+fc6029xpVAFBHcWWCAugZtrcipA0hjnGGUaNNKEhdDtNhcOo2hOCAkJAHJQilOMoVtbDZyuG3Fic51VJQlWJufGG0xBTUU" +
            "zLjk4LjQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "//viBAAO5ylpSJuPTHLlLSkTcemOXqnrIG4wfUvVPWQNxg+pAABesONs0xOI1HgQGjAwJDhcIQKEABoDxoprUBABhCGcKStQBF8kJLMYaRT4mQ" +
            "9YVa4Qk0ztSZD1AMopW8VxDiWkmQolpYWlgMA9EqcKLMBDXEn5qI2MiCTKtRDFRSy1HS1HlAymHCeIdsyemalaPpWt5urEFuXCrszLDmxSODpw" +
            "2IgGKgKFkIESLDsC72ka0SdGOxMxNBoKhVa26RVP1m3Wft5//uf5e5WTSNrwe+XaZQswQswVg2gbPlUCM2WIjbRMmGSQkNx4Pi5wqppYbJGQQw" +
            "hUw2snj9V2rJ8MN5wbRNYtroAAL1hxtmmJxGo8CA0YGBIcLhCBQgANAeNFNagIAMIQzhSVqAIvkhJZjDSKfEyHrCrXCEmmdqTIeoBlFK3iuIcS" +
            "0kyFEtLC0sBgHolThRZgIa4k/NRGxkQSZVqIYqKWWo6Wo8oGUw4TxDtmT0zUrR9K1vN1Ygty4VdmZYc2KRwdOGxEAxUBQshAiRYdgXe0jWiTox" +
            "2JmJoNBUKrW3SKp+s26z9vP/9z/L3KyaRteD3y7TKFmCFmCsG0DZ8qgRmyxEbaJkwySEhuPB8XOFVNLDZIyCGEKmG1k8fqu1ZPhhvODaJrFtdT" +
            "+htCUGVRgOA0rCDmiQlSvTXEYBEgEgBUoLrjoAcgv4wtL1XLutZUIa/GmCp1oS19wPATInIeBgcGI2Qymnelajr8swZG5rC4PiEZizbMTexWx9" +
            "WKQZekjiXnWkMPrugaFIxwDg+LylKOg+IZdKoNXxyB1MEiKA0BwQgoIATjytXOJReJw/j4dDd/B/OhEiP3FinoV5+2OS0zGo6eOicjJiw1P4mk" +
            "t9vkzk890zNs6sz7quzUVKMMQNtnC5pExj8a5Ey48e0dXltoiJlCGlJjh+QW1w/wQ0iSJk4iE8noIJkL67UPUQDIzvmjYyVyKmDadjvtrdT+ht" +
            "CUGVRgOA0rCDmiQlSvTXEYBEgEgBUoLrjoAcgv4wtL1XLutZUIa/GmCp1oS19wPATInIeBgcGI2Qymnelajr8swZG5rC4PiEZizbMTexWx9WKQ" +
            "ZekjiXnWkMPrugaFIxwDg+LylKOg+IZdKoNXxyB1MEiKA0BwQgoIATjytXOJReJw/j4dDd/B/OhEiP3FinoV5+2OS0zGo6eOicjJiw1P4mkt9v" +
            "kzk890zNs6sz7quzUVKMMQNtnC5pExj8a5Ey48e0dXltoiJlCGlJjh+QW1w/wQ0iSJk4iE8noIJkL67UPUQDIzvmjYyVyKmDadjvtrdTEFNRTM" +
            "uOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/74gQADudPcckbT2Ry6e45I2nsjl+16RxuYT" +
            "HL9r0jjcwmOQAAp9D7cR58PNzCEHdLUiMEtIaFuExxOhL8tKzxbC614LmcRLaVltXhb4hjniGEQs+SpHoN52Wtz8OxLiwok7CTKOCU6APIpyQ6" +
            "P9EHST48T0PxpcS3ivJ1rRBdlYiJ1pvjqW6oViErE6HBgkvbl9CEoexwn8yKvB5JNcry+0OYSj4WgSTmhYeIbJqYIMZ2ScT4I8Rk+LVhiWDtRE" +
            "ZPsU6HnLT15gp0zs/nbOVm9FqO9F6343TKNh9nVr2ctfOmFqSUI8UGS9IwywZKaLjssQHhJQoh3VFu0zPbOzsz1sxjUp4Z8bW0Ud71+gABT6H2" +
            "4jz4ebmEIO6WpEYJaQ0LcJjidCX5aVni2F1rwXM4iW0rLavC3xDHPEMIhZ8lSPQbzstbn4diXFhRJ2EmUcEp0AeRTkh0f6IOknx4nofjS4lvFe" +
            "TrWiC7KxETrTfHUt1QrEJWJ0ODBJe3L6EJQ9jhP5kVeDySa5Xl9ocwlHwtAknNCw8Q2TUwQYzsk4nwR4jJ8WrDEsHaiIyfYp0POWnrzBTpnZ/O" +
            "2crN6LUd6L1vxumUbD7OrXs5a+dMLUkoR4oMl6RhlgyU0XHZYgPCShRDuqLdpme2dnZnrZjGpTwz42too73r9ArR/G4GWgwJB0KhwHC0wUB05z" +
            "AwSTuZgEAkWARgsFqrtFAQOC4HL0qZhAHkAOACdqcLL1LFbwMN8W5q8SWTxQ4PKkONhReSbZkzhhzSDCJJZFll6lypFL19rqXc26/HUEjrKS+X" +
            "auxiauXdThhqRLUgFhzeVLDoM5izSJmWsalTpO25D/NdeVkcoiEbjMpkcTjEppwOJQMsIUDJ0yNE8mEnQnq9wIxoXClCMnOD4p1sjusyHqv+nf" +
            "//9fw97DGZsKr05LTjnVNpHlwWQPJZqJraUFTS5brEyTEigVbIRINYs4PchwxcNrcL2S985dpdmCGfnst7UKk3tGhhw/IAVo/jcDLQYEg6FQ4D" +
            "haYKA6c5gYJJ3MwCASLAIwWC1V2igIHBcDl6VMwgDyAHABO1OFl6lit4GG+Lc1eJLJ4ocHlSHGwovJNsyZww5pBhEksiyy9S5Uil6+11Lubdfj" +
            "qCR1lJfLtXYxNXLupww1IlqQCw5vKlh0GcxZpEzLWNSp0nbch/muvKyOURCNxmUyOJxiU04HEoGWEKBk6ZGieTCToT1e4EY0LhShGTnB8U62R3" +
            "WZD1X/Tv//+v4e9hjM2FV6clpxzqm0jy4LIHks1E1tKCppct1iZJiRQKtkIkGsWcHuQ4YuG1uF7Je+cu0uzBDPz2W9qFSb2jQw4fkExBTUUzLj" +
            "k4LjQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/++IEAA7nMW/Im4xPIuYt+RNxieRf4fEcbjDdS/w+I43GG6kAAF6w6UQTEwCMFghQhBAh6oCM" +
            "hRrLgTCmaeCCjPlEm2IACsNBBQAVfNiaG4KGyKacjfwC7aidyCIisln7htSZZAUbT4f6H1uRm43OHoBgKUQ1NxeW2pSyKGn8fd4pC6UEg3EojH" +
            "dfPy6YiWbFlosjuYHoevMtJ7uuKXmT/mhMLZiBR9SSC6fiWWnouQX7QqDs5JhsyMgc4hF1TLlFt/p1f/9Lf///n/lLZXfnRfV0tJSzCCBiLJ4i" +
            "Bno0nBUiiwgOLCxpIfSWZwRIzwrGTR4FySmIyyP93KK+zVILnXaau3dL5l79/YAAL1h0ogmJgEYLBChCCBD1QEZCjWXAmFM08EFGfKJNsQAFYa" +
            "CCgAq+bE0NwUNkU05G/gF21E7kERFZLP3DakyyAo2nw/0PrcjNxucPQDAUohqbi8ttSlkUNP4+7xSF0oJBuJRGO6+fl0xEs2LLRZHcwPQ9eZaT" +
            "3dcUvMn/NCYWzECj6kkF0/EstPRcgv2hUHZyTDZkZA5xCLqmXKLb/Tq//6W////P/KWyu/Oi+rpaSlmEEDEWTxEDPRpOCpFFhAcWFjSQ+kszgi" +
            "RnhWMmjwLklMRlkf7uUV9mqQXOu01du6XzL37+wrGdV0pg8sAILAALGFRSLA0wyDgMGuCQVBoFWCFQIOgtsrMACFXMVpU0LypcqAl6S7afSabO" +
            "RYBCQbZMUB9S9O6DGWr3TAV6u540sk+WdKGuQ7aWzvswbvDrcKJpDWJJLIWyxiK2YGYDgvJaQUAcH4tPwkkeRAEADIMcHZOGQhEhOxEcGFjRpW" +
            "oXll9SDImjkJqsxKxIKbDpqdiayPaW77IhjkfHRaWNWH/rK3eft9dy8/0fT2dn7MzbkVKLV01U21qNU0eMOnS+/udEfNNkwqqNKhopH/zIscvW" +
            "HyFC8XzM9cPizdNhBX24brT2OXTzGNm1ce65jl33RtU7vgW5wrGdV0pg8sAILAALGFRSLA0wyDgMGuCQVBoFWCFQIOgtsrMACFXMVpU0LypcqA" +
            "l6S7afSabORYBCQbZMUB9S9O6DGWr3TAV6u540sk+WdKGuQ7aWzvswbvDrcKJpDWJJLIWyxiK2YGYDgvJaQUAcH4tPwkkeRAEADIMcHZOGQhEh" +
            "OxEcGFjRpWoXll9SDImjkJqsxKxIKbDpqdiayPaW77IhjkfHRaWNWH/rK3eft9dy8/0fT2dn7MzbkVKLV01U21qNU0eMOnS+/udEfNNkwqqNKh" +
            "opH/zIscvWHyFC8XzM9cPizdNhBX24brT2OXTzGNm1ce65jl33RtU7vgW50xBTUUzLjk4LjQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "//viBAAMxx1uyZtvY/Djrdkzbex+HqnVIG29k8vVOqQNt7J5AAKf+MEuDMRgxYQBgEkQpe1JUuKw7N1hFlPE2yl0CuaJRLoeStCjaOBkLuokeU" +
            "+0aIYuB8KMX7fASiOFLL4chyGFovpCFAxI4yVcnD8cU0f7e2vGR8yIB+MZseJpdu1xRwJ6/OxkemB4qVjuJyYdDBaJRy6sEgtSRVhYJgdpUKj4" +
            "9snkRg1KG2y/JUKRyW32SodNHLPRLNvObeZya3mZzv7+vHOZdhCgrWEzw6TV46XQPE45XjScnMKRcX0dEgVsno/HiEciJYl1H0eB8UlgtFonnc" +
            "zDMzvTMxQgKC5Nrj9hR6NAABT/xglwZiMGLCAMAkiFL2pKlxWHZusIsp4m2UugVzRKJdDyVoUbRwMhd1Ejyn2jRDFwPhRi/b4CURwpZfDkOQwt" +
            "F9IQoGJHGSrk4fjimj/b214yPmRAPxjNjxNLt2uKOBPX52Mj0wPFSsdxOTDoYLRKOXVgkFqSKsLBMDtKhUfHtk8iMGpQ22X5KhSOS2+yVDpo5Z" +
            "6JZt5zbzOTW8zOd/f145zLsIUFawmeHSavHS6B4nHK8aTk5hSLi+jokCtk9H48QjkRLEuo+jwPiksFotE87mYZmd6ZmKEBQXJtcfsKPRoAABer" +
            "O6kzNAgwsNER2YeBsmYEkQIQFazYxIVKwGIorsACApCeRALhqeRDXQ9KhjhraFhMEqEKFwlZyCgqQuDLUiuJihh7J0m4MVdog6F0jSbH8TsjOy" +
            "1J4VBN08eYtBiWH4hLGd6HF3UrMcadR7mnjc0ehGEWrTfQCEJnJ5q+KkW6K8iTi3HO5LLxHPYj9EHUxZ2rGWK1XzcWIyQP6E+mI5VcX+htft6T" +
            "94/yDOmd/c775DM21ZE2dUVO3de1M1DV1atWF6k1gNk57VQ2frTmBfLYxqXYzptWJ6RcSa5/Xd/Ge3Pq78cWdbHehcJBREwqX9lyf7AABerO6k" +
            "zNAgwsNER2YeBsmYEkQIQFazYxIVKwGIorsACApCeRALhqeRDXQ9KhjhraFhMEqEKFwlZyCgqQuDLUiuJihh7J0m4MVdog6F0jSbH8TsjOy1J4" +
            "VBN08eYtBiWH4hLGd6HF3UrMcadR7mnjc0ehGEWrTfQCEJnJ5q+KkW6K8iTi3HO5LLxHPYj9EHUxZ2rGWK1XzcWIyQP6E+mI5VcX+htft6T94/" +
            "yDOmd/c775DM21ZE2dUVO3de1M1DV1atWF6k1gNk57VQ2frTmBfLYxqXYzptWJ6RcSa5/Xd/Ge3Pq78cWdbHehcJBREwqX9lyf7TEFNRTMuOTg" +
            "uNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/74AQAAidQcEi7j2Ry6g4JF3Hsjl0xzSTtsH" +
            "1LpjmknbYPqQIAABP6HDzUrYCQoBgaAgyKgMHBlyxCBE32trZVPOvNLl2N3SQUBXQ1JjE8PUuLsmhbi2F6FdFEqDzygCxKYu6vO8kaTRJvyk1P" +
            "yChKNJeTkxohhwWM8TfJY/oex8uakQyVXksL4r50McIabWbOY3DjO5njHnIXVJHOumKzIirks2Vjubry26VYI07BTPmVi1iWlQeXLpgTjAjnm1" +
            "m8f+7lv+1dh3ZmfzPm9MYtNkLXEyx14s2RsOsJXG/TnXfLjETNjo5P6UPWz5DfUJjA8LJWQzZSy27f8yWnu/8ozPZEoe9Q43AXr7+t+gIAABP6" +
            "HDzUrYCQoBgaAgyKgMHBlyxCBE32trZVPOvNLl2N3SQUBXQ1JjE8PUuLsmhbi2F6FdFEqDzygCxKYu6vO8kaTRJvyk1PyChKNJeTkxohhwWM8T" +
            "fJY/oex8uakQyVXksL4r50McIabWbOY3DjO5njHnIXVJHOumKzIirks2Vjubry26VYI07BTPmVi1iWlQeXLpgTjAjnm1m8f+7lv+1dh3ZmfzPm" +
            "9MYtNkLXEyx14s2RsOsJXG/TnXfLjETNjo5P6UPWz5DfUJjA8LJWQzZSy27f8yWnu/8ozPZEoe9Q43AXr7+t+ggKfU+APCg+YcMIphQNWGcoiC" +
            "nXRwagw1AMxdjTLLSxFIv1koUmu7kPMZgt2m/Y9NvY5DxrVdNiLwNLjTPnboX8cCGXpkUwy+Mv7ea+yaTOszZxm0jcB0lWnftpJcK58YG6dYcn" +
            "5oOxXvGsF5mCgSg6OKEHI92MlxLiSqhKHQSEZbFq0dywZD+OSs8QvKba2NGycB2JvLSshasihW2xjYZn5pMFv7pmbzmQR9NKpuuxpXXndHtqy5" +
            "AfKR4OFCpQVi4TFx80dUK/lWVhKHke0h8Zh2XIiSqr9hq5qsRYZfFzEEEiL3O3Ye3TsICn1PgDwoPmHDCKYUDVhnKIgp10cGoMNQDMXY0yy0sR" +
            "SL9ZKFJru5DzGYLdpv2PTb2OQ8a1XTYi8DS40z526F/HAhl6ZFMMvjL+3mvsmkzrM2cZtI3AdJVp37aSXCufGBunWHJ+aDsV7xrBeZgoEoOjih" +
            "ByPdjJcS4kqoSh0EhGWxatHcsGQ/jkrPELym2tjRsnAdiby0rIWrIoVtsY2GZ+aTBb+6Zm85kEfTSqbrsaV153R7asuQHykeDhQqUFYuExcfNH" +
            "VCv5VlYSh5HtIfGYdlyIkqq/YauarEWGXxcxBBIi9zt2Ht07TEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/74gQADMdTb8gbb0zi6m35A23pnF7d1x5uPZNL27rjzceyaQAAVbDqfUKiRnIRJ2nAoHBQONJg" +
            "KCEW2qMZWexRIpdYOD3VY5UQEAkDLkqqoOxRVFgOEYAHQZAxRNidA1jqIQagtwQ4gw+FGij0XB2l3MNJO2QvkrilDBNQ0CjJuiTvXZM1pyNA/V" +
            "AmVWpEKNCC2nm5o9mMM80MSCWXELCogZdLbm5UJGVxIKQJFJwgQg6hQljw4xKF4CRMCQFknSaI141e5OCV7/+/P//f28h1LiqpulnEUVETVUak" +
            "ZEjZS0+catVEKS6IogmbDrLRpuYiU0wCAFoxwip9yusvcliWhYAX2dC7R1rG3HueX4AACrYdT6hUSM5CJO04FA4KBxpMBQQi21RjKz2KJFLrBw" +
            "e6rHKiAgEgZclVVB2KKosBwjAA6DIGKJsToGsdRCDUFuCHEGHwo0Uei4O0u5hpJ2yF8lcUoYJqGgUZN0Sd67JmtORoH6oEyq1IhRoQW083NHsx" +
            "hnmhiQSy4hYVEDLpbc3KhIyuJBSBIpOECEHUKEseHGJQvASJgSAsk6TRGvGr3JwSvf/35//7+3kOpcVVN0s4iioiaqjUjIkbKWnzjVqohSXRFE" +
            "EzYdZaNNzESmmAQAtGOEVPuV1l7ksS0LAC+zoXaOtY249zy/AACXZDI9uCGsGHIxcJGJCQAAgMBwZMFAdUKxkeAuDBgGFxwoAVVIUhNxQDmCQK" +
            "VQg0Vwm6GqDHMBEsRJiFA+wg4ezVIeYgYaKRb1nIhwJsQBCR8nuijvwuEauEeM5DF5AFxbD8OdMrb0no+kQoCSlijLtLHWeDKIQ8IjAHS/EXub" +
            "N1KMnJjQqtDU3ZWpBukL6QexDH85OavOHbDjqoNgTM9VXcPl6JTTrWt1J7ZrftmcmvZtvmsD2bjlzZE/PO0UrTmTBNB4ntJTNLVKuw5eKiE+fn" +
            "LYhna94TXTolMQDlltrz8yxtet73za2xZSjnAm2nLaEa9ProABLshke3BDWDDkYuEjEhIAAQGA4MmCgOqFYyPAXBgwDC44UAKqkKQm4oBzBIFK" +
            "oQaK4TdDVBjmAiWIkxCgfYQcPZqkPMQMNFIt6zkQ4E2IAhI+T3RR34XCNXCPGchi8gC4th+HOmVt6T0fSIUBJSxRl2ljrPBlEIeERgDpfiL3Nm" +
            "6lGTkxoVWhqbsrUg3SF9IPYhj+cnNXnDthx1UGwJmeqruHy9Epp1rW6k9s1v2zOTXs23zWB7Nxy5sifnnaKVpzJgmg8T2kpmlqlXYcvFRCfPzl" +
            "sQzte8Jrp0SmIByy215+ZY2vW975tbYspRzgTbTltCNen11MQU1FMy45OC40AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/" +
            "++IEAA7nN2pIm2xPMubtSRNtieZe3eEgbjx5w9u8JA3HjzgAAGaw85bM0IAEWiAnfYGCxgYAJDK02VxlpavlbVuK+h9YiMrNlhhgBQySPhh9nE" +
            "fYv0xCqvt9VC7i9nUVzMu4wVo8e63ryu01lobL5pxZY7KmKmjKYBhdVlD8Q/acDk6UlbwtheFTLaJNhMZMyQFFi+bCMCAjE6h1ESy+fOjkIVUQ" +
            "bhMSIxCOCDw1rjfzAeYnKrA/HUXGLJap7MDzPze9q9MzmV/72891ibMp9KT1ihOSmlWWiNJBhbRUaXrVhQ40ygWFZJjD4NCASi4kIhOqjJDJWU" +
            "FKcRR9XPZicbgtfeJvmb/hb/QAAM1h5y2ZoQAItEBO+wMFjAwASGVpsrjLS1fK2rcV9D6xEZWbLDDAChkkfDD7OI+xfpiFVfb6qF3F7OormZdx" +
            "grR491vXldprLQ2XzTiyx2VMVNGUwDC6rKH4h+04HJ0pK3hbC8KmW0SbCYyZkgKLF82EYEBGJ1DqIll8+dHIQqog3CYkRiEcEHhrXG/mA8xOVW" +
            "B+OouMWS1T2YHmfm97V6ZnMr/3t57rE2ZT6UnrFCclNKstEaSDC2io0vWrChxplAsKyTGHwaEAlFxIRCdVGSGSsoKU4ij6uezE43Ba+8TfM3/C" +
            "3+jPadzH5bQIDw0FAwMBxNBwIStQwQxGAApNdF9mBfF/lfFuYpElfp9qQhh+IaZmv15SsAw+8TjMkeVejXV2NGXM6CjCommEZFvPceZAi+IkzN" +
            "q9cwUehppHi8fQNl8JskmBaPJKqZIr7I0HahF6ssBdIarmOzkx9UOSoSq7c1GUx2EBHsjC4oE0E8fh/KRhcX58wm5biwGQwD/ScV4XJJvU9N6Q" +
            "3CNqSkLEm7w6//P/37febwJJrOVoErdiVjib6nc1FHjLqZYZpnr9JJFOeVIKoxpll3MrTxQx8ZjCXlWoesH7FKahANc7TWooUh96vLgo8uZxd6" +
            "iBntO5j8toEB4aCgYGA4mg4EJWoYIYjAAUmui+zAvi/yvi3MUiSv0+1IQw/ENMzX68pWAYfeJxmSPKvRrq7GjLmdBRhUTTCMi3nuPMgRfESZm1" +
            "euYKPQ00jxePoGy+E2STAtHklVMkV9kaDtQi9WWAukNVzHZyY+qHJUJVduajKY7CAj2RhcUCaCePw/lIwuL8+YTctxYDIYB/pOK8Lkk3qem9Ib" +
            "hG1JSFiTd4df/n/79vvN4Ek1nK0CVuxKxxN9Tuaijxl1MsM0z1+kkinPKkFUY0yy7mVp4oY+MxhLyrUPWD9ilNQgGudprUUKQ+9XlwUeXM4u9R" +
            "BMQU1FMy45OC40AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//viBAAERvJoyZtvZPDeTRkzbeyeHBmjJ029M4" +
            "ODNGTpt6ZwAACv9F7wOBXNCwaKBhblfa1yYFdBHRir0uw01x1xteVbLGIP451VbzmC4q8fQeTZEAQ1C3rgmC5paMiFbXRlJ9hKZlSClOOOn4h7" +
            "H5QwE2ukyYajvJAVSlbIx3I2C4OTDEm2xmOm2hImJRGJqjPDU7ksQkjGPMfMWyuVEFcmm0Nlk++NxpPZUw082Bb5dWGFCk2lbtS0DlINn++dx6" +
            "Z2enc29K/SX2l0rzpYTCrEYYyYXPlkNcWGqdnTVyAv2JRPXLx8ZxFAjSRDkMIh0RGK+SeOUSjROLnnrtKHnLr6QAAr/Re8DgVzQsGigYW5X2tc" +
            "mBXQR0Yq9LsNNcdcbXlWyxiD+OdVW85guKvH0Hk2RAENQt64JguaWjIhW10ZSfYSmZUgpTjjp+Iex+UMBNrpMmGo7yQFUpWyMdyNguDkwxJtsZ" +
            "jptoSJiURiaozw1O5LEJIxjzHzFsrlRBXJptDZZPvjcaT2VMNPNgW+XVhhQpNpW7UtA5SDZ/vncemdnp3NvSv0l9pdK86WEwqxGGMmFz5ZDXFh" +
            "qnZ01cgL9iUT1y8fGcRQI0kQ5DCIdERivknjlEo0Ti5567Sh5y6+kAIgAu/0z+tNNDDFBlCQpe4y9YMXXGXdT4hpn7pNqxlmDus6XisxL5Yz+R" +
            "Y6jIFeJepD9PJGpw/yBO063HQ/Q5gP1SwjVYGdINZ+Hwm1YxNrEaSHn8uHiJYGFkkLerWNnVx1zMDMyJZWJQfpOyCqlyUKsXJtIQ7SjeyHtUJA" +
            "UJAMk4BDIqTBk2YLFUTAXEpArEgAKBkFBMSpIGlmX5Vef33/cof/3Lx6f8ZqJIlFRSWWjKLCKxLp5NbY1BJe1yemBQ8PSMtPEzRGwJQhyMqHSw" +
            "qMGhx1BUcLCw4wtrmLSmz6wAiAC7/TP6000MMUGUJCl7jL1gxdcZd1PiGmfuk2rGWYO6zpeKzEvljP5FjqMgV4l6kP08kanD/IE7TrcdD9DmA/" +
            "VLCNVgZ0g1n4fCbVjE2sRpIefy4eIlgYWSQt6tY2dXHXMwMzIllYlB+k7IKqXJQqxcm0hDtKN7Ie1QkBQkAyTgEMipMGTZgsVRMBcSkCsSAAoG" +
            "QUExKkgaWZflV5/ff9yh//cvHp/xmokiUVFJZaMosIrEunk1tjUEl7XJ6YFDw9Iy08TNEbAlCHIyodLCowaHHUFRwsLDjC2uYtKbPrTEFNRTMu" +
            "OTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/74gQADubNaMkbbE5Q2a0ZI22JyiE2Bxpu4THEJsDjTdwmOAAAp9TlcYOMSz4gHzDQMgB0Snag" +
            "dyFqLdX4nvgj+shmBKCQG7UOsSpmUQ6rc26cjcIozJmsest5K4P9x81LZbLrozGhVGjAeTeHAQLh8QqDiIKgpeVjE3KB8wcuMKojkpJzo1wsHA" +
            "jDtQ1HG4gEzDoQ4dZUDXxydAK0l+hry5Z0vGrCgmNnth0qSWnfDZQoO5VwQU7679/mYZ7/vue/5XkzK0RjsI44gbLostHBtCicq9MhFDR0uZbH" +
            "TxwIBgqqBoMFW2BIHjhlbAiwQDg+kB2geAzwDJi8wn9AAAU+pyuMHGJZ8QD5hoGQA6JTtQO5C1Fur8T3wR/WQzAlBIDdqHWJUzKIdVubdORuEU" +
            "ZkzWPWW8lcH+4+alstl10ZjQqjRgPJvDgIFw+IVBxEFQUvKxiblA+YOXGFURyUk50a4WDgRh2oajjcQCZh0IcOsqBr45OgFaS/Q15cs6XjVhQT" +
            "Gz2w6VJLTvhsoUHcq4IKd9d+/zMM9/33Pf8ryZlaIx2EccQNl0WWjg2hROVemQiho6XMtjp44EAwVVA0GCrbAkDxwytgRYIBwfSA7QPAZ4Bkxe" +
            "YT+gGRHEC6mGQphgVGBgXmGwAiwCBAVmBgKhAcAICC2iY7PS9jTBYBwcDyDZgaAAWA5kREDY9hXBapaKvy1q9i2wRFRQuS3ZiCaql7di9rTEL2" +
            "bOBEVLU1m8UoRrQUAwkKlps8XsodE1mPg26MywCscSWVDkES6MNOfBVddcHOjGJTIJQ0Glbm0xmsTk0AW3tmZBF3BZy7EBxSoFRppAVB9QNOFa" +
            "0HEzhEhVRJtBkoDgHsgwIdIV6Y9whUtqX9Jf/yl/XyMdWSTIUMEBWVFTC6iYy5c9NNOyeBMRrqY5HEsKQsPYOowufQ7mD6gVpNjU5J0ZK7lOpf" +
            "OhmpfqKTFK9JqL/C73GJbD5lXluSDIjiBdTDIUwwKjAwLzDYARYBAgKzAwFQgOAEBBbRMdnpexpgsA4OB5BswNAALAcyIiBsewrgtUtFX5a1ex" +
            "bYIiooXJbsxBNVS9uxe1piF7NnAiKlqazeKUI1oKAYSFS02eL2UOiazHwbdGZYBWOJLKhyCJdGGnPgquuuDnRjEpkEoaDStzaYzWJyaALb2zMg" +
            "i7gs5diA4pUCo00gKg+oGnCtaDiZwiQqok2gyUBwD2QYEOkK9Me4QqW1L+kv/5S/r5GOrJJkKGCArKiphdRMZcuemmnZPAmI11McjiWFIWHsHU" +
            "YXPodzB9QK0mxqck6MldynUvnQzUv1FJilek1F/hd7jEth8yry3JTEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/" +
            "++IEAAzHe3HIG49k8O9uOQNx7J4dLb0kbb2Py6W3pI23sfkAAF/Q8CaSwLzBQnMGhkEBEHAItwFQKXiAwDSvIgXALtqkXU3ZVVB5qq6oSxPCSs" +
            "8FQSZCRMwiD4PVHsIrFOJKS8n4OEVwl5xEyOROqMdavVJMEcnD3MVVG6zJl4XNkyf0VOGG2sZuIhYOBnckGrJFnUA56NL1VNq6WmRtRkJ+p4cY" +
            "/EauUPOgrlWp0KnTFoc8y4Y7QWyZH5mmJKsST5xasv6eF92Pqx3tZus5szk7XrzrUGRJ1MsHLbmo3ICnlDxOX6XOKOSOyAdr0a1plKvIzLdWoS" +
            "yT2GBaUn09b1+fpHac6len4YIuqRGEBNTMNQAAC/oeBNJYF5goTmDQyCAiDgEW4CoFLxAYBpXkQLgF21SLqbsqqg81VdUJYnhJWeCoJMhImYRB" +
            "8Hqj2EVinElJeT8HCK4S84iZHInVGOtXqkmCOTh7mKqjdZky8LmyZP6KnDDbWM3EQsHAzuSDVkizqAc9Gl6qm1dLTI2oyE/U8OMfiNXKHnQVyr" +
            "U6FTpi0OeZcMdoLZMj8zTElWJJ84tWX9PC+7H1Y72s3Wc2Zydr151qDIk6mWDltzUbkBTyh4nL9LnFHJHZAO16Na0ylXkZlurUJZJ7DAtKT6et" +
            "6/P0jtOdSvT8MEXVIjCAmpmGoAAJn1O4KAYLmMhRiAGhGgDZSmfFFhpW28RdhliZUWT+UxYEQHWdxYRPCxjtEyPdEqZDAvhuna2PTeVhcjRXSH" +
            "F1NA9DpWFpDFKYpc3h3G6ljTOkzDoTZ5Om9iSg/VY5rxuKBob2AvyQVrO1DyXyQMDIS0MtIFDMvPLIYFw6CALiSpJwkrCyuiRk4RVjr0Zy8vBg" +
            "VlI5xJHChpqxT7z3Xndt20+ZmZn7595vm19/GnY3DoxOYes3JmeQr/VnR/Ytn7J8JagyJ5scoRdXm5oWy+dG6FGZwM7zGTNLzPv65JuExVpaAc" +
            "Q5aW31AAEz6ncFAMFzGQoxADQjQBspTPiiw0rbeIuwyxMqLJ/KYsCIDrO4sInhYx2iZHuiVMhgXw3TtbHpvKwuRorpDi6mgeh0rC0hilMUubw7" +
            "jdSxpnSZh0Js8nTexJQfqsc143FA0N7AX5IK1nah5L5IGBkJaGWkChmXnlkMC4dBAFxJUk4SVhZXRIycIqx16M5eXgwKykc4kjhQ01Yp957rzu" +
            "27afMzMz98+83za+/jTsbh0YnMPWbkzPIV/qzo/sWz9k+EtQZE82OUIurzc0LZfOjdCjM4Gd5jJml5n39ck3CYq0tAOIctLb6kxBTUUzLjk4Lj" +
            "QAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//viBAAGZvVqyRtsTqDerVkjbYnUHznrIO48ec" +
            "vnPWQdx485AACf9M+jTBxYoKFksFYkrWKgbdSYCZS1VORAKxqsq1M9p+FGyiWMwbgyOC3UdVrbj3VwrIUXYe4bazTJWZs/hM44khhyicFYkRpI" +
            "BnqhXHwSTwnEUkuoSkwaBAGb6YyOXWxWIjqxp0pD8NRJYJxAP1kJacPgaE6BYNA4kWCYNiJkiRisJjge1duVPbEgMKCRfow+NW4mQbJP7ef11o" +
            "ZfqH93DvJJqpB9WMSJxeA6fypRD8z5JMrJNhXpJlEbiQUgWQmyz9JBFINAKOyDp5PY0qfGlA2ElsEp4fUADt/tQAAE/6Z9GmDixQULJYKxJWsV" +
            "A26kwEylqqciAVjVZVqZ7T8KNlEsZg3BkcFuo6rW3HurhWQouw9w21mmSszZ/CZxxJDDlE4KxIjSQDPVCuPgknhOIpJdQlJg0CAM30xkcutisR" +
            "HVjTpSH4aiSwTiAfrIS04fA0J0CwaBxIsEwbETJEjFYTHA9q7cqe2JAYUEi/Rh8atxMg2Sf28/rrQy/UP7uHeSTVSD6sYkTi8B0/lSiH5nySZW" +
            "SbCvSTKI3EgpAshNln6SCKQaAUdkHTyexpU+NKBsJLYJTw+oAHb/agQvWHKa0YiF4hE4KARgAQGCwgpo/hb8eACNTDIYRSa8vRgqoWiw8qmsEs" +
            "5O2ZSTSxepVWMqmcFIF21OFjNLdRlERX5NoNJoqSOg5y9IMOEpWs8UKUZAj4SKEEiRragRbhgnZIsE9Rqsc128UxlNkywXo0TsclMcKkaWg8X7" +
            "CxTrpCm9hfogwSTDxjoiMqHkdQr0dFPmWzlFXm+OllKqWmWdshQKvdUrAv9a1n+Dn/4pTHt/9azqzy9Z3rDPKxVdK6My9hSTnNI8WVMxM54Mdn" +
            "GGuUNjaRFoZx1VLMxqlTs67NoxJIGCE4tnqwGiJUZcIWfFIrwHKVvxbHIQvWHKa0YiF4hE4KARgAQGCwgpo/hb8eACNTDIYRSa8vRgqoWiw8qm" +
            "sEs5O2ZSTSxepVWMqmcFIF21OFjNLdRlERX5NoNJoqSOg5y9IMOEpWs8UKUZAj4SKEEiRragRbhgnZIsE9Rqsc128UxlNkywXo0TsclMcKkaWg" +
            "8X7CxTrpCm9hfogwSTDxjoiMqHkdQr0dFPmWzlFXm+OllKqWmWdshQKvdUrAv9a1n+Dn/4pTHt/9azqzy9Z3rDPKxVdK6My9hSTnNI8WVMxM54" +
            "MdnGGuUNjaRFoZx1VLMxqlTs67NoxJIGCE4tnqwGiJUZcIWfFIrwHKVvxbHJMQU1FMy45OC40AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/74gQADMbsaMmbb0zw3Y0ZM23pniAF0yBuPZdMALpkDcey6QCAr/TzGcBIAYbLDBg0n4HC7Onm" +
            "SFTigthKunKbvSverc6LZmHv23drrXlgoFYO0Op4eJppJrLsixfOTepCAE5SzM4DvUaLim4nnaBUJcLIlaU6RydZlyK6Ir1Umz7PlfYVfAZZk+" +
            "r3iWValR1TQVy9dlcMOcNbwrUSS4ZiojtpMVWjY6wo26IbqWiOEB6Kh8jDZY0JVe3FV+pynGcY/9Kv/fjtQ2NScrR3WTS77B4jL0woK1VHTi0K" +
            "CrJ7ToYCarWk65OiYYOCk2YAxESwKsFR9A0/GOtC4hfYyK6+wAgK/08xnASAGGywwYNJ+Bwuzp5khU4oLYSrpym70r3q3Oi2Zh79t3a615YKBW" +
            "DtDqeHiaaSay7IsXzk3qQgBOUszOA71Gi4puJ52gVCXCyJWlOkcnWZciuiK9VJs+z5X2FXwGWZPq94llWpUdU0FcvXZXDDnDW8K1EkuGYqI7aT" +
            "FVo2OsKNuiG6lojhAeiofIw2WNCVXtxVfqcpxnGP/Sr/347UNjUnK0d1k0u+weIy9MKCtVR04tCgqye06GAmq1pOuTomGDgpNmAMREsCrBUfQN" +
            "PxjrQuIX2MiuvsAABlsONr0xCHkbAIATBgBHgsYVDYQHyzSIiu6JJgcAAsAgwAt2cFdUAqooSWdpvo9Q2FgGsIzJnLYXCUtQRNo5LXRPj/fh1i" +
            "1ugYbgdg+VSiUaQUyzyJQhwpZJUUXokUUlCqJ4vty7Q9kWzTJMTE512wIe5xXNQLkyLHCiVk/jfeoYuWNRQnKEqjwkDTgEzJW/ZzJVyiUheT+Z" +
            "XJmcXA4rjqbrzJVZb/HTLlJvm7fpn8/vmcmf/KTjzNI+h6JMuobukyrpxaEwLbcCwvGCFQpoiyWj9QZRq7HZXVJAlq1hPJYZnYNjr3Ogggjgjj" +
            "j1vLLqQUpPy60AchVFZtjz0A7YAAMthxtemIQ8jYBACYMAI8FjCobCA+WaREV3RJMDgAFgEGAFuzgrqgFVFCSztN9HqGwsA1hGZM5bC4SlqCJt" +
            "HJa6J8f78OsWt0DDcDsHyqUSjSCmWeRKEOFLJKii9EiikoVRPF9uXaHsi2aZJiYnOu2BD3OK5qBcmRY4USsn8b71DFyxqKE5QlUeEgacAmZK37" +
            "OZKuUSkLyfzK5Mzi4HFcdTdeZKrLf46ZcpN83b9M/n98zkz/5SceZpH0PRJl1Dd0mVdOLQmBbbgWF4wQqFNEWS0fqDKNXY7K6pIEtWsJ5LDM7B" +
            "sde50EEEcEccet5ZdSClJ+XWgDkKorNseegHbTEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/" +
            "++IEAA7nbHJIm29Mcu2OSRNt6Y5dweMmdbeAA7g8ZM628AAAAJ/Q4TCHAYwsdCoWQiQFBRQEBIm1JqY8HJRqUJ8qaJVqbl31bYETqirsXMhJIj" +
            "4LA3IcnC6KYoRuksUAWooh8HIJoqDLSB6uZQCUbyWDMPpJnmeahUiIRospkl5WILYhymQ4znHcGRAqFlVsSNCH0W4QJFKhUK5Mp+OkDMPyOYCU" +
            "giY8F1AAEgYHQZQEAaNOMQjimMgkAe1gRSJgNkZOgLIDpW4m83J72d/+wRVPPacajrEUSNQ4Rm3nMzNk4hTWk2wkiJikkSPRG9WREcFKVEkmzA" +
            "EhciJV5fdpTab2q8vGUFb57Fv4Zlso372QAAn9DhMIcBjCx0KhZCJAUFFAQEibUmpjwclGpQnypolWpuXfVtgROqKuxcyEkiPgsDchycLopihG" +
            "6SxQBaiiHwcgmioMtIHq5lAJRvJYMw+kmeZ5qFSIhGiymSXlYgtiHKZDjOcdwZECoWVWxI0IfRbhAkUqFQrkyn46QMw/I5gJSCJjwXUAASBgdB" +
            "lAQBo04xCOKYyCQB7WBFImA2Rk6AsgOlbibzcnvZ3/7BFU89pxqOsRRI1DhGbeczM2TiFNaTbCSImKSRI9Eb1ZERwUpUSSbMASFyIlXl92lNpv" +
            "ary8ZQVvnsW/hmWyjfvZU+puUSYyhgYKAweBg0DCRQJiQIYGFrHYZYgGKs3TBaOkMSMzCCpYk5YwRKFmWxD0OSLRBQDTEwXSWMg8024n+m2eEV" +
            "h+EoExJQPWiHN8xm8iFen1MX870ii4NKHzEYiXs8Mt7LATetRlWljjLGqVGr1hCWdZgqg00ILchDgZlJV9Rwl9dpyDFPxBrDhHyrKw7jST6nlV" +
            "yo2rZ2KW28+7/Ga2+b2x/n/Mmq7+fWA3O8zvMdTrqlZZIkXdFlXRWOzKwulnWJGKdhKRFFxfvVe2I/CpXLm6Y7o2f/X1W9K3+f/nXzjWfj01qu" +
            "5dJrRcFVPqblEmMoYGCgMHgYNAwkUCYkCGBhax2GWIBirN0wWjpDEjMwgqWJOWMEShZlsQ9Dki0QUA0xMF0ljIPNNuJ/ptnhFYfhKBMSUD1ohz" +
            "fMZvIhXp9TF/O9IouDSh8xGIl7PDLeywE3rUZVpY4yxqlRq9YQlnWYKoNNCC3IQ4GZSVfUcJfXacgxT8Qaw4R8qysO40k+p5VcqNq2diltvPu/" +
            "xmtvm9sf5/zJqu/n1gNzvM7zHU66pWWSJF3RZV0VjsysLpZ1iRinYSkRRcX71XtiPwqVy5umO6Nn/19VvSt/n/51841n49NaruXSa0XBVMQU1F" +
            "My45OC40AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//viBAAAB8SJTJ5ugAL4kSmTzdAAXu2jTP2cgA" +
            "PdtGmfs5AAAAABEgxaQEtNi5yIcYOBhhMYAFpmI+oXgoDMtNgKIvquYAipjIGZgkkU+JcH4gSEDgA0IUDICxbxwEWEHlwV4DJiQRDxAEdx0jCf" +
            "HIFni0CtA/cYo+SBjTIkRpABSopQWsnRbj4YrEeCxCfxEC2RQjDVRIDiKqRME4xDxkzIXKOcak4RchxqkidMiisipcK5QIebjeKRTJg+XSoTxT" +
            "RYzNjQrDwSROFU6dUokkkJkVCQJ4uGLqS9VRfT/puMAdxQNOpR4h10NBGlOrokxTd2Tdk5syRmXEi06BUTZBFVSaTqffdOzN//1MjQRXTZ6qv/" +
            "/Yz1qd3M01VgAAARIMWkBLTYuciHGDgYYTGABaZiPqF4KAzLTYCiL6rmAIqYyBmYJJFPiXB+IEhA4ANCFAyAsW8cBFhB5cFeAyYkEQ8QBHcdIw" +
            "nxyBZ4tArQP3GKPkgY0yJEaQAUqKUFrJ0W4+GKxHgsQn8RAtkUIw1USA4iqkTBOMQ8ZMyFyjnGpOEXIcapInTIorIqXCuUCHm43ikUyYPl0qE8" +
            "U0WMzY0Kw8EkThVOnVKJJJCZFQkCeLhi6kvVUX0/6bjAHcUDTqUeIddDQRpTq6JMU3dk3ZObMkZlxItOgVE2QRVUmk6n33Tszf/9TI0EV02eqr" +
            "//2M9andzNNVZAAALTk6eMEwqTodX3bmChmBBRRf4hCJCwCCA0TxHNNILAgIwCpGMgi6dyQZIASAAA08zww5JCakPVct+4tTw5L45G5U6MPuQy" +
            "92XinXWdyDYYpYetO5I4IlNSiqRi7T4Q/B8E25yRx+UXZFJXbsU9NSXqCxTTlDGMYzalknhiI3JDLn/jEri1mnsV6meNen7G4s8Lh2YccV0FbJ" +
            "53IFjE9DL7xK3f5vD//////////9/lz723gpNRStDem4tvGn/txFxHjXQ/E1llT/Kr1aW078P+4UJfx830euGHtfqGIff9/qWnkFPU7ctU729C" +
            "u1TFNMsEy/lzKAAAWnJ08YJhUnQ6vu3MFDMCCii/xCESFgEEBoniOaaQWBARgFSMZBF07kgyQAkAABp5nhhySE1Ieq5b9xanhyXxyNyp0Yfchl" +
            "7svFOus7kGwxSw9adyRwRKalFUjF2nwh+D4Jtzkjj8ouyKSu3Yp6akvUFimnKGMYxm1LJPDERuSGXP/GJXFrNPYr1M8a9P2NxZ4XDsw44roK2T" +
            "zuQLGJ6GX3iVu/zeH//////////7/Ln3tvBSailaG9NxbeNP/biLiPGuh+JrLKn+VXq0tp34f9woS/j5vo9cMPa/UMQ+/7/UtPIKep25ap3t6F" +
            "dqmKaZYJl/LmUxBTUUzLjk4LjQAAAAAAAAAP/74AQABEdhgtIbbxZw7DBaQ23izh8KC1FNPHnD4UFqKaePOAACwXS+bOREFoXs/AAWBh9BCYgA" +
            "mPBBVAAMPImqzGetBlCYDFZDYxUjMPRWemovYVnRZSMWIQKZiQ8Ag4qERjwITEzFl+PVMskeOBq0Gw+37wwPLo6mmanzt9aVMtr567bG1Vvp3E" +
            "z1GrcqA1YMYzFZtiT5jrrSNN9HLDWzPXJLb3Fa3rrDhWuZ4d9/O5PqTLWpGiK2raiXIqxx9Vq+rpkVOvn4t////////j/MBsZz0Vj0/TfUw9LD" +
            "GMR6rw9hzl+R79MWhpCeks682ra4YHjXWdxpSJ3vzF3M40cbP7v/onWv9dfujP770MEBAAFgul82ciILQvZ+AAsDD6CExABMeCCqAAYeRNVmM9" +
            "aDKEwGKyGxipGYeis9NRewrOiykYsQgUzEh4BBxUIjHgQmJmLL8eqZZI8cDVoNh9v3hgeXR1NM1Pnb60qZbXz122NqrfTuJnqNW5UBqwYxmKzb" +
            "EnzHXWkab6OWGtmeuSW3uK1vXWHCtczw77+dyfUmWtSNEVtW1EuRVjj6rV9XTIqdfPxb////////H+YDYznorHp+m+ph6WGMYj1Xh7DnL8j36Y" +
            "tDSE9JZ15tW1wwPGus7jSkTvfmLuZxo42f3f/ROtf66/dGf33oYICiAAABKeFAlJFUJDkQqEiRNONkCCO4wgxJ4KLQkOccECGJkF4XmiIcdfmC" +
            "ChiKiSgswV8NOVbCJiig1BrT6IEUKlbGuMre2NOUUAGnrfVSsHecar2djJeraWN/k7i8QEUcyAQ2MwqtTKm2V0qWNygq5ILKViG4jd3T8b3gLG" +
            "YbGq4D1fbz4e1kjwHilaaVkeJKImLriYeCoDDcDXWlpIrtyi/7//////////1v3ZKsKwyM5Lk6KI4lCimogAwTwU1E/DVBC1tyUrSlkWVC8pay" +
            "vVWxxkQSxwjmYcE0In7EejCdaoh7uUPROHUzNPp+55T8sykLiXLOiXiAAABKeFAlJFUJDkQqEiRNONkCCO4wgxJ4KLQkOccECGJkF4XmiIcdfm" +
            "CChiKiSgswV8NOVbCJiig1BrT6IEUKlbGuMre2NOUUAGnrfVSsHecar2djJeraWN/k7i8QEUcyAQ2MwqtTKm2V0qWNygq5ILKViG4jd3T8b3gL" +
            "GYbGq4D1fbz4e1kjwHilaaVkeJKImLriYeCoDDcDXWlpIrtyi/7//////////1v3ZKsKwyM5Lk6KI4lCimogAwTwU1E/DVBC1tyUrSlkWVC8pa" +
            "yvVWxxkQSxwjmYcE0In7EejCdaoh7uUPROHUzNPp+55T8sykLiXLOiXTEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/7" +
            "4gQABmeHglTTL03w8PBKmmXpvh26C07NPN8Dt0Fp2aeb4IAAAAAAUTVtlOa0lPWr5IywiclqVKnmkJHGiKERezowKTykC0oC1KgghZWwgGQHG6" +
            "aXgc53FLajLXZWOMxZO0dpPjDMwdhKkSXmhd4A+iiaGURwZBzxzrUJmLRUEsNA0xJ0/O6JQhjVDVY40PThyJSCsMSvuzpRCIUJZVOnPt21SlHB" +
            "zZVqNI2McJ9piplWFvYXsQp00XYphbC9QdqVXxdY3/9////////f//hRp7eGhTc5OKBvtQDM0KsLSaIagwgRmTRttkaWxojI0FkyIxIH12iUjQ" +
            "pZb/ihu8Sy3xhdY73KXnCFfduqq4T/8a3/x6sAAAAAAKJq2ynNaSnrV8kZYROS1KlTzSEjjRFCIvZ0YFJ5SBaUBalQQQsrYQDIDjdNLwOc7ilt" +
            "RlrsrHGYsnaO0nxhmYOwlSJLzQu8AfRRNDKI4Mg5451qEzFoqCWGgaYk6fndEoQxqhqscaHpw5EpBWGJX3Z0ohEKEsqnTn27apSjg5sq1GkbGO" +
            "E+0xUyrC3sL2IU6aLsUwtheoO1Kr4usb/+////////v//wo09vDQpucnFA32oBmaFWFpNENQYQIzJo22yNLY0RkaCyZEYkD67RKRoUst/xQ3eJ" +
            "Zb4wusd7lLzhCvu3VVcJ/+Nb/49Uga3mnJ4cAMU1bGTqkOOSSAQgZA8QJXLcmosdAp8pStwY+YKMVqWivitEm1pixmnY6zd/3iTntryQwSGWa+" +
            "TN0wV0RFRx3pNKWNqCy6SwE5L4QAlqxytImnuS+zoJkqkoW2HASQDdZ5dizoEir/Q3BwmC9ODQzTT1Q5ptJBaJ2xDmORdK1UOOWd1eHJ7QjZjb" +
            "0fJSZ0bC3jGWn/G//v///////7/zv/Dv4ScfO0klN6LLETUGkuWNkzlcQ6Qjv1eLHlfbbWSrpIqFzc3Bii1PVUucYFRJw5GwZNLDRvenZjVPlz" +
            "6xv+hvzf/Hz534cga3mnJ4cAMU1bGTqkOOSSAQgZA8QJXLcmosdAp8pStwY+YKMVqWivitEm1pixmnY6zd/3iTntryQwSGWa+TN0wV0RFRx3pN" +
            "KWNqCy6SwE5L4QAlqxytImnuS+zoJkqkoW2HASQDdZ5dizoEir/Q3BwmC9ODQzTT1Q5ptJBaJ2xDmORdK1UOOWd1eHJ7QjZjb0fJSZ0bC3jGWn" +
            "/G//v///////7/zv/Dv4ScfO0klN6LLETUGkuWNkzlcQ6Qjv1eLHlfbbWSrpIqFzc3Bii1PVUucYFRJw5GwZNLDRvenZjVPlz6xv+hvzf/Hz53" +
            "4dMQU1FMy45OC40AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/++IEAARHbnPUUy9l8u3Oeopl7L5fUd1I7Tx5y+" +
            "o7qR2njznAAAAAAE3EwLlIk8zX4mVHUzZe2UuuJCwWQDBIKK8oQ+AHpnGlAIjbPGYHDO2IBBkw0xG2m32swlEwYDa0hxiB6ep0kQEE6S6jcUpG" +
            "WcTdUGxQsTKcA/B6WI1o5yH7BVZCC/MLCWJ+7J6+ZkJLY37VzJKvxUOTjBZhmdUV92mdkaVcTpUxrJFsisKOUEmK7gnKmnDLCLag2pXGNNTLdN" +
            "8X/+////////v/P+GD+jRm90Pi0sB5VWX4qJiKxxwSTlklnh0VTdbE0tPlaG2tvI+HzLLR8uhzXWLus0fr3xWt89ZucRFAuLBbMBT23/8LAAAA" +
            "AAE3EwLlIk8zX4mVHUzZe2UuuJCwWQDBIKK8oQ+AHpnGlAIjbPGYHDO2IBBkw0xG2m32swlEwYDa0hxiB6ep0kQEE6S6jcUpGWcTdUGxQsTKcA" +
            "/B6WI1o5yH7BVZCC/MLCWJ+7J6+ZkJLY37VzJKvxUOTjBZhmdUV92mdkaVcTpUxrJFsisKOUEmK7gnKmnDLCLag2pXGNNTLdN8X/+////////v" +
            "/P+GD+jRm90Pi0sB5VWX4qJiKxxwSTlklnh0VTdbE0tPlaG2tvI+HzLLR8uhzXWLus0fr3xWt89ZucRFAuLBbMBT23/8IIAAop0hAutDaJbVoi" +
            "OgRoosWSlQarlP8xpkE7B68ZcWHIjXrgEiC4U59E9A0ADRguBRRMmCAgKGMnWbEWkKDKXJhLSUuYcyqGViqllsSG6apMUMLcvl7TqEpxBLk7Ft" +
            "l3KTJURl+0eIrmNTMqvVsDNVuKfBNb5Yl2gHJfYF0rj/dqF4hS6V0WErldZ69guLC9exNyXYGxvZVpmF2FzjsKGvYbalY+/v////////2/+/lV" +
            "suGPbAvF1GErojMoZ1wrW7UjCcNYzDRmi0VsVmq2ownRci7Kc5VCnj9JyaJ4p5RR12XJPartDhtquzezVVVqpCgqbmP2NxducIAAop0hAutDaJ" +
            "bVoiOgRoosWSlQarlP8xpkE7B68ZcWHIjXrgEiC4U59E9A0ADRguBRRMmCAgKGMnWbEWkKDKXJhLSUuYcyqGViqllsSG6apMUMLcvl7TqEpxBL" +
            "k7Ftl3KTJURl+0eIrmNTMqvVsDNVuKfBNb5Yl2gHJfYF0rj/dqF4hS6V0WErldZ69guLC9exNyXYGxvZVpmF2FzjsKGvYbalY+/v////////2/" +
            "+/lVsuGPbAvF1GErojMoZ1wrW7UjCcNYzDRmi0VsVmq2ownRci7Kc5VCnj9JyaJ4p5RR12XJPartDhtquzezVVVqpCgqbmP2NxdudMQU1FMy45" +
            "OC40AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//viBAAERveC0BtPFnDe8FoDaeLOHaWjL01h5cO0tGXprDy4ACTbeCpJvxGHBQlmCwphh6JK/wSE" +
            "DhSDJhBplA5moptAr0EAkxZYzJ4DJxKIbdEBiSmRgxSMSJ0MuS4TrOU6Cuv//mJ6T6r2axU9BVBOrRavnJgUiFWsp1QdykMparWAh0OOcsS1m0" +
            "/XB0pFXXSlcJoMGNCqaLBGVNtyT+WWJ6/qWI9gvbbQ2GoYqdG6eifNFQ7wplU+jbzr//////////4jK1uUUONVdC3ItwTzNCEdDpWI8VvTDnW1" +
            "sxWWC+jzxXjM9YGZCp4EF87iRbQmN+r2BOj6P3dLf83//36hSgBJtvBUk34jDgoSzBYUww9Elf4JCBwpBkwg0ygczUU2gV6CASYssZk8Bk4lEN" +
            "uiAxJTIwYpGJE6GXJcJ1nKdBXX//zE9J9V7NYqegqgnVotXzkwKRCrWU6oO5SGUtVrAQ6HHOWJazafrg6UirrpSuE0GDGhVNFgjKm25J/LLE9f" +
            "1LEewXttobDUMVOjdPRPmiod4UyqfRt51//////////8Rla3KKHGquhbkW4J5mhCOh0rEeK3phzra2YrLBfR54rxmesDMhU8CC+dxItoTG/V7A" +
            "nR9H7ulv+b//79QpQIAAUp5zzIwFUQDKNIxqDq7d1WFbKVAQ4xRlyAEsq0FIEHbDkP0giUHHAoyJZv4qR3lJQyYJNRPBDzFoq0QX4m48V2OcTW" +
            "QlRfC5k6H2SVzej0n2cBfnzrAuJeE6oEK3rvt4+YvxzdQw5jAHFWR6hKIXZOnKU3jLQ1PqBQto9RJ3ZdjvVLhLZdHOhC6coylgsEJdjwIcdZYY" +
            "puEjXDMnldVCmGJu39/r/H////9s6zCz7L7NLEYswmGjeeL+R+3sBLdtUWFFXZKnx2q46NmgKSowjvbSeDAQ9NHKqGSGbicKlhf9SAPQ0H50Fy" +
            "6CwEAAKU855kYCqIBlGkY1B1du6rCtlKgIcYoy5ACWVaCkCDthyH6QRKDjgUZEs38VI7ykoZMEmongh5i0VaIL8TceK7HOJrISovhcydD7JK5v" +
            "R6T7OAvz51gXEvCdUCFb1328fMX45uoYcxgDirI9QlELsnTlKbxloan1AoW0eok7sux3qlwlsujnQhdOUZSwWCEux4EOOssMU3CRrhmTyuqhTD" +
            "E3b+/1/j////+2dZhZ9l9mliMWYTDRvPF/I/b2Alu2qLCirslT47VcdGzQFJUYR3tpPBgIemjlVDJDNxOFSwv+pAHoaD86C5dBZMQU1FMy45OC" +
            "40AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/7" +
            "4gQADud5dkibmXly7y7JE3MvLl5h6RxOPTkLzD0jicenIQAAHGwOTgMVzFYGMHBYtKw2WK5e5rDNZ123sASA4AvwAAHBacQIQOqpNwECgjLJWm" +
            "NIL8exlBJWEQI+IaSHWJsIqdJO0uOgekzgrgbWlDHLaSwYx8G8Q4voS5QH4dwXxNgbKELhATP3OJAb4+Ew4KQXJSj9WWRgMVmSLK4olWM2bYQ1" +
            "OnCcxuP1erjyQ1FzeZS2YmSRgRDSaaG63EYzeHqeSTPMb+P////n//////7/rNaFLfD6R01RIFt4y2xryUlWI0F2opGxhZWBmd4U0diYk64M7R" +
            "SImU+sRvT/P+v7+FK8mz5aZi2343AsFXKMAAAONgcnAYrmKwMYOCxaVhssVy9zWGazrtvYAkBwBfgAAOC04gQgdVSbgIFBGWStMaQX49jKCSsI" +
            "gR8Q0kOsTYRU6SdpcdA9JnBXA2tKGOW0lgxj4N4hxfQlygPw7gvibA2UIXCAmfucSA3x8JhwUguSlH6ssjAYrMkWVxRKsZs2whqdOE5jcfq9XH" +
            "khqLm8ylsxMkjAiGk00N1uIxm8PU8kmeY38f////P//////3/Wa0KW+H0jpqiQLbxltjXkpKsRoLtRSNjCysDM7wpo7ExJ1wZ2ikRMp9Yjen+f" +
            "9f38KV5Nny0zFtvxuBYKuUYL060hDLhbMQCAwOLlBwEBwgIGJQ+sowAFlZ3YSJJQAYEASITkJ3LoRWJAeXzDgCgnEIMEIEQ2CAE4U/HVjo0PCq" +
            "Fh8GSpbCOawCqL8qkWUB+R5BioMEJQ4hGyFi3kYL+3G8rxBiWPWId5LhXCfl/Vu0m5VYjjOMsBynnEwfhWv2WZzSifYznwiHJ0y3L6cKGNyWVB" +
            "/tGor9qjLh/vL1OvXEGSciRjzMMgygz/Yef8//m/3nrN+fr453hSBdTkxELoYxxWVy9SWcspTESeXiq0QGLMyYbLobMEr0FxM5Ta7ENsrHJpKr" +
            "4q6pyVxNtZ0fJBY8OL060hDLhbMQCAwOLlBwEBwgIGJQ+sowAFlZ3YSJJQAYEASITkJ3LoRWJAeXzDgCgnEIMEIEQ2CAE4U/HVjo0PCqFh8GSp" +
            "bCOawCqL8qkWUB+R5BioMEJQ4hGyFi3kYL+3G8rxBiWPWId5LhXCfl/Vu0m5VYjjOMsBynnEwfhWv2WZzSifYznwiHJ0y3L6cKGNyWVB/tGor9" +
            "qjLh/vL1OvXEGSciRjzMMgygz/Yef8//m/3nrN+fr453hSBdTkxELoYxxWVy9SWcspTESeXiq0QGLMyYbLobMEr0FxM5Ta7ENsrHJpKr4q6pyV" +
            "xNtZ0fJBY8OTEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/++IEAAzGomjJm29M8tRNGTNt6Z5hGesYTmEzzC" +
            "M9YwnMJnkAAmaw1nFMjEFblB12sMRsTnhLWHLaWutpiSCSTEnjZ0thPRrC5GYv+54rqKLqYxgIA5Gs4FfIoEGmFpEHMgyxjFPccSNHOoCyYlWo" +
            "2nTSaKkTiqfsJyJaV+fyHt5yoc5qNUv51ydD5+pWRwQ5r7axttWh4q4SddjCLemVCsNrpdsEF+pNvGxwbXKCzQ0Y8GEAkMIZqlVITnOH3//+Wf" +
            "//+rv3cLuNzW861WOY3KHZS8UnLOSg48cOEZhC29YjcRyLwNxJTEQ1NuP4i0x0XmsMGoMResAAmaw1nFMjEFblB12sMRsTnhLWHLaWutpiSCST" +
            "EnjZ0thPRrC5GYv+54rqKLqYxgIA5Gs4FfIoEGmFpEHMgyxjFPccSNHOoCyYlWo2nTSaKkTiqfsJyJaV+fyHt5yoc5qNUv51ydD5+pWRwQ5r7a" +
            "xttWh4q4SddjCLemVCsNrpdsEF+pNvGxwbXKCzQ0Y8GEAkMIZqlVITnOH3//+Wf//+rv3cLuNzW861WOY3KHZS8UnLOSg48cOEZhC29YjcRyLw" +
            "NxJTEQ1NuP4i0x0XmsMGoMResABpM+8w0iezI5YMEhwwKJzHYqJhQ1kAhAwAHQcLQQHAABxCBhABQSCwgFlvQuCRoMDAAaIJCQCA9Y6QYIGCgE" +
            "h0sVkDw3jQ4LyVhbuXkTrRiQTJ3KqMoJSEIlmK+TkY41hAK7ReVd6DsInVcLCJesOXh6xYDjSRCHFejDV2MMZcyBM5Ukti7WpQ0x/WXO4+mVZn" +
            "T7v1GXellBDkRmGrPbRRSQw7BMrhy/Kr0gzm71yjpJdXmJimMJNym3LJZW5/9/3N/+Tz/1Lrx99mKpDFAibUg5qyuU5AmcY2d4sKhWREa6IjQo" +
            "2GXidG8uefLDLbaIyTUfOcsVaUUVuSSbqgUq7UVYlBhphFKYABpM+8w0iezI5YMEhwwKJzHYqJhQ1kAhAwAHQcLQQHAABxCBhABQSCwgFlvQuC" +
            "RoMDAAaIJCQCA9Y6QYIGCgEh0sVkDw3jQ4LyVhbuXkTrRiQTJ3KqMoJSEIlmK+TkY41hAK7ReVd6DsInVcLCJesOXh6xYDjSRCHFejDV2MMZcy" +
            "BM5Ukti7WpQ0x/WXO4+mVZnT7v1GXellBDkRmGrPbRRSQw7BMrhy/Kr0gzm71yjpJdXmJimMJNym3LJZW5/9/3N/+Tz/1Lrx99mKpDFAibUg5q" +
            "yuU5AmcY2d4sKhWREa6IjQo2GXidG8uefLDLbaIyTUfOcsVaUUVuSSbqgUq7UVYlBhphFKZMQU1FMy45OC40AAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//viBAAMxtFpShtvZPDaLSlDbeyeIOHtGk7h68wcPaNJ3D15AAUv2OuDSoAERQIAoOA2HsVWInGp" +
            "XCXfj7OVg4RCo+sFUeGNOrbfR4UOMqOcCrNXaCNs7op/IfGWzofsikMNtgsKynzDzPOu0a1qw949VtXJ9x0hZyK1iRqKZWJCFPhdIWnTNVzGM8" +
            "6EEXY+217cp0LSTM+ciLPBcMZyLuVRqFuVKZXOV1DYLsMWMeadLVXFm+wK3vvN9pk3mZgmZmZ396a1pzV4fcUVUUeof1iSLWrac+uxQqL7xZZO" +
            "zFu5dYNTkopzxaXFK8PVcYLxD7495IPKcKoU0nGLRrQAApfsdcGlQAIigQBQcBsPYqsRONSuEu/H2crBwiFR9YKo8MadW2+jwocZUc4FWau0Eb" +
            "Z3RT+Q+MtnQ/ZFIYbbBYVlPmHmeddo1rVh7x6rauT7jpCzkVrEjUUysSEKfC6QtOmarmMZ50IIux9tr25ToWkmZ85EWeC4YzkXcqjULcqUyucr" +
            "qGwXYYsY806WquLN9gVvfeb7TJvMzBMzMzv701rTmrw+4oqoo9Q/rEkWtW059dihUX3iyydmLdy6wanJRTni0uKV4eq4wXiH3x7yQeU4VQppOM" +
            "WjWgAC6OQR7MZhDMCABMAADMOAFFgQMDAjKoOmCwGmCADGCoFr4L5JiASC+1ExAsKDATgH8HuSOT7EAAgA0FAMvov8y5kiUpZNK+kLWNsXPLxs" +
            "cWytJYccC0tvOsJpFvCMJdiARZRSBxIovgtptj8AIUPIwXceLKUZ+krNJEkvQo84RhI81EWJsfi5OpUOJkrgmNC2F7XNHLMh1muh6pTa8aaFvT" +
            "ksxoe0wILLFetjAyQ4qJRbDGZrbiarq9Lb3//76///+Me39tfETFL0rf5xrM0SBCtR9EiP521Rr7DVavV9CfnEwt7ZuLDXDvqyKzRcWmpn71eW" +
            "24MLMPyT0z54lW6lraxJ6zQGAALo5BHsxmEMwIAEwAAMw4AUWBAwMCMqg6YLAaYIAMYKgWvgvkmIBIL7UTECwoMBOAfwe5I5PsQACADQUAy+i/" +
            "zLmSJSlk0r6QtY2xc8vGxxbK0lhxwLS286wmkW8Iwl2IBFlFIHEii+C2m2PwAhQ8jBdx4spRn6Ss0kSS9CjzhGEjzURYmx+Lk6lQ4mSuCY0LYX" +
            "tc0csyHWa6HqlNrxpoW9OSzGh7TAgssV62MDJDiolFsMZmtuJqur0tvf//vr///4x7f218RMUvSt/nGszRIEK1H0SI/nbVGvsNVq9X0J+cTC3t" +
            "m4sNcO+rIrNFxaamfvV5bbgwsw/JPTPniVbqWtrEnrNAYTEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/7" +
            "4gQADucPcUgbb03S4e4pA23pumEJ8RhOYevEIT4jCcw9eAAAXYz2JgZPTGQByxIsDgtI8OAy6C/hwBQjWkCAJSA4ERyMM8SccttpGrUrKsdnq1" +
            "y+bKkBD3Jlp9pJOA2gV5jxXApQYosrYCbTq6FgHgQsxTVaY6vdoIvrOnmA01CuKOTKS5vOcmB4Ie5l9bm2jI+LnEZW9ClzK/3FXl0n1wnJowsb" +
            "QXofSSkwuz/bICJU6PgqCKr3878eD9qzMO+HlEEZvur9183N///++rvHb5SWKSybkMfl65QmbWUciWXRpUPoWvQw5qiEgPTOmKbYHLb+KTrrGv" +
            "sG78MTiZunNpfxjagAALsZ7EwMnpjIA5YkWBwWkeHAZdBfw4AoRrSBAEpAcCI5GGeJOOW20jVqVlWOz1a5fNlSAh7ky0+0knAbQK8x4rgUoMUW" +
            "VsBNp1dCwDwIWYpqtMdXu0EX1nTzAaahXFHJlJc3nOTA8EPcy+tzbRkfFziMrehS5lf7iry6T64Tk0YWNoL0PpJSYXZ/tkBEqdHwVBFV7+d+PB" +
            "+1ZmHfDyiCM33V+6+bm////fV3jt8pLFJZNyGPy9coTNrKORLLo0qH0LXoYc1RCQHpnTFNsDlt/FJ11jX2Dd+GJxM3Tm0v4xtQpTNGBMjHUxGC" +
            "zFI5AowCgXdAAAMOGJgQOAAJAYOA9AFUaSmk4JOpsZRo/n8gJ8LUT5CBjSgUQSAoMMiQ1TESPnmDOS19cbWA4aK41hMtNNENIlwlrLivpXImNd" +
            "Z0DnEKEhLuXoylScgIYggOhkKIFUJyQoQwKAQwXAgpDhCTFgDYLgRyghK1SrZ5K0fKEsR0H7lfYXppGWhZji6HOX9kX1fGYas7xiWXrYwNrHCb" +
            "FSpbx6x3964xSmP///j///f/x8VrXXzT7ebjx9wo0N1AkZo+fGboLfAcJ4a4jQ1bM/7Ixx/SM7jRds0dWajXZI8KA7jQGOkLL+jFiuJMX8CJD9" +
            "MvaYd6xLHmKUzRgTIx1MRgsxSOQKMAoF3QAADDhiYEDgACQGDgPQBVGkppOCTqbGUaP5/ICfC1E+QgY0oFEEgKDDIkNUxEj55gzktfXG1gOGiu" +
            "NYTLTTRDSJcJay4r6VyJjXWdA5xChIS7l6MpUnICGIIDoZCiBVCckKEMCgEMFwIKQ4QkxYA2C4EcoIStUq2eStHyhLEdB+5X2F6aRloWY4uhzl" +
            "/ZF9XxmGrO8Yll62MDaxwmxUqW8esd/euMUpj///4///3/8fFa1180+3m48fcKNDdQJGaPnxm6C3wHCeGuI0NWzP+yMcf0jO40XbNHVmo12SPC" +
            "gO40BjpCy/oxYriTF/AiQ/TL2mHesSx5kxBTUUzLjk4LjQAAAAAAAAAAAAAAAAAAAAAAAAD/++IEAAzG7GjKG3h4YN2NGUNvDwwfFdkgbmHly+" +
            "K7JA3MPLkAFu/U4OEMpIDXEvKmykC2OWP03Z5mHqDTsNPqSCeESo+kcQgi1eAgk+H6Zczarj7tuMWh4KE/kWiS5HYoWRdKMxHy5XmRMK26kXaI" +
            "QBBC3ofEfKpvP8/CRFwHwiFQXA0C8qAsarP2KwHWS8hB6IxbcLsSUeOlqCkGNAqQ/jwLaqlWqWJIIqucr8WeAxqZlFefplOQVapnaUtXD2+df3" +
            "+/vOc/4/+b/Nc+2HdJH0G7bBOzbqbNcRfuVsnsz4hKePIxRnq9ejO9Y4tLVZmRdJWWsNhcB42EFuQTFtzE3oABbv1ODhDKSA1xLypspAtjlj9N" +
            "2eZh6g07DT6kgnhEqPpHEIItXgIJPh+mXM2q4+7bjFoeChP5FokuR2KFkXSjMR8uV5kTCtupF2iEAQQt6HxHyqbz/PwkRcB8IhUFwNAvKgLGqz" +
            "9isB1kvIQeiMW3C7ElHjpagpBjQKkP48C2qpVqliSCKrnK/FngMamZRXn6ZTkFWqZ2lLVw9vnX9/v7znP+P/m/zXPth3SR9Bu2wTs26mzXEX7l" +
            "bJ7M+ISnjyMUZ6vXozvWOLS1WZkXSVlrDYXAeNhBbkExbcxN6AAAFIzqQ5MDCYwwNDI4DVgFRCOqBlQ7qR6YIshlJKFFpiwyEWKXoYQNDOTCOo" +
            "UEIxCNwAAEWZzKFbGKUxW5wG0nS8ihFrJSUJojWLumBRl1Q4pRcxws5ti6C4kaOIKhzaAjZRuTyOkRcXjAz1ZHhBktLEenGPtqhlgUqVQ5GNdV" +
            "Mr26rku9KtEsS0zKddN0JdPqwvWJqMwsiyjUYZxbG+I8Yp85ntJjeLa38/0///39/29Pl62UYFbDbIUFPvVH8Se9svPBY2Z+/Xe2NkamVWP8MK" +
            "ggwYbgqz9Vjgfy5cVXbcusyw3CbWZsWgZixs3iwM01WGaBiwzQAAAApGdSHJgYTGGBoZHAasAqIR1QMqHdSPTBFkMpJQotMWGQixS9DCBoZyYR" +
            "1CghGIRuAAAizOZQrYxSmK3OA2k6XkUItZKShNEaxd0wKMuqHFKLmOFnNsXQXEjRxBUObQEbKNyeR0iLi8YGerI8IMlpYj04x9tUMsClSqHIxr" +
            "qple3Vcl3pVoliWmZTrpuhLp9WF6xNRmFkWUajDOLY3xHjFPnM9pMbxbW/n+n//+/v+3p8vWyjArYbZCgp96o/iT3tl54LGzP3672xsjUyqx/h" +
            "hUEGDDcFWfqscD+XLiq7bl1mWG4TazNi0DMWNm8WBmmqwzQMWGaAExBTUUzLjk4LjQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//viBAAIht1uyrtvY3DbrdlXbexuIK3hHm3h5cwVvCPNvDy5AgACHPsdiglzRIQRJQOUxhplKqjp" +
            "qUPq/DCnKUI7J0MCqDLajaGUYI/SeH6A4To5oeqVaXkYaapOGU7UCvtBpSmM3RmFgJ01tqFHSW90QZMvmUc7O6U4hnGkMHyOR/UE9XA2W2j0bg" +
            "FQgPiEPKpGCJ4fQqRp8RSoqIRa8rJjM46F60DOjgdOLh+GJSiFY6rPUZWLPc/rT+zM36ZmZmfmey07HS7S0umjhZqerz6JD3eXplx7ESGHTpZZ" +
            "WODa4u6IRXRLAfWJXhWPp8XlA/z2xzzbmzBzRUcSogJEcBAAEOfY7FBLmiQgiSgcpjDTKVVHTUofV+GFOUoR2ToYFUGW1G0MowR+k8P0BwnRzQ" +
            "9Uq0vIw01ScMp2oFfaDSlMZujMLATprbUKOkt7ogyZfMo52d0pxDONIYPkcj+oJ6uBsttHo3AKhAfEIeVSMETw+hUjT4ilRUQi15WTGZx0L1oG" +
            "dHA6cXD8MSlEKx1WeoysWe5/Wn9mZv0zMzM/M9lp2Ol2lpdNHCzU9Xn0SHu8vTLj2IkMOnSyyscG1xd0QiuiWA+sSvCsfT4vKB/ntjnm3NmDmi" +
            "o4lRASI4AABVo+S8FRkxgrMHATAhgOVAKcJeEJBgxHJQESGhlBRUuj6FiIvoAD2V9R1QGENCBTASAh0Dyt+wUJx0ZpSgPB4D2E+C1BIkIBhgNx" +
            "MG4mRchJA5ANUH0DhKoOcEbJI6LuSs1WsWpdtx+NwMsYiBUYG0N5fPYfAe5lBFciQ3SSjVcikJuh6VQR4oUnFA9ZYyoJadcBDUaqWheVEk+GqF" +
            "DRSUVq4O00TVjD7YHJkhNOosO0WNi98f4zrf/3/n++v9Rb2y3TYs4MMZvT7RW+KUxCdxvSWO8ft8CS0BWKhC3FV4esS48q82KdiW48bKrxa80L" +
            "bBqFAgRom59eeNXEDMCHDvtIByAAAKtHyXgqMmMFZg4CYEMByoBThLwhIMGI5KAiQ0MoKKl0fQsRF9AAeyvqOqAwhoQKYCQEOgeVv2ChOOjNKU" +
            "B4PAewnwWoJEhAMMBuJg3EyLkJIHIBqg+gcJVBzgjZJHRdyVmq1i1LtuPxuBljEQKjA2hvL57D4D3MoIrkSG6SUarkUhN0PSqCPFCk4oHrLGVB" +
            "LTrgIajVS0Lyoknw1QoaKSitXB2miasYfbA5MkJp1Fh2ixsXvj/Gdb/+/8/31/qLe2W6bFnBhjN6faK3xSmITuN6Sx3j9vgSWgKxUIW4qvD1iX" +
            "HlXmxTsS3HjZVeLXmhbYNQoECNE3Przxq4gZgQ4d9pAORMQU1FMy45OC40AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/7" +
            "4AQADuchbcobb2Vy5C25Q23srmC12RxuYe3MFrsjjcw9uQAAZ7TIokxkKFh4AAqCFr0uU8ig0yWP+sJFmXsTcNKiOMybZOOPiEAdV02Kq5bxdc" +
            "RJBCMxViyJRuQxmXx8pgY4vUebylFyLJiUJYDoTygPQ62ZCTKU90e6VqhinqaB75ugzQS1mJXSLuKznKpnqfWHjpkUj6rQnYzif5GiVtBYncFW" +
            "RV5CIaxpURle1MSmjqYYs53trLAVDm/h1pn4pj53j+3TMzMzM7tp7G3/9rtJ/MnC1ZVt92NK8urSrp4IwpjZPzwe1qJEdaMyibFkticS3Cee7O" +
            "ZXHPxvxb/ME00W7dm2ioAAM9pkUSYyFCw8AAVBC16XKeRQaZLH/WEizL2JuGlRHGZNsnHHxCAOq6bFVct4uuIkghGYqxZEo3IYzL4+UwMcXqPN" +
            "5Si5FkxKEsB0J5QHodbMhJlKe6PdK1QxT1NA983QZoJazErpF3FZzlUz1PrDx0yKR9VoTsZxP8jRK2gsTuCrIq8hENY0qIyvamJTR1MMWc721l" +
            "gKhzfw60z8Ux87x/bpmZmZmd209jb/+12k/mThasq2+7GleXVpV08EYUxsn54Pa1EiOtGZRNiyWxOJbhPPdnMrjn434t/mCaaLduzbRUGJnc4M" +
            "YXDRmgBGWRIYcFYkDDC4+MFgUChcRgkMDMPylM2TAkwwk2NCHhYKKgzwGTM3zK0EMADk/lB3GSdb154EUIjDMl/MAU0HALkRhZWglRlh5ZkYWD" +
            "ZMuZk8bTNVtEmMPS3VG02KxlX002rEkwkQyhNhxjnE1EhaWgtp3MpQuDEijDL2ox2HUbyrV6tbVXRsgmshTOkmRKMDyDNRqxfDlm8yoZC5j+PI" +
            "1Xjkxq5ye/H8l73i/eseX///61rHz7bzmDmdziRJY18PXLPewYkGEwt9p4bEn4bIr5GdOw286TgUZupJggMa4sX+Z68vSBWBWs9Y9+/rhk89N3" +
            "ncaP4KgIkHATAMTO5wYwuGjNACMsiQw4KxIGGFx8YLAoFC4jBIYGYflKZsmBJhhJsaEPCwUVBngMmZvmVoIYAHJ/KDuMk63rzwIoRGGZL+YApo" +
            "OAXIjCytBKjLDyzIwsGyZczJ42maraJMYeluqNpsVjKvpptWJJhIhlCbDjHOJqJC0tBbTuZShcGJFGGXtRjsOo3lWr1a2qujZBNZCmdJMiUYHk" +
            "GajVi+HLN5lQyFzH8eRqvHJjVzk9+P5L3vF+9Y8v///1rWPn23nMHM7nEiSxr4euWe9gxIMJhb7Tw2JPw2RXyM6dht50nAozdSTBAY1xYv8z15" +
            "ekCsCtZ6x79/XDJ56bvO40fwVARIOAmExBTUUzLjk4LjQAAAAAAAAAAAAAAAAAAAAAAAAP/74gQADMcdbkmbb03y463JM23pvl393SJtvZHLv7" +
            "ukTbeyOQAC57RPMBooluBhALiiRLHwADMjT4cZUzRV5wfJ5C8LBXbiiZqPAkBuSWAIQBLiNjfmXMoiLhutKbSPKmYnbgrT9LiyTMatQ05znQw3" +
            "jpeQVWdbYeb4uqfVySbzKakQzWgvUZMfq2lVSc5Jmso1gnZ/vmQ0kJOhC0LUSuc8PC/KpXQEPkSzfLZhZ4zJWE5aOJNJ4c5kHdAfvo2Myx9va0" +
            "3E+NY/p//9Z/x/r71rwPLFbssz81GGCk1mOnGESVGIWRZxZTUMT4N6IiZBAWWNAaceDIcBPwvv37SXR4RblO9l5Z390AAXPaJ5gNFEtwMIBcUS" +
            "JY+AAZkafDjKmaKvOD5PIXhYK7cUTNR4EgNySwBCAJcRsb8y5lERcN1pTaR5UzE7cFafpcWSZjVqGnOc6GG8dLyCqzrbDzfF1T6uSTeZTUiGa0" +
            "F6jJj9W0qqTnJM1lGsE7P98yGkhJ0IWhaiVznh4X5VK6Ah8iWb5bMLPGZKwnLRxJpPDnMg7oD99GxmWPt7Wm4nxrH9P//rP+P9feteB5Yrdlmf" +
            "mowwUmsx04wiSoxCyLOLKahifBvRETIICyxoDTjwZDgJ+F9+/aS6PCLcp3svLO/ugAAprD65FOgKHQQdA4RIBBExMdLVNpcDXGqRFY6o2bRVWO" +
            "Bk9EzDKDdWBoBhE7BcCq0vE7RozjRP4SQmZZosvx5q6Gfg53JCyEhUmkKWTNKj6L+y7LgpCVDLNM4WdSPE6ME4GxwNwkyieOD9TF/HWMVUFsyz" +
            "HWZMIm5P1aP45lPlHj4bnpDJpuNx/ChafDymM4pLo+LzlAgITI8D5VPRltu3Ug797s2uX6Z2ZmvbSZtbKUq+3ElTQOLIVzM86Vm6Lzpxp4t7iy" +
            "5oqL7AoH4gxLHiKWh/MikZiSLS9X/6r2fatcyv1dz7QW2b7zUa1Of0AABTWH1yKdAUOgg6BwiQCCJiY6WqbS4GuNUiKx1Rs2iqscDJ6JmGUG6s" +
            "DQDCJ2C4FVpeJ2jRnGifwkhMyzRZfjzV0M/BzuSFkJCpNIUsmaVH0X9l2XBSEqGWaZws6keJ0YJwNjgbhJlE8cH6mL+OsYqoLZlmOsyYRNyfq0" +
            "fxzKfKPHw3PSGTTcbj+FC0+HlMZxSXR8XnKBAQmR4HyqejLbdupB373Ztcv0zszNe2kza2UpV9uJKmgcWQrmZ50rN0XnTjTxb3FlzRUX2BQPxB" +
            "iWPEUtD+ZFIzEkWl6v/1Xs+1a5lfq7n2gts33mo1qc/oTEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/++IEAAzHDGjJG29j0uGNGSNt7HpgTfEYTmFxxAm+IwnMLjgAAufUxqaBQSoCAQpI1NArBnPblD7S" +
            "obTDetPhdSukcxb0YpD5QwVAEI9wfxDSTt6jSx3lUp5CEqtTsRbBwrKFQiaPDjMlPLaPOknDeqLxNKDiClEmx0ZhaYHR6QxeKyOuPTgMj89Qwq" +
            "Jw0nAmCERSSrAgOQ9l8xHIc2AcLY8oQJCQtVpxKsrQFcK/zspF2MAEaFiuJgzL5ytSevnv6c76/jvzMzeZ+d6urrYcqMdPSW0S1mnS3Pvi6r0S" +
            "HZc8YHT5cVkpOuPipYvBU5j/YeFk5PtLykAABQ8fW/RP/ye15z8wAC59TGpoFBKgIBCkjU0CsGc9uUPtKhtMN60+F1K6RzFvRikPlDBUAQj3B/" +
            "ENJO3qNLHeVSnkISq1OxFsHCsoVCJo8OMyU8to86ScN6ovE0oOIKUSbHRmFpgdHpDF4rI649OAyPz1DConDScCYIRFJKsCA5D2XzEchzYBwtjy" +
            "hAkJC1WnEqytAVwr/OykXYwARoWK4mDMvnK1J6+e/pzvr+O/MzN5n53q6uthyox09JbRLWadLc++LqvRIdlzxgdPlxWSk64+Kli8FTmP9h4WTk" +
            "+0vKQAAFDx9b9E//J7XnPzAAdTq05MVlwwILygGgIVmAAKHAgABMwkKDAwILKJylgAJ7tsjeh8WACYNDEMILFszocFpBu1yhAkaAhb7MkYGnAk" +
            "Uh6tyMuClAk0vdIpsMOswLkrUepB2TIE1lJbEBVIotPTF1VFTl/kzExRAFH5JKHVNlTNOaNAT8sdcFdrDYktFcqfS6meLpdt1K882GGmPwLCXx" +
            "0/16IAPkElxtKyBIh6DAuNTA+yiWgrYnBKBKNzWR22dPzdWxBju/+P//3cR/b/2tQabJHt0PPJ0lwVUTVUEDA6pnCUgoVjQbHywpcTD5wkHCPl" +
            "BPe08eSNpYaF+UJm6bE3HVUDxitdnp3IrvfSIADqdWnJisuGBBeUA0BCswABQ4EAAJmEhQYGBBZROUsABPdtkb0PiwATBoYhhBYtmdDgtIN2uU" +
            "IEjQELfZkjA04EikPVuRlwUoEml7pFNhh1mBclaj1IOyZAmspLYgKpFFp6Yuqoqcv8mYmKIAo/JJQ6psqZpzRoCfljrgrtYbElorlT6XUzxdLt" +
            "upXnmww0x+BYS+On+vRAB8gkuNpWQJEPQYFxqYH2US0FbE4JQJRuayO2zp+bq2IMd3/x//+7iP7f+1qDTZI9uh55OkuCqiaqggYHVM4SkFCsaD" +
            "Y+WFLiYfOEg4R8oJ72njyRtLDQvyhM3TYm46qgeMVrs9O5Fd76RTEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//vi" +
            "BAAMx3R2R5uMTyLujsjzcYnkXUHDIm49j8uoOGRNx7H5AAAUjO1sAGjgw4DIqAAKYcAAyCBYBgYLCQOYmLAYtunmvVAmpqW5S/SfZ2mEJAhdz2" +
            "tFGQERARYdb6ei8VytFZY5TzMopnmUWUtWATKtRx0ICp4Qrcw5mK+FqvYpJ3bUBKJQHBj5QlaDfhKDAnpzxIjK4oEYewMFYgOn4bKdOhShEpQq" +
            "MSzBd2JxsWj4ORFZ48ZWsRvKlCGvLjvpIQUQzejRLJyte7uPz/57v///3PL86n8Zi+aDFmiYtCdOrFrRyY02gaRuR6qdIkRUlA0QCIB9RmxODJ" +
            "Y0LtMwSjf+RfNVaq1VSodhqa0UQxxEgWAAAUjO1sAGjgw4DIqAAKYcAAyCBYBgYLCQOYmLAYtunmvVAmpqW5S/SfZ2mEJAhdz2tFGQERARYdb6" +
            "ei8VytFZY5TzMopnmUWUtWATKtRx0ICp4Qrcw5mK+FqvYpJ3bUBKJQHBj5QlaDfhKDAnpzxIjK4oEYewMFYgOn4bKdOhShEpQqMSzBd2JxsWj4" +
            "ORFZ48ZWsRvKlCGvLjvpIQUQzejRLJyte7uPz/57v///3PL86n8Zi+aDFmiYtCdOrFrRyY02gaRuR6qdIkRUlA0QCIB9RmxODJY0LtMwSjf+Rf" +
            "NVaq1VSodhqa0UQxxEgWAAKn1OKEgwACULBIBF5QUAVFUE70JWMfZS+xfFk7C2vrHC4JsAIQXx/FgQshJYx5tR2FElIY/lZCJiXMlSaNBcG4Uo" +
            "misGIzkFP9RHO0H7RLFxXK5RqhfqJgNXSjOoj0epSFl/ZlObKdT8hnMg8qxKHo2QQSLulpMZnJ+IzYcFQAQHh8RCQhDqZj9ywq/Api4vwrk5UP" +
            "QVAGSkgntvtnXW2k3+1ty9PntmZpLU5+fSzCaSoZreQ2js5Ro0NYhXcqcRNstpUM1SEtCMTDF5jZxSPaFkK80WRh3We/p9tzJ3JmXLVOSXx8NW" +
            "bUAAKn1OKEgwACULBIBF5QUAVFUE70JWMfZS+xfFk7C2vrHC4JsAIQXx/FgQshJYx5tR2FElIY/lZCJiXMlSaNBcG4UomisGIzkFP9RHO0H7RL" +
            "FxXK5RqhfqJgNXSjOoj0epSFl/ZlObKdT8hnMg8qxKHo2QQSLulpMZnJ+IzYcFQAQHh8RCQhDqZj9ywq/Api4vwrk5UPQVAGSkgntvtnXW2k3+" +
            "1ty9PntmZpLU5+fSzCaSoZreQ2js5Ro0NYhXcqcRNstpUM1SEtCMTDF5jZxSPaFkK80WRh3We/p9tzJ3JmXLVOSXx8NWbUTEFNRTMuOTguNAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/74gQADudAcMebjE+S6A4Y83GJ8l/R7RhOYY/L+j" +
            "2jCcwx+QAAXYzbMlMAkAx+FzBQLMFAhJMweDGHBgDWgBQIs1EpOXB+ltlmoqW6Xeuguo3he1Ddbqe6GLZ1VUtndZsok4iDTgLvgN5WgKPq6fuS" +
            "wBOM7daULUd+hXWvxwX6p3VjUaoV8ymG4IkNaNNqtVmTdpTempBL77nILpyPTZVLQoEPjseHEpnjhsnFIzICZWZngtWEVK0WT6EtK2DsCigYIF" +
            "xLRC+/K5M5vjee/uVf/2s/q4VJRqbblu5AvK+R42gncTSyauEg6XJSpebzWjRiJCaQNK97aQl+NVLwlKflsr6vLIiwqfllBUcAAF2M2zJTAJAM" +
            "fhcwUCzBQISTMHgxhwYA1oAUCLNRKTlwfpbZZqKlul3roLqN4XtQ3W6nuhi2dVVLZ3WbKJOIg04C74DeVoCj6un7ksATjO3WlC1HfoV1r8cF+q" +
            "d1Y1GqFfMphuCJDWjTarVZk3aU3pqQS++5yC6cj02VS0KBD47HhxKZ44bJxSMyAmVmZ4LVhFStFk+hLStg7AooGCBcS0QvvyuTOb43nv7lX/9r" +
            "P6uFSUam25buQLyvkeNoJ3E0smrhIOlyUqXm81o0YiQmkDSve2kJfjVS8JSn5bK+ryyIsKn5ZQVHHo0L5gcgzJweBgXMYhYxwEAuBB0ECAaDgS" +
            "QkoYK0ioSeEkAwBBDRC/BF1phN5kZe5B9BgRjDAKYqAg5DPkcH3XsulQRroQBJcqAFQMKIgv0mGkCukMKr6TyxJpa8qiaGjL2vKtZmxNtV4sKk" +
            "zbQ4xJQBxqGXPqsRrMYeGSPK/RKHMXjUSTMJH4i+QaQlUOlxeEtkTFI+lA3JC44OmfOIjhdZh9xee3KdEJjlsUxZS1IummzM1vM5szM/W+ZT/y" +
            "cyFmVjTpwsomdYohucpLyhGy2dXhOFA0RsrMWvHInHg5tLENJAL7Q01g5dqtv14rJLx0vY8bRu5v3riPb9L1GB6NC+YHIMycHgYFzGIWMcBALg" +
            "QdBAgGg4EkJKGCtIqEnhJAMAQQ0QvwRdaYTeZGXuQfQYEYwwCmKgIOQz5HB917LpUEa6EASXKgBUDCiIL9JhpArpDCq+k8sSaWvKomhoy9ryrW" +
            "ZsTbVeLCpM20OMSUAcahlz6rEazGHhkjyv0ShzF41EkzCR+IvkGkJVDpcXhLZExSPpQNyQuODpnziI4XWYfcXntynRCY5bFMWUtSLppszNbzOb" +
            "MzP1vmU/8nMhZlY06cLKJnWKIbnKS8oRstnV4ThQNEbKzFrxyJx4ObSxDSQC+0NNYOXarb9eKyS8dL2PG0bub964j2/S9RhMQU1FMy45OC40AA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/++IEAA7nCWjIm29lMuEtGRNt7KZfXe0cbj05C+u9o43HpyEAAGa0/ZpGrkAA5KJI3goEYWLA0FqV" +
            "LVShLYNeQTMhTFR/aMvR+5ttH/UWWGVyjmXWjC/kUnzKFlLkRSvHqL2MBUpQyXJOiTQiBLvqtSKaKtjALoT0ljY1ncwKVKUYsUW1BUfKdYsKR8" +
            "YD6jcEAdh8DxDadcbmM14mk5KIRcOgMD2DVeJ5NFxy+6VaDqPaY9It0l7pFK9c8e1WZaub9P6czOmZmdrPTnbFOunlMao8zQ9P3EqeK7hPWsr1" +
            "th9OTxtII56jURry0ujFYjmZifPFJookl/k2kP8JL8BBHydsJ6yAADNafs0jVyAAclEkbwUCMLFgaC1KlqpQlsGvIJmQpio/tGXo/c22j/qLLD" +
            "K5RzLrRhfyKT5lCylyIpXj1F7GAqUoZLknRJoRAl31WpFNFWxgF0J6SxsazuYFKlKMWKLagqPlOsWFI+MB9RuCAOw+B4htOuNzGa8TSclEIuHQ" +
            "GB7BqvE8mi45fdKtB1HtMekW6S90ileueParMtXN+n9OZnTMzO1npztinXTymNUeZoen7iVPFdwnrWV62w+nJ42kEc9RqI15aXRisRzMxPnik0" +
            "USS/ybSH+El+Agj5O2E9ZLsZ+0yCEVF5zCQgAIYMEgcHAkWBqPIcFjAYDCAODQEYEABahFJOFhhKBCgNJlWkdm2jbOlMEZk+EkEP0d01HWZSwt" +
            "p60XJcdBqYaKhu7hDxukzP9HKAdKLLYhJgm6Q0eDeLuegrUW4rKVgSNQuBcS5EodaiLzdAcyNrBTxzsM5VI5TIZHP6A2v2lXHuhBP0tEJqxsSq" +
            "b3N89UiPS6kbHThFmVHbPA6HFEkKBAyuvGUvPIb04/3cfWXlr/U6RSJZZsHHTpphA4uTdTJ0imRjgYMk8BQOOTXkXeMMAKJUBkOCIhKzggztpE" +
            "grfD2jn8i0nc1WbnWVDLufha8xdjP2mQQiovOYSEABDBgkDg4EiwNR5DgsYDAYQBwaAjAgALUIpJwsMJQIUBpMq0js20bZ0pgjMnwkgh+jumo6" +
            "zKWFtPWi5LjoNTDRUN3cIeN0mZ/o5QDpRZbEJME3SGjwbxdz0Fai3FZSsCRqFwLiXIlDrUReboDmRtYKeOdhnKpHKZDI5/QG1+0q490IJ+lohN" +
            "WNiVTe5vnqkR6XUjY6cIsyo7Z4HQ4okhQIGV14yl55DenH+7j6y8tf6nSKRLLNg46dNMIHFybqZOkUyMcDBkngKBxya8i7xhgBRKgMhwREJWcE" +
            "GdtIkFb4e0c/kWk7mqzc6yoZdz8LXmTEFNRTMuOTguNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//vi" +
            "BAAGZwVoyRtvZPDgrRkjbeyeG92lIu29jYt7tKRdt7GxAACv+PsNDFjcFHCZJEErLT1d5iylqdTIy2tkqgOKmbXmqNajLTp10lHGCGrBEegsRG" +
            "VBFQ4RzSjlalQhyFDPb1EW5VHmaavQ1rXaKQtJOBY3NFLiZZyfx1KNzZT+IMW1UsUNXp1U7khIxGt5P2xZT6pdzq5+r1PBUKdfiRl9OlSOozHI" +
            "SRW4eJaLtCbNTYMDVoq2WokpniDhlGzmzb8np57cnJ6dYtswqWkV1a9iGbNsucnNEKJGYQoKbkJo8WCiFDVoJca85LFlq41LYkC2Afi4SExUIB" +
            "O6NJvNsE4r0il4AAV/x9hoYsbgo4TJIglZaervMWUtTqZGW1slUBxUza81RrUZadOuko4wQ1YIj0FiIyoIqHCOaUcrUqEOQoZ7eoi3Ko8zTV6G" +
            "ta7RSFpJwLG5opcTLOT+OpRubKfxBi2qlihq9OqnckJGI1vJ+2LKfVLudXP1ep4KhTr8SMvp0qR1GY5CSK3DxLRdoTZqbBgatFWy1ElM8QcMo2" +
            "c2bfk9PPbk5PTrFtmFS0iurXsQzZtlzk5ohRIzCFBTchNHiwUQoatBLjXnJYstXGpbEgWwD8XCQmKhAJ3RpN5tgnFekUvQU1pv+uYqaO0moEBw" +
            "cKBYAAoSwgGgDpt0QhnrA7hOBbBJwPBtmwLw9DMCYYDbcFIKNXIwsR3CDoejmAcRyPi/IXVGkMPY4CfkuY2dRIWWzMtjkDcgChYsXAkXxIUKSB" +
            "SAiITBIViK7RgICII46lczK5mYna8kjuVDUeBOeHsdVpMBq4cmYzPSqxUhuQMuFxARFxFCPC88cdmWu93r9kzPf0zN5zv+b5mWzmUJzWKlNx1u" +
            "8DPwNyLYj9UV5ouQkmpoxQkF9UZbTlsrmYJFg/OVDCF6IQ/mZWXrEYRJbi+9oKa03/XMVNHaTUCA4OFAsAAUJYQDQB026IQz1gdwnAtgk4Hg2z" +
            "YF4ehmBMMBtuCkFGrkYWI7hB0PRzAOI5HxfkLqjSGHscBPyXMbOokLLZmWxyBuQBQsWLgSL4kKFJApAREJgkKxFdowEBEEcdSuZlczMTteSR3K" +
            "hqPAnPD2Oq0mA1cOTMZnpVYqQ3IGXC4gIi4ihHheeOOzLXe71+yZnv6Zm853/N8zLZzKE5rFSm463eBn4G5FsR+qK80XISTU0YoSC+qMtpy2Vz" +
            "MEiwfnKhhC9EIfzMrL1iMIktxfe0xBTUUzLjk4LjQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/74gQADMb6aMkbb2Ty300ZI23snmCh3RpuYY/MFD" +
            "ujTcwx+QAUt9TWZgKD5iYGPEYGLS7KD6/mLuSiu8DRWHPm97CpcqKcSSf5YBuiwtUWI/iWqJIoch5+mczLJ+oaiVQoH5IGlEGemzuUrW2rmKcp" +
            "2q0fp2HEh6je0aYDkQJMLawqEQvLtNGKwvIdGJTq9EpQ3nzSotu407kqWtbFoYw0Gc5D8PJDjKXmsxUKUatWmE5nBDcA2NBYtHFhK6ih12Ol7X" +
            "v+ZszuzOzM/M3+K1O49uxK2Fk91D88u1G6xZjNtVfapkeYMENDuR8PzfFTxeJTCIsliB39E/Q3/cW42jlrKXqABS31NZmAoPmJgY8RgYtLsoPr" +
            "+Yu5KK7wNFYc+b3sKlyopxJJ/lgG6LC1RYj+JaokihyHn6ZzMsn6hqJVCgfkgaUQZ6bO5StbauYpynarR+nYcSHqN7RpgORAkwtrCoRC8u00Yr" +
            "C8h0YlOr0SlDefNKi27jTuSpa1sWhjDQZzkPw8kOMpeazFQpRq1aYTmcENwDY0Fi0cWErqKHXY6Xte/5mzO7M7Mz8zf4rU7j27ErYWT3UPzy7U" +
            "brFmM21V9qmR5gwQ0O5Hw/N8VPF4lMIiyWIHf0T9Df9xbjaOWspeoAAAxs+grzGAFMuCIwaEgcKHBkCGT8jwVCCACgYIgIIAAMg4GAQwGDlAE5" +
            "yt7an0YwEcEGcTBYMDCLfLrl4GjJEQ+Clo7LyTfRMQSCAKJ69lwphMuGh/cRRRpWMjCj6l63doTO0622T0eJkSSlekehzl/uI/yrU/Iy7DM3ph" +
            "GGa9B1H0GBVCcnl1WOwhCgxHoSuke1pNQiqTx+JhYN4lI53WRLD+E81oODQ2CeApxuHp4picoxk9M9OZBk/MzPzTMm7+bXG2inC25BG0oZOuQq" +
            "EMtrymR4zSElrG2KmxTUEI7SDwIA8VUxNj8u8ZGlGIluIc0RethfvDB+NU3ZjXv/SOay0RkAAAxs+grzGAFMuCIwaEgcKHBkCGT8jwVCCACgYI" +
            "gIIAAMg4GAQwGDlAE5yt7an0YwEcEGcTBYMDCLfLrl4GjJEQ+Clo7LyTfRMQSCAKJ69lwphMuGh/cRRRpWMjCj6l63doTO0622T0eJkSSlekeh" +
            "zl/uI/yrU/Iy7DM3phGGa9B1H0GBVCcnl1WOwhCgxHoSuke1pNQiqTx+JhYN4lI53WRLD+E81oODQ2CeApxuHp4picoxk9M9OZBk/MzPzTMm7+" +
            "bXG2inC25BG0oZOuQqEMtrymR4zSElrG2KmxTUEI7SDwIA8VUxNj8u8ZGlGIluIc0RethfvDB+NU3ZjXv/SOay0RlMQU1FMy45OC40AAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    }
}