using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Xml;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;

using Svg;

using Task = System.Threading.Tasks.Task;

namespace SvgViewer
{
    internal enum ScalingMode
    {
        ScaleDown,
        ScaleUp,
    }

    internal class SvgAdornment : Image
    {
        private readonly ITextView _view;

        public SvgAdornment(IWpfTextView view)
        {
            _view = view;

            Visibility = Visibility.Hidden;

            IAdornmentLayer adornmentLayer = view.GetAdornmentLayer(AdornmentLayer.LayerName);

            if (adornmentLayer.IsEmpty)
            {
                adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, this, null);
            }

            _view.TextBuffer.PostChanged += OnTextBufferChanged;
            _view.Closed += OnTextViewClosed;
            _view.ViewportHeightChanged += SetAdornmentLocation;
            _view.ViewportWidthChanged += SetAdornmentLocation;

            GenerateImageAsync().FireAndForget();
        }

        private void OnTextBufferChanged(object sender, EventArgs e)
        {
            var lastVersion = _view.TextBuffer.CurrentSnapshot.Version.VersionNumber;

            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Task.Delay(500);

                if (_view.TextBuffer.CurrentSnapshot.Version.VersionNumber == lastVersion)
                {
                    await GenerateImageAsync();
                }
            });
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _view.Closed -= OnTextViewClosed;
            _view.TextBuffer.PostChanged -= OnTextBufferChanged;
            _view.ViewportHeightChanged -= SetAdornmentLocation;
            _view.ViewportWidthChanged -= SetAdornmentLocation;
        }

        private void OnDocumentSaved(object sender, TextDocumentFileActionEventArgs e)
        {
            GenerateImageAsync().FireAndForget();
        }

        /// <summary>
        /// Length of the preview side that's being scaled to.
        /// If <see cref="ScalingMode"/> is ScaleUp, that's the minimal side length.
        /// If <see cref="ScalingMode"/> is ScaleDown, that's the maximal side length.
        /// </summary>
        public int PreviewSideLength { get; set; } = 250;

        /// <summary>
        /// Scale the image up or down?
        /// </summary>
        public ScalingMode ScalingMode { get; set; } = ScalingMode.ScaleUp;

        private async Task GenerateImageAsync()
        {
            await TaskScheduler.Default;

            try
            {
                if (!TryGetBufferAsXmlDocument(out XmlDocument xml))
                {
                    Source = null;
                    return;
                }

                var svg = SvgDocument.Open(xml);

                if (svg == null)
                {
                    return;
                }

                Size size = CalculateDimensions(new Size(svg.Width.Value, svg.Height.Value));

                var bitmap = new BitmapImage();

                using (System.Drawing.Bitmap bmp = svg.Draw((int)size.Width, (int)size.Height))
                {
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;

                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                    }
                }


                bitmap.Freeze();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ToolTip = $"Width: {svg.Width}\nHeight: {svg.Height}";
                Source = bitmap;
                UpdateAdornmentLocation(bitmap.Width, bitmap.Height);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        private bool TryGetBufferAsXmlDocument(out XmlDocument document)
        {
            document = new XmlDocument();

            try
            {
                var xml = _view.TextBuffer.CurrentSnapshot.GetText();
                document.LoadXml(xml);

                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        private Size CalculateDimensions(Size currentSize)
        {
            var sourceWidth = currentSize.Width;
            var sourceHeight = currentSize.Height;

            var widthPercent = PreviewSideLength / sourceWidth;
            var heightPercent = PreviewSideLength / sourceHeight;

            var percent = ScalingMode == ScalingMode.ScaleUp ?
                Math.Max(heightPercent, widthPercent) :
                Math.Min(heightPercent, widthPercent);

            var destWidth = (int)(sourceWidth * percent);
            var destHeight = (int)(sourceHeight * percent);

            return new Size(destWidth, destHeight);
        }

        private void SetAdornmentLocation(object sender, EventArgs e)
        {
            UpdateAdornmentLocation(ActualWidth, ActualHeight);
        }

        private void UpdateAdornmentLocation(double width, double height)
        {
            Canvas.SetLeft(this, _view.ViewportRight - width - 20);
            Canvas.SetTop(this, _view.ViewportBottom - height - 20);
            Visibility = Visibility.Visible;
        }

    }
}
