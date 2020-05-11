using System;

namespace SharedClustering.Core
{
    /// <summary>
    /// A progress bar that updates on the Dispatcher thread.
    /// Includes settable textual descriptions as well as numeric estimates of completion time.
    /// </summary>
    public interface IProgressData
    {
        // Textual description, settable by the client.
        string Description { get; set; }

        // Set the progress back to zero.
        void Reset(string description = null);

        // Set the progress back to zero and also set the description of the nest chunk of work.
        void Reset(TimeSpan elapsed, string description = null);

        void Reset(string description, int maximum);

        void Increment();
    }
}
