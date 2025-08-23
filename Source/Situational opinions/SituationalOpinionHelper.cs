using NudityMattersMore; //  InfoHelper, PawnInteractionManager, DressState, InteractionType, PawnState, TabGender
using NudityMattersMore_opinions.CalculationHelpers;
using RimWorld; // TraitDef, PawnKindDef, LifeStageDef, Need_Sex (for thresh_*), HediffDefOf, PawnRelationDefOf
using rjw; // Quirk, Genital_Helper, ISexPartHediff, HediffComp_SexPart
using System; // Exception
using System.Collections.Generic; //  List, Dictionary
using System.Linq; //  LINQ
using System.Runtime.InteropServices;
using Verse;

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// Helper class for handling situational opinion logic.
    /// Responsible for determining suitable opinions based on the current situation
    /// and adding them to the pawn log.
    /// </summary>
    /// 

    public static class SituationalOpinionHelper
    {
        // new entry for dynamic interaction bubbles
        public static BodyPartDef LastObservedBodyPartDef = null;


        // Dictionary for storing memory of situational opinions for each pawn.
        // Removed persistence of this dictionary so that the log is not saved.
        private static Dictionary<Pawn, PawnSituationalOpinionMemory> pawnSituationalOpinionMemories = new Dictionary<Pawn, PawnSituationalOpinionMemory>();
        private static System.Random random = new System.Random();

        // --- КЭШИРОВАНИЕ КВИРКОВ ДЛЯ БЫСТРОГО ПОИСКА ---
       // private static readonly Dictionary<string, Quirk> _cachedQuirks = new Dictionary<string, Quirk>();

        private static readonly bool IsrjwsexperienceideologyActive;

        public static readonly bool IsRjwActive;

        public static readonly bool IsPrivacyPleaseActive;

        static SituationalOpinionHelper()
        {

            // Заполняем кэш квирков при старте мода
      //      foreach (var quirk in rjw.Check)
       //     {
        //        if (!_cachedQuirks.ContainsKey(quirk.Key))
        //        {
        //            _cachedQuirks.Add(quirk.Key, quirk);
       //         }
       //         if (!string.IsNullOrEmpty(quirk.LocaliztionKey) && !_cachedQuirks.ContainsKey(quirk.LocaliztionKey))
       //         {
        //            _cachedQuirks.Add(quirk.LocaliztionKey, quirk);
       //         }
       //     }
            // Проверяем активность RJW один раз и сохраняем результат
            IsRjwActive = ModLister.HasActiveModWithName("RimJobWorld");
            IsrjwsexperienceideologyActive = ModLister.HasActiveModWithName("rjw.sexperience.ideology");
            IsPrivacyPleaseActive = ModLister.HasActiveModWithName("abscon.privacy.please");
        }


        // +++ COOLDOWN TO PREVENT DUPLICATION +++
        // УВЕЛИЧЕНО: Было 10, теперь 60 тиков (1 секунда). Это поможет избежать пропусков записей.
        private const int OpinionCooldownTicks = 60;
        private static Dictionary<Tuple<Pawn, Pawn>, int> lastOpinionTick = new Dictionary<Tuple<Pawn, Pawn>, int>();


        /// <summary>
        /// Returns or creates a PawnSituationalOpinionMemory object for the given pawn.
        /// </summary>
        /// <param name="pawn">The pawn to use opinion memory for.</param>
        /// <returns>The PawnSituationalOpinionMemory object.</returns>
        public static PawnSituationalOpinionMemory GetOrCreatePawnSituationalOpinionMemory(Pawn pawn)
        {
            if (!pawnSituationalOpinionMemories.ContainsKey(pawn))
            {
                pawnSituationalOpinionMemories[pawn] = new PawnSituationalOpinionMemory();
            }
            return pawnSituationalOpinionMemories[pawn];
        }


        /// <summary>
        /// Deletes the opinion memory for a pawn, such as when the pawn dies or leaves the map.
        /// </summary>
        /// <param name="pawn">The pawn whose opinion memory should be deleted.</param>
        public static void RemovePawnSituationalOpinionMemory(Pawn pawn)
        {
            if (pawnSituationalOpinionMemories.ContainsKey(pawn))
            {
                pawnSituationalOpinionMemories.Remove(pawn);
            }
        }
        /// <summary>
        /// NEW: Determines the correct pawn state directly, making the logic more robust.
        /// НОВОЕ: Напрямую определяет состояние пешки (спит, без сознания), делая логику более надежной.
        /// </summary>
        private static PawnState GetCorrectedPawnState(Pawn pawn, PawnState originalState)
        {
            if (pawn == null || !pawn.Spawned) return originalState;

            // A pawn that is Downed is considered Unconscious for our purposes.
            // Пешка в состоянии Downed считается Unconscious для наших целей.
            if (pawn.Downed)
            {
                // NOTE: Assumes 'Unconscious' exists in the NudityMattersMore.PawnState enum.
                // ПРИМЕЧАНИЕ: Предполагается, что 'Unconscious' существует в перечислении NudityMattersMore.PawnState.
                return (PawnState)Enum.Parse(typeof(PawnState), "Unconscious");
            }

            // A pawn in a bed with an 'asleep' job driver is sleeping.
            // Пешка в кровати с заданием 'asleep' - спит.
            if (pawn.jobs?.curDriver?.asleep ?? false)
            {
                // NOTE: Assumes 'Asleep' exists in the NudityMattersMore.PawnState enum.
                // ПРИМЕЧАНИЕ: Предполагается, что 'Asleep' существует в перечислении NudityMattersMore.PawnState.
                return (PawnState)Enum.Parse(typeof(PawnState), "Asleep");
            }

            return originalState;
        }

        /// <summary>
        /// Generates and adds a situational opinion to the log of the corresponding pawn.
        /// This method is called from the MapComponent, which iterates over the NMM logs.
        /// Ensures that only one entry is added to the log for each event trigger.
        /// </summary>
        /// <param name="logOwner">The pawn whose opinion memory should be updated (the owner of the log).</param>
        /// <param name="interactingPawn">The other pawn participating in the interaction.</param>
        /// <param name="nmmInteractionType">Interaction type from NMM.</param>
        /// <param name="nmmPawnState">State of the observed pawn from NMM.</param>
        /// <param name="aware">Whether the observed pawn was conscious.</param>
        /// <param name="isLogOwnerObserver">True if logOwner is an observer in this interaction; false if logOwner is an observable.</param>
        public static void GenerateAndAddSituationalOpinionEntry(
            Pawn logOwner,
            Pawn interactingPawn, // Another pawn participating in the interaction
            InteractionType nmmInteractionType,
            PawnState nmmPawnState,
            bool aware,
            bool isLogOwnerObserver // true if logOwner is an observer, false if logOwner is an observable
        )
        {

            // COOLDOWN CHECK
            // Create a DIRECTIONAL key: (thinker -> thinkee).
            // This allows both the observer and the observed to voice their opinion,
            // but prevents duplication for the same observer.
            var pairKey = Tuple.Create(logOwner, interactingPawn);
            int currentTick = Find.TickManager.TicksGame;

            if (lastOpinionTick.TryGetValue(pairKey, out int lastTick) && currentTick - lastTick < OpinionCooldownTicks)
            {
                return; // Exit if this logOwner has already recently formed an opinion about this target.
            }

            PawnSituationalOpinionMemory logOwnerMemory = GetOrCreatePawnSituationalOpinionMemory(logOwner);

            Pawn opinionSubjectPawn; // A pawn whose "thought" or "feeling" is expressed
            Pawn opinionTargetPawn;  // A pawn about which an opinion is formed
            OpinionPerspective requiredPerspective; // Define the required perspective here
            bool entryIsObserverPerspectiveForLogEntry; // Flag for the OpinionLogEntry constructor

            bool isSelfObservation = (logOwner == interactingPawn);

            if (isSelfObservation)
            {
                opinionSubjectPawn = logOwner;
                opinionTargetPawn = logOwner;
                requiredPerspective = OpinionPerspective.UsedForSelf;
                entryIsObserverPerspectiveForLogEntry = true;
            }
            else if (isLogOwnerObserver)
            {
                opinionSubjectPawn = logOwner;
                opinionTargetPawn = interactingPawn;
                requiredPerspective = OpinionPerspective.UsedForObserver;
                entryIsObserverPerspectiveForLogEntry = true;
            }
            else
            {
                opinionSubjectPawn = logOwner;
                opinionTargetPawn = interactingPawn;
                requiredPerspective = OpinionPerspective.UsedForObserved;
                entryIsObserverPerspectiveForLogEntry = false;
            }

            // Define opinoin category
            OpinionCategory category = OpinionCategoryCalculator.GetCategory(opinionSubjectPawn, opinionTargetPawn);


            // Get visible body parts for the generator
            List<Tuple<BodyPartDef, float, string, GenitalFamily>> visibleBodyParts = GetVisibleBodyPartsData(actualObserver: isLogOwnerObserver ? logOwner : interactingPawn, actualObserved: isLogOwnerObserver ? interactingPawn : logOwner);


            // Call SelectOpinionText to get the opinion text (with the new chance logic)
            string opinionText = SelectOpinionText(
                opinionSubjectPawn,
                opinionTargetPawn,
                nmmInteractionType,
                nmmPawnState,
                aware,
                isSelfObservation,
                requiredPerspective
            );


            if (!string.IsNullOrEmpty(opinionText))
            {
                Pawn logEntryObserverPawn = entryIsObserverPerspectiveForLogEntry ? opinionSubjectPawn : opinionTargetPawn;
                Pawn logEntryObservedPawn = entryIsObserverPerspectiveForLogEntry ? opinionTargetPawn : opinionSubjectPawn;


                // Create an entry for our own log in the ITab tab
                OpinionLogEntry newLogEntry = new OpinionLogEntry(
                    logEntryObserverPawn,
                    logEntryObservedPawn,
                    opinionText,
                    Find.TickManager.TicksGame,
                    nmmInteractionType,
                    nmmPawnState,
                    category,
                    aware,
                    isSelfObservation,
                    entryIsObserverPerspectiveForLogEntry
                );
                logOwnerMemory.AddEntry(newLogEntry);

                // Cooldown update

                // Update the last opinion time for this pair after successful creation.
                lastOpinionTick[pairKey] = currentTick;


            }
        }


        /// <summary>
        /// Determines and returns the text of a situational opinion based on the given conditions.
        /// Now includes chance logic for choosing between a canned and generated opinion.
        /// </summary>
        private static string SelectOpinionText(Pawn opinionPawn, Pawn targetPawn, InteractionType interactionType, PawnState pawnState, bool aware, bool isSelfObservation, OpinionPerspective requiredPerspective)
        {
            // Determine who is the actual observer and observable for condition checks
            Pawn actualObserver = null;
            Pawn actualObserved = null;

            if (requiredPerspective == OpinionPerspective.UsedForObserver)
            {
                actualObserver = opinionPawn; // The owner of the opinion is an observer
                actualObserved = targetPawn;  // The target of the opinion is observable
            }
            else if (requiredPerspective == OpinionPerspective.UsedForObserved)
            {
                actualObserver = targetPawn;  // The target of the opinion is the observer
                actualObserved = opinionPawn; // The owner of the opinion is observable
            }
            else if (requiredPerspective == OpinionPerspective.UsedForSelf)
            {
                actualObserver = opinionPawn; // The owner of an opinion is his own observer
                actualObserved = opinionPawn; // The owner of the opinion is himself observed
            }
            else // OpinionPerspective.Any or other cases if any
            {

                // By default, if perspective is not defined, consider opinionPawn as an observer
                actualObserver = opinionPawn;
                actualObserved = targetPawn;
            }


            // --- ПРЕДВАРИТЕЛЬНЫЕ ПРОВЕРКИ И ВЫЧИСЛЕНИЯ ДЛЯ ОПТИМИЗАЦИИ ---
            // Если одна из пешек null, мы не можем продолжить.
            if (actualObserver == null || actualObserved == null)
            {
                Log.Warning($"[NMM Opinions] SelectOpinionText: actualObserver or actualObserved is null. Observer: {actualObserver?.LabelShort ?? "null"}, Observed: {actualObserved?.LabelShort ?? "null"}. Returning empty string.");
                return "";
            }

            // Предварительно вычисляем состояние одежды наблюдаемой пешки
            DressState currentObservedDressState = GetPawnDressState(actualObserved);
            // Предварительно вычисляем состояние одежды наблюдателя (если нужно)
            DressState currentObserverDressState = GetPawnDressState(actualObserver);

            PawnState currentPawnState = GetCorrectedPawnState(actualObserved, pawnState);

            // Предварительно вычисляем, есть ли у наблюдаемой пешки грудь
            bool observedHasBreasts = NudityMattersMore.InfoHelper.HasBreasts(actualObserved);


            List<(OpinionDef_Situational def, int weight)> weighedCandidateOpinions = new List<(OpinionDef_Situational def, int weight)>();

            // 1. First, filter out specific opinions
            // ИСПРАВЛЕНО: Заменен LINQ .Where().ToList() на foreach с ранними выходами для производительности.
            foreach (var def in SituationalOpinionDefCache.SpecificOpinions)
            {
                OpinionConditionExtension_Situational ext = def.GetModExtension<OpinionConditionExtension_Situational>();

                // Мы также отфильтровываем те, у которых вообще нет расширений мода (поскольку они неспецифичны).
                if (ext == null) continue;

                // Filter by the required perspective.
                if (ext.perspective != OpinionPerspective.Any && ext.perspective != requiredPerspective)
                {
                    continue;
                }

                // --- LOGIC: Exclude breast/topless opinions for regular male pawns ---
                // Если наблюдаемая пешка мужского пола и не имеет груди (например, не фута),
                // и мнение требует состояния "топлесс" или относится к частям тела "грудь"/"грудная клетка",
                // тогда исключаем это мнение.
                if (actualObserved.gender == Gender.Male && !observedHasBreasts)
                {
                    if (ext.requiredObservedDressState == DressState.Topless ||
                        (ext.requiredBodyPartSeen != null && (ext.requiredBodyPartSeen.defName == "Breasts" || ext.requiredBodyPartSeen.defName == "Chest")))
                    {
                        continue; // Исключаем мнения о груди/топлесс для мужчин без груди
                    }
                }

                // Basic filtering by DressState: if a specific state is specified in the XML (not the default Clothed),
                // it must match. If Clothed, the pawn must be Clothed.
                if (ext.requiredObservedDressState != DressState.Clothed)
                {
                    if (ext.requiredObservedDressState != currentObservedDressState)
                    {
                        continue;
                    }
                }
                else // ext.requiredObservedDressState == DressState.Clothed (default statement)
                {
                    // Если мнение по умолчанию предназначено для "одетой" пешки, оно должно срабатывать только если пешка действительно одета.
                    // Если пешка находится в любом другом состоянии (Naked, Topless, Bottomless, Covering), это мнение неприменимо.
                    if (currentObservedDressState != DressState.Clothed)
                    {
                        continue;
                    }
                }

                // Check InteractionType and PawnState conditions (strict filters)
                if (ext.requiredInteractionType != InteractionType.None && ext.requiredInteractionType != interactionType)
                { continue; }
                if (ext.requiredPawnState != PawnState.None && ext.requiredPawnState != currentPawnState) continue;

                // Conditions associated with awareness
                if (ext.requiredObservedAware.HasValue && ext.requiredObservedAware.Value != aware) { continue; }

                // Body Part Related Conditions (for situational opinions)
                if (ext.requiredBodyPartSeen != null) // If a specific body part is required
                {
                    bool partActuallySeen = isSelfObservation || IsPartSeen(actualObserver, actualObserved, ext.requiredBodyPartSeen);
                    if (!partActuallySeen) { continue; }
                }

                if (ext.requiredPartSizeRange.HasValue)
                {
                    float currentSeverity = GetPartSeverity(actualObserved, ext.requiredBodyPartSeen);
                    if (!(currentSeverity >= ext.requiredPartSizeRange.Value.min && currentSeverity <= ext.requiredPartSizeRange.Value.max)) { continue; }
                }

                if (ext.requiredGenitalFamily != GenitalFamily.Undefined)
                {
                    GenitalFamily detectedFamily = GetGenitalFamily(actualObserved, ext.requiredBodyPartSeen);
                    if (ext.requiredGenitalFamily != detectedFamily) { continue; }
                }


                // Check conditions for observer
                if (ext.requiredObserverTrait != null && (actualObserver.story == null || !actualObserver.story.traits.HasTrait(ext.requiredObserverTrait))) continue;
                if (ext.requiredObserverHediffDef != null && (actualObserver.health == null || !actualObserver.health.hediffSet.HasHediff(ext.requiredObserverHediffDef))) continue;
                if (ext.requiredObserverGeneDef != null && (actualObserver.genes == null || !actualObserver.genes.HasActiveGene(ext.requiredObserverGeneDef))) continue;
                if (ext.requiredObserverGender.HasValue && actualObserver.gender != ext.requiredObserverGender.Value) continue;
                if (ext.requiredObserverLifeStage != null && (actualObserver.ageTracker == null || actualObserver.ageTracker.CurLifeStage != ext.requiredObserverLifeStage)) continue;
                if (ext.requiredObserverPawnKind != null && actualObserver.kindDef != ext.requiredObserverPawnKind) continue;
                if (ext.requiredObserverMinBiologicalAge > 0 && (actualObserver.ageTracker == null || actualObserver.ageTracker.AgeBiologicalYears < ext.requiredObserverMinBiologicalAge)) continue;
                if (ext.requiredObserverMaxBiologicalAge > 0 && (actualObserver.ageTracker == null || actualObserver.ageTracker.AgeBiologicalYears > ext.requiredObserverMaxBiologicalAge)) continue;
                if (ext.requiredObserverNeedSexState != NeedSexState.Any && GetNeedSexState(actualObserver) != ext.requiredObserverNeedSexState) continue;

                // More explicit check for requiredObserverDressState
                if (ext.requiredObserverDressState != DressState.Clothed)
                {
                    if (currentObserverDressState != ext.requiredObserverDressState)
                    {
                        continue;
                    }
                }

                // More explicit requiredObserverIsCovering check
                if (ext.requiredObserverIsCovering.HasValue)
                {
                    bool observerIsCovering = NudityMattersMore.InfoHelper.IsCovering(actualObserver, out _);
                    if (observerIsCovering != ext.requiredObserverIsCovering.Value)
                    {
                        continue;
                    }
                }

                if (ext.requiredObserverPregnancyTrimester != PregnancyTrimester.Any)
                {
                    Hediff_Pregnant observerPregnancyHediff = actualObserver?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;
                    if (observerPregnancyHediff == null || GetPregnancyTrimester(observerPregnancyHediff) != ext.requiredObserverPregnancyTrimester) continue;
                }
                if (ext.requiredObserverRelation != null && !actualObserver.relations.DirectRelationExists(ext.requiredObserverRelation, actualObserved)) continue;



                // Check conditions for observable
                if (ext.requiredObservedTrait != null && (actualObserved.story == null || !actualObserved.story.traits.HasTrait(ext.requiredObservedTrait))) continue;
                if (ext.requiredObservedHediffDef != null && (actualObserved.health == null || !actualObserved.health.hediffSet.HasHediff(ext.requiredObservedHediffDef))) continue;
                if (ext.requiredObservedGeneDef != null && (actualObserved.genes == null || !actualObserved.genes.HasActiveGene(ext.requiredObservedGeneDef))) continue;
                if (ext.requiredObservedGender.HasValue && actualObserved.gender != ext.requiredObservedGender.Value) continue;
                if (ext.requiredObservedLifeStage != null && (actualObserved.ageTracker == null || actualObserved.ageTracker.CurLifeStage != ext.requiredObservedLifeStage)) continue;
                if (ext.requiredObservedPawnKind != null && actualObserved.kindDef != ext.requiredObservedPawnKind) continue;
                if (ext.requiredObservedMinBiologicalAge > 0 && (actualObserved.ageTracker == null || actualObserved.ageTracker.AgeBiologicalYears < ext.requiredObservedMinBiologicalAge)) continue;
                if (ext.requiredObservedMaxBiologicalAge > 0 && (actualObserved.ageTracker == null || actualObserved.ageTracker.AgeBiologicalYears <= ext.requiredObservedMaxBiologicalAge)) continue;
                if (ext.requiredObservedNeedSexState != NeedSexState.Any && GetNeedSexState(actualObserved) != ext.requiredObservedNeedSexState) continue;


                // More explicit check for requiredObservedIsCovering
                if (ext.requiredObservedIsCovering.HasValue)
                {
                    bool observedIsCovering = NudityMattersMore.InfoHelper.IsCovering(actualObserved, out _);
                    if (observedIsCovering != ext.requiredObservedIsCovering.Value)
                    {
                        continue;
                    }
                }

                if (ext.requiredObservedPregnancyTrimester != PregnancyTrimester.Any)
                {
                    Hediff_Pregnant observedPregnancyHediff = actualObserved?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;
                    if (observedPregnancyHediff == null || GetPregnancyTrimester(observedPregnancyHediff) != ext.requiredObservedPregnancyTrimester) continue;
                }
                if (ext.requiredObservedRelation != null && !actualObserved.relations.DirectRelationExists(ext.requiredObservedRelation, actualObserver)) continue;

                // Если все условия пройдены, добавляем мнение в кандидаты
                weighedCandidateOpinions.Add((def, 0)); // Вес будет рассчитан ниже
            }

            // --- РАСЧЕТ ВЕСОВ ДЛЯ ОТОБРАННЫХ КАНДИДАТОВ ---
            // ИСПРАВЛЕНО: Расчет весов теперь происходит после первичной фильтрации,
            // чтобы избежать лишних вычислений для отброшенных мнений.
            List<(OpinionDef_Situational def, int weight)> finalWeighedCandidateOpinions = new List<(OpinionDef_Situational def, int weight)>();
            foreach (var opinionTuple in weighedCandidateOpinions)
            {
                var opinionDef = opinionTuple.def;
                int currentWeight = 0;
                OpinionConditionExtension_Situational ext = opinionDef.GetModExtension<OpinionConditionExtension_Situational>();

                // --- PRIORITY: requiredInteractionType ---
                if (ext.requiredInteractionType == interactionType && interactionType != InteractionType.None)
                {
                    currentWeight += 10000; // Very high bonus for exact match of interaction type
                }


                // Improvement: Add weight bonus depending on perspective match.
                if (ext.perspective == requiredPerspective)
                {
                    currentWeight += 1000; // High bonus for exact match to perspective
                }
                else if (ext.perspective == OpinionPerspective.Any)
                {
                    currentWeight += 10; // Smaller bonus for "Any" perspective (will work if there is no exact match)
                }


                // Add weight based on the state of the garment
                if (ext.requiredObservedDressState == currentObservedDressState)
                {

                    // Add weights depending on the specificity of the clothing state, giving priority to Naked.
                    if (ext.requiredObservedDressState == DressState.Naked) currentWeight += 500;
                    else if (ext.requiredObservedDressState == DressState.Topless || ext.requiredObservedDressState == DressState.Bottomless) currentWeight += 100;
                    else if (ext.requiredObservedDressState == DressState.Covering) currentWeight += 200; // Adding weight for Covering
                    else if (ext.requiredObservedDressState == DressState.Clothed) currentWeight += 10; // Smaller weight for clothed
                }


                // Add weight for matching PawnState
                if (ext.requiredPawnState == currentPawnState && currentPawnState != PawnState.None)
                {
                    currentWeight += 1000;
                }


                // Add weight for compliance with awareness
                if (ext.requiredObservedAware.HasValue && ext.requiredObservedAware.Value == aware)
                {
                    currentWeight += 30;
                }


                // Add weight for body part matching
                if (ext.requiredBodyPartSeen != null)
                {
                    currentWeight += 40;
                    if (ext.requiredPartSizeRange.HasValue) currentWeight += 10;
                    if (ext.requiredGenitalFamily != GenitalFamily.Undefined) currentWeight += 10;
                }



                // --- Check and weigh conditions for the Observer (actualObserver in this context) ---
                if (ext.requiredObserverTrait != null && actualObserver.story != null && actualObserver.story.traits.HasTrait(ext.requiredObserverTrait)) { currentWeight += 10; }
                if (ext.requiredObserverGender.HasValue && actualObserver.gender == ext.requiredObserverGender.Value) { currentWeight += 5; }
                if (ext.requiredObserverLifeStage != null && actualObserver.ageTracker != null && actualObserver.ageTracker.CurLifeStage == ext.requiredObserverLifeStage) { currentWeight += 5; }
            //    if (!string.IsNullOrEmpty(ext.requiredObserverQuirk))
            //    {
            //        // ИСПРАВЛЕНО: Использование кэшированного словаря для быстрого поиска квирков
            //        if (_cachedQuirks.TryGetValue(ext.requiredObserverQuirk, out Quirk targetQuirk) && rjw.PawnExtensions.Has(actualObserver, targetQuirk)) { currentWeight += 15; }
            //    }
                if (ext.requiredObserverMinBiologicalAge > 0 && actualObserver.ageTracker != null && actualObserver.ageTracker.AgeBiologicalYears >= ext.requiredObserverMinBiologicalAge) { currentWeight += 5; }
                if (ext.requiredObserverMaxBiologicalAge > 0 && actualObserver.ageTracker != null && actualObserver.ageTracker.AgeBiologicalYears <= ext.requiredObserverMaxBiologicalAge) { currentWeight += 5; }
                if (ext.requiredObserverNeedSexState != NeedSexState.Any && GetNeedSexState(actualObserver) == ext.requiredObserverNeedSexState) { currentWeight += 10; }


                // More explicit check for requiredObserverDressState
                if (ext.requiredObserverDressState != DressState.Clothed)
                {
                    if (currentObserverDressState == ext.requiredObserverDressState)
                    {
                        currentWeight += 10;  // Add weight if clothes condition matches
                    }
                }


                // More explicit requiredObserverIsCovering check
                if (ext.requiredObserverIsCovering.HasValue)
                {
                    bool observerIsCovering = NudityMattersMore.InfoHelper.IsCovering(actualObserver, out _);
                    if (observerIsCovering == ext.requiredObserverIsCovering.Value)
                    {
                        currentWeight += 10; // Add weight if cover state matches
                    }
                }


                if (ext.requiredObserverPregnancyTrimester != PregnancyTrimester.Any)
                {
                    // Используем 'actualObserver' и сразу приводим к типу Hediff_Pregnant
                    Hediff_Pregnant observerPregnancyHediff = actualObserver?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;

                    // Проверяем, что беременность существует и триместр совпадает
                    if (observerPregnancyHediff != null && GetPregnancyTrimester(observerPregnancyHediff) == ext.requiredObserverPregnancyTrimester)
                    {
                        currentWeight += 20;
                    }
                }
                if (ext.requiredObserverRelation != null && actualObserver.relations.DirectRelationExists(ext.requiredObserverRelation, actualObserved)) { currentWeight += 5; } // Добавлен вес, если условие выполнено


                // --- Check and weigh conditions for the Observable (actualObserved in this context) ---
                if (ext.requiredObservedTrait != null && actualObserved.story != null && actualObserved.story.traits.HasTrait(ext.requiredObservedTrait)) { currentWeight += 10; }
                if (ext.requiredObservedHediffDef != null && actualObserved.health != null && actualObserved.health.hediffSet != null && actualObserved.health.hediffSet.HasHediff(ext.requiredObservedHediffDef)) { currentWeight += 10; }
                if (ext.requiredObservedGeneDef != null && actualObserved.genes != null && actualObserved.genes.HasActiveGene(ext.requiredObservedGeneDef)) { currentWeight += 10; }
                if (ext.requiredObservedGender.HasValue && actualObserved.gender == ext.requiredObservedGender.Value) { currentWeight += 5; }
                if (ext.requiredObservedLifeStage != null && actualObserved.ageTracker != null && actualObserved.ageTracker.CurLifeStage == ext.requiredObservedLifeStage) { currentWeight += 5; }
                if (ext.requiredObservedMinBiologicalAge > 0 && actualObserved.ageTracker != null && actualObserved.ageTracker.AgeBiologicalYears >= ext.requiredObservedMinBiologicalAge) { currentWeight += 5; }
                if (ext.requiredObservedMaxBiologicalAge > 0 && actualObserved.ageTracker != null && actualObserved.ageTracker.AgeBiologicalYears <= ext.requiredObservedMaxBiologicalAge) { currentWeight += 5; }
                if (ext.requiredObservedNeedSexState != NeedSexState.Any && GetNeedSexState(actualObserved) == ext.requiredObservedNeedSexState) { currentWeight += 10; }
                if (ext.requiredObservedIsCovering.HasValue)
                {
                    bool observedIsCovering = NudityMattersMore.InfoHelper.IsCovering(actualObserved, out _);
                    if (observedIsCovering == ext.requiredObservedIsCovering.Value)
                    {
                        currentWeight += 10;
                    }
                }
                if (ext.requiredObservedPregnancyTrimester != PregnancyTrimester.Any)
                {
                    Hediff_Pregnant observedPregnancyHediff = actualObserved?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;
                    if (observedPregnancyHediff != null && GetPregnancyTrimester(observedPregnancyHediff) == ext.requiredObservedPregnancyTrimester)
                    {
                        currentWeight += 20;
                    }
                }
                if (ext.requiredObservedRelation != null && actualObserved.relations.DirectRelationExists(ext.requiredObservedRelation, actualObserver)) { currentWeight += 5; } // Добавлен вес, если условие выполнено

                finalWeighedCandidateOpinions.Add((opinionDef, currentWeight));
            }


            OpinionDef_Situational selectedOpinionDef = null;

            // Select the best specific opinion
            if (finalWeighedCandidateOpinions.Any())
            {
                int maxWeight = finalWeighedCandidateOpinions.Max(x => x.weight);
                var bestOpinions = finalWeighedCandidateOpinions.Where(x => x.weight == maxWeight).ToList();
                selectedOpinionDef = bestOpinions.RandomElement().def;
            }

            // If no specific opinion is found, use BaseSituationalOpinionDef as a fallback
            if (selectedOpinionDef == null)
            {
                selectedOpinionDef = SituationalOpinionDefCache.BaseOpinion;
            }

            string finalOpinionText = "";

            // --- NEW LOGIC OF CHOICE AND OPINION GENERATION ---


            // Scenario 1: A suitable prepared opinion is found.
            if (selectedOpinionDef != null)
            {

                // Check if we should try to replace it with the generated one (for variety).
                bool tryToGenerate = NudityMattersMore_opinions_Mod.settings.enableSituationalOpinionGenerator &&
                                     random.NextDouble() * 100 < NudityMattersMore_opinions_Mod.settings.chanceOfGeneratedOpinion;

                // СТАЛО (правильно):
                if (tryToGenerate)
                {
                    // Используем правильные переменные из текущего метода
                    OpinionCategory category = OpinionCategoryCalculator.GetCategory(actualObserver, actualObserved);
                    List<Tuple<BodyPartDef, float, string, GenitalFamily>> visibleBodyParts = GetVisibleBodyPartsData(actualObserver, actualObserved);
                    finalOpinionText = SituationalOpinionGenerator.GenerateFullOpinion(
                        actualObserver, actualObserved, interactionType, pawnState, aware,
                        isSelfObservation, category, visibleBodyParts, requiredPerspective); // Переменная category теперь тоже правильная
                }

                // If the generator was not used (disabled, did not have a chance, or could not create anything), we use the prepared opinion.
                if (string.IsNullOrEmpty(finalOpinionText) && selectedOpinionDef.opinionTexts.Any())
                {
                    string rawText = selectedOpinionDef.opinionTexts.RandomElement();
                    finalOpinionText = ProcessOpinionText(rawText, actualObserver, actualObserved, null);
                }
            }
            // Scenario 2: Predefined opinion NOT found. Generator becomes primary source.
            // СТАЛО (правильно):
            else // Scenario 2
            {
                if (NudityMattersMore_opinions_Mod.settings.enableSituationalOpinionGenerator)
                {
                    // Вызываем новый централизованный калькулятор
                    OpinionCategory category = OpinionCategoryCalculator.GetCategory(actualObserver, actualObserved);
                    List<Tuple<BodyPartDef, float, string, GenitalFamily>> visibleBodyParts = GetVisibleBodyPartsData(actualObserver, actualObserved);
                    finalOpinionText = SituationalOpinionGenerator.GenerateFullOpinion(
                        actualObserver, actualObserved, interactionType, pawnState, aware,
                        isSelfObservation, category, visibleBodyParts, requiredPerspective);
                }
            }


            // Final fallback: if after all attempts the text is still not received.
            if (string.IsNullOrEmpty(finalOpinionText))
            {

                // This message now means that no prepared opinion was found,
                // And (if the generator is enabled) the generator was unable to create anything either.
                Log.Warning($"[NMM Opinions] No specific, base, or generated situational opinion could be determined for {opinionPawn?.LabelShort ?? "N/A"} about {targetPawn?.LabelShort ?? "N/A"} (Interaction: {interactionType}, State: {pawnState}, Aware: {aware}, Perspective: {requiredPerspective}). Falling back to generic text.");
                finalOpinionText = "An unidentifiable interaction occurred.";
            }
            else
            {
                // Apply final formatting if the text was successfully retrieved.
                if (!string.IsNullOrEmpty(finalOpinionText))
                {
                    finalOpinionText = char.ToUpper(finalOpinionText[0]) + finalOpinionText.Substring(1);
                    if (!finalOpinionText.EndsWith(".") && !finalOpinionText.EndsWith("!") && !finalOpinionText.EndsWith("?"))
                    {
                        finalOpinionText += ".";
                    }
                }
            }

            return finalOpinionText;

        }
        /// <summary>
        /// Determines and returns the text of a situational opinion for the social bubble based on the given conditions.
        /// Prioritizes predefined opinions and returns an empty string if none are found.
        /// </summary>
        /// <param name="opinionPawn">The pawn whose opinion is being formed (observer or observed).</param>
        /// <param name="targetPawn">The pawn who is the target of the opinion (observed or observer).</param>
        /// <param name="nmmInteractionType">Interaction type from NMM.</param>
        /// <param name="nmmPawnState">State of the observed pawn from NMM.</param>
        /// <param name="aware">Whether the observed pawn was conscious.</param>
        /// <param name="isSelfObservation">True if opinionPawn and targetPawn are the same.</param>
        /// <param name="requiredPerspective">The perspective from which the opinion is formed.</param>
        /// <returns>The selected opinion text, or an empty string if no suitable predefined opinion is found.</returns>
        public static string SelectOpinionTextForBubble(Pawn opinionPawn, Pawn targetPawn, InteractionType nmmInteractionType, PawnState nmmPawnState, bool aware, bool isSelfObservation, OpinionPerspective requiredPerspective)
        {
            // Determine who is the actual observer and observable for condition checks
            Pawn actualObserver = null;
            Pawn actualObserved = null;

            if (requiredPerspective == OpinionPerspective.UsedForObserver)
            {
                actualObserver = opinionPawn; // The owner of the opinion is an observer
                actualObserved = targetPawn;  // The target of the opinion is observable
            }
            else if (requiredPerspective == OpinionPerspective.UsedForObserved)
            {
                actualObserver = targetPawn;  // The target of the opinion is the observer
                actualObserved = opinionPawn; // The owner of the opinion is observable
            }
            else if (requiredPerspective == OpinionPerspective.UsedForSelf)
            {
                actualObserver = opinionPawn; // The owner of an opinion is his own observer
                actualObserved = opinionPawn; // The owner of the opinion is himself observed
            }
            else // OpinionPerspective.Any or other cases if any (should not happen with strict requiredPerspective)
            {
                actualObserver = opinionPawn;
                actualObserved = targetPawn;
            }

            // --- ПРЕДВАРИТЕЛЬНЫЕ ПРОВЕРКИ И ВЫЧИСЛЕНИЯ ДЛЯ ОПТИМИЗАЦИИ ---
            // Если одна из пешек null, мы не можем продолжить.
            if (actualObserver == null || actualObserved == null)
            {
                Log.Warning($"[NMM Opinions] SelectOpinionTextForBubble: actualObserver or actualObserved is null. Observer: {actualObserver?.LabelShort ?? "null"}, Observed: {actualObserved?.LabelShort ?? "null"}. Returning empty string.");
                return "";
            }

            // Предварительно вычисляем состояние одежды наблюдаемой пешки
            DressState currentObservedDressState = GetPawnDressState(actualObserved);
            // Предварительно вычисляем состояние одежды наблюдателя (если нужно)
            DressState currentObserverDressState = GetPawnDressState(actualObserver);

            PawnState currentPawnState = GetCorrectedPawnState(actualObserved, nmmPawnState);

            // Предварительно вычисляем, есть ли у наблюдаемой пешки грудь
            bool observedHasBreasts = NudityMattersMore.InfoHelper.HasBreasts(actualObserved);


            List<(OpinionDef_Situational def, int weight)> weighedCandidateOpinions = new List<(OpinionDef_Situational def, int weight)>();

            // 1. Filter specific opinions
            // ИСПРАВЛЕНО: Заменен LINQ .Where().ToList() на foreach с ранними выходами для производительности.
            foreach (var def in SituationalOpinionDefCache.SpecificOpinions)
            {
                OpinionConditionExtension_Situational ext = def.GetModExtension<OpinionConditionExtension_Situational>();

                // Мы также отфильтровываем те, у которых вообще нет расширений мода (поскольку они неспецифичны).
                if (ext == null) continue;

                // Filter by the required perspective.
                // For bubbles, we only want opinions explicitly defined for the correct perspective.
                if (ext.perspective != requiredPerspective)
                {
                    continue;
                }

                // --- LOGIC: Exclude breast/topless opinions for regular male pawns ---
                if (actualObserved.gender == Gender.Male && !observedHasBreasts)
                {
                    if (ext.requiredObservedDressState == DressState.Topless ||
                        (ext.requiredBodyPartSeen != null && (ext.requiredBodyPartSeen.defName == "Breasts" || ext.requiredBodyPartSeen.defName == "Chest")))
                    {
                        continue;
                    }
                }

                // Basic filtering by DressState
                if (ext.requiredObservedDressState != DressState.Clothed)
                {
                    if (ext.requiredObservedDressState != currentObservedDressState)
                    {
                        continue;
                    }
                }
                else
                {
                    if (currentObservedDressState != DressState.Clothed)
                    {
                        continue;
                    }
                }

                // Check InteractionType and PawnState conditions
                if (ext.requiredInteractionType != InteractionType.None && ext.requiredInteractionType != nmmInteractionType)
                { continue; }
                if (ext.requiredPawnState != PawnState.None && ext.requiredPawnState != currentPawnState) continue;

                // Conditions associated with awareness
                if (ext.requiredObservedAware.HasValue && ext.requiredObservedAware.Value != aware) { continue; }

                // Body Part Related Conditions
                if (ext.requiredBodyPartSeen != null)
                {
                    bool partActuallySeen = isSelfObservation || IsPartSeen(actualObserver, actualObserved, ext.requiredBodyPartSeen);
                    if (!partActuallySeen) { continue; }
                }

                if (ext.requiredPartSizeRange.HasValue)
                {
                    float currentSeverity = GetPartSeverity(actualObserved, ext.requiredBodyPartSeen);
                    if (!(currentSeverity >= ext.requiredPartSizeRange.Value.min && currentSeverity <= ext.requiredPartSizeRange.Value.max)) { continue; }
                }

                if (ext.requiredGenitalFamily != GenitalFamily.Undefined)
                {
                    GenitalFamily detectedFamily = GetGenitalFamily(actualObserved, ext.requiredBodyPartSeen);
                    if (ext.requiredGenitalFamily != detectedFamily) { continue; }
                }

                // Check conditions for observer
                if (ext.requiredObserverTrait != null && (actualObserver.story == null || !actualObserver.story.traits.HasTrait(ext.requiredObserverTrait))) continue;
                if (ext.requiredObserverHediffDef != null && (actualObserver.health == null || !actualObserver.health.hediffSet.HasHediff(ext.requiredObserverHediffDef))) continue;
                if (ext.requiredObserverGeneDef != null && (actualObserver.genes == null || !actualObserver.genes.HasActiveGene(ext.requiredObserverGeneDef))) continue;
                if (ext.requiredObserverGender.HasValue && actualObserver.gender != ext.requiredObserverGender.Value) continue;
                if (ext.requiredObserverLifeStage != null && (actualObserver.ageTracker == null || actualObserver.ageTracker.CurLifeStage != ext.requiredObserverLifeStage)) continue;
                if (ext.requiredObserverPawnKind != null && actualObserver.kindDef != ext.requiredObserverPawnKind) continue;
             //   if (!string.IsNullOrEmpty(ext.requiredObserverQuirk))
              //  {
                    // ИСПРАВЛЕНО: Использование кэшированного словаря для быстрого поиска квирков
              //      if (!_cachedQuirks.TryGetValue(ext.requiredObserverQuirk, out Quirk targetQuirk) || !rjw.PawnExtensions.Has(actualObserver, targetQuirk)) continue;
             //   }
                if (ext.requiredObserverMinBiologicalAge > 0 && (actualObserver.ageTracker == null || actualObserver.ageTracker.AgeBiologicalYears < ext.requiredObserverMinBiologicalAge)) continue;
                if (ext.requiredObserverMaxBiologicalAge > 0 && (actualObserver.ageTracker == null || actualObserver.ageTracker.AgeBiologicalYears > ext.requiredObserverMaxBiologicalAge)) continue;
                if (ext.requiredObserverNeedSexState != NeedSexState.Any && GetNeedSexState(actualObserver) != ext.requiredObserverNeedSexState) continue;
                if (ext.requiredObserverDressState != DressState.Clothed)
                {
                    if (currentObserverDressState != ext.requiredObserverDressState)
                    {
                        continue;
                    }
                }
                if (ext.requiredObserverIsCovering.HasValue)
                {
                    bool observerIsCovering = NudityMattersMore.InfoHelper.IsCovering(actualObserver, out _);
                    if (observerIsCovering != ext.requiredObserverIsCovering.Value)
                    {
                        continue;
                    }
                }
                if (ext.requiredObserverPregnancyTrimester != PregnancyTrimester.Any)
                {
                    Hediff_Pregnant observerPregnancyHediff = actualObserver?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;
                    if (observerPregnancyHediff == null || GetPregnancyTrimester(observerPregnancyHediff) != ext.requiredObserverPregnancyTrimester) continue;
                }
                if (ext.requiredObserverRelation != null && !actualObserver.relations.DirectRelationExists(ext.requiredObserverRelation, actualObserved)) continue;


                // Check conditions for observable
                if (ext.requiredObservedTrait != null && (actualObserved.story == null || !actualObserved.story.traits.HasTrait(ext.requiredObservedTrait))) continue;
                if (ext.requiredObservedHediffDef != null && (actualObserved.health == null || !actualObserved.health.hediffSet.HasHediff(ext.requiredObservedHediffDef))) continue;
                if (ext.requiredObservedGeneDef != null && (actualObserved.genes == null || !actualObserved.genes.HasActiveGene(ext.requiredObservedGeneDef))) continue;
                if (ext.requiredObservedGender.HasValue && actualObserved.gender != ext.requiredObservedGender.Value) continue;
                if (ext.requiredObservedLifeStage != null && (actualObserved.ageTracker == null || actualObserved.ageTracker.CurLifeStage != ext.requiredObservedLifeStage)) continue;
                if (ext.requiredObservedPawnKind != null && actualObserved.kindDef != ext.requiredObservedPawnKind) continue;
                if (ext.requiredObservedMinBiologicalAge > 0 && (actualObserved.ageTracker == null || actualObserved.ageTracker.AgeBiologicalYears < ext.requiredObservedMinBiologicalAge)) continue;
                if (ext.requiredObservedMaxBiologicalAge > 0 && (actualObserved.ageTracker == null || actualObserved.ageTracker.AgeBiologicalYears <= ext.requiredObservedMaxBiologicalAge)) continue;
                if (ext.requiredObservedNeedSexState != NeedSexState.Any && GetNeedSexState(actualObserved) != ext.requiredObservedNeedSexState) continue;
                if (ext.requiredObservedIsCovering.HasValue)
                {
                    bool observedIsCovering = NudityMattersMore.InfoHelper.IsCovering(actualObserved, out _);
                    if (observedIsCovering != ext.requiredObservedIsCovering.Value)
                    {
                        continue;
                    }
                }
                if (ext.requiredObservedPregnancyTrimester != PregnancyTrimester.Any)
                {
                    Hediff_Pregnant observedPregnancyHediff = actualObserved?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;
                    if (observedPregnancyHediff == null || GetPregnancyTrimester(observedPregnancyHediff) != ext.requiredObservedPregnancyTrimester) continue;
                }
                if (ext.requiredObservedRelation != null && !actualObserved.relations.DirectRelationExists(ext.requiredObservedRelation, actualObserver)) continue;

                // Если все условия пройдены, добавляем мнение в кандидаты
                weighedCandidateOpinions.Add((def, 0)); // Вес будет рассчитан ниже
            }

            // --- РАСЧЕТ ВЕСОВ ДЛЯ ОТОБРАННЫХ КАНДИДАТОВ ---
            // ИСПРАВЛЕНО: Расчет весов теперь происходит после первичной фильтрации,
            // чтобы избежать лишних вычислений для отброшенных мнений.
            List<(OpinionDef_Situational def, int weight)> finalWeighedCandidateOpinions = new List<(OpinionDef_Situational def, int weight)>();
            foreach (var opinionTuple in weighedCandidateOpinions)
            {
                var opinionDef = opinionTuple.def;
                int currentWeight = 0;
                OpinionConditionExtension_Situational ext = opinionDef.GetModExtension<OpinionConditionExtension_Situational>();

                // --- PRIORITY: requiredInteractionType ---
                if (ext.requiredInteractionType == nmmInteractionType && nmmInteractionType != InteractionType.None)
                {
                    currentWeight += 10000; // Very high bonus for exact match of interaction type
                }

                if (ext.perspective == requiredPerspective)
                {
                    currentWeight += 1000; // High bonus for exact match to perspective
                }
                else if (ext.perspective == OpinionPerspective.Any)
                {
                    currentWeight += 10; // Smaller bonus for "Any" perspective (will work if there is no exact match)
                }

                // Add weight based on the state of the garment
                if (ext.requiredObservedDressState == currentObservedDressState)
                {
                    if (ext.requiredObservedDressState == DressState.Naked) currentWeight += 500;
                    else if (ext.requiredObservedDressState == DressState.Topless || ext.requiredObservedDressState == DressState.Bottomless) currentWeight += 100;
                    else if (ext.requiredObservedDressState == DressState.Covering) currentWeight += 200; // Adding weight for Covering
                    else if (ext.requiredObservedDressState == DressState.Clothed) currentWeight += 10; // Smaller weight for clothed
                }

                // Add weight for matching PawnState
                if (ext.requiredPawnState == nmmPawnState && nmmPawnState != PawnState.None)
                {
                    currentWeight += 1000;
                }

                // Add weight for compliance with awareness
                if (ext.requiredObservedAware.HasValue && ext.requiredObservedAware.Value == aware)
                {
                    currentWeight += 30;
                }

                // Add weight for body part matching
                if (ext.requiredBodyPartSeen != null)
                {
                    currentWeight += 40;
                    if (ext.requiredPartSizeRange.HasValue) currentWeight += 10;
                    if (ext.requiredGenitalFamily != GenitalFamily.Undefined) currentWeight += 10;
                }

                // --- Check and weigh conditions for the Observer (actualObserver) ---
                if (ext.requiredObserverTrait != null && actualObserver.story != null && actualObserver.story.traits.HasTrait(ext.requiredObserverTrait)) { currentWeight += 10; }
                if (ext.requiredObserverGender.HasValue && actualObserver.gender == ext.requiredObserverGender.Value) { currentWeight += 5; }
                if (ext.requiredObserverLifeStage != null && actualObserver.ageTracker != null && actualObserver.ageTracker.CurLifeStage == ext.requiredObserverLifeStage) { currentWeight += 5; }
             //   if (!string.IsNullOrEmpty(ext.requiredObserverQuirk))
             //   {
                    // ИСПРАВЛЕНО: Использование кэшированного словаря для быстрого поиска квирков
            //        if (_cachedQuirks.TryGetValue(ext.requiredObserverQuirk, out Quirk targetQuirk) && rjw.PawnExtensions.Has(actualObserver, targetQuirk)) { currentWeight += 15; }
           //     }
                if (ext.requiredObserverMinBiologicalAge > 0 && actualObserver.ageTracker != null && actualObserver.ageTracker.AgeBiologicalYears >= ext.requiredObserverMinBiologicalAge) { currentWeight += 5; }
                if (ext.requiredObserverMaxBiologicalAge > 0 && actualObserver.ageTracker != null && actualObserver.ageTracker.AgeBiologicalYears <= ext.requiredObserverMaxBiologicalAge) { currentWeight += 5; }
                if (ext.requiredObserverNeedSexState != NeedSexState.Any && GetNeedSexState(actualObserver) == ext.requiredObserverNeedSexState) { currentWeight += 10; }
                if (ext.requiredObserverDressState != DressState.Clothed)
                {
                    if (currentObserverDressState == ext.requiredObserverDressState)
                    {
                        currentWeight += 10;
                    }
                }
                if (ext.requiredObserverIsCovering.HasValue)
                {
                    bool observerIsCovering = NudityMattersMore.InfoHelper.IsCovering(actualObserver, out _);
                    if (observerIsCovering == ext.requiredObserverIsCovering.Value)
                    {
                        currentWeight += 10;
                    }
                }
                if (ext.requiredObserverPregnancyTrimester != PregnancyTrimester.Any)
                {
                    Hediff_Pregnant observerPregnancyHediff = actualObserver?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;
                    if (observerPregnancyHediff != null && GetPregnancyTrimester(observerPregnancyHediff) == ext.requiredObserverPregnancyTrimester)
                    {
                        currentWeight += 20;
                    }
                }
                if (ext.requiredObserverRelation != null && actualObserver.relations.DirectRelationExists(ext.requiredObserverRelation, actualObserved)) { currentWeight += 5; }


                // --- Check and weigh conditions for the Observable (actualObserved) ---
                if (ext.requiredObservedTrait != null && actualObserved.story != null && actualObserved.story.traits.HasTrait(ext.requiredObservedTrait)) { currentWeight += 10; }
                if (ext.requiredObservedHediffDef != null && actualObserved.health != null && actualObserved.health.hediffSet != null && actualObserved.health.hediffSet.HasHediff(ext.requiredObservedHediffDef)) { currentWeight += 10; }
                if (ext.requiredObservedGeneDef != null && actualObserved.genes != null && actualObserved.genes.HasActiveGene(ext.requiredObservedGeneDef)) { currentWeight += 10; }
                if (ext.requiredObservedGender.HasValue && actualObserved.gender == ext.requiredObservedGender.Value) { currentWeight += 5; }
                if (ext.requiredObservedLifeStage != null && actualObserved.ageTracker != null && actualObserved.ageTracker.CurLifeStage == ext.requiredObservedLifeStage) { currentWeight += 5; }
                if (ext.requiredObservedMinBiologicalAge > 0 && actualObserved.ageTracker != null && actualObserved.ageTracker.AgeBiologicalYears >= ext.requiredObservedMinBiologicalAge) { currentWeight += 5; }
                if (ext.requiredObservedMaxBiologicalAge > 0 && actualObserved.ageTracker != null && actualObserved.ageTracker.AgeBiologicalYears <= ext.requiredObservedMaxBiologicalAge) { currentWeight += 5; }
                if (ext.requiredObservedNeedSexState != NeedSexState.Any && GetNeedSexState(actualObserved) == ext.requiredObservedNeedSexState) { currentWeight += 10; }
                if (ext.requiredObservedIsCovering.HasValue)
                {
                    bool observedIsCovering = NudityMattersMore.InfoHelper.IsCovering(actualObserved, out _);
                    if (observedIsCovering == ext.requiredObservedIsCovering.Value)
                    {
                        currentWeight += 10;
                    }
                }
                if (ext.requiredObservedPregnancyTrimester != PregnancyTrimester.Any)
                {
                    Hediff_Pregnant observedPregnancyHediff = actualObserved?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;
                    if (observedPregnancyHediff != null && GetPregnancyTrimester(observedPregnancyHediff) == ext.requiredObservedPregnancyTrimester)
                    {
                        currentWeight += 20;
                    }
                }
                if (ext.requiredObservedRelation != null && actualObserved.relations.DirectRelationExists(ext.requiredObservedRelation, actualObserver)) { currentWeight += 5; }

                finalWeighedCandidateOpinions.Add((opinionDef, currentWeight));
            }

            OpinionDef_Situational selectedOpinionDef = null;
            if (finalWeighedCandidateOpinions.Any())
            {
                int maxWeight = finalWeighedCandidateOpinions.Max(x => x.weight);
                var bestOpinions = finalWeighedCandidateOpinions.Where(x => x.weight == maxWeight).ToList();
                selectedOpinionDef = bestOpinions.RandomElement().def;
            }

            string rawText = "";
            if (selectedOpinionDef != null && selectedOpinionDef.opinionTexts.Any())
            {
                rawText = selectedOpinionDef.opinionTexts.RandomElement();
                // Устанавливаем эту переменную, чтобы NMMFixationLogPatches мог ее скопировать.
                SituationalOpinionHelper.LastObservedBodyPartDef = selectedOpinionDef.GetConditionExtension()?.requiredBodyPartSeen;
            }
            else
            {
                SituationalOpinionHelper.LastObservedBodyPartDef = null; // Сбрасываем, если нет мнения
                return ""; // Ничего не найдено, используем запасной Def
            }

            // НЕ ВЫЗЫВАЙТЕ ProcessOpinionText здесь! Это сделает Harmony-патч.
            return rawText; // Возвращаем сырой текст
        }

        /// <summary>
        /// Helper method for determining opinion category (Positive, Negative, Neutral).
        /// This logic can be extended based on relationships, character traits, etc.
        /// </summary>
        /// <summary>
        /// Определяет категорию мнения (Позитивная, Негативная, Нейтральная) на основе взвешенной системы.
        /// </summary>
        /// 

        

        /// <summary>
        /// Gets the current pregnancy trimester from Hediff_Pregnant.
        /// </summary>
        private static PregnancyTrimester GetPregnancyTrimester(Hediff_Pregnant pregnancyHediff)
        {
            if (pregnancyHediff == null) return PregnancyTrimester.Any;

            // Hediff_Pregnant.Progress from 0.0 to 1.0
            float progress = pregnancyHediff.Severity;

            if (progress <= 0.33f) return PregnancyTrimester.First;
            if (progress <= 0.66f) return PregnancyTrimester.Second;
            return PregnancyTrimester.Third;
        }


        /// <summary>
        /// Gets the current Need_Sex state of the pawn.
        /// </summary>
        private static NeedSexState GetNeedSexState(Pawn pawn)
        {
            if (!IsRjwActive || pawn?.needs == null)
            {
                return NeedSexState.Any;
            }

            Need_Sex sexNeed = pawn.needs.TryGetNeed<Need_Sex>();
            if (sexNeed == null)
            {
                return NeedSexState.Any;
            }

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
            else
            {
                return NeedSexState.Satisfied;
            }
        }

        /// <summary>
        /// Determines the DressState of the pawn based on NMM InfoHelper functions.
        /// Modified to correctly detect the 'Covering' state.
        /// </summary>
        private static DressState GetPawnDressState(Pawn pawn)
        {
            if (pawn == null) return DressState.Clothed; // Default if pawn is null


            // Use the if/else if structure to ensure mutually exclusive checks.
            // The order is important: from most specific (Naked) to least.
            if (NudityMattersMore.InfoHelper.IsNaked(pawn))
            {
                return DressState.Naked;
            }

            // Check Topless only if IsNaked returned false.
            else if (NudityMattersMore.InfoHelper.IsTopless(pawn) && NudityMattersMore.InfoHelper.HasBreasts(pawn))
            {
                return DressState.Topless;
            }

            // Check Bottomless only if IsNaked and IsTopless returned false.
            else if (NudityMattersMore.InfoHelper.IsBottomless(pawn))
            {
                return DressState.Bottomless;
            }
            // Check Covering only if the pawn is not naked in any form.
            else if (NudityMattersMore.InfoHelper.IsCovering(pawn, out _))
            {
                return DressState.Covering;
            }
            // If none of the conditions are met, the pawn is dressed.
            else
            {
                return DressState.Clothed;
            }
        }

        /// <summary>
        /// Gets the severity for the given body part.
        /// </summary>
        private static float GetPartSeverity(Pawn pawn, BodyPartDef part)
        {
            if (pawn == null || part == null || !IsRjwActive) return 0f;

            ISexPartHediff sexHediff = null;
            List<Hediff> allSexHediffsOnPawn = rjw.Genital_Helper.get_AllPartsHediffList(pawn);

            if (part.defName == "Genitals")
            {
                sexHediff = allSexHediffsOnPawn.OfType<ISexPartHediff>().FirstOrDefault(h =>
                    (pawn.gender == Gender.Female && (h.Def.genitalFamily == GenitalFamily.Vagina || h.Def.genitalFamily == GenitalFamily.FemaleOvipositor)) ||
                    (h.Def.genitalFamily == GenitalFamily.Vagina));

                if (sexHediff == null)
                {
                    sexHediff = allSexHediffsOnPawn.OfType<ISexPartHediff>().FirstOrDefault(h =>
                        (pawn.gender == Gender.Male && (h.Def.genitalFamily == GenitalFamily.Penis || h.Def.genitalFamily == GenitalFamily.MaleOvipositor)) ||
                        (h.Def.genitalFamily == GenitalFamily.Penis));
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

            if (sexHediff != null && sexHediff.AsHediff != null && sexHediff.AsHediff.TryGetComp<HediffComp_SexPart>(out var sexComp))
            {
                return sexComp.GetSeverity();
            }
            return 0f;
        }

        /// <summary>
        /// Gets the GenitalFamily for the given body part.
        /// </summary>
        private static GenitalFamily GetGenitalFamily(Pawn pawn, BodyPartDef part)
        {
            if (pawn == null || part == null || !IsRjwActive) return GenitalFamily.Undefined;

            List<Hediff> allSexHediffsOnPawn = rjw.Genital_Helper.get_AllPartsHediffList(pawn);
            ISexPartHediff sexHediff = null;

            if (part.defName == "Genitals")
            {
                sexHediff = allSexHediffsOnPawn.OfType<ISexPartHediff>().FirstOrDefault(h =>
                    (pawn.gender == Gender.Female && (h.Def.genitalFamily == GenitalFamily.Vagina || h.Def.genitalFamily == GenitalFamily.FemaleOvipositor)) ||
                    (h.Def.genitalFamily == GenitalFamily.Vagina));

                if (sexHediff == null)
                {
                    sexHediff = allSexHediffsOnPawn.OfType<ISexPartHediff>().FirstOrDefault(h =>
                        (pawn.gender == Gender.Male && (h.Def.genitalFamily == GenitalFamily.Penis || h.Def.genitalFamily == GenitalFamily.MaleOvipositor)) ||
                        (h.Def.genitalFamily == GenitalFamily.Penis));
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

            return sexHediff?.Def.genitalFamily ?? GenitalFamily.Undefined;
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

                    // A torso is considered visible if the top or bottom is visible
                    return profile.HasSeenTop || profile.HasSeenBottom;
                }
            }
            return false;
        }


        /// <summary>
        /// Collects data about the visible body parts of the observed pawn.
        /// </summary>
        /// <param name="actualObserver">The actual observer.</param>
        /// <param name="actualObserved">The actual observable.</param>
        /// <returns>List of tuples: BodyPartDef, Severity, SizeLabel, GenitalFamily.</returns>
        private static List<Tuple<BodyPartDef, float, string, GenitalFamily>> GetVisibleBodyPartsData(Pawn actualObserver, Pawn actualObserved)
        {
            List<Tuple<BodyPartDef, float, string, GenitalFamily>> visibleParts = new List<Tuple<BodyPartDef, float, string, GenitalFamily>>();

            if (actualObserved == null) return visibleParts;

            // Determine which body parts can be visible
            List<BodyPartDef> potentialParts = new List<BodyPartDef>();
            if (actualObserved.gender == Gender.Female || NudityMattersMore.InfoHelper.HasBreasts(actualObserved))
            {

                // Use DefDatabase<BodyPartDef>.GetNamed to get the BodyPartDef "Chest"
                potentialParts.Add(DefDatabase<BodyPartDef>.GetNamed("Chest"));
            }

            // Use DefDatabase<BodyPartDef>.GetNamed to get the BodyPartDef "Genitals"
            potentialParts.Add(DefDatabase<BodyPartDef>.GetNamed("Genitals"));

            // Use DefDatabase<BodyPartDef>.GetNamed to get the BodyPartDef "Anus"
            potentialParts.Add(DefDatabase<BodyPartDef>.GetNamed("Anus"));

            foreach (BodyPartDef partDef in potentialParts)
            {
                if (IsPartSeen(actualObserver, actualObserved, partDef))
                {
                    float severity = GetPartSeverity(actualObserved, partDef);
                    string sizeLabel = GetDescriptiveSizeLabel(severity); // using existing method
                    GenitalFamily genitalFamily = GetGenitalFamily(actualObserved, partDef);


                    // For the chest, if it is a man without chest, we do not add
                    if (partDef.defName == "Chest" && actualObserved.gender == Gender.Male && !NudityMattersMore.InfoHelper.HasBreasts(actualObserved))
                    {
                        continue;
                    }

                    visibleParts.Add(Tuple.Create(partDef, severity, sizeLabel, genitalFamily));
                }
            }
            return visibleParts;
        }

        /// <summary>
        /// Method to get descriptive size label based on severity
        /// </summary>
        private static string GetDescriptiveSizeLabel(float severity)
        {
            if (severity <= 0.15f) // Adjusted ranges for more granularity
            {
                return "tiny";
            }
            else if (severity <= 0.35f)
            {
                return "small";
            }
            else if (severity <= 0.65f)
            {
                return "average";
            }
            else if (severity <= 0.85f)
            {
                return "large";
            }
            else
            {
                return "huge";
            }
        }

        /// <summary>
        /// Gets a specific genital name (e.g. "vagina", "penis") based on GenitalFamily.
        /// </summary>
        /// <param name="pawn">Pawn.</param>
        /// <param name="partDef">Body part definition.</param>
        /// <returns>A string with the genital name, or the LabelCap of the body part if not genital.</returns>
        private static string GetGenitalSpecificLabel(Pawn pawn, BodyPartDef partDef)
        {
            if (pawn == null || partDef == null) return partDef?.LabelCap ?? "unknown part";

            if (partDef.defName == "Genitals" || partDef.defName == "Anus" || partDef.defName == "Breasts" || partDef.defName == "Chest")
            {
                GenitalFamily family = GetGenitalFamily(pawn, partDef);
                switch (family)
                {
                    case GenitalFamily.Vagina: return "vagina";
                    case GenitalFamily.Penis: return "penis";
                    case GenitalFamily.Breasts: return "breasts";
                    case GenitalFamily.Anus: return "anus";
                    case GenitalFamily.FemaleOvipositor: return "female ovipositor";
                    case GenitalFamily.MaleOvipositor: return "male ovipositor";
                    default: return partDef.LabelCap.ToLower(); // Fallback to generic label, lowercase
                }
            }
            return partDef.LabelCap.ToLower();  // For non-genital body parts, lowercase
        }

        /// <summary>
        /// Processes tokens in the opinion text, replacing them with the corresponding pawn data.
        /// Uses both standard RimWorld tokens and the new {OBSERVER_...} / {OBSERVED_...}.
        /// </summary>
        /// <param name="text">The original opinion text with tokens.</param>
        /// <param name="observer">The observer pawn.</param>
        /// <param name="observed">The pawn being observed.</param>
        /// <param name="specificPartDef">Optional: a specific body part, if the token refers to one (for {OBSERVED_genitalSpecificLabel}).</param>
        /// <returns>The processed opinion text.</returns>
        public static string ProcessOpinionText(string text, Pawn observer, Pawn observed, BodyPartDef specificPartDef = null)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Helper to get possessive form of a short label
            Func<Pawn, string> GetShortPossessive = (p) =>
            {
                string shortLabel = p?.LabelShort;
                if (string.IsNullOrEmpty(shortLabel)) return "";
                return shortLabel + (shortLabel.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? "'" : "'s");
            };

            // Handle standard RimWorld {PAWN_...} tokens, assuming they refer to the OBSERVED pawn
            text = text.Replace("{PAWN_nameShort}", observed?.LabelShort ?? "a pawn");
            text = text.Replace("{PAWN_nameFull}", observed?.LabelShort ?? "a pawn");
            text = text.Replace("{PAWN_gender}", observed?.gender.ToString().ToLower() ?? "unknown"); // Lowercase
            text = text.Replace("{PAWN_possessive}", observed?.gender.GetPossessive() ?? "their");
            text = text.Replace("{PAWN_pronoun}", observed?.gender.GetPronoun() ?? "they");
            text = text.Replace("{PAWN_objective}", observed?.gender.GetObjective() ?? "them");
            text = text.Replace("{PAWN_subjective}", observed?.gender.GetPronoun() ?? "they");
            text = text.Replace("{PAWN_kindDef}", observed?.kindDef?.LabelCap.ToLower() ?? "unknown kind"); // Lowercase
            text = text.Replace("{PAWN_faction}", observed?.Faction?.Name ?? "None");
            text = text.Replace("{PAWN_nameShortPossessive}", GetShortPossessive(observed));


            // Handle {OBSERVER_...} and {OBSERVED_...} tokens
            text = text.Replace("{OBSERVER_nameShort}", observer?.LabelShort ?? "a pawn");
            text = text.Replace("{OBSERVED_nameShort}", observed?.LabelShort ?? "a pawn");
            text = text.Replace("{OBSERVER_nameShortPossessive}", GetShortPossessive(observer));
            text = text.Replace("{OBSERVED_nameShortPossessive}", GetShortPossessive(observed));

            text = text.Replace("{OBSERVER_gender}", observer?.gender.ToString().ToLower() ?? "unknown"); // Lowercase
            text = text.Replace("{OBSERVED_gender}", observed?.gender.ToString().ToLower() ?? "unknown"); // Lowercase

            text = text.Replace("{OBSERVER_possessive}", observer?.gender.GetPossessive() ?? "their");
            text = text.Replace("{OBSERVED_possessive}", observed?.gender.GetPossessive() ?? "their");

            text = text.Replace("{OBSERVER_pronoun}", observer?.gender.GetPronoun() ?? "they");
            // Capitalize {OBSERVED_pronoun} if it's at the start of the string or preceded by a period and space
            if (text.StartsWith("{OBSERVED_pronoun}"))
            {
                text = text.Replace("{OBSERVED_pronoun}", observed?.gender.GetPronoun().CapitalizeFirst() ?? "They");
            }
            else
            {
                text = text.Replace(". {OBSERVED_pronoun}", $". {observed?.gender.GetPronoun().CapitalizeFirst() ?? "They"}");
                text = text.Replace("{OBSERVED_pronoun}", observed?.gender.GetPronoun() ?? "they");
            }

            text = text.Replace("{OBSERVER_objective}", observer?.gender.GetObjective() ?? "them");
            text = text.Replace("{OBSERVED_objective}", observed?.gender.GetObjective() ?? "them");

            text = text.Replace("{OBSERVER_nameFull}", observer?.LabelShort ?? "a pawn");
            text = text.Replace("{OBSERVED_nameFull}", observed?.LabelShort ?? "a pawn");
            text = text.Replace("{OBSERVER_kindDef}", observer?.kindDef?.LabelCap.ToLower() ?? "unknown kind"); // Lowercase
            text = text.Replace("{OBSERVED_kindDef}", observed?.kindDef?.LabelCap.ToLower() ?? "unknown kind"); // Lowercase
            text = text.Replace("{OBSERVER_faction}", observer?.Faction?.Name ?? "None");
            text = text.Replace("{OBSERVED_faction}", observed?.Faction?.Name ?? "None");

            // Biological age
            text = text.Replace("{OBSERVER_ageBiologicalYears}", observer?.ageTracker?.AgeBiologicalYears.ToString() ?? "N/A");
            text = text.Replace("{OBSERVED_ageBiologicalYears}", observed?.ageTracker?.AgeBiologicalYears.ToString() ?? "N/A");

            // Tokens for Need_Sex State
            text = text.Replace("{OBSERVER_needSexState}", GetNeedSexState(observer).ToString().ToLower()); // Lowercase
            text = text.Replace("{OBSERVED_needSexState}", GetNeedSexState(observed).ToString().ToLower()); // Lowercase

            // NEW: Token for specific genital name
            if (text.Contains("{OBSERVED_genitalSpecificLabel}"))
            {
                text = text.Replace("{OBSERVED_genitalSpecificLabel}", GetGenitalSpecificLabel(observed, specificPartDef));
            }


            // NEW: Logic for naked and covered state token
            if (text.Contains("{OBSERVED_nudityAndCoveringStatus}"))
            {
                string nudityStatus = NudityMattersMore_opinions.OpinionHelper.GetNudityStatus(observed).ToLower(); // Lowercase
                bool isCovering = NudityMattersMore.InfoHelper.IsCovering(observed, out _);
                DressState observedDressState = GetPawnDressState(observed); // Get DressState for the observed pawn

                string combinedStatus = nudityStatus;

                if (isCovering)
                {
                    if (observedDressState == DressState.Naked)
                    {
                        combinedStatus = "fully nude, but covering";
                    }
                    else if (observedDressState == DressState.Topless)
                    {
                        combinedStatus = "topless, but covering";
                    }
                    else if (observedDressState == DressState.Bottomless)
                    {
                        combinedStatus = "bottomless, but covering";
                    }
                    else  // If not one of the above, but covered (for example, in clothes, but covered)
                    {
                        combinedStatus = $"{nudityStatus} and covering";
                    }
                }
                text = text.Replace("{OBSERVED_nudityAndCoveringStatus}", combinedStatus);
            }
            else  // If the {OBSERVED_nudityAndCoveringStatus} token is not used, keep the old tokens for backwards compatibility
            {

                // Tokens for cover state
                string observerCoveringStatus = (observer != null && NudityMattersMore.InfoHelper.IsCovering(observer, out _)) ? "covering" : "not covering";
                string observedCoveringStatus = (observed != null && NudityMattersMore.InfoHelper.IsCovering(observed, out _)) ? "covering" : "not covering";
                text = text.Replace("{OBSERVER_coveringStatus}", observerCoveringStatus);
                text = text.Replace("{OBSERVED_coveringStatus}", observedCoveringStatus);


                // Tokens for NMM nudity status (Using your OpinionHelper.GetNudityStatus)
                text = text.Replace("{OBSERVED_nudityStatus}", NudityMattersMore_opinions.OpinionHelper.GetNudityStatus(observed).ToLower()); // Lowercase
            }



            // ADDED: Tokens for relationships between pawns
            // Observer relationship to the observed pawn
            var observerToObservedRel = PawnRelationUtility.GetMostImportantRelation(observer, observed);
            string observerToObservedRelationLabel = observerToObservedRel != null ? observerToObservedRel.GetGenderSpecificLabel(observer).ToLower() : "pawn"; // Lowercase
            text = text.Replace("{OBSERVED_relationLabel}", observerToObservedRelationLabel);


            // The relationship of the observed pawn to the observer
            var observedToObserverRel = PawnRelationUtility.GetMostImportantRelation(observed, observer);
            string observedToObserverRelationLabel = observedToObserverRel != null ? observedToObserverRel.GetGenderSpecificLabel(observed).ToLower() : "pawn"; // Lowercase
            text = text.Replace("{OBSERVER_relationLabel}", observedToObserverRelationLabel);


            return text;
        }
    }


}
