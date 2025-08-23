using Verse;
using System.Collections.Generic;

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// Definition for the initial phrases of situational opinions.
    /// </summary>
    public class OpinionStartDef : Def
    {
        /// <summary>
        /// List of possible texts to begin an opinion.
        /// </summary>
        public List<string> texts = new List<string>();

        /// <summary>
        /// The perspective to which this principle of opinion belongs (observer, observed, self).
        /// Allows you to have different introductions for different perspectives.
        /// </summary>
        public OpinionPerspective perspective = OpinionPerspective.Any;
    }
}
