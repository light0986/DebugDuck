using System;
using System.IO;
using System.Web.Script.Serialization;

namespace DebugDuck
{
    /// <summary>存在 %AppData%\DebugDuck\state.json 的一點點使用者設定（目前只有小鴨位置）。</summary>
    public sealed class AppState
    {
        public double PetLeft { get; set; } = double.NaN;
        public double PetTop { get; set; } = double.NaN;

        private static string FilePath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DebugDuck");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "state.json");
            }
        }

        public static AppState Load()
        {
            try
            {
                var path = FilePath;
                if (File.Exists(path))
                {
                    var s = new JavaScriptSerializer().Deserialize<AppState>(File.ReadAllText(path));
                    if (s != null) return s;
                }
            }
            catch
            {
                // 讀不到就用預設。
            }
            return new AppState();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this));
            }
            catch
            {
                // 存不了就算了，不要因為這個崩潰。
            }
        }
    }
}
