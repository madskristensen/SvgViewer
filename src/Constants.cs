namespace SvgViewer
{
    /// <summary>
    /// Application-wide constants to eliminate magic numbers
    /// </summary>
    internal static class Constants
    {
        // Preview sizing
        public const int DefaultPreviewSize = 250;
        public const int MinPreviewSize = 50;
        public const int MaxPreviewSize = 1000;

        // Preview positioning
        public const int DefaultPreviewMargin = 20;
        public const int MinPreviewMargin = 0;
        public const int MaxPreviewMargin = 100;

        // Debounce timings (milliseconds)
        public const int DefaultDebounceDelay = 500;
        public const int ViewportDebounceDelay = 50;
        public const int MinDebounceDelay = 100;
        public const int MaxDebounceDelay = 2000;

        // Memory stream buffer sizes
        public const int InitialMemoryStreamCapacity = 65536; // 64KB initial size for bitmap encoding
    }
}
