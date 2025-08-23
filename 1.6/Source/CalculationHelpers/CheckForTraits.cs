using RimWorld;
using Verse;
using System.Reflection;

namespace NudityMattersMore_opinions
{
    [StaticConstructorOnStartup]
    internal static class CheckForTraits
    {
        // Romance Diversified
        public static readonly bool RomanceDiversifiedIsActive;
        public static readonly TraitDef straight;
        public static readonly TraitDef faithful;
        public static readonly TraitDef philanderer;
        public static readonly TraitDef polyamorous;

        // Consolidated Traits
        public static readonly bool CTIsActive;
        public static readonly TraitDef RCT_NeatFreak;
        public static readonly TraitDef RCT_Savant;
        public static readonly TraitDef RCT_Inventor;
        public static readonly TraitDef RCT_AnimalLover;

        // RJW
        public static readonly TraitDef nymphomaniac;
        public static readonly TraitDef masochist;
        public static readonly TraitDef necrophiliac;
        public static readonly TraitDef rapist;
        public static readonly TraitDef zoophile;
        public static readonly TraitDef footSlut;
        public static readonly TraitDef cumSlut;
        public static readonly TraitDef buttSlut;

        // NMM (твои трейты)
        public static readonly TraitDef NMM_Prude;
        public static readonly TraitDef NMM_Exhibitionist;
        public static readonly TraitDef NMM_Voyeur;

        static CheckForTraits()
        {
            // Romance Diversified
            RomanceDiversifiedIsActive = ModsConfig.IsActive("neronix17.romancediversified");
            straight = DefDatabase<TraitDef>.GetNamedSilentFail("Straight");
            faithful = DefDatabase<TraitDef>.GetNamedSilentFail("Faithful");
            philanderer = DefDatabase<TraitDef>.GetNamedSilentFail("Philanderer");
            polyamorous = DefDatabase<TraitDef>.GetNamedSilentFail("Polyamorous");

            // Consolidated Traits
            CTIsActive = ModsConfig.IsActive("ConsolidatedTraits");
            RCT_NeatFreak = DefDatabase<TraitDef>.GetNamedSilentFail("RCT_NeatFreak");
            RCT_Savant = DefDatabase<TraitDef>.GetNamedSilentFail("RCT_Savant");
            RCT_Inventor = DefDatabase<TraitDef>.GetNamedSilentFail("RCT_Inventor");
            RCT_AnimalLover = DefDatabase<TraitDef>.GetNamedSilentFail("RCT_AnimalLover");

            // RJW
            nymphomaniac = DefDatabase<TraitDef>.GetNamedSilentFail("Nymphomaniac");
            masochist = DefDatabase<TraitDef>.GetNamedSilentFail("Masochist");
            necrophiliac = DefDatabase<TraitDef>.GetNamedSilentFail("Necrophiliac");
            zoophile = DefDatabase<TraitDef>.GetNamedSilentFail("Zoophile");
            footSlut = DefDatabase<TraitDef>.GetNamedSilentFail("FootSlut");
            cumSlut = DefDatabase<TraitDef>.GetNamedSilentFail("CumSlut");
            buttSlut = DefDatabase<TraitDef>.GetNamedSilentFail("ButtSlut");
            rapist = DefDatabase<TraitDef>.GetNamed("Rapist");

            // NMM
            NMM_Prude = DefDatabase<TraitDef>.GetNamedSilentFail("NMM_Prude");
            NMM_Exhibitionist = DefDatabase<TraitDef>.GetNamedSilentFail("NMM_Exhibitionist");
            NMM_Voyeur = DefDatabase<TraitDef>.GetNamedSilentFail("NMM_Voyeur");
        }
    }
}