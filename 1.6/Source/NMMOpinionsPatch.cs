using HarmonyLib; 
using RimWorld; 
using Verse; 
using NudityMattersMore; 
using NudityMattersMore_opinions; 
using System.Collections.Generic; 
using System.Linq; 

namespace NudityMattersMore_opinions_patches
{
    /// <summary>
    /// Class initializing all Harmony patches for NMM Opinions.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class NMMOpinionsHarmonyPatches
    {
        static NMMOpinionsHarmonyPatches()
        {
            var harmony = new Harmony("shark510.nuditymattersmoreopinions.nmmo.patches");

            // Patch for NudityMattersMore.PawnInteractionManager.CheckPawn
            // We want to intercept the logic of InteractionType detection to add 'Covering'
            harmony.Patch(
                original: AccessTools.Method(typeof(PawnInteractionManager), nameof(PawnInteractionManager.CheckPawn)),
                prefix: new HarmonyMethod(typeof(NMMOpinionsHarmonyPatches), nameof(CheckPawn_Prefix))
            );

            // NEW PATCH: For NudityMattersMore.PawnInteractionManager.ProcessInteraction
            // This patch ensures that InteractionType.Covering is handled correctly
            // and does not result in false nudity detection in NMM.
            harmony.Patch(
                original: AccessTools.Method(typeof(PawnInteractionManager), nameof(PawnInteractionManager.ProcessInteraction)),
                prefix: new HarmonyMethod(typeof(NMMOpinionsHarmonyPatches), nameof(ProcessInteraction_Prefix))
            );

            // Patches for message generation methods (ApplyFirstTimeThought, ApplyRenewThought, ApplyFirstTimeEverThought)
            // We want to intercept them to add our own messages for DressState.Covering
            harmony.Patch(
                original: AccessTools.Method(typeof(PawnInteractionManager), "ApplyFirstTimeThought"),
                prefix: new HarmonyMethod(typeof(NMMOpinionsHarmonyPatches), nameof(ApplyFirstTimeThought_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(PawnInteractionManager), "ApplyRenewThought"),
                prefix: new HarmonyMethod(typeof(NMMOpinionsHarmonyPatches), nameof(ApplyRenewThought_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(PawnInteractionManager), "ApplyFirstTimeEverThought"),
                prefix: new HarmonyMethod(typeof(NMMOpinionsHarmonyPatches), nameof(ApplyFirstTimeEverThought_Prefix))
            );

            ModLog.Message("[NMM Opinions] Harmony patches applied successfully.");
        }

        /// <summary>
        /// Prefix patch for NudityMattersMore.PawnInteractionManager.CheckPawn.
        /// This patch checks if the observed pawn is covered. If so,
        /// it calls ProcessInteraction with InteractionType.Covering and skips the
        /// original CheckPawn logic to avoid duplication or conflicts with NMM's nudity
        /// detection.
        /// </summary>
        /// <param name="observer">The observer.</param>
        /// <param name="observed">The pawn being observed.</param>
        /// <param name="sight">The observer's vision level.</param>
        /// <param name="__instance">The PawnInteractionManager instance (for accessing non-static methods).</param>
        /// <returns>False to skip the original method if we handled the 'Covering' state.</returns>
        public static bool CheckPawn_Prefix(Pawn observer, Pawn observed, float sight, PawnInteractionManager __instance)
        {
            // Check if we ignore family members (logic from original mod)
            // Changed: Access to NudityMattersMore.settings.ignoreFamily
            if (NudityMattersMore.NudityMattersMore.settings.ignoreFamily && InfoHelper.RelatedNotLovers(observer, observed))
            {
                return true; // Continue executing the original method if ignored
            }

            // Get the cover state
            Hediff coverHediff = observed.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("CoverBody"));
            //if (coverHediff == null)
            //{
                // If there is no CoverBody header, it may not have been checked yet.
                // Call CoverCheck so NMM can set it.
               // CoverBody.CoverCheck(observed);
               // coverHediff = observed.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("CoverBody"));
            //}

            bool covering = InfoHelper.IsCovering(observed, out coverHediff);

            // Check if there is a "slip" so as not to confuse "cover" with "slip"
            Hediff slipHediff = observed.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("NipSlip"));
            bool slip = slipHediff != null && (slipHediff.Severity > NipSlip.hediffSlipB || slipHediff.Severity > NipSlip.hediffSlipC);

            // If the pawn is covered AND there is no "slippage"
            if (covering && !slip)
            {
                // Determine the state of the pawn (aware, asleep, etc.)
                // This logic is taken from the original CheckPawn for consistency
                PawnState state = PawnState.None;
                bool aware = observed.Awake();
                bool conscious = observed.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) > .1f;
                Hediff notCovering = observed.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("NotCovering"));

                if (!aware) // If a pawn is unconscious or asleep, it is not "aware"
                {
                    if (!conscious) state = PawnState.Unconscious;
                    else if (!observed.Awake()) state = PawnState.Asleep; // If not awake, but conscious, then asleep
                    else state = PawnState.Unaware; // Just not aware for other reasons
                }
                else if (notCovering != null && notCovering.Severity == CoverBody.hediffCannotCover)
                {
                    state = PawnState.Unable;
                }
                else if (InfoHelper.IsUncaring(observed))
                {
                    state = PawnState.Uncaring;
                }
                else if (InfoHelper.CanSeeNaked(observed, observer))
                {
                    state = PawnState.CanSee;
                }
                else if (notCovering != null && notCovering.Severity == CoverBody.hediffDrafted)
                {
                    state = PawnState.Unallowed;
                }

                // Call ProcessInteraction with our new InteractionType.Covering
                // We pass false for shower, bath, breasts, prude as they are not relevant for "covering" in this context
                // or will be defined inside ProcessInteraction/UpdateInteraction.
                // ProcessInteraction is a public method so we can call it.
                __instance.ProcessInteraction(observer, observed, InteractionType.Covering, state, aware, false, false, InfoHelper.HasBreasts(observed), InfoHelper.IsPrude(observed));

                // Return false to skip the original CheckPawn method.
                // This will prevent further nudity checks (Topless, Bottomless, Naked)
                // for a pawn that is already defined as "Covering".
                return false;
            }

            // If the pawn is not covered or there is "slippage", let the original method execute.
            return true;
        }

        /// <summary>
        /// Prefix patch for NudityMattersMore.PawnInteractionManager.ProcessInteraction.
        /// This patch intercepts the call to ProcessInteraction. If interactionType is InteractionType.Covering,
        /// it manually updates the interaction profiles and logs the event, but at the same time
        /// prevents the original UpdateInteraction from being called, which would make the pawn "naked".
        /// </summary>
        public static bool ProcessInteraction_Prefix(Pawn observer, Pawn observed, InteractionType interactionType, PawnState state, bool aware, bool shower, bool bath, bool breasts, bool prude, PawnInteractionManager __instance)
        {
            if (interactionType == InteractionType.Covering)
            {
                // Get interaction profiles for the observer and the observed.
                string keyObserver = PawnInteractionManager.GenerateKey(observer, observed);
                string keyObserved = PawnInteractionManager.GenerateKey(observed, observer);

                PawnInteractionProfile observerInteraction;
                if (!PawnInteractionManager.InteractionProfiles.TryGetValue(keyObserver, out observerInteraction))
                {
                    observerInteraction = new PawnInteractionProfile();
                    PawnInteractionManager.InteractionProfiles[keyObserver] = observerInteraction;
                }

                PawnInteractionProfile observedInteraction;
                if (!PawnInteractionManager.InteractionProfiles.TryGetValue(keyObserved, out observedInteraction))
                {
                    observedInteraction = new PawnInteractionProfile();
                    PawnInteractionManager.InteractionProfiles[keyObserved] = observedInteraction;
                }

                // Update the "care" flags (they are not related to nudity).
                bool cares = !InfoHelper.CanSeeNaked(observed, observer);
                observerInteraction.TheyCareIfISee = cares;
                observedInteraction.ICareIfTheySee = cares;

                // Important: We do NOT update the HasSeenTop/Bottom or LastSeenTop/Bottom flags here
                // for InteractionType.Covering, to avoid making the pawn "naked" in the NMM logic.
                // The original UpdateInteraction would have been called here if we hadn't intercepted
                // it, and it could have set those flags by default.

                // Log the interaction for the observer and observable.
                // These calls will use InteractionType.Covering,
                // which will allow your opinion system to handle this state correctly.
                AccessTools.Method(typeof(PawnInteractionManager), "LogInteractionObserver").Invoke(__instance, new object[] { observer, observed, interactionType, state, aware });
                AccessTools.Method(typeof(PawnInteractionManager), "LogInteractionObserved").Invoke(__instance, new object[] { observer, observed, interactionType, state, prude, aware });

                // Skip the original ProcessInteraction method since we've already handled the logic for 'Covering'.
                return false;
            }

            return true; // For all other interaction types, continue executing the original ProcessInteraction.
        }

        /// <summary>
        /// Prefix patch for NudityMattersMore.PawnInteractionManager.ApplyFirstTimeThought.
        /// Intercepts the call if DressState == Covering, prints our message and skips the original.
        /// </summary>
        public static bool ApplyFirstTimeThought_Prefix(Pawn observer, Pawn observed, DressState dress)
        {
            // Changed: Access to NudityMattersMore.settings.noNotifs
            if (dress == DressState.Covering)
            {
                if (!NudityMattersMore.NudityMattersMore.settings.noNotifs)
                {
                    string relationNameString = GetRelationsString(observer, observed); // Вспомогательный метод для получения строки отношений
                    Messages.Message($"{observer.LabelShort} увидел, как {relationNameString} впервые прикрывает {observed.gender.GetPossessive()} наготу.", MessageTypeDefOf.PositiveEvent, true);
                }
                return false; // Skip the original method
            }
            return true; // Continue executing the original method
        }

        /// <summary>
        /// Prefix patch for NudityMattersMore.PawnInteractionManager.ApplyRenewThought.
        /// Intercepts the call if DressState == Covering, prints our message and skips the original.
        /// </summary>
        public static bool ApplyRenewThought_Prefix(Pawn observer, Pawn observed, DressState dress)
        {
            // Changed: Access to NudityMattersMore.settings.noNotifs
            if (dress == DressState.Covering)
            {
                if (!NudityMattersMore.NudityMattersMore.settings.noNotifs)
                {
                    string relationNameString = GetRelationsString(observer, observed);
                    Messages.Message($"{observer.LabelShort} увидел, как {relationNameString} снова прикрывает {observed.gender.GetPossessive()} наготу.", MessageTypeDefOf.PositiveEvent, true);
                }
                return false; // Skip the original method
            }
            return true; // Continue executing the original method
        }

        /// <summary>
        /// Prefix patch for NudityMattersMore.PawnInteractionManager.ApplyFirstTimeEverThought.
        /// Intercepts the call if DressState == Covering, prints our message and skips the original.
        /// </summary>
        public static bool ApplyFirstTimeEverThought_Prefix(Pawn observer, Pawn observed, DressState dress, bool observerPOV)
        {
            // Changed: Access to NudityMattersMore.settings.noNotifs
            if (dress == DressState.Covering)
            {
                if (!NudityMattersMore.NudityMattersMore.settings.noNotifs)
                {
                    if (observerPOV)
                    {
                        Messages.Message($"{observer.LabelShort} увидел, как кто-то впервые прикрывает {observed.gender.GetPossessive()} наготу.", MessageTypeDefOf.SituationResolved, true);
                    }
                    else
                    {
                        Messages.Message($"{observed.LabelShort} впервые был(а) замечен(а) прикрывающим(ся) {observed.gender.GetPossessive()} наготу.", MessageTypeDefOf.NegativeEvent, true);
                    }
                }
                return false; // Skip the original method
            }
            return true; // Continue executing the original method
        }

        /// <summary>
        /// Helper method for getting the relationship string, copied from the original PawnInteractionManager.
        /// Necessary because the original method is private.
        /// </summary>
        private static string GetRelationsString(Pawn observer, Pawn observed)
        {
            float opinion = (observer.relations.OpinionOf(observed));

            List<PawnRelationDef> relations = new List<PawnRelationDef>();

            foreach (PawnRelationDef relation in observer.GetRelations(observed))
            {
                relations.Add(relation);
            }

            relations.Sort((PawnRelationDef a, PawnRelationDef b) => b.importance.CompareTo(a.importance));

            string name = observed.LabelShort;

            string text = name;
            if (relations.Count == 0)
            {
                if (opinion < -20)
                {
                    return $"{observer.Possessive()} соперник {name}";
                }
                if (opinion > 20)
                {
                    return $"{observer.Possessive()} друг {name}";
                }
                return name;
            }
            for (int i = 0; i < relations.Count; i++)
            {
                PawnRelationDef pawnRelationDef = relations[i];

                if (pawnRelationDef == PawnRelationDefOf.Parent)
                {
                    text = $"{observer.Possessive()} {pawnRelationDef.GetGenderSpecificLabel(observed)}";
                }
                else
                {
                    text = $"{observer.Possessive()} {pawnRelationDef.GetGenderSpecificLabel(observed)} {name}";
                }
            }
            return text;
        }
    }
}
