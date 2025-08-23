using Verse; 
using RimWorld; //  TraitDef, GeneDef, LifeStageDef, PawnKindDef, PawnRelationDef
using System; 
using rjw; 
using System.Collections.Generic;
using NudityMattersMore; // DressState, InteractionType, PawnState

namespace NudityMattersMore_opinions
{

    /// <summary>
    /// Enumeration to indicate which "perspective" the opinion is for.
    /// UsedForObserver: This is the observer's opinion of the observed pawn.
    /// UsedForObserved: This is the observed pawn's opinion of being seen.
    /// UsedForSelf: This is the pawn's opinion of itself (self-observation).
    /// </summary>
    public enum OpinionPerspective
    {
        Any, // Applies to any perspective unless a more specific opinion is given
        UsedForObserver, // Observer's opinion of another
        UsedForObserved, // Observer's opinion of being seen
        UsedForSelf // Pawn's opinion of itself
    }


    /// <summary>
    /// Enumeration for specifying the trimester of pregnancy.
    /// </summary>
    public enum PregnancyTrimester
    {
        Any,
        First,
        Second,
        Third
    }

    /// <summary>
    /// Def extension class for defining conditions for activating situational opinions.
    /// Allows flexible configuration of when and for whom a certain opinion will be triggered.
    /// Fields from OpinionConditionExtension.cs have been merged to avoid duplicating conditions
    /// and XML parsing errors.
    /// </summary>
    public class OpinionConditionExtension_Situational : DefModExtension
    {
        /// <summary>
        /// The perspective to which this opinion pertains (observer, observed, self).
        /// </summary>
        public OpinionPerspective perspective = OpinionPerspective.Any;

        // --- Conditions for OBSERVER pawn ---
        /// <summary>
        /// TraitDef required for observer pawn to activate this opinion.
        /// If null, the condition on the trait does not apply.
        /// </summary>
        public TraitDef requiredObserverTrait;

        /// <summary>
        /// The required HediffDef of the observer pawn.
        /// If null, the condition does not apply.
        /// </summary>
        public HediffDef requiredObserverHediffDef;

        /// <summary>
        /// The required GeneDef of the observer pawn.
        /// If null, the condition does not apply.
        /// </summary>
        public GeneDef requiredObserverGeneDef;

        /// <summary>
        /// The required Gender of the observer pawn.
        /// Nullable for optionality (Male, Female, None).
        /// </summary>
        public Gender? requiredObserverGender;

        /// <summary>
        /// The required life stage (LifeStageDef) of the observer pawn.
        /// If null, the life stage condition is not applied.
        /// </summary>
        public LifeStageDef requiredObserverLifeStage;

        /// <summary>
        /// The required PawnKindDef of the spectating pawn.
        /// If null, the condition does not apply.
        /// </summary>
        public PawnKindDef requiredObserverPawnKind;


        /// <summary>
        /// The required Quirk of the observer pawn.
        /// If null or an empty string, the Quirk condition is not applied.
        /// </summary>
       // public string requiredObserverQuirk;

        /// <summary>
        /// Minimum biological age (in years) of the observer pawn.
        /// If 0 or less, the minimum age requirement does not apply.
        /// </summary>
        public int requiredObserverMinBiologicalAge;

        /// <summary>
        /// Maximum biological age (in years) of the observer pawn.
        /// If 0 or less, the maximum age condition does not apply.
        /// </summary>
        public int requiredObserverMaxBiologicalAge;

        /// <summary>
        /// The required Need_Sex state of the observer pawn.
        /// Defaults to Any if not specified.
        /// </summary>
        public NeedSexState requiredObserverNeedSexState = NeedSexState.Any;

        /// <summary>
        /// The desired state of clothing for the spectating pawn.
        /// Defaults to Clothed if not specified.
        /// </summary>
        public DressState requiredObserverDressState = DressState.Clothed;

        /// <summary>
        /// Whether the observing pawn is required to cover.
        /// </summary>
        public bool? requiredObserverIsCovering;

        /// <summary>
        /// The required trimester of pregnancy for the observer.
        /// Only used if requiredObserverHediffDef is set to Pregnant.
        /// </summary>
        public PregnancyTrimester requiredObserverPregnancyTrimester = PregnancyTrimester.Any;


        /// <summary>
        /// The required relationship of the observer to the observed pawn (e.g. Lover, Spouse).
        /// If null, the condition on the relationship does not apply.
        /// </summary>
        public PawnRelationDef requiredObserverRelation;



        // --- Conditions for OBSERVED pawn ---
        /// <summary>
        /// TraitDef required on the observed pawn to activate this opinion.
        /// If null, the condition on the trait is not applied.
        /// </summary>
        public TraitDef requiredObservedTrait;

        /// <summary>
        /// The required HediffDef of the observed pawn.
        /// If null, the condition does not apply.
        /// </summary>
        public HediffDef requiredObservedHediffDef;

        /// <summary>
        /// The required GeneDef of the observed pawn.
        /// If null, the condition does not apply.
        /// </summary>
        public GeneDef requiredObservedGeneDef;

        /// <summary>
        /// The required Gender of the observed pawn.
        /// Nullable for optionality (Male, Female, None).
        /// </summary>
        public Gender? requiredObservedGender;


        /// <summary>
        /// The required life stage (LifeStageDef) of the observed pawn.
        /// If null, the life stage condition is not applied.
        /// </summary>
        public LifeStageDef requiredObservedLifeStage;

        /// <summary>
        /// The required PawnKindDef of the observed pawn.
        /// If null, the condition does not apply.
        /// </summary>
        public PawnKindDef requiredObservedPawnKind;

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
        /// The required Need_Sex state of the observed pawn.
        /// Defaults to Any if not specified.
        /// </summary>
        public NeedSexState requiredObservedNeedSexState = NeedSexState.Any;


        /// <summary>
        /// If true, this opinion is for self-observation only (observer == observed).
        /// If false, this opinion is for observing other pawns only (observer != observed).
        /// Defaults to false (for observing others).
        /// </summary>
        public bool isSelfOpinion = false; // По умолчанию - не самооценка



        // --- Situational Opinion Specific Conditions (from your original file) ---
        /// <summary>
        /// The required state of clothing for the observed pawn.
        /// Defaults to Clothed if not specified.
        /// </summary>
        public DressState requiredObservedDressState = DressState.Clothed; // По умолчанию - одета, если не указано

        /// <summary>
        /// Требуется ли, чтобы наблюдаемая пешка прикрывалась.
        /// </summary>
        public bool? requiredObservedIsCovering; // Nullable bool для опциональности (true, false, Any)

        /// <summary>
        /// Whether the observed pawn is required to be covered.
        /// </summary>
        public InteractionType requiredInteractionType = InteractionType.None;

        /// <summary>
        /// The PawnState the observed pawn was in
        /// </summary>
        public PawnState requiredPawnState = PawnState.None;


        /// <summary>
        /// Whether the observed pawn is required to be conscious.
        /// </summary>
        public bool? requiredObservedAware; // Nullable bool


        /// <summary>
        /// A specific BodyPartDef that was visible (e.g. Genitals, Chest, Anus).
        /// </summary>
        public BodyPartDef requiredBodyPartSeen;

        /// <summary>
        /// Required body part size range (severity)
        /// </summary>
        public FloatRange? requiredPartSizeRange; // Nullable FloatRange for variaty


        /// <summary>
        /// The required FamilyGenital (e.g. Penis, Vagina, Breasts) for the body part.
        /// </summary>
        public GenitalFamily requiredGenitalFamily = GenitalFamily.Undefined;


        /// <summary>
        /// The required pregnancy trimester of the observed pawn.
        /// Only used if requiredObservedHediffDef is set to Pregnant.
        /// </summary>
        public PregnancyTrimester requiredObservedPregnancyTrimester = PregnancyTrimester.Any;

        /// <summary>
        /// The required relation of the observed pawn to the observer (e.g. Lover, Spouse).
        /// If null, the relation condition does not apply.
        /// </summary>
        public PawnRelationDef requiredObservedRelation;
    }
}
