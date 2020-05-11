using System;

namespace SharedClustering.Core
{
    /// <summary>
    /// A progress tracker that does nothing
    /// </summary>
    public class SuppressProgress : IProgressData
    {
        public string Description { get; set; }
        public void Reset(string description = null) { }
        public void Reset(TimeSpan elapsed, string description = null) { }
        public void Reset(string description, int maximum) { }
        public void Increment() { }
    }
}
