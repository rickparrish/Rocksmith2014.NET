module DLCBuilder.Views.IssueViewer

open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Media
open Rocksmith2014.DLCProject
open Rocksmith2014.XML.Processing
open Rocksmith2014.XML.Processing.Utils
open DLCBuilder

let private getIssueHeaderAndHelp issueType =
    match issueType with
    | EventBetweenIntroApplause eventCode ->
        translatef "EventBetweenIntroApplause" [| eventCode |],
        translate "EventBetweenIntroApplauseHelp"
    | LyricWithInvalidChar (invalidChar, customFontUsed) ->
        let postfix = if customFontUsed then "CustomFont" else ""
        translatef $"LyricWithInvalidChar{postfix}" [| invalidChar |],
        translate $"LyricWithInvalidChar{postfix}Help"
    | LyricTooLong invalidLyric ->
        translatef "LyricTooLong" [| invalidLyric |],
        translate "LyricTooLongHelp"
    | other ->
        let locStr = string other
        translate locStr,
        translate (locStr + "Help")

let private isImportant = function
    | LinkNextMissingTargetNote
    | LinkNextSlideMismatch
    | LinkNextFretMismatch
    | LinkNextBendMismatch
    | IncorrectLinkNext
    | UnpitchedSlideWithLinkNext
    | PhraseChangeOnLinkNextNote
    | MissingBendValue
    | MissingLinkNextChordNotes
    | AnchorInsideHandShape
    | AnchorInsideHandShapeAtPhraseBoundary
    | FirstPhraseNotEmpty
    | LyricTooLong _
    | MutedStringInNonMutedChord
    | InvalidShowlights
    | OverlappingBendValues
    | InvalidBassArrangementString
    | LowBassTuningWithoutWorkaround -> true
    | _ -> false

let private viewForIssue dispatch issueType times canIgnore =
    let header, help = getIssueHeaderAndHelp issueType

    StackPanel.create [
        StackPanel.margin (0., 5.)
        StackPanel.children [
            // Issue Type & Explanation
            hStack [
                TextBlock.create [
                    TextBlock.text header
                    TextBlock.fontSize 16.
                    TextBlock.verticalAlignment VerticalAlignment.Center
                ]
                // Hep Button
                HelpButton.create [
                    HelpButton.helpText help
                ]
                // Ignore/Enable Issue Button
                Button.create [
                    ToolTip.tip (translate (if canIgnore then "IgnoreIssueToolTip" else "EnableIssueToolTip"))
                    Button.content (translate (if canIgnore then "Ignore" else "Enable"))
                    Button.onClick ((fun _ ->
                        let msg =
                            if canIgnore then
                                IgnoreIssueForProject
                            else
                                EnableIssueForProject

                        issueType
                        |> issueCode
                        |> msg
                        |> dispatch), SubPatchOptions.Always)
                ]
            ]

            if not <| List.isEmpty times then
                // Issue Times
                WrapPanel.create [
                    WrapPanel.maxWidth 600.
                    WrapPanel.children times
                ]
        ]
    ] |> generalize

let private toIssueView dispatch (canIgnore: bool) (issues: Issue list) =
    issues
    |> List.groupBy (fun issue -> issue.IssueType)
    |> List.map (fun (issueType, issues) ->
        let issueTimes =
            issues
            |> List.choose (fun issue ->
                match issue with
                | GeneralIssue _ ->
                    None
                | IssueWithTimeCode (_, time) ->
                    TextBlock.create [
                        TextBlock.margin (10., 2.)
                        TextBlock.fontSize 16.
                        TextBlock.fontFamily Media.Fonts.monospace
                        TextBlock.text (timeToString time)
                    ]
                    |> generalize
                    |> Some
            )

        viewForIssue dispatch issueType issueTimes canIgnore)

let private issueListHeader locString =
    Border.create [
        Border.cornerRadius 8.
        Border.borderThickness 2.
        Border.borderBrush Brushes.Gray
        Border.background "#222"
        Border.child (
            TextBlock.create [
                TextBlock.classes [ "issue-header" ]
                TextBlock.text (translate locString)
            ]
        )
    ]

let view state dispatch (arrangement: Arrangement) =
    let issues =
        state.ArrangementIssues
        |> Map.tryFind (Arrangement.getId arrangement)
        |> Option.map (fun issues ->
            // Divide the issues into ignored and active
            let ignored, active =
                issues
                |> List.partition (fun issue ->
                    state.Project.IgnoredIssues.Contains(issueCode issue.IssueType))

            // Divide the active ones into important and minor issues
            let important, minor =
                active
                |> List.partition (fun x -> isImportant x.IssueType)

            {| Important = toIssueView dispatch true important
               Minor = toIssueView dispatch true minor
               Ignored = toIssueView dispatch false ignored |})

    StackPanel.create [
        StackPanel.spacing 8.
        StackPanel.minWidth 500.
        StackPanel.children [
            StackPanel.create [
                StackPanel.orientation Orientation.Horizontal
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.children [
                    // Icon
                    Path.create [
                        Path.fill Brushes.Gray
                        Path.data Media.Icons.alertTriangle
                        Path.verticalAlignment VerticalAlignment.Center
                        Path.margin (0., 0., 14., 0.)
                    ]
                    // Header Text
                    locText "Issues" [
                        TextBlock.fontSize 18.
                    ]
                ]
            ]

            // Validate Again Button
            Button.create [
                Button.content (translate "ValidateAgain")
                Button.padding (0., 4.)
                Button.onClick (fun _ -> arrangement |> CheckArrangement |> dispatch)
                Button.isEnabled (not (state.RunningTasks |> Set.contains ArrangementCheckOne))
            ]

            ScrollViewer.create [
                ScrollViewer.maxHeight 500.
                ScrollViewer.maxWidth 700.
                ScrollViewer.content (
                    vStack [
                        match issues with
                        | None ->
                            // Should not happen unless the program ends up in an invalid state
                            TextBlock.create [
                                TextBlock.text "ERROR: Validation issues for the arrangement could not be found."
                                TextBlock.horizontalAlignment HorizontalAlignment.Center
                                TextBlock.verticalAlignment VerticalAlignment.Center
                            ]
                        | Some issues ->
                            if issues.Important.IsEmpty && issues.Minor.IsEmpty && issues.Ignored.IsEmpty then
                                TextBlock.create [
                                    TextBlock.text (translate "NoIssuesFound")
                                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                                    TextBlock.verticalAlignment VerticalAlignment.Center
                                ]
                            else
                                // Important
                                if not issues.Important.IsEmpty then
                                    issueListHeader "ImportantIssues"
                                    yield! issues.Important

                                // Minor
                                if not issues.Minor.IsEmpty then
                                    issueListHeader "MinorIssues"
                                    yield! issues.Minor

                                // Ignored
                                if not issues.Ignored.IsEmpty then
                                    issueListHeader "IgnoredIssues"
                                    yield! issues.Ignored
                    ]
                )
            ]

            // OK button
            Button.create [
                Button.fontSize 18.
                Button.padding (80., 10.)
                Button.horizontalAlignment HorizontalAlignment.Center
                Button.content (translate "OK")
                Button.onClick (fun _ -> dispatch (CloseOverlay OverlayCloseMethod.OverlayButton))
                Button.isDefault true
            ]
        ]
    ] |> generalize
