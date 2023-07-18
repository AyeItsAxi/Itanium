using System;
using System.Diagnostics;

namespace Itanium
{
    public class OffsetWatch
    {
        private readonly Stopwatch _stopwatch = null;
        TimeSpan _offsetTimeSpan;

        public OffsetWatch(TimeSpan offsetElapsedTimeSpan)
        {
            _offsetTimeSpan = offsetElapsedTimeSpan;
            _stopwatch = new();
        }
        
        public void Start()
        {
            _stopwatch.Start();
        }

        public void Stop()
        {
            _stopwatch.Stop();
        }

        public TimeSpan ElapsedTimeSpan
        {
            get
            {
                return _stopwatch.Elapsed + _offsetTimeSpan;
            }
            set
            {
                _offsetTimeSpan = value;
            }
        }
    }
}
