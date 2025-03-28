using System;
using System.Threading;

namespace SvgViewer
{
    public class Debouncer : IDisposable
    {
        private Thread _thread;
        private volatile Action _action;
        private volatile int _delay = 0;
        private volatile int _frequency;
        private readonly object _lock = new object();

        public void Debounce(Action action, int delay = 250, int frequency = 10)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            lock (_lock)
            {
                _action = action;
                _delay = delay;
                _frequency = frequency;

                if (_thread == null)
                {
                    _thread = new Thread(() => RunThread())
                    {
                        IsBackground = true
                    };

                    _thread.Start();
                }
            }
        }

        private void RunThread()
        {
            while (true)
            {
                lock (_lock)
                {
                    _delay -= _frequency;
                }

                Thread.Sleep(_frequency);

                lock (_lock)
                {
                    if (_delay <= 0 && _action != null)
                    {
                        _action();
                        _action = null;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_thread != null)
            {
                _thread.Abort();
                _thread = null;
            }
        }
    }
}