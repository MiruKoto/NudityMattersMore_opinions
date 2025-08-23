using Verse;
using System.Collections.Generic;

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// Definition for the final phrases of situational opinions.
    /// </summary>
    public class ConclusionOpinionDef : Def
    {
        /// <summary>
        /// The opinion category (Positive, Negative, Neutral) to which this opinion belongs.
        /// </summary>
        public OpinionCategory opinionCategory;

        /// <summary>
        /// List of possible texts for the conclusion.
        /// </summary>
        public List<string> texts = new List<string>();

        /// <summary>
        /// The perspective to which this conclusion relates.
        /// </summary>
        public OpinionPerspective perspective = OpinionPerspective.Any;
    }
}
