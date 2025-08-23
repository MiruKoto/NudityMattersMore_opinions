using Verse; 
using NudityMattersMore; 
using RimWorld; 
using System; 

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// A class representing a single entry in the pawn's situational opinion log.
    /// It stores information about the interaction, including the pawns involved,
    /// the interaction type, its state, and the opinion text.
    /// </summary>
    public class OpinionLogEntry : IExposable
    {
        // Observer pawn (if this is an observer log)
        public Pawn ObserverPawn;
        // Observed pawn (if this is an observable log)
        public Pawn ObservedPawn;
        // Opinion text that will be displayed in the log
        public string OpinionText;
        // Game timestamp when the interaction happened (reserved for Scribe, but not used in output)
        public int GameTick;
        // Interaction type (from NMM.InteractionType)
        public InteractionType InteractionType;
        // Pawn state during interaction (from NMM.PawnState)
        public PawnState PawnState;
        // Indicates whether the opinion was positive, negative, or neutral
        public OpinionCategory OpinionCategory;
        // Flag indicating whether the observed pawn was aware that it was seen
        public bool Aware;
        // Flag indicating whether the entry is a result of introspection
        public bool IsSelfOpinion;
        // Flag indicating whether this opinion is generated from the observer (true) or the observable (false)
        public bool IsObserverPerspective;

        /// <summary>
        /// Default constructor for Scribe (serialization/deserialization).
        /// </summary>
        public OpinionLogEntry() { }

        /// <summary>
        /// Basic constructor for creating a new opinion log entry.
        /// </summary>
        /// <param name="observerPawn">The pawn that observed.</param>
        /// <param name="observedPawn">The pawn that was observed.</param>
        /// <param name="opinionText">The generated opinion text.</param>
        /// <param name="gameTick">Current game timestamp (for Scribe, not for output).</param>
        /// <param name="interactionType">Interaction type (e.g. Naked, Topless).</param>
        /// <param name="pawnState">State of the pawn being observed (e.g. Asleep, Uncaring).</param>
        /// <param name="opinionCategory">Opinion category (Positive, Negative, Neutral).</param>
        /// <param name="aware">Whether the observed pawn was conscious during the interaction.</param>
        /// <param name="isSelfOpinion">Whether this is an introspection.</param>
        /// <param name="isObserverPerspective">Whether this is an observer perspective.</param>
        public OpinionLogEntry(Pawn observerPawn, Pawn observedPawn, string opinionText, int gameTick,
                               InteractionType interactionType, PawnState pawnState, OpinionCategory opinionCategory,
                               bool aware, bool isSelfOpinion, bool isObserverPerspective)
        {
            this.ObserverPawn = observerPawn;
            this.ObservedPawn = observedPawn;
            this.OpinionText = opinionText;
            this.GameTick = gameTick; 
            this.InteractionType = interactionType;
            this.PawnState = pawnState;
            this.OpinionCategory = opinionCategory;
            this.Aware = aware;
            this.IsSelfOpinion = isSelfOpinion;
            this.IsObserverPerspective = isObserverPerspective;
        }

        /// <summary>
        /// Method for serializing/deserializing log entry data.
        /// Used by RimWorld to save and load mod data.
        /// </summary>
        public void ExposeData()
        {
            Scribe_References.Look(ref ObserverPawn, "observerPawn");
            Scribe_References.Look(ref ObservedPawn, "observedPawn");
            Scribe_Values.Look(ref OpinionText, "opinionText");
            Scribe_Values.Look(ref GameTick, "gameTick", 0); // Сохраняем GameTick
            Scribe_Values.Look(ref InteractionType, "interactionType", InteractionType.None);
            Scribe_Values.Look(ref PawnState, "pawnState", PawnState.None);
            Scribe_Values.Look(ref OpinionCategory, "opinionCategory", OpinionCategory.Neutral);
            Scribe_Values.Look(ref Aware, "aware", false);
            Scribe_Values.Look(ref IsSelfOpinion, "isSelfOpinion", false);
            Scribe_Values.Look(ref IsObserverPerspective, "isObserverPerspective", false);
        }

        /// <summary>
        /// Returns a formatted string representing the log entry to display.
        /// Now undated, as a simple opinion.
        /// </summary>
        public string GetFormattedLogString()
        {
            string colorTag = "<color=white>"; // Default color

            // Determine color based on opinion category
            if (OpinionCategory == OpinionCategory.Positive)
            {
                colorTag = "<color=#99FF99>"; // Light green
            }
            else if (OpinionCategory == OpinionCategory.Negative)
            {
                colorTag = "<color=#FF9999>"; // Light red
            }
            else // Neutral
            {
                colorTag = "<color=#FFFF99>"; // Light yellow
            }

            // The actual pawn whose log this is (the one who's "thinking" or "experiencing")
            Pawn logOwner = IsObserverPerspective ? ObserverPawn : ObservedPawn;

            // The "other" pawn in the interaction
            Pawn otherPawn = IsObserverPerspective ? ObservedPawn : ObserverPawn;

            // If this is introspection, the text should already be correct.
            if (IsSelfOpinion)
            {
                return $"{colorTag}{OpinionText}</color>";
            }

            // For external observation/being observed, prepend with the involved pawns
            string prefix = IsObserverPerspective
                ? $"{logOwner.LabelShort} observed {otherPawn.LabelShort}: "
                : $"{otherPawn.LabelShort} observed {logOwner.LabelShort}: ";

            return $"{colorTag}{prefix}{OpinionText}</color>";
        }
    }
}
