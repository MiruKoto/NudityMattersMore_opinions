using Verse;
using RimWorld; // For access TraitDef, GeneDef, LifeStageDef
using System; 
using rjw; // For access Quirk

namespace NudityMattersMore_opinions
{
    // Перечисление для состояний Need_Sex
    public enum NeedSexState
    {
        Any, // State doesn't matter
        Frustrated, // Need level below thresh_frustrated
        Horny, // Need level between thresh_frustrated and thresh_horny
        Neutral, // Need level between thresh_horny and thresh_neutral
        Satisfied, // Need level between thresh_neutral and thresh_satisfied
        Ahegao // Need level above thresh_satisfied (or thresh_ahegao)
    }


    /// <summary>
    /// A Def extension for adding conditions to opinion definitions.
    /// Allows you to specify required traits for the observer, specific hediffs (body parts),
    /// genes, gender, and life stage for the observed/observing pawn, as well as the observer's quirks.
    /// </summary>
    public class OpinionConditionExtension : DefModExtension
    {

        /// <summary>
        /// The required TraitDef on the observer pawn to activate this opinion.
        /// If null, the trait condition is not applied.
        /// </summary>
        public TraitDef requiredObserverTrait;

        /// <summary>
        /// The required HediffDef in the observation pawn (its body part) to strengthen this opinion.
        /// For example, DogPenis is about dog genitalia for me.
        /// If the value is zero, the condition on the Hediff does not apply.
        /// </summary>
        public HediffDef requiredObservedHediffDef;


        /// <summary>
        /// The required gene (GeneDef) of the observed pawn to activate this opinion.
        /// If null, the gene condition is not applied.
        /// </summary>
        public GeneDef requiredObservedGeneDef;

        /// <summary>
        /// The required Gender of the observer pawn to activate this opinion.
        /// Use nullable Gender? for optionality.
        /// </summary>
        public Gender? requiredObserverGender;

        /// <summary>
        /// The required Gender of the observed pawn to activate this opinion.
        /// Use nullable Gender? for optionality.
        /// </summary>
        public Gender? requiredObservedGender;

        /// <summary>
        /// The required life stage (LifeStageDef) of the observed pawn to activate this opinion.
        /// For example, HumanlikeAdult, HumanlikeChild.
        /// If null, the life stage condition is not applied.
        /// </summary>
        public LifeStageDef requiredObservedLifeStage;

        /// <summary>
        /// The required life stage (LifeStageDef) of the observer pawn to activate this opinion.
        /// For example, HumanlikeAdult, HumanlikeChild.
        /// If null, the life stage condition is not applied.
        /// </summary>
        public LifeStageDef requiredObserverLifeStage;

        /// <summary>
        /// The required quirk (a string name of Quirk.Key or Quirk.LocaliztionKey) of the observer pawn.
        /// For example, "ExhibitionistQuirk" or "DemonLoverQuirk".
        /// If null or empty, the quirk condition is not applied.
        /// </summary>
       // public string requiredObserverQuirk;


        /// <summary>
        /// Minimum biological age (in years) of the observer pawn.
        /// If 0 or less, the minimum age requirement does not apply.
        /// </summary>
        public int minBiologicalAge;

        /// <summary>
        /// Maximum biological age (in years) of the observer pawn.
        /// If 0 or less, the maximum age condition does not apply.
        /// </summary>
        public int maxBiologicalAge;

        /// <summary>
        /// Minimum biological age (in years) of the observed pawn.
        /// If 0 or less, the minimum age condition does not apply.
        /// </summary>
        public int requiredObservedMinBiologicalAge;

        /// <summary>
        /// Maximum biological age (in years) of the observed pawn.
        /// If 0 or less, the maximum age condition does not apply.
        /// </summary>
        public int requiredObservedMaxBiologicalAge;

        /// <summary>
        /// The required Need_Sex state of the observer pawn.
        /// Defaults to Any if not specified.
        /// </summary>
        public NeedSexState requiredObserverNeedSexState = NeedSexState.Any;

        /// <summary>
        /// The required Need_Sex state of the observed pawn.
        /// Defaults to Any if not specified.
        /// </summary>
        public NeedSexState requiredObservedNeedSexState = NeedSexState.Any;


        /// <summary>
        /// If true, this opinion is for self-observation only (observer == observed).
        /// If false, this opinion is for observing other pawns only (observer != observed).
        /// Defaults to false (for observing others).
        /// </summary>
        public bool isSelfOpinion = false;
    }
}
