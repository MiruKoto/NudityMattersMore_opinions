using Verse;
using System.Collections.Generic;

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// Definition for personal reactions of a pawn to a situational opinion.
    /// </summary>
    public class PersonalReactionDef : Def
    {
        /// <summary>
        /// The opinion category (Positive, Negative, Neutral) to which this reaction belongs.
        /// </summary>
        public OpinionCategory opinionCategory;

        /// <summary>
        /// List of possible texts for personal reaction.
        /// </summary>
        public List<string> texts = new List<string>();

        /// <summary>
        /// The perspective to which this reaction relates.
        /// </summary>
        public OpinionPerspective perspective = OpinionPerspective.Any;
    }
}
