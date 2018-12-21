using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace SvgViewer
{
    public class BrowserView : IDisposable
    {
        private readonly string _filePath;
        private FileSystemWatcher _fsw;
        private bool _isDisposed;
        private readonly string _htmlFormat = @"<!DOCTYPE html>
<html>
<head>
  <style>body{{margin:20px;text-align:center;}}</style>
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
</head>
<body>
  <img style=""zoom:{0}%"" src=""{1}?{2}"" />
</body>
</html>";

        public BrowserView(string filePath)
        {
            _filePath = filePath;
            Init();
        }

        public WebBrowser Browser { get; private set; }

        private void Init()
        {
            Browser = new WebBrowser
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            RenderAsync().ConfigureAwait(false);

            string dir = Path.GetDirectoryName(_filePath);
            string fileName = Path.GetFileName(_filePath);

            _fsw = new FileSystemWatcher(dir, fileName);
            _fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.LastAccess;
            _fsw.Changed += OnFileChanged;
            _fsw.Renamed += OnFileChanged;
            _fsw.EnableRaisingEvents = true;
        }

        private async Task RenderAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                Browser.NavigateToString(string.Format(_htmlFormat, GetZoomFactor(), _filePath, Guid.NewGuid()));
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.FullPath.Equals(_filePath, StringComparison.OrdinalIgnoreCase))
            {
                RenderAsync().ConfigureAwait(false);
            }
        }

        private static int GetZoomFactor()
        {
            using (var g = Graphics.FromHwnd(Process.GetCurrentProcess().MainWindowHandle))
            {
                int baseLine = 96;
                float dpi = g.DpiX;

                if (baseLine == dpi)
                    return 100;

                // 150% scaling => 225
                // 250% scaling => 400

                double scale = dpi * ((dpi - baseLine) / baseLine + 1);
                return Convert.ToInt32(Math.Ceiling(scale / 25)) * 25; // round up to nearest 25
            }
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Browser.Dispose();
                    _fsw.EnableRaisingEvents = false;
                    _fsw.Dispose();
                }

                Browser = null;
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
