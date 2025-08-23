using NudityMattersMore;
using NudityMattersMore_opinions.CalculationHelpers;
using RimWorld;
using rjw; 
using rjw.Modules.Shared;
using rjw.Modules.Shared.Extensions; 
using System; 
using System.Collections.Generic; 
using System.Linq; 
using UnityEngine;
using Verse;
using Verse.Sound;
// Using the correct namespace for Vanilla TraitDefOf
using VanillaTraitDefOf = RimWorld.TraitDefOf;



namespace NudityMattersMore_opinions
{
    // Class for storing data about remembered opinions
    // This will be a persistent storage for each observer pawn
    public class PawnOpinionMemory : IExposable
    {
        // Each field is declared on a separate line with correct initialization.
        public Dictionary<Pawn, Dictionary<BodyPartDef, PartOpinionData>> RememberedOpinions = new Dictionary<Pawn, Dictionary<BodyPartDef, PartOpinionData>>();
        public Dictionary<Pawn, Dictionary<BodyPartDef, bool>> AnusOpinionRegistered = new Dictionary<Pawn, Dictionary<BodyPartDef, bool>>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref RememberedOpinions, "rememberedOpinions", LookMode.Reference, LookMode.Deep);
            Scribe_Collections.Look(ref AnusOpinionRegistered, "anusOpinionRegistered", LookMode.Reference, LookMode.Deep);
        }

        public PartOpinionData GetOpinionData(Pawn targetPawn, BodyPartDef part)
        {
            if (RememberedOpinions.TryGetValue(targetPawn, out var partOpinions) && partOpinions.TryGetValue(part, out var opinionData))
            {
                return opinionData;
            }
            return null;
        }

        public void SetOpinionData(Pawn targetPawn, BodyPartDef part, PartOpinionData opinionData)
        {
            if (!RememberedOpinions.ContainsKey(targetPawn))
            {
                RememberedOpinions[targetPawn] = new Dictionary<BodyPartDef, PartOpinionData>();
            }
            RememberedOpinions[targetPawn][part] = opinionData;
        }

        public bool IsAnusOpinionRegistered(Pawn targetPawn, BodyPartDef part)
        {
            if (AnusOpinionRegistered.TryGetValue(targetPawn, out var partRegistrations) && partRegistrations.TryGetValue(part, out var registered))
            {
                return registered;
            }
            return false;
        }

        public void SetAnusOpinionRegistered(Pawn targetPawn, BodyPartDef part, bool registered)
        {
            if (!AnusOpinionRegistered.ContainsKey(targetPawn))
            {
                AnusOpinionRegistered[targetPawn] = new Dictionary<BodyPartDef, bool>();
            }
            AnusOpinionRegistered[targetPawn][part] = registered;
        }
    }

    // Class for storing data about an opinion about a body part (including size)
    public class PartOpinionData : IExposable
    {
        public string OpinionText;
        public float Severity;
        public string SizeLabel;
        public string PartSpecificLabel; // "Vagina", "Penis", "Breasts", "Anus"
        public int LastSeenTick; // To track when it was last seen for update logic

        public PartOpinionData() { } // Parameterless constructor for Scribe

        public PartOpinionData(string opinionText, float severity, string sizeLabel, string partSpecificLabel, int lastSeenTick)
        {
            OpinionText = opinionText;
            Severity = severity;
            SizeLabel = sizeLabel;
            PartSpecificLabel = partSpecificLabel;
            LastSeenTick = lastSeenTick;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref OpinionText, "opinionText");
            Scribe_Values.Look(ref Severity, "severity");
            Scribe_Values.Look(ref PartSpecificLabel, "partSpecificLabel");
            Scribe_Values.Look(ref SizeLabel, "sizeLabel");
            Scribe_Values.Look(ref LastSeenTick, "lastSeenTick");
        }
    }

    // New Def for defining opinions about sex parts with severity ranges
    public class OpinionDef_SexPart : Def
    {
        public BodyPartDef targetBodyPart; // The body part this opinion applies to (e.g., Genitals, Chest, Anus)
        public OpinionCategory opinionCategory; // The category of opinion (Positive, Negative, Neutral)
        public FloatRange severityRange; // The severity range for this specific opinion
        public List<string> opinionTexts = new List<string>(); // Changed to List<string> to hold multiple texts

        // Added: To specify if the opinion applies to Penis, Vagina, Breasts, Anus, etc.
        public GenitalFamily genitalFamily;

        // To store conditions from ModExtension
        public OpinionConditionExtension GetConditionExtension() => this.GetModExtension<OpinionConditionExtension>();
    }


    // Main class for handling opinion logic
    public static class OpinionHelper
    {
        // A dictionary for storing the opinion memory for each observer pawn
        private static Dictionary<Pawn, PawnOpinionMemory> pawnOpinionMemories = new Dictionary<Pawn, PawnOpinionMemory>();
        private static System.Random random = new System.Random();

        // Cached PreceptDefs for "Privacy, Please!" mod
        private static PreceptDef Exhibitionism_Acceptable;
        private static PreceptDef Exhibitionism_Approved;
        private static PreceptDef Exhibitionism_Disapproved;

        // Static constructor to initialize PreceptDefs
        static OpinionHelper()
        {
            // Try to load PreceptDefs from "Privacy, Please!" mod if it exists
            if (ModLister.HasActiveModWithName("Privacy, Please!"))
            {
                Exhibitionism_Acceptable = DefDatabase<PreceptDef>.GetNamedSilentFail("Exhibitionism_Acceptable");
                Exhibitionism_Approved = DefDatabase<PreceptDef>.GetNamedSilentFail("Exhibitionism_Approved");
                Exhibitionism_Disapproved = DefDatabase<PreceptDef>.GetNamedSilentFail("Exhibitionism_Disapproved");
            }
        }


        // Method to get or create opinion memory for a pawn
        public static PawnOpinionMemory GetOrCreatePawnOpinionMemory(Pawn observer)
        {
            if (!pawnOpinionMemories.ContainsKey(observer))
            {
                pawnOpinionMemories[observer] = new PawnOpinionMemory();
            }
            return pawnOpinionMemories[observer];
        }


        // Method to determine visibility type from NMM
        public static string GetNudityStatus(Pawn pawn)
        {
            if (pawn == null) return "Unknown";

            bool topless = NudityMattersMore.InfoHelper.IsTopless(pawn);
            bool bottomless = NudityMattersMore.InfoHelper.IsBottomless(pawn);

            if (topless && bottomless)
            {
                // Check if the pawn is 'covering' using NMM logic.
                // This might be reflected in PawnInteractionManager.cs through 'CoverBody' hediff or 'IsCovering'
                if (NudityMattersMore.InfoHelper.IsCovering(pawn, out _))
                {
                    return "Fully nude (covering)";
                }
                return "Fully nude";
            }
            else if (topless)
            {
                return "Breasts exposed"; // Assumed 'topless' means exposed breasts
            }
            else if (bottomless)
            {
                return "Genitals exposed"; // Assumed 'bottomless' means exposed genitals
            }

            // Check for 'NipSlip' or 'BreastSlip' (from PawnInteractionManager)
            Hediff slipHediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("NipSlip"));
            if (slipHediff != null)
            {
                float severity = slipHediff.Severity;
                if (severity > NudityMattersMore.NipSlip.hediffSlipC)
                    return "Breast slip";
                else if (severity > NudityMattersMore.NipSlip.hediffSlipB)
                    return "Nip slip";
            }

            return "Clothed";
        }


        // Method to get the header depending on the Observer/Observed mode
        public static string GetHeaderLabel(Pawn currentPawn, Pawn targetPawn, bool isObserverMode)
        {
            string currentPawnName = currentPawn?.LabelCap ?? "Selected Pawn"; // Use LabelCap for main header
            string targetPawnName = targetPawn?.LabelCap ?? "Pawn from list"; // Use LabelCap for main header

            if (targetPawn == null)
            {
                return "Opinion about body of selected pawn:";
            }

            if (isObserverMode)
            {
                // (Pawn name) thoughts about (Pawn Selected From List) body.
                return $"{currentPawnName}'s thoughts about {targetPawnName}'s body:";
            }
            else
            {
                // (Pawn Selected From List) thoughts about (Pawn name) body.
                return $"{targetPawnName}'s thoughts about {currentPawnName}'s body:";
            }
        }


        // Method for obtaining an opinion about a body part given its size and type (for genitals)
        public static PartOpinionData GetOpinionDataForPart(Pawn observer, Pawn observed, BodyPartDef part, PawnOpinionMemory memory, string nudityStatus)
        {
            Hediff rjwPartHediff = null;
            float currentSeverity = 0f;
            string currentSizeLabel = "N/A";
            GenitalFamily targetGenitalFamily = GenitalFamily.Undefined; // Track the detected genital family
            string partSpecificLabel = part.LabelCap; // Default part label (e.g., "Genitals", "Chest", "Anus")

            if (ModLister.HasActiveModWithName("RimJobWorld"))
            {
                ISexPartHediff sexHediff = null;
                List<Hediff> allSexHediffsOnPawn = Genital_Helper.get_AllPartsHediffList(observed); // Using get_AllPartsHediffList

                // Фильтрация по GenitalFamily
                if (part.defName == "Genitals")
                {

                    // For Genitals, first try to find Vagina, then Penis, taking into account gender
                    sexHediff = allSexHediffsOnPawn.OfType<ISexPartHediff>().FirstOrDefault(h =>
                        (observed.gender == Gender.Female && (h.Def.genitalFamily == GenitalFamily.Vagina || h.Def.genitalFamily == GenitalFamily.FemaleOvipositor)) ||
                        (h.Def.genitalFamily == GenitalFamily.Vagina)); // Priority to vagina for women or if it's futa/trap

                    if (sexHediff == null) // If vagina not found or pawn is male
                    {
                        sexHediff = allSexHediffsOnPawn.OfType<ISexPartHediff>().FirstOrDefault(h =>
                            (observed.gender == Gender.Male && (h.Def.genitalFamily == GenitalFamily.Penis || h.Def.genitalFamily == GenitalFamily.MaleOvipositor)) ||
                            (h.Def.genitalFamily == GenitalFamily.Penis)); // Then looking for penis
                    }
                }
                else if (part.defName == "Anus")
                {
                    sexHediff = allSexHediffsOnPawn.OfType<ISexPartHediff>().FirstOrDefault(h => h.Def.genitalFamily == GenitalFamily.Anus);
                }
                else if (part.defName == "Breasts" || part.defName == "Chest") 
                {
                    sexHediff = allSexHediffsOnPawn.OfType<ISexPartHediff>().FirstOrDefault(h => h.Def.genitalFamily == GenitalFamily.Breasts);
                }
                // Add another BodyPartDef, if needed

                if (sexHediff != null)
                {
                    rjwPartHediff = sexHediff.AsHediff;
                    if (rjwPartHediff != null && rjwPartHediff.TryGetComp<HediffComp_SexPart>(out var sexComp))
                    {
                        currentSeverity = sexComp.GetSeverity();
                        targetGenitalFamily = sexHediff.Def.genitalFamily; // Defining found GenitalFamily
                        partSpecificLabel = sexHediff.Def.label; // We use the label from the Def of the part itself

                        // Try to get SizeLabel from RJW. If null or empty, generate our own.
                        if (!string.IsNullOrEmpty(sexComp.SizeLabel) && sexComp.SizeLabel != "N/A")
                        {
                            currentSizeLabel = sexComp.SizeLabel;
                        }
                        else
                        {
                            currentSizeLabel = GetDescriptiveSizeLabel(currentSeverity); // generate on severity base
                        }
                    }
                }
                else
                {
                    // If no specific sex hediff is found for a body part, provide a generic size label based on severity 0
                    currentSizeLabel = GetDescriptiveSizeLabel(0f); // Severity defaults to 0 if there is no specific hediff
                    // partSpecificLabel stays default (e.g. "Genitals", "Chest", "Anus")
                }
            }
            else
            {
                currentSizeLabel = "N/A (RJW not active)";
                // partSpecificLabel stays default
            }

            PartOpinionData rememberedOpinionData = memory.GetOpinionData(observed, part);
            bool partSeen = IsPartSeen(observer, observed, part);

            // Always consider own body parts as seen if observer == observed
            if (observer == observed)
            {
                partSeen = true;
            }

            if (!partSeen)
            {
                // Message when the body part has not been seen at all yet
                return new PartOpinionData("Not observed yet.", currentSeverity, currentSizeLabel, partSpecificLabel, Find.TickManager.TicksGame);
            }

            // Anus registration logic
            if (part.defName == "Anus" && !memory.IsAnusOpinionRegistered(observed, part))
            {
                // Get interaction profile for direct HasSeenBottom check
                string interactionKey = NudityMattersMore.PawnInteractionManager.GenerateKey(observer, observed);
                NudityMattersMore.PawnInteractionProfile interactionProfile = null;
                bool profileExists = NudityMattersMore.PawnInteractionManager.InteractionProfiles.TryGetValue(interactionKey, out interactionProfile);


                // Register anus as seen if profile exists and HasSeenBottom is active OR it is self-observation
                if ((profileExists && interactionProfile.HasSeenBottom) || observer == observed)
                {
                    memory.SetAnusOpinionRegistered(observed, part, true);
                }
                else
                {

                    // If the anus is not yet registered and the conditions are not met, return a wait message
                    return new PartOpinionData("Not observed yet (Anus: awaiting for nudity registration).", currentSeverity, currentSizeLabel, partSpecificLabel, Find.TickManager.TicksGame);
                }
            }
            

            bool needsUpdate = false;
            if (rememberedOpinionData == null ||
                Math.Abs(rememberedOpinionData.Severity - currentSeverity) > 0.05f ||
                rememberedOpinionData.SizeLabel != currentSizeLabel ||
                rememberedOpinionData.PartSpecificLabel != partSpecificLabel) // Checking also PartSpecificLabel
            {
                needsUpdate = true;
            }

            if (!needsUpdate && rememberedOpinionData != null)
            {
                return rememberedOpinionData;
            }

            OpinionCategory category = OpinionCategoryCalculator.GetCategory(observer, observed);

            string newOpinionText = GetSexPartOpinion(part, category, currentSeverity, targetGenitalFamily, observer, observed);

            PartOpinionData newOpinionData = new PartOpinionData(newOpinionText, currentSeverity, currentSizeLabel, partSpecificLabel, Find.TickManager.TicksGame);
            memory.SetOpinionData(observed, part, newOpinionData);

            return newOpinionData;
        }

        /// <summary>
        /// Helper method to get the current Need_Sex state of the pawn.
        /// Returns NeedSexState.Any if Need_Sex is missing or RJW is not active.
        /// </summary>
        private static NeedSexState GetNeedSexState(Pawn pawn)
        {
            if (!ModLister.HasActiveModWithName("RimJobWorld") || pawn.needs == null)
            {
                return NeedSexState.Any;
            }

            Need_Sex sexNeed = pawn.needs.TryGetNeed<Need_Sex>();
            if (sexNeed == null)
            {
                return NeedSexState.Any;
            }


            // Use CurLevel and thresholds from Need_Sex
            if (sexNeed.CurLevel <= sexNeed.thresh_frustrated())
            {
                return NeedSexState.Frustrated;
            }
            else if (sexNeed.CurLevel <= sexNeed.thresh_horny())
            {
                return NeedSexState.Horny;
            }
            else if (sexNeed.CurLevel <= sexNeed.thresh_neutral())
            {
                return NeedSexState.Neutral;
            }
            else if (sexNeed.CurLevel <= sexNeed.thresh_satisfied())
            {
                return NeedSexState.Satisfied;
            }
            else // CurLevel > thresh_satisfied(), что соответствует thresh_ahegao()
            {
                return NeedSexState.Ahegao;
            }
        }



        /// <summary>
        /// Selects opinion text about a sexual body part based on severity, genitalFamily, and additional conditions (traits, specific hediffs, genes, gender, life stage).
        /// Prioritizes opinions with more specific conditions.
        /// </summary>
        private static string GetSexPartOpinion(BodyPartDef part, OpinionCategory category, float currentSeverity, GenitalFamily genitalFamily, Pawn observer, Pawn observed)
        {
            // Determine if the current interaction is self-observation
            bool isCurrentSelfObservation = (observer == observed);

            // Get all matching opinion Defs for a given body part and category
            var candidateOpinions = DefDatabase<OpinionDef_SexPart>.AllDefsListForReading
                                    .Where(def => def.targetBodyPart == part &&
                                                  def.opinionCategory == category)
                                    .ToList();

            // NEW: Filter opinions based on whether it is self-observation.
            // Opinions with isSelfOpinion=true are only applied when self-observing.
            // Opinions with isSelfOpinion=false (default) are only applied when observing others.
            // This ensures strict isolation.
            candidateOpinions = candidateOpinions.Where(def => {
                OpinionConditionExtension ext = def.GetModExtension<OpinionConditionExtension>();
                if (ext == null) // If there is no extension, it is a general opinion, not self/other specific.
                {
                    return true; // Let it participate in further selection
                }
                return ext.isSelfOpinion == isCurrentSelfObservation;
            }).ToList();



            // Create a list to store opinions that match the conditions, with their "weight"
            List<(OpinionDef_SexPart def, int weight)> weighedOpinions = new List<(OpinionDef_SexPart def, int weight)>();

            // Collect reasons why the opinion was skipped for debugging
            List<string> debugFailedReasons = new List<string>();


            // Define a small epsilon for float comparison
            const float EPSILON = 0.0001f;

            foreach (var opinionDef in candidateOpinions)
            {
                int currentWeight = 0;
                OpinionConditionExtension ext = opinionDef.GetConditionExtension();

                bool passedAllConditions = true; // Flag to track whether all conditions are met

                // Severity range check should always be first
                // USING EPSILON TO COMPARE FLOAT
                if (!(currentSeverity >= (opinionDef.severityRange.min - EPSILON) && currentSeverity <= (opinionDef.severityRange.max + EPSILON)))
                {
                    debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Severity {currentSeverity:F4} not in fuzzy range [{opinionDef.severityRange.min:F4}~{opinionDef.severityRange.max:F4}] (actual: [{opinionDef.severityRange.min - EPSILON:F4}~{opinionDef.severityRange.max + EPSILON:F4}])");
                    passedAllConditions = false;
                }

                // Check for GenitalFamily compliance. This is the basic condition.
                if (passedAllConditions && opinionDef.genitalFamily != GenitalFamily.Undefined && opinionDef.genitalFamily != genitalFamily)
                {
                    debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': GenitalFamily mismatch (Expected: {opinionDef.genitalFamily}, Got: {genitalFamily})");
                    passedAllConditions = false;
                }

                // If genitalFamily is Undefined or the same, add the base weight
                if (passedAllConditions) // Add weight only if base conditions are met
                    currentWeight += 1; //  Weight for conformity to genital family(or lack thereof for common ones)

                        //  ModExtension check
                        if (ext != null && passedAllConditions) // Continue checking only if the basic conditions are met
                {
                    // Required Observer Trait
                    if (ext.requiredObserverTrait != null)
                    {
                        if (observer.story.traits.HasTrait(ext.requiredObserverTrait))
                        {
                            currentWeight += 5; // High weight
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observer missing trait {ext.requiredObserverTrait.defName}");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observed HediffDef
                    if (ext.requiredObservedHediffDef != null)
                    {
                        if (observed.health.hediffSet.HasHediff(ext.requiredObservedHediffDef))
                        {
                            currentWeight += 10; // Very high weight for a specific body part
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observed missing hediff {ext.requiredObservedHediffDef.defName}");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observed GeneDef
                    if (ext.requiredObservedGeneDef != null)
                    {
                        if (observed.genes != null && observed.genes.HasActiveGene(ext.requiredObservedGeneDef))
                        {
                            currentWeight += 8; // High weight for a specific gene
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observed missing gene {ext.requiredObservedGeneDef.defName}");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observer Gender
                    if (ext.requiredObserverGender.HasValue)
                    {
                        if (observer.gender == ext.requiredObserverGender.Value)
                        {
                            currentWeight += 4; // middle weight
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observer gender mismatch (Expected: {ext.requiredObserverGender.Value}, Got: {observer.gender})");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observed Gender
                    if (ext.requiredObservedGender.HasValue)
                    {
                        if (observed.gender == ext.requiredObservedGender.Value)
                        {
                            currentWeight += 4; // middle weight
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observed gender mismatch (Expected: {ext.requiredObservedGender.Value}, Got: {observed.gender})");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observer LifeStage
                    if (ext.requiredObserverLifeStage != null)
                    {
                        if (observer.ageTracker.CurLifeStage == ext.requiredObserverLifeStage)
                        {
                            currentWeight += 7; // High weight for observer life stage
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observer life stage mismatch (Expected: {ext.requiredObserverLifeStage.defName}, Got: {observer.ageTracker.CurLifeStage.defName})");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observed LifeStage
                    if (ext.requiredObservedLifeStage != null)
                    {
                        if (observed.ageTracker.CurLifeStage == ext.requiredObservedLifeStage)
                        {
                            currentWeight += 7; // High weight for the observed pawn's life stage
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observed life stage mismatch (Expected: {ext.requiredObservedLifeStage.defName}, Got: {observed.ageTracker.CurLifeStage.defName})");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observer Quirk
                    if (!string.IsNullOrEmpty(ext.requiredObserverQuirk))
                    {
                        Quirk targetQuirk = Quirk.All.FirstOrDefault(q =>
                            q.Key == ext.requiredObserverQuirk || q.LocaliztionKey == ext.requiredObserverQuirk);

                        if (targetQuirk != null && observer.Has(targetQuirk))
                        {
                            currentWeight += 6; // Weight for the presence of a quirk in the observer
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observer missing quirk {ext.requiredObserverQuirk}");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observer Minimum Biological Age
                    if (ext.minBiologicalAge > 0)
                    {
                        if (observer.ageTracker.AgeBiologicalYears >= ext.minBiologicalAge)
                        {
                            currentWeight += 3; // Middle weight
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observer too young ({observer.ageTracker.AgeBiologicalYears} < {ext.minBiologicalAge})");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observer Maximum Biological Age
                    if (ext.maxBiologicalAge > 0)
                    {
                        if (observer.ageTracker.AgeBiologicalYears <= ext.maxBiologicalAge)
                        {
                            currentWeight += 3; // Middle weight
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observer too old ({observer.ageTracker.AgeBiologicalYears} > {ext.maxBiologicalAge})");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observed Minimum Biological Age
                    if (ext.requiredObservedMinBiologicalAge > 0)
                    {
                        if (observed.ageTracker.AgeBiologicalYears >= ext.requiredObservedMinBiologicalAge)
                        {
                            currentWeight += 3; // Middle weight
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observed too young ({observed.ageTracker.AgeBiologicalYears} < {ext.requiredObservedMinBiologicalAge})");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observed Maximum Biological Age
                    if (ext.requiredObservedMaxBiologicalAge > 0)
                    {
                        if (observed.ageTracker.AgeBiologicalYears <= ext.requiredObservedMaxBiologicalAge)
                        {
                            currentWeight += 3; // Middle weight
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observed too old ({observed.ageTracker.AgeBiologicalYears} > {ext.requiredObservedMaxBiologicalAge})");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observer Need Sex State
                    if (ext.requiredObserverNeedSexState != NeedSexState.Any)
                    {
                        if (GetNeedSexState(observer) == ext.requiredObserverNeedSexState)
                        {
                            currentWeight += 8; // Very high weight for Need_Sex condition
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observer sex state mismatch (Expected: {ext.requiredObserverNeedSexState}, Got: {GetNeedSexState(observer)})");
                            passedAllConditions = false;
                        }
                    }

                    // Required Observed Need Sex State
                    if (ext.requiredObservedNeedSexState != NeedSexState.Any)
                    {
                        if (GetNeedSexState(observed) == ext.requiredObservedNeedSexState)
                        {
                            currentWeight += 8; // Very high weight for Need_Sex condition
                        }
                        else
                        {
                            debugFailedReasons.Add($"[OpinionDebug] '{opinionDef.defName}': Observed sex state mismatch (Expected: {ext.requiredObservedNeedSexState}, Got: {GetNeedSexState(observed)})");
                            passedAllConditions = false;
                        }
                    }
                }

                // Add an opinion with its weight only if all conditions are met
                if (passedAllConditions)
                {
                    weighedOpinions.Add((opinionDef, currentWeight));
                }
            }


            //LOGGING WHEN STUBBOUND
            // If no matching weighted opinions were found
            // Additional check that candidateOpinions is not empty.
            // If it is empty, it means that opinions were filtered early (isSelfOpinion, targetBodyPart, opinionCategory).
            if (!weighedOpinions.Any())
            {
                ModLog.Message($"[OpinionDebug] No specific opinion found for {observer.NameShortColored} observing {observed.NameShortColored}'s {part.defName}.");
                ModLog.Message($"[OpinionDebug] Current state: Observer biological age: {observer.ageTracker.AgeBiologicalYears}, Observer sex need: {GetNeedSexState(observer)} ({observer.needs.TryGetNeed<Need_Sex>()?.CurLevel ?? -1f:F2}), Observed biological age: {observed.ageTracker.AgeBiologicalYears}, Observed sex need: {GetNeedSexState(observed)} ({observed.needs.TryGetNeed<Need_Sex>()?.CurLevel ?? -1f:F2}), Part severity: {currentSeverity:F2}, Part genital family: {genitalFamily}.");

                if (debugFailedReasons.Any())
                {
                    ModLog.Message($"[OpinionDebug] Detailed reasons for skipped specific opinions for {observer.NameShortColored} -> {observed.NameShortColored} ({part.defName}):");
                    foreach (string reason in debugFailedReasons.Distinct()) // Выводим только уникальные причины
                    {
                        ModLog.Message(reason);
                    }
                }
                else
                {
                    // This message will now fire if _all_ opinions were filtered initially.
                    // More information about initial filtering
                    ModLog.Message($"[OpinionDebug] No specific opinions matched initial filters (targetBodyPart: {part.defName}, opinionCategory: {category}, isSelfOpinion: {isCurrentSelfObservation}).");


                    // Show what defs were in candidateOpinions before further checks (for debugging isSelfOpinion, targetBodyPart, opinionCategory)
                    if (candidateOpinions.Any())
                    {
                        ModLog.Message("[OpinionDebug] Candidates before detailed condition checks:");
                        foreach (var opDef in candidateOpinions)
                        {
                            ModLog.Message($"    - {opDef.defName} (isSelfOpinion: {opDef.GetModExtension<OpinionConditionExtension>()?.isSelfOpinion ?? false})");
                        }
                    }
                    else
                    {
                        ModLog.Message("[OpinionDebug] No opinions passed initial filtering (targetBodyPart, opinionCategory, isSelfOpinion).");
                    }
                }
            }
            //End of logging


            // Select the opinion with the maximum weight
            OpinionDef_SexPart selectedOpinionDef = null;
            if (weighedOpinions.Any())
            {
                int maxWeight = weighedOpinions.Max(x => x.weight);
                var bestOpinions = weighedOpinions.Where(x => x.weight == maxWeight).ToList();
                selectedOpinionDef = bestOpinions.RandomElement().def; // We take a random one from the "best"
            }

            string selectedOpinionText = null;

            if (selectedOpinionDef != null && selectedOpinionDef.opinionTexts.Any())
            {
                selectedOpinionText = selectedOpinionDef.opinionTexts.RandomElement();
            }
            else
            {

                // If no opinions with the conditions are found, we look for the closest one by severity among ALL matching ones
                // (which match targetBodyPart, opinionCategory, genitalFamily=Undefined/matching)
                // Now we should use the filtered list of candidateOpinions, since it already takes into account isSelfOpinion
                var allCandidateOpinionsFallback = candidateOpinions
                                                        .Where(def => (def.genitalFamily == genitalFamily || def.genitalFamily == GenitalFamily.Undefined))
                                                        .ToList();

                // Filter by severity (if it is not present, then the range is 0-1)
                // USE EPSILON TO COMPARE FLOAT
                allCandidateOpinionsFallback = allCandidateOpinionsFallback
                    .Where(def => (currentSeverity >= (def.severityRange.min - EPSILON) && currentSeverity <= (def.severityRange.max + EPSILON)))
                    .ToList();


                OpinionDef_SexPart closestOpinion = null;
                if (allCandidateOpinionsFallback.Any())
                {

                    // If there are several, choose a random one
                    closestOpinion = allCandidateOpinionsFallback.RandomElement();
                }

                if (closestOpinion != null && closestOpinion.opinionTexts.Any())
                {
                    selectedOpinionText = closestOpinion.opinionTexts.RandomElement();
                }
                else
                {
                    // Absolute fallback: old OpinionDef
                    selectedOpinionText = GetOpinionFromDefs(part, category);
                }
            }


            // Make sure we don't return null or an empty string if opinionTexts was empty
            if (string.IsNullOrEmpty(selectedOpinionText))
            {
                selectedOpinionText = GetOpinionFromDefs(part, category); // final reserve variant
            }

            return ProcessOpinionText(selectedOpinionText, observer, observed);
        }


        /// <summary>
        /// Helper method to check if a body part has been seen.
        /// Returns true if observer == observable.
        /// </summary>
        private static bool IsPartSeen(Pawn observer, Pawn observed, BodyPartDef part)
        {
            // If the observer and the observed are the same pawn, always consider it seen
            if (observer == observed)
            {
                return true;
            }

            string key = NudityMattersMore.PawnInteractionManager.GenerateKey(observer, observed);
            if (NudityMattersMore.PawnInteractionManager.InteractionProfiles.TryGetValue(key, out NudityMattersMore.PawnInteractionProfile profile))
            {
                if (part.defName == "Chest" || part.defName == "Breasts")
                {
                    return profile.HasSeenTop;
                }
                if (part.defName == "Genitals")
                {
                    return profile.HasSeenBottom;
                }
                if (part.defName == "Anus")
                {
                    return profile.HasSeenBottom;
                }
                if (part.defName == "Torso")
                {
                    return profile.HasSeenTop && profile.HasSeenBottom;
                }
            }
            return false;
        }


        // Method for testing attractiveness based on sexual orientation and gender
        private static bool IsPawnAttractedBySexualityAndGender(Pawn observer, Pawn observed)
        {
            if (ModLister.HasActiveModWithName("RimJobWorld"))
            {
                bool observerIsHomosexual = observer.story.traits.HasTrait(VanillaTraitDefOf.Gay);
                bool observerIsBisexual = observer.story.traits.HasTrait(VanillaTraitDefOf.Bisexual);
                bool observerIsAsexual = observer.story.traits.HasTrait(VanillaTraitDefOf.Asexual);

                if (observerIsAsexual)
                {
                    return false;
                }
                else if (observerIsBisexual)
                {
                    return true;
                }
                else if (observerIsHomosexual)
                {
                    return observer.gender == observed.gender;
                }
                else // Assume heterosexual by default if no other traits are present
                {
                    return observer.gender != observed.gender;
                }
            }
            // Fallback for non-RJW or if RJW traits are not set
            return observer.relations.SecondaryRomanceChanceFactor(observed) > 0f;
        }

        // Method for handling substitution keys in opinion text
        public static string ProcessOpinionText(string text, Pawn observer, Pawn observed)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Helper to get possessive form of a short label
            Func<Pawn, string> GetShortPossessive = (p) =>
            {
                string shortLabel = p.LabelShort;
                if (string.IsNullOrEmpty(shortLabel)) return "";
                return shortLabel + (shortLabel.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? "'" : "'s");
            };

            // Handle standard RimWorld {PAWN_...} tokens, assuming they refer to the OBSERVED pawn
            text = text.Replace("{PAWN_nameShort}", observed.LabelShort);
            text = text.Replace("{PAWN_nameFull}", observed.LabelShort); // LabelShort
            text = text.Replace("{PAWN_gender}", observed.gender.ToString());
            text = text.Replace("{PAWN_possessive}", observed.gender.GetPossessive());
            text = text.Replace("{PAWN_pronoun}", observed.gender.GetPronoun());
            text = text.Replace("{PAWN_objective}", observed.gender.GetObjective());
            text = text.Replace("{PAWN_kindDef}", observed.kindDef.LabelCap);
            text = text.Replace("{PAWN_faction}", observed.Faction?.Name ?? "None");
            text = text.Replace("{PAWN_nameShortPossessive}", GetShortPossessive(observed)); 


            // Handle {OBSERVER_...} and {OBSERVED_...} tokens
            text = text.Replace("{OBSERVER_nameShort}", observer.LabelShort);
            text = text.Replace("{OBSERVED_nameShort}", observed.LabelShort);
            text = text.Replace("{OBSERVER_nameShortPossessive}", GetShortPossessive(observer)); 
            text = text.Replace("{OBSERVED_nameShortPossessive}", GetShortPossessive(observed)); 


            text = text.Replace("{OBSERVER_gender}", observer.gender.ToString());
            text = text.Replace("{OBSERVED_gender}", observed.gender.ToString());

            text = text.Replace("{OBSERVER_possessive}", observer.gender.GetPossessive());
            text = text.Replace("{OBSERVED_possessive}", observed.gender.GetPossessive());

            text = text.Replace("{OBSERVER_pronoun}", observer.gender.GetPronoun());
            text = text.Replace("{OBSERVED_pronoun}", observed.gender.GetPronoun());

            text = text.Replace("{OBSERVER_objective}", observer.gender.GetObjective());
            text = text.Replace("{OBSERVED_objective}", observed.gender.GetObjective());

            text = text.Replace("{OBSERVER_nameFull}", observer.LabelShort); 
            text = text.Replace("{OBSERVED_nameFull}", observed.LabelShort); 
            text = text.Replace("{OBSERVER_kindDef}", observer.kindDef.LabelCap);
            text = text.Replace("{OBSERVED_kindDef}", observed.kindDef.LabelCap);
            text = text.Replace("{OBSERVER_faction}", observer.Faction?.Name ?? "None");
            text = text.Replace("{OBSERVED_faction}", observed.Faction?.Name ?? "None");

            // Biological age
            text = text.Replace("{OBSERVER_ageBiologicalYears}", observer.ageTracker.AgeBiologicalYears.ToString());
            text = text.Replace("{OBSERVED_ageBiologicalYears}", observed.ageTracker.AgeBiologicalYears.ToString());

            //  Need_Sex State
            text = text.Replace("{OBSERVER_needSexState}", GetNeedSexState(observer).ToString());
            text = text.Replace("{OBSERVED_needSexState}", GetNeedSexState(observed).ToString());


            return text;
        }


        // Method to get descriptive size label based on severity
        private static string GetDescriptiveSizeLabel(float severity)
        {
            if (severity <= 0.15f) // Adjusted ranges for more granularity
            {
                return "Tiny";
            }
            else if (severity <= 0.35f)
            {
                return "Small";
            }
            else if (severity <= 0.65f)
            {
                return "Average";
            }
            else if (severity <= 0.85f)
            {
                return "Large";
            }
            else
            {
                return "Huge";
            }
        }

        // Method for getting opinion text from Defs (Fallback for generic opinions)
        private static string GetOpinionFromDefs(BodyPartDef part, OpinionCategory category)
        {
            OpinionDef opinionDef = DefDatabase<OpinionDef>.AllDefsListForReading
                                    .FirstOrDefault(def => def.targetBodyPart == part);

            if (opinionDef == null)
            {
                return $"No opinion def found for {part.defName}.";
            }

            switch (category)
            {
                case OpinionCategory.Positive:
                    return opinionDef.positiveOpinions.Any() ? opinionDef.positiveOpinions.RandomElement() : "Positive opinion.";
                case OpinionCategory.Negative:
                    return opinionDef.negativeOpinions.Any() ? opinionDef.negativeOpinions.RandomElement() : "Negative opinion.";
                case OpinionCategory.Neutral:
                default:
                    return opinionDef.neutralOpinions.Any() ? opinionDef.neutralOpinions.RandomElement() : "Neutral opinion.";
            }
        }
    }

    // Enum for opinion categories
    public enum OpinionCategory
    {
        Any,
        Positive,
        Negative,
        Neutral
    }

    // Def for defining opinions
    // This existing OpinionDef is for generic opinions, not specific to sex part severity.
    // It is kept for backward compatibility or other generic opinion needs.
    public class OpinionDef : Def
    {
        public BodyPartDef targetBodyPart;
        public List<string> positiveOpinions = new List<string>();
        public List<string> negativeOpinions = new List<string>();
        public List<string> neutralOpinions = new List<string>();
    }
}
