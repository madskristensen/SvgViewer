using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Svg;
using Task = System.Threading.Tasks.Task;

namespace SvgViewer
{
    class SvgAdornment : Image
    {
        private readonly ITextView _view;
        private readonly ITextDocument _document;
        private const int _maxSize = 250;

        public SvgAdornment(IWpfTextView view, ITextDocument document)
        {
            _document = document;

            _view = view;
            _view.Closed += OnTextviewClosed;
            
            Visibility = Visibility.Hidden;

            IAdornmentLayer adornmentLayer = view.GetAdornmentLayer(AdornmentLayer.LayerName);

            if (adornmentLayer.IsEmpty)
                adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, this, null);

            base.Loaded += (s, e) =>
            {
                SetAdornmentLocation(_view, EventArgs.Empty);

                _view.ViewportHeightChanged += SetAdornmentLocation;
                _view.ViewportWidthChanged += SetAdornmentLocation;
            };

            document.FileActionOccurred += OnDocumentSaved;
            GenerateImageAsync().ConfigureAwait(false);
        }

        private void OnTextviewClosed(object sender, EventArgs e)
        {
            _view.Closed -= OnTextviewClosed;
            _view.ViewportHeightChanged -= SetAdornmentLocation;
            _view.ViewportWidthChanged -= SetAdornmentLocation;
            _document.FileActionOccurred -= OnDocumentSaved;
        }

        private void OnDocumentSaved(object sender, TextDocumentFileActionEventArgs e)
        {
            GenerateImageAsync().ConfigureAwait(false);
        }

        private async Task GenerateImageAsync()
        {
            string xml = _view.TextBuffer.CurrentSnapshot.GetText();
            var svg = SvgDocument.FromSvg<SvgDocument>(xml);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var bitmap = new BitmapImage();
            Size size = CalculateDimensions(new Size(svg.Width.Value, svg.Height.Value), _maxSize, _maxSize);

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

            ToolTip = $"Width: {svg.Width}\nHeight: {svg.Height}";
            Source = bitmap;
        }

        private static Size CalculateDimensions(Size currentSize, double maxWidth, double maxHeight)
        {
            double sourceWidth = currentSize.Width;
            double sourceHeight = currentSize.Height;

            double widthPercent = maxWidth / sourceWidth;
            double heightPercent = maxHeight / sourceHeight;

            double percent = heightPercent < widthPercent
                           ? heightPercent
                           : widthPercent;

            int destWidth = (int)(sourceWidth * percent);
            int destHeight = (int)(sourceHeight * percent);

            return new Size(destWidth, destHeight);
        }

        private void SetAdornmentLocation(object sender, EventArgs e)
        {
            var view = (IWpfTextView)sender;
            Canvas.SetLeft(this, view.ViewportRight - ActualWidth - 20);
            Canvas.SetTop(this, view.ViewportBottom - ActualHeight - 20);
            Visibility = Visibility.Visible;
        }

    }
}
