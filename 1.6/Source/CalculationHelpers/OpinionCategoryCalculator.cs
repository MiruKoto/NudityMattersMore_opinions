using NudityMattersMore; //  InfoHelper, PawnInteractionManager, DressState, InteractionType, PawnState, TabGender
using NudityMattersMore_opinions.CalculationHelpers;
using RimWorld; // TraitDef, PawnKindDef, LifeStageDef, Need_Sex (for thresh_*), HediffDefOf, PawnRelationDefOf
using rjw; // Quirk, Genital_Helper, ISexPartHediff, HediffComp_SexPart
using System; // Exception
using System.Collections.Generic; //  List, Dictionary
using System.Linq; //  LINQ
using System.Runtime.InteropServices;
using Verse;

namespace NudityMattersMore_opinions.CalculationHelpers
{

    
    public static class OpinionCategoryCalculator
    {
        // Эти поля нужно инициализировать
        private static readonly bool IsrjwsexperienceideologyActive;
        public static readonly bool IsRjwActive;
        public static readonly bool IsPrivacyPleaseActive;

        [StaticConstructorOnStartup]
        public static class IdeoDefs
        {
            public static readonly MemeDef Collectivist = DefDatabase<MemeDef>.GetNamedSilentFail("Collectivist");
            public static readonly MemeDef Individualist = DefDatabase<MemeDef>.GetNamedSilentFail("Individualist");
            public static readonly MemeDef Nudism = DefDatabase<MemeDef>.GetNamedSilentFail("Nudism");
            public static readonly MemeDef MaleSupremacy = DefDatabase<MemeDef>.GetNamedSilentFail("MaleSupremacy");
            public static readonly MemeDef FemaleSupremacy = DefDatabase<MemeDef>.GetNamedSilentFail("FemaleSupremacy");

            public static readonly PreceptDef NudityAlwaysMandatory = DefDatabase<PreceptDef>.GetNamedSilentFail("Nudity_Always_Mandatory_Everyone");
        }

        // +++ ДОБАВЛЕНО: Статический конструктор для инициализации полей +++
        static OpinionCategoryCalculator()
        {
            // Проверяем активность модов один раз при запуске
            IsRjwActive = ModLister.HasActiveModWithName("RimJobWorld");
            IsrjwsexperienceideologyActive = ModLister.HasActiveModWithName("rjw.sexperience.ideology");
            IsPrivacyPleaseActive = ModLister.HasActiveModWithName("abscon.privacy.please");
        }
        // +++ ДОБАВЛЕНО: Метод GetNeedSexState из SituationalOpinionHelper.cs +++
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

            if (sexNeed.CurLevel <= sexNeed.thresh_frustrated()) return NeedSexState.Frustrated;
            if (sexNeed.CurLevel <= sexNeed.thresh_horny()) return NeedSexState.Horny;
            if (sexNeed.CurLevel <= sexNeed.thresh_neutral()) return NeedSexState.Neutral;

            return NeedSexState.Satisfied; // Упрощенный вариант, можно расширить до Ahegao если нужно
        }

        // +++ ДОБАВЛЕНО: Метод GetPartSeverity из SituationalOpinionHelper.cs +++
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

        private static bool IsAttractedToGender(Pawn observer, Gender targetGender)
        {
            if (observer == null || observer.story?.traits == null)
                return false;

            var traits = observer.story.traits;

            // Асексуал — ни к кому не испытывает влечения
            if (traits.HasTrait(TraitDefOf.Asexual))
                return false;

            bool isGay = traits.HasTrait(TraitDefOf.Gay);
            bool isBi = traits.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("Bisexual")); // для модов, если есть

            if (isBi)
                return true; // бисексуалы привлекаются к любому полу

            // Гей — привлекается к тому же полу
            if (isGay)
                return observer.gender == targetGender;

            // По умолчанию — гетеро
            return observer.gender != targetGender;
        }
        [DefOf]
        public static class InternalDefOf
        {
            static InternalDefOf()
            {
                DefOfHelper.EnsureInitializedInCtor(typeof(InternalDefOf));
            }

            public static GeneDef Ageless;
        }

        public static OpinionCategory GetCategory(Pawn opinionPawn, Pawn targetPawn)
        {
            if (opinionPawn == null || targetPawn == null) return OpinionCategory.Neutral;

            // 1. Starting weight
            int totalWeight = 0;

            // 2. Social relations weights
            int opinionOfTarget = opinionPawn.relations.OpinionOf(targetPawn);
            int relationWeight = opinionOfTarget / 10;
            relationWeight = Math.Max(-7, Math.Min(7, relationWeight)); // Limiting output from -7 to +7
            totalWeight += relationWeight;

            // 3. Attraction
            // Straight using vanilla beauty definition.
            totalWeight += (int)targetPawn.GetStatValue(StatDefOf.PawnBeauty);


            // 4. Sexual attraction
            if (opinionPawn.story?.traits != null)
            {
                var traits = opinionPawn.story.traits;
                bool isAsexual = traits.HasTrait(TraitDefOf.Asexual);

                if (isAsexual)
                {
                    totalWeight -= 3; // Assexuals will give negative, at least for now
                }
                else
                {
                    if (IsAttractedToGender(opinionPawn, targetPawn.gender))
                    {
                        totalWeight += 3; // bonus for sexual atraction
                    }
                }
            }

            // 5. Traits influence
            if (opinionPawn.story?.traits != null)
            {

                var traits = opinionPawn.story.traits;
                if (traits.HasTrait(TraitDefOf.Kind)) totalWeight += 2;
                if (traits.HasTrait(TraitDefOf.Abrasive)) totalWeight -= 2;
                if (traits.HasTrait(TraitDefOf.Psychopath)) totalWeight -= 4;
                if (traits.HasTrait(TraitDefOf.Bloodlust)) totalWeight -= 2;
                if (traits.HasTrait(TraitDefOf.Nudist)) totalWeight += 1;
                if (traits.HasTrait(TraitDefOf.BodyPurist)) totalWeight += 1;
                if (traits.HasTrait(TraitDefOf.DislikesMen) && targetPawn.gender == Gender.Male) totalWeight -= 10;
                if (traits.HasTrait(TraitDefOf.DislikesWomen) && targetPawn.gender == Gender.Female) totalWeight -= 10;
                if (traits.HasTrait(TraitDefOf.Jealous)) totalWeight -= 1;
                if (traits.HasTrait(TraitDefOf.Joyous)) totalWeight += 1;
                if (traits.HasTrait(TraitDefOf.Greedy)) totalWeight -= 1;
                if (traits.HasTrait(TraitDefOf.Occultist)) totalWeight -= 1;
                if (traits.HasTrait(TraitDefOf.Transhumanist)) totalWeight -= 1;
                if (traits.HasTrait(TraitDefOf.VoidFascination)) totalWeight -= 1;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.NMM_Exhibitionist) == true) totalWeight += 2;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.NMM_Voyeur) == true) totalWeight += 4;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.NMM_Prude) == true) totalWeight -= 8;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.faithful) == true)
                {
                    bool hasLoveRelation = LovePartnerRelationUtility.LovePartnerRelationExists(opinionPawn, targetPawn);

                    if (hasLoveRelation)
                    {
                        totalWeight += 4;
                    }
                    else
                    {
                        totalWeight -= 4;
                    }
                }
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.philanderer) == true)
                {
                    if (IsAttractedToGender(opinionPawn, targetPawn.gender))
                    {
                        totalWeight += 3;
                    }
                    else
                    {
                        totalWeight -= 2;
                    }
                }
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.polyamorous) == true)
                {
                    if (LovePartnerRelationUtility.LovePartnerRelationExists(opinionPawn, targetPawn))
                    {
                        totalWeight -= 4;
                    }
                    else
                    {
                        if (IsAttractedToGender(opinionPawn, targetPawn.gender))
                        {
                            totalWeight += 4;
                        }
                        else
                        {
                            totalWeight += 1;
                        }
                    }
                }

                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.RCT_Savant) == true) totalWeight -= 1;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.RCT_AnimalLover) == true) totalWeight -= 1;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.nymphomaniac) == true) totalWeight += 2;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.masochist) == true) totalWeight += 2;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.necrophiliac) == true) totalWeight -= 2;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.rapist) == true) totalWeight += 3;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.zoophile) == true) totalWeight -= 2;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.footSlut) == true) totalWeight += 1;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.cumSlut) == true && targetPawn.gender == Gender.Male) totalWeight += 2;
                if (opinionPawn.story?.traits?.HasTrait(CheckForTraits.buttSlut) == true && targetPawn.gender == Gender.Male) totalWeight += 2;

            }

            // Age check

            float checkAge = targetPawn.ageTracker.AgeBiologicalYears;

            // Is biotech DLC active and have Agless gene
            if (ModsConfig.BiotechActive && targetPawn.genes?.HasActiveGene(InternalDefOf.Ageless) == true)
            {
                totalWeight += 2;
            }
            else
            {
                // teen to young adult
                if (checkAge < 18)
                {
                    totalWeight += 3;
                }
                else if (checkAge >= 18 && checkAge < 30)
                {
                    // young adult
                    totalWeight += 2;
                }
                else if (checkAge >= 30 && checkAge < 49)
                {
                    // middle aged adult
                    totalWeight += 1;
                }
                else if (checkAge >= 50 && checkAge < 60)
                {
                    // post middle age
                    totalWeight -= 2;
                }
                else // old 60+
                {
                    totalWeight -= 3;
                }
            }


            // Sexual desire (RJW)
            if (IsRjwActive)
            {
                NeedSexState sexNeedState = GetNeedSexState(opinionPawn);
                if (sexNeedState == NeedSexState.Frustrated) totalWeight -= 3;
                if (sexNeedState == NeedSexState.Horny) totalWeight += 3;
                if (sexNeedState == NeedSexState.Ahegao) totalWeight += 2;
            }


            // 6. IDEOLOGY PERCEPTS
            if (IsrjwsexperienceideologyActive && opinionPawn.Ideo != null)
            {
                // Start, getting Def for genitals. 'false' in the end prevents error if not found.
                var genitalsDef = DefDatabase<BodyPartDef>.GetNamed("Genitals", false);

                if (genitalsDef != null)
                {
                    // Checking for severity range
                    float genitalSeverity = GetPartSeverity(targetPawn, genitalsDef);

                    // Does observer belives in bigger better
                    if (opinionPawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamed("GenitalSize_Big_Better")))
                    {
                        // Big genitals size range (severity >= 0.60)
                        if (genitalSeverity >= 0.6f)
                        {
                            totalWeight += 8; // overshooting with positive
                        }
                        // If size is not in category of big (severity <= 0.4)
                        else if (genitalSeverity <= 0.4f)
                        {
                            totalWeight -= 8; // overshooting with negative
                        }
                    }
                    // Else checking if observer believes in small supremacy
                    else if (opinionPawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamed("GenitalSize_Smaller_Better")))
                    {
                        // If small or lesser
                        if (genitalSeverity <= 0.4f)
                        {
                            totalWeight += 8; // Bonus to overshoot total opinion
                        }
                        // If large or bigger
                        else if (genitalSeverity >= 0.6f)
                        {
                            totalWeight -= 8; // Overshoot for more negative reaction
                        }
                    }
                }
            }
            // Exhibitionism precept
            if (IsPrivacyPleaseActive && opinionPawn.Ideo != null)
            {
                // Does observer belives nudity is blessing
                if (opinionPawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamed("Exhibitionism_Approved")))
                {
                    totalWeight += 8;
                }
                // Or checking if Exhibitionism is just acceptable
                else if (opinionPawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamed("Exhibitionism_Acceptable")))
                {
                    totalWeight += 2; // small bonus
                }
                // Or penalty for being nude for observed
                else if (opinionPawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamed("Exhibitionism_Disapproved")))
                {
                    totalWeight -= 6;
                }

                // If percept doesnt exist in ideo, no gain or loss
            }

            // Vanilla ideology DLC
            if (ModsConfig.IdeologyActive && opinionPawn.Ideo != null)
            {
                if (opinionPawn.Ideo.HasMeme(IdeoDefs.MaleSupremacy) && targetPawn.gender == Gender.Female)
                    totalWeight -= 5;

                if (opinionPawn.Ideo.HasMeme(IdeoDefs.FemaleSupremacy) && targetPawn.gender == Gender.Male)
                    totalWeight -= 5;

                if (opinionPawn.Ideo.HasMeme(IdeoDefs.Nudism) || opinionPawn.Ideo.HasPrecept(IdeoDefs.NudityAlwaysMandatory))
                    totalWeight += 3;

                if (opinionPawn.Ideo.HasMeme(IdeoDefs.Collectivist))
                    totalWeight += 1;

                if (opinionPawn.Ideo.HasMeme(IdeoDefs.Individualist))
                    totalWeight -= 1;
            }


            // 7. Summary, deciding positive, neutral, negative
            if (totalWeight >= 5) return OpinionCategory.Positive;
            if (totalWeight <= -5) return OpinionCategory.Negative;

            return OpinionCategory.Neutral;
        }
    }
}
