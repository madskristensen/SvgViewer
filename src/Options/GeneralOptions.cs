using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace SvgViewer
{
    /// <summary>
    /// Options page for SVG Viewer settings
    /// </summary>
    [ComVisible(true)]
    [Guid("9E5D5E5A-7F3B-4B5E-9C5A-1E5F5E5A7F3B")]
    public class GeneralOptions : DialogPage
    {
        private int _previewSize = Constants.DefaultPreviewSize;
        private PreviewPosition _previewPosition = PreviewPosition.BottomRight;
        private int _previewMargin = Constants.DefaultPreviewMargin;
        private bool _showErrorIndicator = true;
        private bool _showLoadingIndicator = true;
        private int _debounceDelay = Constants.DefaultDebounceDelay;

        /// <summary>
        /// Raised when any option value changes
        /// </summary>
        public static event EventHandler<OptionsChangedEventArgs> OptionsChanged;

        [Category("Preview")]
        [DisplayName("Preview Size")]
        [Description("The maximum width/height of the SVG preview in pixels (50-1000).")]
        [DefaultValue(Constants.DefaultPreviewSize)]
        public int PreviewSize
        {
            get => _previewSize;
            set
            {
                var newValue = MathHelper.Clamp(value, 50, 1000);
                if (_previewSize != newValue)
                {
                    _previewSize = newValue;
                    RaiseOptionsChanged(nameof(PreviewSize));
                }
            }
        }

        [Category("Preview")]
        [DisplayName("Preview Position")]
        [Description("The corner of the editor where the preview is displayed.")]
        [DefaultValue(PreviewPosition.BottomRight)]
        public PreviewPosition PreviewPosition
        {
            get => _previewPosition;
            set
            {
                if (_previewPosition != value)
                {
                    _previewPosition = value;
                    RaiseOptionsChanged(nameof(PreviewPosition));
                }
            }
        }

        [Category("Preview")]
        [DisplayName("Preview Margin")]
        [Description("The margin from the edge of the editor in pixels (0-100).")]
        [DefaultValue(Constants.DefaultPreviewMargin)]
        public int PreviewMargin
        {
            get => _previewMargin;
            set
            {
                var newValue = MathHelper.Clamp(value, 0, 100);
                if (_previewMargin != newValue)
                {
                    _previewMargin = newValue;
                    RaiseOptionsChanged(nameof(PreviewMargin));
                }
            }
        }

        [Category("Feedback")]
        [DisplayName("Show Error Indicator")]
        [Description("Show an error icon when the SVG cannot be parsed.")]
        [DefaultValue(true)]
        public bool ShowErrorIndicator
        {
            get => _showErrorIndicator;
            set
            {
                if (_showErrorIndicator != value)
                {
                    _showErrorIndicator = value;
                    RaiseOptionsChanged(nameof(ShowErrorIndicator));
                }
            }
        }

        [Category("Feedback")]
        [DisplayName("Show Loading Indicator")]
        [Description("Show a loading indicator while rendering large SVG files.")]
        [DefaultValue(true)]
        public bool ShowLoadingIndicator
        {
            get => _showLoadingIndicator;
            set
            {
                if (_showLoadingIndicator != value)
                {
                    _showLoadingIndicator = value;
                    RaiseOptionsChanged(nameof(ShowLoadingIndicator));
                }
            }
        }

        [Category("Performance")]
        [DisplayName("Debounce Delay (ms)")]
        [Description("Delay in milliseconds before re-rendering after typing (100-2000).")]
        [DefaultValue(Constants.DefaultDebounceDelay)]
        public int DebounceDelay
        {
            get => _debounceDelay;
            set
            {
                var newValue = MathHelper.Clamp(value, 100, 2000);
                if (_debounceDelay != newValue)
                {
                    _debounceDelay = newValue;
                    RaiseOptionsChanged(nameof(DebounceDelay));
                }
            }
        }

        private void RaiseOptionsChanged(string propertyName)
        {
            OptionsChanged?.Invoke(this, new OptionsChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Event args for options changed event
    /// </summary>
    public class OptionsChangedEventArgs : EventArgs
    {
        public string PropertyName { get; }

        public OptionsChangedEventArgs(string propertyName)
        {
            PropertyName = propertyName;
        }
    }

    /// <summary>
    /// Preview position options
    /// </summary>
    public enum PreviewPosition
    {
        [Description("Top Left")]
        TopLeft,
        [Description("Top Right")]
        TopRight,
        [Description("Bottom Left")]
        BottomLeft,
        [Description("Bottom Right")]
        BottomRight
    }

    /// <summary>
    /// Math helper for .NET Framework 4.7.2 (System.Math.Clamp not available)
    /// </summary>
    internal static class MathHelper
    {
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
