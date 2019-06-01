using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AncestryDnaClustering.ViewModels
{
    /// <summary>
    /// A ViewModel for a progress bar that updates on the Dispatcher thread.
    /// Includes settable textual descriptions as well as numeric estimates of completion time.
    /// </summary>
    public class ProgressData : ObservableObject
    {
        private readonly bool _updateProgress;

        public static ProgressData SuppressProgress = new ProgressData(false);

        public ProgressData() : this(true) { }

        private ProgressData(bool updateProgress)
        {
            _updateProgress = updateProgress;
        }

        // Textual description, settable by the client.
        private string _description;
        public string Description
        {
            get => _description;
            set => SetFieldValue(ref _description, value, nameof(Description));
        }

        // Maximum value of the progress bar.
        private int _maximum = 100;
        public int Maximum
        {
            get => _maximum;
            set
            {
                // Progress bars with a maximum of zero show as 'full' rather than 'empty'. 
                // Make sure that the maximum is at least 1.
                if (SetFieldValue(ref _maximum, Math.Max(value, 1), nameof(Maximum)))
                {
                    _referenceTimeValueIncrement = Math.Max((int)(Maximum / 100.0), 1);
                }
            }
        }

        // Current value of the progress bar.
        private int _value;
        public int Value
        {
            get => _value;
            set
            {
                var oldValue = Value;
                if (SetFieldValue(ref _value, value, nameof(Value)))
                {
                    Percent = Maximum == 0 ? 0 : (double)Value / Maximum;
                    if (oldValue == 0)
                    {
                        // This was the first increment. Record the time when it happened.
                        _referenceTimes = new List<DateTime> { DateTime.Now };
                    }
                    else if (Value == 0)
                    {
                        // Progress was reset. Time left is undefined.
                        TimeLeftString = null;
                    }
                    else if (Value % _referenceTimeValueIncrement == 0)
                    {
                        // Update the TimeLeftString every _referenceTimeValueIncrement.
                        // Estimate of the time left is based on the most recent 20 reference times.
                        // This smooths out short-term fluctuations while still adjusting for long-term rate changes.
                        var increments = Math.Min(20, _referenceTimes.Count);
                        var referenceTime = _referenceTimes[_referenceTimes.Count - increments];
                        var elapsedTime = DateTime.Now - referenceTime;
                        var remainingTime = TimeSpan.FromTicks((long)(elapsedTime.Ticks * (double)(Maximum - Value) / _referenceTimeValueIncrement / increments));

                        TimeLeftString = GetTimeString(remainingTime, false);

                        _referenceTimes.Add(DateTime.Now);

                    }
                }
            }
        }

        // Build a textual representation of the time remaining.
        private static string GetTimeString(TimeSpan timeSpan, bool complete)
        {
            // Don't bother trying to update the description if there's basically no time remaining.
            if (timeSpan < TimeSpan.FromSeconds(1))
            {
                return null;
            }

            var segments = new[]
            {
                timeSpan.Days == 0 ? null : $"{timeSpan.Days} day{(timeSpan.Days == 1 ? "" : "s")}",
                timeSpan.Hours == 0 ? null : $"{timeSpan.Hours} hour{(timeSpan.Hours == 1 ? "" : "s")}",
                timeSpan.Minutes == 0 ? null : $"{timeSpan.Minutes} minute{(timeSpan.Minutes == 1 ? "" : "s")}",
                timeSpan.Seconds == 0 ? null : $"{timeSpan.Seconds} second{(timeSpan.Seconds == 1 ? "" : "s")}",
                complete 
                    ? $"elapsed. Complete: {(DateTime.Now):h:mm:ss tt}" 
                    : timeSpan.Days == 0
                    ? $"remaining. Expected completion: {(DateTime.Now + timeSpan).ToLongTimeString()}"
                    : $"remaining. Expected completion: {(DateTime.Now + timeSpan).ToLongTimeString()} on {(DateTime.Now + timeSpan).ToLongDateString()}",
            };
            return string.Join(" ", segments.Where(s => s != null));
        }

        private List<DateTime> _referenceTimes = new List<DateTime>();
        private int _referenceTimeValueIncrement = 1;

        // A textual representation of the time remaining.
        private string _timeLeftString;
        public string TimeLeftString
        {
            get => _timeLeftString;
            set => SetFieldValue(ref _timeLeftString, value, nameof(TimeLeftString));
        }

        // Fraction of progress completed.
        private double _percent;
        public double Percent
        {
            get => _percent;
            set => SetFieldValue(ref _percent, value, nameof(Percent));
        }

        // Set the progress back to zero.
        public void Reset(string description = null)
        {
            var elapsed = _referenceTimes.Any() ? DateTime.Now - _referenceTimes.First() : TimeSpan.Zero;
            Reset(elapsed, description);
        }

        // Set the progress back to zero and also set the description of the nest chunk of work.
        public void Reset(TimeSpan elapsed, string description = null)
        {
            if (!_updateProgress)
            {
                return;
            }

            // Update the bound properties explicitly on the Dispatcher thread so that this method can be called from a background task.
            // Application.Current might be null when the application is in the process of quitting.
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Description = description;
                Value = 0;
                if (elapsed > TimeSpan.Zero)
                {
                    TimeLeftString = GetTimeString(elapsed, true);
                }
            });
        }

        public void Reset(string description, int maximum)
        {
            if (!_updateProgress)
            {
                return;
            }

            // Update the bound properties explicitly on the Dispatcher thread so that this method can be called from a background task.
            // Application.Current might be null when the application is in the process of quitting.
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Description = description;
                Maximum = maximum;
                Value = 0;
            });
        }

        public void Increment()
        {
            if (!_updateProgress)
            {
                return;
            }

            // Update the bound properties explicitly on the Dispatcher thread so that this method can be called from a background task.
            // Application.Current might be null when the application is in the process of quitting.
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Value = Value + 1;
            });
        }
    }
}
