using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;

using Svg;

using Task = System.Threading.Tasks.Task;

namespace SvgViewer
{
    /// <summary>
    /// Displays a live SVG preview as an editor adornment
    /// </summary>
    internal sealed class SvgAdornment : Image
    {
        // Cached error indicator bitmap (shared across all instances)
        private static readonly Lazy<BitmapSource> _cachedErrorIndicator = new Lazy<BitmapSource>(CreateErrorIndicatorBitmap);

        private readonly ITextView _view;
        private readonly Debouncer _debouncer = new Debouncer();
        private readonly Debouncer _viewportDebouncer;

        // Cached rendering state
        private string _lastContentHash;
        private int _lastPreviewSize;
        private Size? _lastSourceSize;
        private Size? _lastCalculatedSize;

        // Reusable memory stream to reduce allocations
        private MemoryStream _reusableStream;

        // Error state
        private string _lastError;
        private bool _isLoading;

        // Zoom state
        private double _zoomFactor = 1.0;
        private const double _minZoom = 0.5;
        private const double _maxZoom = 4.0;
        private const double _zoomStep = 0.25;

        // Store original bitmap for zoom operations
        private BitmapSource _currentBitmap;
        private string _currentSvgWidth;
        private string _currentSvgHeight;

        public SvgAdornment(IWpfTextView view)
        {
            _view = view;
            _viewportDebouncer = new Debouncer(useDispatcher: true);

            Visibility = Visibility.Hidden;
            Cursor = Cursors.Hand;

            IAdornmentLayer adornmentLayer = view.GetAdornmentLayer(AdornmentLayer.LayerName);

            if (adornmentLayer.IsEmpty)
            {
                adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, this, null);
            }

            _view.TextBuffer.PostChanged += OnTextBufferChanged;
            _view.Closed += OnTextViewClosed;
            _view.ViewportHeightChanged += OnViewportChanged;
            _view.ViewportWidthChanged += OnViewportChanged;

            // Subscribe to mouse events for click-to-copy and zoom
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseWheel += OnMouseWheel;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;

            // Subscribe to options changes
            GeneralOptions.OptionsChanged += OnOptionsChanged;

            GenerateImageAsync().FireAndForget();
        }

        private GeneralOptions Options => SvgViewerPackage.Options;

        private void OnTextBufferChanged(object sender, EventArgs e)
        {
            var debounceDelay = Options?.DebounceDelay ?? Constants.DefaultDebounceDelay;

            _debouncer.Debounce(() =>
            {
                GenerateImageAsync().FireAndForget();
            }, debounceDelay);
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _view.Closed -= OnTextViewClosed;
            _view.TextBuffer.PostChanged -= OnTextBufferChanged;
            _view.ViewportHeightChanged -= OnViewportChanged;
            _view.ViewportWidthChanged -= OnViewportChanged;

            MouseLeftButtonUp -= OnMouseLeftButtonUp;
            MouseWheel -= OnMouseWheel;
            MouseEnter -= OnMouseEnter;
            MouseLeave -= OnMouseLeave;

            GeneralOptions.OptionsChanged -= OnOptionsChanged;

            _debouncer.Dispose();
            _viewportDebouncer.Dispose();
            _reusableStream?.Dispose();
            _reusableStream = null;
        }

        private void OnViewportChanged(object sender, EventArgs e)
        {
            _viewportDebouncer.Debounce(() =>
            {
                UpdateAdornmentLocation(ActualWidth, ActualHeight);
            }, Constants.ViewportDebounceDelay);
        }

        private void OnOptionsChanged(object sender, OptionsChangedEventArgs e)
        {
            // Re-render on size change, reposition on position/margin change
            switch (e.PropertyName)
            {
                case nameof(GeneralOptions.PreviewSize):
                    // Clear cache to force re-render at new size
                    _lastContentHash = null;
                    GenerateImageAsync().FireAndForget();
                    break;

                case nameof(GeneralOptions.PreviewPosition):
                case nameof(GeneralOptions.PreviewMargin):
                    // Just reposition, no re-render needed
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        UpdateAdornmentLocation(ActualWidth, ActualHeight);
                    }).FireAndForget();
                    break;

                case nameof(GeneralOptions.ShowErrorIndicator):
                    // Re-apply current state
                    if (_lastError != null)
                    {
                        GenerateImageAsync().FireAndForget();
                    }
                    break;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Copy rendered image to clipboard
            if (_currentBitmap != null && Source != null)
            {
                try
                {
                    Clipboard.SetImage(_currentBitmap);

                    // Visual feedback - brief opacity flash
                    Opacity = 0.3;
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            if (!_isLoading)
                            {
                                Opacity = 1.0;
                            }
                        }).FireAndForget();
                    });

                    // Update tooltip briefly to show success
                    ToolTip = "Copied to clipboard!";
                    Task.Delay(1500).ContinueWith(_ =>
                    {
                        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            UpdateTooltip();
                        }).FireAndForget();
                    });
                }
                catch
                {
                    // Clipboard access can fail - ignore
                }
            }

            e.Handled = true;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentBitmap == null)
            {
                return;
            }

            // Zoom in/out based on wheel direction
            if (e.Delta > 0)
            {
                _zoomFactor = System.Math.Min(_maxZoom, _zoomFactor + _zoomStep);
            }
            else
            {
                _zoomFactor = System.Math.Max(_minZoom, _zoomFactor - _zoomStep);
            }

            ApplyZoom();
            e.Handled = true;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            // Show zoom hint in tooltip
            UpdateTooltip();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            // Reset zoom when mouse leaves
            if (_zoomFactor != 1.0)
            {
                _zoomFactor = 1.0;
                ApplyZoom();
            }
        }

        private void ApplyZoom()
        {
            if (_currentBitmap == null)
            {
                return;
            }

            var zoomedWidth = _currentBitmap.Width * _zoomFactor;
            var zoomedHeight = _currentBitmap.Height * _zoomFactor;

            Width = zoomedWidth;
            Height = zoomedHeight;

            UpdateAdornmentLocation(zoomedWidth, zoomedHeight);
            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            if (_lastError != null)
            {
                ToolTip = $"SVG Error:\n{_lastError}";
            }
            else if (_currentSvgWidth != null && _currentSvgHeight != null)
            {
                var zoomInfo = _zoomFactor != 1.0 ? $"\nZoom: {_zoomFactor:P0}" : "";
                ToolTip = $"Width: {_currentSvgWidth}\nHeight: {_currentSvgHeight}{zoomInfo}\n\nClick to copy • Scroll to zoom";
            }
        }

        private async Task GenerateImageAsync()
        {
            // Show loading indicator if enabled
            var showLoading = Options?.ShowLoadingIndicator ?? true;
            if (showLoading && !_isLoading)
            {
                _isLoading = true;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ShowLoadingState();
            }

            await TaskScheduler.Default;

            RenderResult result = await RenderSvgAsync();

            // Single UI thread switch for all updates
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _isLoading = false;
            ApplyRenderResult(result);
        }

        private async Task<RenderResult> RenderSvgAsync()
        {
            try
            {
                var xmlContent = _view.TextBuffer.CurrentSnapshot.GetText();

                // Quick validation before expensive parsing
                if (!LooksLikeSvg(xmlContent))
                {
                    return RenderResult.Empty();
                }

                // Check content hash to avoid re-rendering identical content
                var contentHash = ComputeHash(xmlContent);
                var previewSize = Options?.PreviewSize ?? Constants.DefaultPreviewSize;

                if (contentHash == _lastContentHash && previewSize == _lastPreviewSize)
                {
                    return RenderResult.Unchanged();
                }

                // Parse XML
                if (!TryParseXml(xmlContent, out XmlDocument xml, out var parseError))
                {
                    return RenderResult.Error(parseError);
                }

                // Parse SVG
                SvgDocument svg;
                try
                {
                    svg = SvgDocument.Open(xml);
                }
                catch (Exception ex)
                {
                    return RenderResult.Error($"SVG parse error: {ex.Message}");
                }

                if (svg == null)
                {
                    return RenderResult.Error("Unable to parse SVG document");
                }

                // Get SVG dimensions (from width/height attributes or viewBox)
                Size svgSize = GetSvgDimensions(svg);
                var displayWidth = FormatSvgDimension(svg.Width, svgSize.Width);
                var displayHeight = FormatSvgDimension(svg.Height, svgSize.Height);

                // Calculate render dimensions
                Size size = CalculateDimensions(svgSize, previewSize);

                // Render to bitmap (with timeout protection)
                BitmapImage bitmap = await RenderToBitmapAsync(svg, size);

                if (bitmap == null)
                {
                    return RenderResult.Error($"Failed to render SVG (rendering timed out after {Constants.RenderingTimeoutMs / 1000}s or SVG is too complex)");
                }

                // Update cache
                _lastContentHash = contentHash;
                _lastPreviewSize = previewSize;

                return RenderResult.Success(bitmap, displayWidth, displayHeight);
            }
            catch (OperationCanceledException)
            {
                return RenderResult.Cancelled();
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                return RenderResult.Error($"Unexpected error: {ex.Message}");
            }
        }

        private async Task<BitmapImage> RenderToBitmapAsync(SvgDocument svg, Size size)
        {
            try
            {
                // Ensure reusable stream exists with adequate capacity
                if (_reusableStream == null)
                {
                    _reusableStream = new MemoryStream(Constants.InitialMemoryStreamCapacity);
                }
                else
                {
                    _reusableStream.SetLength(0);
                }

                // Run SVG rendering with a timeout to prevent hangs on complex SVGs
                using (var cts = new CancellationTokenSource(Constants.RenderingTimeoutMs))
                {
                    System.Drawing.Bitmap bmp = await Task.Run(() =>
                    {
                        // Check cancellation before starting expensive operation
                        cts.Token.ThrowIfCancellationRequested();
                        return svg.Draw((int)size.Width, (int)size.Height);
                    }, cts.Token);

                    if (bmp == null)
                    {
                        return null;
                    }

                    using (bmp)
                    {
                        bmp.Save(_reusableStream, System.Drawing.Imaging.ImageFormat.Png);
                        _reusableStream.Position = 0;

                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = _reusableStream;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        return bitmap;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Rendering timed out - return null to show error state
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void ApplyRenderResult(RenderResult result)
        {
            _lastError = result.ErrorMessage;

            switch (result.State)
            {
                case RenderState.Success:
                    _currentBitmap = result.Bitmap;
                    _currentSvgWidth = result.SvgWidth;
                    _currentSvgHeight = result.SvgHeight;
                    _zoomFactor = 1.0; // Reset zoom on new render

                    Source = result.Bitmap;
                    Width = result.Bitmap.Width;
                    Height = result.Bitmap.Height;
                    Opacity = 1.0;
                    UpdateTooltip();
                    UpdateAdornmentLocation(result.Bitmap.Width, result.Bitmap.Height);
                    break;

                case RenderState.Error:
                    _currentBitmap = null;
                    _currentSvgWidth = null;
                    _currentSvgHeight = null;

                    var showError = Options?.ShowErrorIndicator ?? true;
                    if (showError)
                    {
                        ShowErrorState(result.ErrorMessage);
                    }
                    else
                    {
                        Source = null;
                        Visibility = Visibility.Hidden;
                    }
                    break;

                case RenderState.Empty:
                    _currentBitmap = null;
                    _currentSvgWidth = null;
                    _currentSvgHeight = null;
                    Source = null;
                    Visibility = Visibility.Hidden;
                    break;

                case RenderState.Unchanged:
                    // Keep current state, just ensure visibility
                    if (Source != null)
                    {
                        Opacity = 1.0;
                        Visibility = Visibility.Visible;
                    }
                    break;

                case RenderState.Cancelled:
                    // Do nothing on cancellation
                    break;
            }
        }

        private void ShowLoadingState()
        {
            ToolTip = "Rendering SVG...";
            Opacity = 0.5;
        }

        private void ShowErrorState(string errorMessage)
        {
            _lastError = errorMessage;
            ToolTip = $"SVG Error:\n{errorMessage}";

            // Use cached error indicator
            BitmapSource errorBitmap = _cachedErrorIndicator.Value;
            Source = errorBitmap;
            Width = errorBitmap.Width;
            Height = errorBitmap.Height;
            Opacity = 0.8;
            UpdateAdornmentLocation(errorBitmap.Width, errorBitmap.Height);
        }

        private static BitmapSource CreateErrorIndicatorBitmap()
        {
            var size = 48;
            var visual = new DrawingVisual();

            using (DrawingContext context = visual.RenderOpen())
            {
                // Red circle background
                var backgroundBrush = new SolidColorBrush(Color.FromArgb(200, 220, 53, 69));
                backgroundBrush.Freeze();

                var borderPen = new Pen(Brushes.DarkRed, 2);
                borderPen.Freeze();

                context.DrawEllipse(
                    backgroundBrush,
                    borderPen,
                    new Point(size / 2, size / 2),
                    size / 2 - 2,
                    size / 2 - 2);

                // White X
                var xPen = new Pen(Brushes.White, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                xPen.Freeze();

                context.DrawLine(xPen, new Point(14, 14), new Point(34, 34));
                context.DrawLine(xPen, new Point(34, 14), new Point(14, 34));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();

            return bitmap;
        }

        /// <summary>
        /// Gets the dimensions of an SVG, falling back to viewBox if width/height are not specified
        /// </summary>
        private static Size GetSvgDimensions(SvgDocument svg)
        {
            const double defaultSize = 300; // Default size when no dimensions are specified

            double width = 0;
            double height = 0;

            // Try to get explicit width/height first
            if (svg.Width.Type != Svg.SvgUnitType.None && svg.Width.Type != Svg.SvgUnitType.Percentage)
            {
                width = svg.Width.Value;
            }

            if (svg.Height.Type != Svg.SvgUnitType.None && svg.Height.Type != Svg.SvgUnitType.Percentage)
            {
                height = svg.Height.Value;
            }

            // If width or height is missing/invalid, try to get from viewBox
            if ((width <= 0 || height <= 0) && svg.ViewBox != Svg.SvgViewBox.Empty)
            {
                if (width <= 0)
                {
                    width = svg.ViewBox.Width;
                }

                if (height <= 0)
                {
                    height = svg.ViewBox.Height;
                }
            }

            // If still no valid dimensions, use default
            if (width <= 0)
            {
                width = defaultSize;
            }

            if (height <= 0)
            {
                height = defaultSize;
            }

            return new Size(width, height);
        }

        /// <summary>
        /// Formats an SVG dimension for display, showing the original value or computed value
        /// </summary>
        private static string FormatSvgDimension(Svg.SvgUnit unit, double computedValue)
        {
            if (unit.Type == Svg.SvgUnitType.None || unit.Type == Svg.SvgUnitType.Percentage || unit.Value <= 0)
            {
                return $"{computedValue:0}px (from viewBox)";
            }

            return unit.ToString();
        }

        private static bool LooksLikeSvg(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            // Find first non-whitespace character position
            var start = 0;
            while (start < content.Length && char.IsWhiteSpace(content[start]))
            {
                start++;
            }

            // Check if it starts with XML declaration or SVG tag
            if (start >= content.Length)
            {
                return false;
            }

            // Check for <svg or <?xml (which may contain svg)
            return content.IndexOf("<svg", start, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryParseXml(string content, out XmlDocument document, out string error)
        {
            document = null;
            error = null;

            try
            {
                document = new XmlDocument();
                document.LoadXml(content);

                if (document.DocumentElement?.Name.Equals("svg", StringComparison.OrdinalIgnoreCase) != true)
                {
                    error = "Document root is not an SVG element";
                    document = null;
                    return false;
                }

                return true;
            }
            catch (XmlException ex)
            {
                error = $"XML error at line {ex.LineNumber}: {ex.Message}";
                return false;
            }
        }

        private static string ComputeHash(string content)
        {
            // Use simple hash for change detection (not security)
            // Combine hash code with length for better collision resistance
            var hash = content.GetHashCode();
            return $"{hash:X8}-{content.Length}";
        }

        private Size CalculateDimensions(Size currentSize, int previewSize)
        {
            // Use cached calculation if inputs haven't changed
            if (_lastSourceSize.HasValue &&
                _lastSourceSize.Value == currentSize &&
                _lastCalculatedSize.HasValue &&
                _lastPreviewSize == previewSize)
            {
                return _lastCalculatedSize.Value;
            }

            var sourceWidth = currentSize.Width;
            var sourceHeight = currentSize.Height;

            // Handle edge cases
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                return new Size(previewSize, previewSize);
            }

            var widthRatio = previewSize / sourceWidth;
            var heightRatio = previewSize / sourceHeight;
            var ratio = System.Math.Min(widthRatio, heightRatio);

            // Ensure minimum size of 1 pixel
            var destWidth = System.Math.Max(1, (int)(sourceWidth * ratio));
            var destHeight = System.Math.Max(1, (int)(sourceHeight * ratio));

            var calculatedSize = new Size(destWidth, destHeight);

            // Cache the results
            _lastSourceSize = currentSize;
            _lastCalculatedSize = calculatedSize;

            return calculatedSize;
        }

        private void UpdateAdornmentLocation(double width, double height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var margin = Options?.PreviewMargin ?? Constants.DefaultPreviewMargin;
            PreviewPosition position = Options?.PreviewPosition ?? PreviewPosition.BottomRight;

            double left, top;

            switch (position)
            {
                case PreviewPosition.TopLeft:
                    left = margin;
                    top = margin;
                    break;

                case PreviewPosition.TopRight:
                    left = _view.ViewportRight - width - margin;
                    top = margin;
                    break;

                case PreviewPosition.BottomLeft:
                    left = margin;
                    top = _view.ViewportBottom - height - margin;
                    break;

                case PreviewPosition.BottomRight:
                default:
                    left = _view.ViewportRight - width - margin;
                    top = _view.ViewportBottom - height - margin;
                    break;
            }

            Canvas.SetLeft(this, left);
            Canvas.SetTop(this, top);
            Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Represents the result of an SVG render operation
        /// </summary>
        private sealed class RenderResult
        {
            public RenderState State { get; private set; }
            public BitmapSource Bitmap { get; private set; }
            public string SvgWidth { get; private set; }
            public string SvgHeight { get; private set; }
            public string ErrorMessage { get; private set; }

            private RenderResult() { }

            public static RenderResult Success(BitmapSource bitmap, string width, string height) =>
                new RenderResult { State = RenderState.Success, Bitmap = bitmap, SvgWidth = width, SvgHeight = height };

            public static RenderResult Error(string message) =>
                new RenderResult { State = RenderState.Error, ErrorMessage = message };

            public static RenderResult Empty() =>
                new RenderResult { State = RenderState.Empty };

            public static RenderResult Unchanged() =>
                new RenderResult { State = RenderState.Unchanged };

            public static RenderResult Cancelled() =>
                new RenderResult { State = RenderState.Cancelled };
        }

        private enum RenderState
        {
            Success,
            Error,
            Empty,
            Unchanged,
            Cancelled
        }
    }
}
