using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SvgViewer
{
    /// <summary>
    /// A thread-safe debouncer that uses async/await with optional UI thread marshaling
    /// </summary>
    public sealed class Debouncer : IDisposable
    {
        private readonly object _lock = new object();
        private readonly bool _useDispatcher;
        private readonly Dispatcher _dispatcher;

        private CancellationTokenSource _cancellationTokenSource;
        private DispatcherTimer _dispatcherTimer;
        private Action _pendingAction;
        private volatile bool _disposed;

        /// <summary>
        /// Creates a new debouncer
        /// </summary>
        /// <param name="useDispatcher">If true, executes actions on the UI thread using DispatcherTimer</param>
        public Debouncer(bool useDispatcher = false)
        {
            _useDispatcher = useDispatcher;

            if (_useDispatcher)
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
            }
        }

        /// <summary>
        /// Debounces the execution of an action, canceling previous calls if a new one arrives
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="delay">Delay in milliseconds before execution</param>
        public void Debounce(Action action, int delay = 250)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (_disposed)
            {
                return;
            }

            if (_useDispatcher)
            {
                DebounceWithDispatcher(action, delay);
            }
            else
            {
                DebounceWithTask(action, delay);
            }
        }

        private void DebounceWithDispatcher(Action action, int delay)
        {
            // Must be called from UI thread when using dispatcher mode
            if (_dispatcher.CheckAccess())
            {
                DebounceWithDispatcherCore(action, delay);
            }
            else
            {
                _dispatcher.BeginInvoke(new Action(() => DebounceWithDispatcherCore(action, delay)));
            }
        }

        private void DebounceWithDispatcherCore(Action action, int delay)
        {
            // Check disposed state - important for delayed BeginInvoke callbacks
            if (_disposed)
            {
                return;
            }

            // Stop existing timer
            DispatcherTimer existingTimer = _dispatcherTimer;
            if (existingTimer != null)
            {
                existingTimer.Stop();
                existingTimer.Tick -= OnDispatcherTimerTick;
                _dispatcherTimer = null;
            }

            _pendingAction = action;

            // Create and start new timer
            var newTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(delay)
            };
            newTimer.Tick += OnDispatcherTimerTick;
            _dispatcherTimer = newTimer;
            newTimer.Start();
        }

        private void OnDispatcherTimerTick(object sender, EventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            var timer = sender as DispatcherTimer;
            timer?.Stop();

            Action action = _pendingAction;
            _pendingAction = null;

            action?.Invoke();
        }

        private void DebounceWithTask(Action action, int delay)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                // Cancel any existing operation
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();

                // Create new cancellation token for this operation
                _cancellationTokenSource = new CancellationTokenSource();
                CancellationToken token = _cancellationTokenSource.Token;

                // Start the debounced task
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(delay, token);

                        // Double-check disposed state after delay
                        if (!token.IsCancellationRequested && !_disposed)
                        {
                            action();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when debouncing - ignore
                    }
                }, token);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Mark disposed first to prevent new operations
            _disposed = true;

            lock (_lock)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }

            // Capture timer reference before nulling to avoid race condition
            DispatcherTimer timerToDispose = _dispatcherTimer;
            _dispatcherTimer = null;
            _pendingAction = null;

            // Dispatcher timer cleanup must happen on UI thread
            if (timerToDispose != null)
            {
                if (_dispatcher?.CheckAccess() == true)
                {
                    timerToDispose.Stop();
                    timerToDispose.Tick -= OnDispatcherTimerTick;
                }
                else
                {
                    _dispatcher?.BeginInvoke(new Action(() =>
                    {
                        timerToDispose.Stop();
                        timerToDispose.Tick -= OnDispatcherTimerTick;
                    }));
                }
            }
        }
    }
}