using System;
using System.IO;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebugDuck
{
    /// <summary>
    /// 純程式合成的鴨子「嘎」聲：鋸齒波 + 指數下滑音高 + 低頻 AM 顫動 + 包絡，
    /// 產生 16-bit PCM WAV 到記憶體，再用 System.Media.SoundPlayer 播放。零外部相依。
    /// </summary>
    public static class DuckVoice
    {
        /// <summary>設 false 可整個關掉叫聲。</summary>
        public static bool Enabled = true;

        private const int SampleRate = 22050;

        private static readonly object _gate = new object();
        private static readonly Random _rng = new Random();

        /// <summary>依聊天回覆字串發出對應叫聲。</summary>
        public static void Speak(string line)
        {
            switch (line)
            {
                case "嘎":     Quack(1); break;
                case "嘎嘎":   Quack(2); break;
                case "嘎嘎嘎": Quack(3); break;
                case "?":      Quack(1, rising: true); break;
                case "!":      Quack(2, excited: true); break;
                default:       Quack(1); break;
            }
        }

        /// <param name="times">連叫幾聲</param>
        /// <param name="rising">音高上揚（像疑問）</param>
        /// <param name="excited">更短更急（像驚呼）</param>
        public static void Quack(int times, bool rising = false, bool excited = false)
        {
            if (!Enabled || times < 1) return;

            byte[] wav;
            try { wav = BuildWav(times, rising, excited); }
            catch { return; }

            Task.Run(() =>
            {
                // 正在播就直接略過這次，不要排隊堆積
                if (!Monitor.TryEnter(_gate)) return;
                try
                {
                    using (var ms = new MemoryStream(wav))
                    using (var sp = new SoundPlayer(ms))
                        sp.PlaySync();
                }
                catch { /* 沒有音效裝置之類的就算了 */ }
                finally { Monitor.Exit(_gate); }
            });
        }

        private static byte[] BuildWav(int times, bool rising, bool excited)
        {
            double quackDur = excited ? 0.13 : 0.18;   // 每聲長度（秒），低沉版拉長一點
            double gap = excited ? 0.05 : 0.09;         // 聲與聲之間的留白

            int quackSamples = (int)(quackDur * SampleRate);
            int gapSamples = (int)(gap * SampleRate);
            int total = times * quackSamples + (times - 1) * gapSamples;

            var pcm = new short[total];
            int pos = 0;

            for (int q = 0; q < times; q++)
            {
                // 基頻壓低：一般約 460→150 Hz，疑問版微上揚
                double fStart = (rising ? 190 : 460) * (1 + (_rng.NextDouble() - 0.5) * 0.10);
                double fEnd   = (rising ? 340 : 150) * (1 + (_rng.NextDouble() - 0.5) * 0.10);
                if (q > 0 && !rising) { fStart *= 0.9; fEnd *= 0.9; }    // 後面幾聲再壓低

                double growlHz = excited ? 55 : 38;                     // 低頻嘶吼調變
                double drive = excited ? 3.2 : 2.6;                     // 軟削波驅動量 → 沙啞
                double noiseAmt = excited ? 0.32 : 0.4;                 // 混入的氣音/雜訊比例
                double norm = Math.Tanh(drive);

                double phase = 0, lp = 0, lp2 = 0, fjit = 0;

                for (int i = 0; i < quackSamples; i++)
                {
                    double t = (double)i / quackSamples;                // 0..1
                    double baseFreq = fStart * Math.Pow(fEnd / fStart, t);

                    // 音高隨機游走 → 破音、粗糙感
                    fjit = fjit * 0.985 + (_rng.NextDouble() - 0.5) * 7.0;
                    double freq = baseFreq * (1 + fjit / 100.0);

                    phase += 2 * Math.PI * freq / SampleRate;
                    if (phase > 2 * Math.PI) phase -= 2 * Math.PI;

                    double saw = phase / Math.PI - 1.0;                 // 鋸齒波
                    double growl = 0.55 + 0.45 * Math.Sin(2 * Math.PI * growlHz * i / SampleRate);
                    double noise = _rng.NextDouble() * 2 - 1;           // 白雜訊（氣音）

                    double raw = saw * growl * (1 - noiseAmt) + noise * noiseAmt;

                    // 兩級低通壓暗
                    lp  += 0.33 * (raw - lp);
                    lp2 += 0.5  * (lp  - lp2);

                    // 軟削波：加諧波、壓縮動態 → 沙啞
                    double shaped = Math.Tanh(lp2 * drive) / norm;

                    double env = (1 - Math.Exp(-t * 45)) * Math.Exp(-t * 2.8);
                    if (t > 0.82) env *= (1 - t) / 0.18;                // 結尾淡出

                    double s = shaped * env * 0.55;
                    if (s > 1) s = 1; else if (s < -1) s = -1;
                    pcm[pos++] = (short)(s * 32000);
                }

                if (q < times - 1) pos += gapSamples;                   // 留白（陣列已是 0）
            }

            return WrapWav(pcm);
        }

        private static byte[] WrapWav(short[] pcm)
        {
            int dataBytes = pcm.Length * 2;
            using (var ms = new MemoryStream(44 + dataBytes))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);                     // fmt chunk 大小
                w.Write((short)1);               // PCM
                w.Write((short)1);               // 單聲道
                w.Write(SampleRate);
                w.Write(SampleRate * 2);         // byte rate
                w.Write((short)2);               // block align
                w.Write((short)16);              // 位元深度
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (var s in pcm) w.Write(s);
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
