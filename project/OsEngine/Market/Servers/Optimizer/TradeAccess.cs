/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.Optimizer
{
    /// <summary>
    /// FEATURE-0107. Forward-only access to the tick data of one security for one period.
    /// The consumer (SecurityOptimizer) must call EnsureLoaded(index) before At(index); while index &lt; Count
    /// the tick is guaranteed. Absolute indices never shift, trimming only releases already consumed ticks.
    /// </summary>
    public interface ITradeAccess : IDisposable
    {
        /// <summary>Exact number of ticks of the period. Stable, fixed at creation.</summary>
        int Count { get; }

        Trade At(int absoluteIndex);

        void EnsureLoaded(int absoluteIndex);

        /// <summary>Releases the ticks with indices lower than absoluteIndex. Does not change absolute indices.</summary>
        void TrimBefore(int absoluteIndex);
    }

    /// <summary>
    /// FEATURE-0107, mode "RemoveTradesFromMemory = false". Wrapper over the list loaded as before,
    /// so the default behaviour stays exactly as it was: no trimming, no file handles.
    /// </summary>
    public sealed class TradeListAccess : ITradeAccess
    {
        private readonly List<Trade> _trades;

        public TradeListAccess(List<Trade> trades)
        {
            _trades = trades ?? new List<Trade>();
        }

        public int Count
        {
            get { return _trades.Count; }
        }

        public Trade At(int absoluteIndex)
        {
            if (absoluteIndex < 0 || absoluteIndex >= _trades.Count)
            {
                throw new IndexOutOfRangeException("TradeListAccess. Index: " + absoluteIndex +
                                                   ". Count: " + _trades.Count);
            }

            return _trades[absoluteIndex];
        }

        public void EnsureLoaded(int absoluteIndex)
        {
            // everything is already in the list

        }

        public void TrimBefore(int absoluteIndex)
        {
            // the list mode must keep the behaviour bit-by-bit as before, so the list is never mutated

        }

        public void Dispose()
        {
            // the list is owned by DataStorage

        }
    }

    /// <summary>
    /// FEATURE-0107, mode "RemoveTradesFromMemory = true". Reads the tick file of the security sequentially
    /// and keeps only a window of not-yet-consumed ticks in memory. The file is held open with FileShare.Read
    /// for the whole run (same as LoadTradesFromFolder opens it), Dispose releases the handle.
    /// Counting and serving use the same period filter and the same stop-on-corrupt-line semantics
    /// as OptimizerDataStorage.LoadTradesFromFolder.
    /// </summary>
    public sealed class TradeFileWindowAccess : ITradeAccess
    {
        private readonly Security _security;

        private readonly string _fileAddress;

        private readonly DateTime _timeStart;

        private readonly DateTime _timeEnd;

        private readonly int _tradesInMemoryCount;

        private readonly Action<string> _logError;

        private FileStream _stream;

        private StreamReader _reader;

        /// <summary>Sliding window of the not-yet-trimmed ticks. Index 0 corresponds to _windowStart.</summary>
        private readonly List<Trade> _window = new List<Trade>();

        /// <summary>Absolute index of the first tick inside the window. Grows with every trim, never shifts back.</summary>
        private int _windowStart;

        /// <summary>Absolute index just after the last tick loaded from the file.</summary>
        private int _loadedEnd;

        /// <summary>Reading source is exhausted: end of file, end of period or corrupt line.</summary>
        private bool _stopped;

        /// <summary>Exact tick count of the period. Taken once by counting scan in the constructor.</summary>
        private int _count;

        private long _nextTradeId;

        private bool _disposed;

        public TradeFileWindowAccess(Security security, string fileAddress,
            DateTime timeStart, DateTime timeEnd, int tradesInMemoryCount, Action<string> logError)
        {
            _security = security;
            _fileAddress = fileAddress;
            _timeStart = timeStart;
            _timeEnd = timeEnd;
            _tradesInMemoryCount = tradesInMemoryCount < 100 ? 100 : tradesInMemoryCount;
            _logError = logError ?? delegate { };

            try
            {
                _stream = new FileStream(_fileAddress, FileMode.Open, FileAccess.Read, FileShare.Read);
                // leaveOpen = true: the counting scan is rewound, the stream must stay open for serving

                _reader = new StreamReader(_stream, System.Text.Encoding.UTF8, true, 4096, true);

                _count = CountTradesInPeriod();
            }
            catch (Exception error)
            {
                DisposeReader();
                LogError("TradeFileWindowAccess. File is not available: " + _fileAddress + ". " + error);
                throw;
            }
        }

        public int Count
        {
            get { return _count; }
        }

        public Trade At(int absoluteIndex)
        {
            ThrowIfDisposed();

            if (absoluteIndex < 0 || absoluteIndex >= _count)
            {
                LogError("TradeFileWindowAccess.At. Index out of range. Index: " + absoluteIndex +
                         ". Count: " + _count + ". File: " + _fileAddress);
                throw new IndexOutOfRangeException("TradeFileWindowAccess.At. Index: " + absoluteIndex +
                                                   ". Count: " + _count);
            }

            if (absoluteIndex < _windowStart)
            {
                LogError("TradeFileWindowAccess.At. Index is below the trim border. Index: " + absoluteIndex +
                         ". Trim border: " + _windowStart + ". File: " + _fileAddress);
                throw new IndexOutOfRangeException("TradeFileWindowAccess.At. Index below trim: " + absoluteIndex);
            }

            EnsureLoaded(absoluteIndex);

            return _window[absoluteIndex - _windowStart];
        }

        public void EnsureLoaded(int absoluteIndex)
        {
            ThrowIfDisposed();

            // guarantee that the requested tick is in the window

            while (_stopped == false && _loadedEnd <= absoluteIndex)
            {
                LoadNextTrade();
            }

            // top the prefetch up to the configured window size beyond the consumer index

            while (_stopped == false && _loadedEnd - absoluteIndex - 1 < _tradesInMemoryCount)
            {
                if (LoadNextTrade() == false)
                {
                    break;
                }
            }
        }

        public void TrimBefore(int absoluteIndex)
        {
            ThrowIfDisposed();

            if (absoluteIndex <= _windowStart)
            {
                return;
            }

            int border = absoluteIndex > _loadedEnd ? _loadedEnd : absoluteIndex;
            int countToRemove = border - _windowStart;

            if (countToRemove > 0)
            {
                _window.RemoveRange(0, countToRemove);
                _windowStart = border;
            }
        }

        public void Dispose()
        {
            if (_disposed == true)
            {
                return;
            }

            _disposed = true;
            DisposeReader();
            _window.Clear();
        }

        private int CountTradesInPeriod()
        {
            int count = 0;

            // one probe instance because only the Time of the line matters here

            Trade probe = new Trade();

            while (true)
            {
                string line = _reader.ReadLine();

                if (line == null)
                {
                    break;
                }

                try
                {
                    probe.SetTradeFromString(line);
                }
                catch (Exception error)
                {
                    LogError("TradeFileWindowAccess. Error while parsing the line in " + _fileAddress + ". " + error);
                    break;
                }

                if (probe.Time < _timeStart)
                {
                    continue;
                }
                else if (probe.Time > _timeEnd)
                {
                    break;
                }

                count++;
            }

            // the counting scan consumed the file position. Rewind for serving

            _stream.Position = 0;
            _reader.Dispose();
            _reader = new StreamReader(_stream, System.Text.Encoding.UTF8, true, 4096, true);

            return count;
        }

        /// <summary>Loads the next in-period tick into the window. False if the source is exhausted.</summary>
        private bool LoadNextTrade()
        {
            Trade trade = new Trade();

            while (true)
            {
                string line = _reader.ReadLine();

                if (line == null)
                {
                    _stopped = true;
                    return false;
                }

                try
                {
                    trade.SetTradeFromString(line);
                }
                catch (Exception error)
                {
                    // same stop-on-corrupt-line as LoadTradesFromFolder, the error goes to the log

                    LogError("TradeFileWindowAccess. Error while parsing the line in " + _fileAddress + ". " + error);
                    _stopped = true;
                    return false;
                }

                if (trade.Time < _timeStart)
                {
                    continue;
                }
                else if (trade.Time > _timeEnd)
                {
                    _stopped = true;
                    return false;
                }

                trade.IdInTester = _nextTradeId++;
                trade.SecurityNameCode = _security.Name;
                _window.Add(trade);
                _loadedEnd++;

                return true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed == true)
            {
                LogError("TradeFileWindowAccess. Access after Dispose. File: " + _fileAddress);
                throw new InvalidOperationException("TradeFileWindowAccess. Access after Dispose");
            }
        }

        private void DisposeReader()
        {
            if (_reader != null)
            {
                // the reader was created with leaveOpen, so the stream is released explicitly

                _reader.Dispose();
                _reader = null;
            }

            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        private void LogError(string message)
        {
            try
            {
                _logError(message);
            }
            catch (Exception error)
            {
                DebugWriteLine("TradeFileWindowAccess. Error while sending the log message. " + error);
            }
        }

        private static void DebugWriteLine(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}
