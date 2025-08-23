using Verse;
using System.Collections.Generic;
using rjw; // for GenitalFamily access

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// Definition for describing body parts in situational opinions.
    /// </summary>
    public class BodyPartOpinionDef : Def
    {
        /// <summary>
        /// The body part to which the description applies (e.g. Genitals, Chest, Anus).
        /// </summary>
        public BodyPartDef targetBodyPart;

        /// <summary>
        /// The family of genitalia (Penis, Vagina, Breasts, Anus) to which this description belongs.
        /// </summary>
        public GenitalFamily genitalFamily;

        /// <summary>
        /// Opinion category (Positive, Negative, Neutral).
        /// </summary>
        public OpinionCategory opinionCategory;

        /// <summary>
        /// The severity range for the body part this description applies to.
        /// </summary>
        public FloatRange severityRange;

        /// <summary>
        /// List of possible texts to describe a body part.
        /// </summary>
        public List<string> texts = new List<string>();

        /// <summary>
        /// The perspective to which this description applies.
        /// </summary>
        public OpinionPerspective perspective = OpinionPerspective.Any;
    }
}
