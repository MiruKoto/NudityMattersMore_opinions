using Verse; 
using System.Collections.Generic; 

namespace NudityMattersMore_opinions
{

    /// <summary>
    /// A Def class for defining situational opinions.
    /// Each opinion definition can have multiple text variants
    /// and be associated with a set of conditions via OpinionConditionExtension_Situational.
    /// Can also use IsBaseOpinionExtension to mark as a fallback.
    /// </summary>
    public class OpinionDef_Situational : Def
    {

        // List of possible texts for this opinion. One will be chosen randomly.
        public List<string> opinionTexts = new List<string>();

        // opinion category (Positive, Negative, Neutral).
        public OpinionCategory opinionCategory;

        /// <summary>
        /// Method to get the condition extension attached to this Def.
        /// </summary>
        /// <returns>OpinionConditionExtension_Situational or null if not attached.</returns>
        public OpinionConditionExtension_Situational GetConditionExtension() => this.GetModExtension<OpinionConditionExtension_Situational>();
    }
}