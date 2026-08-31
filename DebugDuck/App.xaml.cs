using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DebugDuck
{
    public partial class App : Application
    {
        private static ImageSource _duckIcon;

        /// <summary>視窗工作列縮圖用的圖示：內嵌資源 Assets/黃小鴨.png。</summary>
        public static ImageSource DuckIcon
        {
            get { return _duckIcon ?? (_duckIcon = LoadDuckIcon()); }
        }

        private static ImageSource LoadDuckIcon()
        {
            try
            {
                var sri = GetResourceStream(new Uri("Assets/黃小鴨.png", UriKind.Relative));
                if (sri == null) return null;
                using (var s = sri.Stream)
                    return BitmapFrame.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
            catch
            {
                return null;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var pet = new PetWindow();
            MainWindow = pet;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            pet.Show();
        }
    }
}
