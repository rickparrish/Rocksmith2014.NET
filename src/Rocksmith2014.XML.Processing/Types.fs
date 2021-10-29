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
    | MissingIgnore
    | SeventhFretHarmonicWithSustain
    | MissingBendValue
    | ToneChangeOnNote
    | NoteInsideNoguitarSection
    | VaryingChordNoteSustains
    | MissingLinkNextChordNotes
    | ChordAtEndOfHandShape
    | FingeringAnchorMismatch
    | AnchorInsideHandShape
    | AnchorInsideHandShapeAtPhraseBoundary
    | AnchorCloseToUnpitchedSlide
    | AnchorNotOnNote of distance: int
    | FirstPhraseNotEmpty
    | NoEndPhrase
    | LyricWithInvalidChar of invalidChar: char
    | LyricTooLong of lyric: string
    | LyricsHaveNoLineBreaks
    | InvalidShowlights

type Issue =
    { Type: IssueType
      TimeCode: int }
