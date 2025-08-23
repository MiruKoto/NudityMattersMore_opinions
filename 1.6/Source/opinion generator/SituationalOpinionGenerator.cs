using Verse;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using rjw;
using NudityMattersMore; // To access InfoHelper, PawnInteractionManager, DressState, InteractionType, PawnState, TabGender

namespace NudityMattersMore_opinions
{

    /// <summary>
    /// Class for dynamic generation of situational opinions.
    /// Assembles an opinion from various "tokens" (text templates),
    /// selecting the appropriate parts based on current conditions.
    /// </summary>
    public static class SituationalOpinionGenerator
    {
        // +++ NEW CODE: List of interactions that inherently involve nudity +++
        /// <summary>
        /// This list contains interactions that will cause the pawn to be considered naked,
        /// even if it technically still has clothes on. You can easily expand this list.
        /// </summary>
        private static readonly HashSet<InteractionType> inherentlyNudeInteractions = new HashSet<InteractionType>
        {
            InteractionType.Shower,
            InteractionType.Bath,
            InteractionType.Sauna,
            InteractionType.Swimming,
            InteractionType.HotTub,
            InteractionType.Sex,
            InteractionType.Rape,
            InteractionType.Raped,
            InteractionType.MedicalFull,
            InteractionType.MedicalFullSelf,
            InteractionType.Surgery,
            InteractionType.Biopod,
            InteractionType.HumanArtFull
            // Add other interaction types here if needed
        };
        // +++ END OF NEW CODE +++
        private static System.Random random = new System.Random();

        /// <summary>
        /// Main method for generating a complete situational opinion.
        /// Assembles a sentence from various tokens based on the provided data.
        /// </summary>
        /// <param name="observer">Observer pawn.</param>
        /// <param name="observed">Observed pawn.</param>
        /// <param name="interactionType">Interaction type.</param>
        /// <param name="pawnState">Observed pawn state.</param>
        /// <param name="aware">Does observed pawn was aware.</param>
        /// <param name="isSelfObservation">True, if its self observation.</param>
        /// <param name="opinionCategory">Opinion category (Positive, Negative, Neutral).</param>
        /// <param name="visibleBodyParts">List of visible body parts.</param>
        /// <param name="currentPerspective">Current opinion perspective (Observer, Observed, Self).</param>
        /// <returns>Generated opinion text.</returns>
        /// 
        public static string GenerateFullOpinion(
            Pawn observer,
            Pawn observed,
            InteractionType interactionType,
            PawnState pawnState,
            bool aware,
            bool isSelfObservation,
            OpinionCategory opinionCategory,
            List<Tuple<BodyPartDef, float, string, GenitalFamily>> visibleBodyParts, // PartDef, Severity, SizeLabel, GenitalFamily
            OpinionPerspective currentPerspective // Added perspective parameter
        )
        {
            List<string> parts = new List<string>();

            // 1. Sentence begining (Observer_opinionEntry)
            var startDefs = DefDatabase<OpinionStartDef>.AllDefsListForReading
                            .Where(d => d.texts.Any() && (d.perspective == OpinionPerspective.Any || d.perspective == currentPerspective))
                            .ToList();
            if (startDefs.Any())
            {
                string start = startDefs.RandomElement().texts.RandomElement();
                parts.Add(SituationalOpinionHelper.ProcessOpinionText(start, observer, observed));
            }


            // 2. Token describing the pawn that is the object of attention in this opinion.
            // If the perspective is "UsedForObserved", then we describe the observer who saw the log owner.
            // Otherwise, we describe the observed pawn (or the log owner himself during self-observation).
            string focalPawnDesc = "";
            Pawn focalPawn = null;

            if (currentPerspective == OpinionPerspective.UsedForObserved)
            {
                focalPawn = observer; // The owner of the log was observed, so we describe the observer
            }
            else
            {
                focalPawn = observed; // The owner of the log was an observer or this is self-observation, we describe the observed/ourselves
            }

            if (focalPawn != null)
            {
                focalPawnDesc += focalPawn.LabelShort;

                // Add a faction if the pawn is not from the colony
                if (focalPawn.Faction != null && !focalPawn.IsColonist)
                {
                    focalPawnDesc += $" from {focalPawn.Faction.Name}";
                }
            }
            if (!string.IsNullOrEmpty(focalPawnDesc))
            {
                parts.Add(focalPawnDesc);
            }


            // 3. Personal opinion token (personal_reaction_positive/neutral/negative)
            var personalReactionDefs = DefDatabase<PersonalReactionDef>.AllDefsListForReading
                                       .Where(d => d.opinionCategory == opinionCategory && d.texts.Any() && (d.perspective == OpinionPerspective.Any || d.perspective == currentPerspective))
                                       .ToList();
            if (personalReactionDefs.Any())
            {
                string reaction = personalReactionDefs.RandomElement().texts.RandomElement();
                parts.Add(SituationalOpinionHelper.ProcessOpinionText(reaction, observer, observed));
            }


            // 4 - Nudity state token +++
            string nudityStatusText = "";
            // Checking if the interaction is "naked in its essence"
            bool isInherentlyNudeAction = inherentlyNudeInteractions.Contains(interactionType);
            // Get the actual status of the pawn's clothes
            string actualNudityStatus = OpinionHelper.GetNudityStatus(observed);

            // If the interaction is "naked" but the pawn is formally "dressed", we override the status.
            if (isInherentlyNudeAction && actualNudityStatus.Equals("clothed", StringComparison.OrdinalIgnoreCase))
            {
                // We say that she is "naked", and the context (eg "in the shower") will be added in point 5.
                nudityStatusText = "nude";
            }
            else
            {
                // Otherwise, we use standard logic to get the status (naked, topless, covering, etc.)
                nudityStatusText = SituationalOpinionHelper.ProcessOpinionText("{OBSERVED_nudityAndCoveringStatus}", observer, observed);
            }

            if (!string.IsNullOrEmpty(nudityStatusText))
            {
                string pronounSubject = (currentPerspective == OpinionPerspective.UsedForObserved) ? "I" : (observed?.gender.GetPronoun() ?? "they");
                parts.Add($"{pronounSubject} was {nudityStatusText}");
            }


            // 5. Token, check for action (requiredInteractionType)
            var interactionDefs = DefDatabase<InteractionOpinionDef>.AllDefsListForReading
                                  .Where(d => d.interactionType == interactionType && d.texts.Any() && (d.perspective == OpinionPerspective.Any || d.perspective == currentPerspective))
                                  .ToList();
            if (interactionType != InteractionType.None && interactionDefs.Any())
            {
                string interactionDesc = interactionDefs.RandomElement().texts.RandomElement();
                parts.Add(SituationalOpinionHelper.ProcessOpinionText(interactionDesc, observer, observed));
            }


            // 6. Token, check for body parts.
            // For each visible body part, generate a description
            foreach (var partTuple in visibleBodyParts)
            {
                BodyPartDef partDef = partTuple.Item1;
                float severity = partTuple.Item2;
                string sizeLabel = partTuple.Item3;
                GenitalFamily genitalFamily = partTuple.Item4;

                // Replace tokens in sizeLabel
                string processedSizeLabel = SituationalOpinionHelper.ProcessOpinionText(sizeLabel, observer, observed);
                string processedPartSpecificLabel;

                if (partDef.defName == "Genitals" || partDef.defName == "Anus" || partDef.defName == "Breasts" || partDef.defName == "Chest")
                {
                    // For genitals and breasts we use a new token for a specific body part name
                    processedPartSpecificLabel = SituationalOpinionHelper.ProcessOpinionText("{OBSERVED_genitalSpecificLabel}", observer, observed, partDef);
                }
                else
                {
                    // For other body parts we use a regular LabelCap
                    processedPartSpecificLabel = SituationalOpinionHelper.ProcessOpinionText(partDef.LabelCap, observer, observed);
                }

                // Select a template based on GenitalFamily, size and opinion category
                var bodyPartOpinionDefs = DefDatabase<BodyPartOpinionDef>.AllDefsListForReading
                                          .Where(d => d.targetBodyPart == partDef &&
                                                      (d.genitalFamily == GenitalFamily.Undefined || d.genitalFamily == genitalFamily) &&
                                                      d.opinionCategory == opinionCategory &&
                                                      (severity >= d.severityRange.min && severity <= d.severityRange.max) &&
                                                      d.texts.Any() &&
                                                      (d.perspective == OpinionPerspective.Any || d.perspective == currentPerspective))
                                          .ToList();

                if (bodyPartOpinionDefs.Any())
                {
                    string partDesc = bodyPartOpinionDefs.RandomElement().texts.RandomElement();
                    // Replace specific tokens for body part
                    partDesc = partDesc.Replace("{OBSERVED_partSpecificLabel}", processedPartSpecificLabel);
                    partDesc = partDesc.Replace("{OBSERVED_sizeLabel}", processedSizeLabel);
                    parts.Add(SituationalOpinionHelper.ProcessOpinionText(partDesc, observer, observed));
                }
            }


            // 7. Token, summing up (observer_observation_opinion)
            var conclusionDefs = DefDatabase<ConclusionOpinionDef>.AllDefsListForReading
                                 .Where(d => d.opinionCategory == opinionCategory && d.texts.Any() && (d.perspective == OpinionPerspective.Any || d.perspective == currentPerspective))
                                 .ToList();
            if (conclusionDefs.Any())
            {
                string conclusion = conclusionDefs.RandomElement().texts.RandomElement();
                parts.Add(SituationalOpinionHelper.ProcessOpinionText(conclusion, observer, observed));
            }

            // We collect all parts into one sentence, removing empty or duplicate spaces
            string fullOpinion = string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();


            // Additional processing for formatting:
            // - Make sure the first word starts with a capital letter.
            // - Add a period at the end if there is none.
            if (!string.IsNullOrEmpty(fullOpinion))
            {
                fullOpinion = char.ToUpper(fullOpinion[0]) + fullOpinion.Substring(1);
                if (!fullOpinion.EndsWith(".") && !fullOpinion.EndsWith("!") && !fullOpinion.EndsWith("?"))
                {
                    fullOpinion += ".";
                }
            }

            return fullOpinion;
        }
    }
}
