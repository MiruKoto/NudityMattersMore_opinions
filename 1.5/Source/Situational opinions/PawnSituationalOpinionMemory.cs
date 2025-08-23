using Verse; 
using System.Collections.Generic; 
using System.Linq; 

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// A class for storing remembered situational opinions for a particular pawn.
    /// It acts as a journal, storing a limited number of the most recent entries.
    /// </summary>
    public class PawnSituationalOpinionMemory : IExposable
    {

        // The maximum number of entries that should be stored in the log.
        private const int MaxLogEntries = 20;


        // List of all log entries for this pawn.
        // Use List<OpinionLogEntry> to store the entries.
        public List<OpinionLogEntry> LogEntries = new List<OpinionLogEntry>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PawnSituationalOpinionMemory() { }

        /// <summary>
        /// Adds a new entry to the opinion log.
        /// If the number of entries exceeds MaxLogEntries, the oldest entries are removed.
        /// Entries are added to the beginning of the list so that the most recent ones are always first.
        /// </summary>
        /// <param name="entry">The opinion entry to add.</param>
        public void AddEntry(OpinionLogEntry entry)
        {
            if (entry == null) return;

            // Add a new entry to the top of the list (the most recent one)
            LogEntries.Insert(0, entry);

            // Delete old records if limit is exceeded
            if (LogEntries.Count > MaxLogEntries)
            {
                LogEntries.RemoveRange(MaxLogEntries, LogEntries.Count - MaxLogEntries);
            }
        }

        /// <summary>
        /// Returns a list of log entries.
        /// The entries are already sorted by time descending (most recent first)
        /// by using Insert(0, entry).
        /// </summary>
        /// <returns>A list of OpinionLogEntry.</returns>
        public List<OpinionLogEntry> GetRecentLogEntries()
        {
            // Return a copy of the list to avoid external modifications.
            // Sorting by GameTick is not needed, since Insert(0, ...) already guarantees order.
            return new List<OpinionLogEntry>(LogEntries);
        }

        /// <summary>
        /// Method for serializing/deserializing opinion memory data.
        /// </summary>
        public void ExposeData()
        {
            Scribe_Collections.Look(ref LogEntries, "logEntries", LookMode.Deep);

            // After loading, make sure the list doesn't exceed MaxLogEntries
            // and that it's sorted by GameTick (in case the order got messed up when saving/loading
            // or if the entries weren't added via AddEntry).
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (LogEntries == null)
                {
                    LogEntries = new List<OpinionLogEntry>();
                }
                else if (LogEntries.Count > MaxLogEntries)
                {

                    // Sort by GameTick (descending) and take only the required amount
                    LogEntries = LogEntries.OrderByDescending(e => e.GameTick).Take(MaxLogEntries).ToList();
                }
            }
        }
    }
}
