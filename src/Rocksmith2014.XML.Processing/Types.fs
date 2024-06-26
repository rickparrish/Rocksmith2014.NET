[<AutoOpen>]
module Rocksmith2014.XML.Processing.Types

type IssueType =
    | ApplauseEventWithoutEnd
    | EventBetweenIntroApplause of eventCode: string
    | NoteLinkedToChord
    | LinkNextMissingTargetNote
    | LinkNextSlideMismatch
    | LinkNextFretMismatch
    | LinkNextBendMismatch
    | IncorrectLinkNext
    | UnpitchedSlideWithLinkNext
    | PhraseChangeOnLinkNextNote
    | DoubleHarmonic
    | SeventhFretHarmonicWithSustain
    | NaturalHarmonicWithBend
    | MissingBendValue
    | OverlappingBendValues
    | ToneChangeOnNote
    | NoteInsideNoguitarSection
    | MissingLinkNextChordNotes
    | FingeringAnchorMismatch
    | PossiblyWrongChordFingering
    | BarreOverOpenStrings
    | MutedStringInNonMutedChord
    | AnchorInsideHandShape
    | AnchorInsideHandShapeAtPhraseBoundary
    | AnchorCloseToUnpitchedSlide
    | FirstPhraseNotEmpty
    | NoEndPhrase
    | MoreThan100Phrases
    | IncorrectMover1Phrase
    | HopoIntoSameNote
    | FingerChangeDuringSlide
    | PositionShiftIntoPullOff
    | InvalidBassArrangementString
    | FretNumberMoreThan24
    | NoteAfterSongEnd
    | TechniqueNoteWithoutSustain
    | LyricWithInvalidChar of invalidChar: char * customFontUsed: bool
    | LyricTooLong of lyric: string
    | LyricsHaveNoLineBreaks
    | InvalidShowlights
    | LowBassTuningWithoutWorkaround
    | IncorrectLowBassTuningForTuningPitch

type Issue =
    | GeneralIssue of issue: IssueType
    | IssueWithTimeCode of issue: IssueType * time: int

    member this.IssueType =
        match this with
        | GeneralIssue t -> t
        | IssueWithTimeCode (t, _) -> t

    member this.TimeCode =
        match this with
        | GeneralIssue _ -> None
        | IssueWithTimeCode (_, time) -> Some time

let issueCode = function
    | ApplauseEventWithoutEnd -> "I01"
    | EventBetweenIntroApplause _ -> "I02"
    | NoteLinkedToChord -> "I03"
    | LinkNextMissingTargetNote -> "I04"
    | LinkNextSlideMismatch -> "I05"
    | LinkNextFretMismatch -> "I06"
    | LinkNextBendMismatch -> "I07"
    | IncorrectLinkNext -> "I08"
    | UnpitchedSlideWithLinkNext -> "I09"
    | PhraseChangeOnLinkNextNote -> "I10"
    | DoubleHarmonic -> "I11"
    //| MissingIgnore -> "I12"
    | SeventhFretHarmonicWithSustain -> "I13"
    | MissingBendValue -> "I14"
    | ToneChangeOnNote -> "I15"
    | NoteInsideNoguitarSection -> "I16"
    //| VaryingChordNoteSustains -> "I17"
    | MissingLinkNextChordNotes -> "I18"
    //| ChordAtEndOfHandShape -> "I19"
    | FingeringAnchorMismatch -> "I20"
    | AnchorInsideHandShape -> "I21"
    | AnchorInsideHandShapeAtPhraseBoundary -> "I22"
    | AnchorCloseToUnpitchedSlide -> "I23"
    //| AnchorNotOnNote _ -> "I24"
    | FirstPhraseNotEmpty -> "I25"
    | NoEndPhrase -> "I26"
    | PossiblyWrongChordFingering -> "I27"
    | BarreOverOpenStrings -> "I28"
    | MutedStringInNonMutedChord -> "I29"
    | MoreThan100Phrases -> "I30"
    | HopoIntoSameNote -> "I31"
    | FingerChangeDuringSlide -> "I32"
    | PositionShiftIntoPullOff -> "I33"
    | OverlappingBendValues -> "I34"
    | NaturalHarmonicWithBend -> "I35"
    | InvalidBassArrangementString -> "I36"
    | IncorrectMover1Phrase -> "I37"
    | FretNumberMoreThan24 -> "I38"
    | NoteAfterSongEnd -> "I39"
    | TechniqueNoteWithoutSustain -> "I40"
    | LowBassTuningWithoutWorkaround -> "I41"
    | IncorrectLowBassTuningForTuningPitch -> "I42"
    | LyricWithInvalidChar _ -> "V01"
    | LyricTooLong _ -> "V02"
    | LyricsHaveNoLineBreaks -> "V03"
    | InvalidShowlights -> "S01"
