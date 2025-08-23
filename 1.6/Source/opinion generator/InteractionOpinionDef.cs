using Verse;
using System.Collections.Generic;
using NudityMattersMore; // To access InteractionType

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// Definition for descriptions of interactions in situational opinions.
    /// </summary>
    public class InteractionOpinionDef : Def
    {
        /// <summary>
        /// The type of interaction this description applies to.
        /// </summary>
        public InteractionType interactionType;

        /// <summary>
        /// List of possible texts to describe the interaction.
        /// </summary>
        public List<string> texts = new List<string>();

        /// <summary>
        /// The perspective to which this description applies.
        /// </summary>
        public OpinionPerspective perspective = OpinionPerspective.Any;
    }
}
