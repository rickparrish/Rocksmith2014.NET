﻿module DLCBuilder.Main

open Rocksmith2014.Common
open Rocksmith2014.Common.Manifest
open Rocksmith2014.DLCProject
open Rocksmith2014
open Elmish
open System.Runtime.InteropServices
open System.Xml
open System
open Avalonia
open Avalonia.Media.Imaging
open Avalonia.Input
open Avalonia.Platform
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.FuncUI.DSL
open Avalonia.Layout

let private loadPlaceHolderAlbumArt () =
    let assets = AvaloniaLocator.Current.GetService<IAssetLoader>()
    new Bitmap(assets.Open(new Uri("avares://DLCBuilder/placeholder.png")))

let init () =
    { Project = DLCProject.Empty
      Config = Configuration.Default
      CoverArt = Some (loadPlaceHolderAlbumArt())
      SelectedArrangement = None
      SelectedTone = None
      ShowSortFields = false
      ShowJapaneseFields = false
      Overlay = NoOverlay
      ImportTones = []
      PreviewStartTime = TimeSpan()
      BuildInProgress = false
      CurrentPlatform = if RuntimeInformation.IsOSPlatform OSPlatform.OSX then Mac else PC
      OpenProjectFile = None }, Cmd.OfAsync.perform Configuration.load () SetConfiguration

let private loadArrangement (fileName: string) =
    let rootName =
        using (XmlReader.Create(fileName))
              (fun reader -> reader.MoveToContent() |> ignore; reader.LocalName)

    match rootName with
    | "song" ->
        let metadata = XML.MetaData.Read fileName
        let toneInfo = XML.InstrumentalArrangement.ReadToneNames fileName
        let baseTone =
            if isNull toneInfo.BaseToneName then
                metadata.Arrangement + "_Base"
            else
                toneInfo.BaseToneName
        let tones =
            toneInfo.Names
            |> Array.filter (isNull >> not)
            |> Array.toList
        let arr =
            { XML = fileName
              Name = ArrangementName.Parse metadata.Arrangement
              Priority =
                if metadata.ArrangementProperties.Represent then ArrangementPriority.Main
                elif metadata.ArrangementProperties.BonusArrangement then ArrangementPriority.Bonus
                else ArrangementPriority.Alternative
              Tuning = metadata.Tuning.Strings
              CentOffset = metadata.CentOffset
              RouteMask =
                if metadata.ArrangementProperties.PathBass then RouteMask.Bass
                elif metadata.ArrangementProperties.PathLead then RouteMask.Lead
                else RouteMask.Rhythm
              ScrollSpeed = 1.3
              BaseTone = baseTone
              Tones = tones
              BassPicked = metadata.ArrangementProperties.BassPick
              MasterID = RandomGenerator.next()
              PersistentID = Guid.NewGuid() }
            |> Arrangement.Instrumental
        Ok (arr, Some metadata)

    | "vocals" ->
        // Attempt to infer whether the lyrics are Japanese from the filename
        let isJapanese =
            fileName.Contains("jvocal", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("jlyric", StringComparison.OrdinalIgnoreCase)

        // Try to find custom font for Japanese vocals
        let customFont =
            let lyricFile = IO.Path.Combine(IO.Path.GetDirectoryName fileName, "lyrics.dds")
            if isJapanese && IO.File.Exists lyricFile then Some lyricFile else None

        let arr =
            { XML = fileName
              Japanese = isJapanese
              CustomFont = customFont
              MasterID = RandomGenerator.next()
              PersistentID = Guid.NewGuid() }
            |> Arrangement.Vocals
        Ok (arr, None)

    | "showlights" ->
        let arr =
            { XML = fileName }
            |> Arrangement.Showlights
        Ok (arr, None)

    | _ -> Error "Not a Rocksmith 2014 arrangement."

let private updateArrangement old updated state =
    let arrangements =
        state.Project.Arrangements
        |> List.update old updated
    { state with Project = { state.Project with Arrangements = arrangements }
                 SelectedArrangement = Some updated }, Cmd.none

let private updateTone old updated state =
    let tones =
        state.Project.Tones
        |> List.update old updated
    { state with Project = { state.Project with Tones = tones } 
                 SelectedTone = Some updated }, Cmd.none

let update (msg: Msg) (state: State) =
    match msg with
    | ImportTonesChanged item ->
        if isNull item then state, Cmd.none
        else
            let tones = [ item :?> Tone ]
                //items
                //|> Seq.cast<Tone>
                //|> Seq.toList
            { state with ImportTones = tones }, Cmd.none

    | ImportSelectedTones ->
        let importedTones =
            state.ImportTones
            |> List.map (fun x ->
                if isNull x.ToneDescriptors || x.ToneDescriptors.Length = 0 then
                    let descs =
                        ToneDescriptor.getDescriptionsOrDefault x.Name
                        |> Array.map (fun x -> x.UIName)
                    { x with ToneDescriptors = descs; SortOrder = Nullable(); NameSeparator = " - " }
                else
                    { x with SortOrder = Nullable(); NameSeparator = " - " })
        let tones =
            state.Project.Tones
            |> List.append importedTones
        { state with Project = { state.Project with Tones = tones }
                     Overlay = NoOverlay }, Cmd.none

    | CloseOverlay -> {state with Overlay = NoOverlay }, Cmd.none

    | SelectOpenArrangement ->
        let dialog = Dialogs.openMultiFileDialog "Select Arrangement" Dialogs.xmlFileFilter
        state, Cmd.OfAsync.perform dialog None AddArrangements

    | SelectCoverArt ->
        let dialog = Dialogs.openFileDialog "Select Cover Art" Dialogs.imgFileFilter
        state, Cmd.OfAsync.perform dialog None AddCoverArt

    | SelectAudioFile ->
        let dialog = Dialogs.openFileDialog "Select Audio File" Dialogs.audioFileFilters
        state, Cmd.OfAsync.perform dialog None AddAudioFile

    | SelectCustomFont ->
        let dialog = Dialogs.openFileDialog "Select Custom Font Texture" Dialogs.ddsFileFilter
        state, Cmd.OfAsync.perform dialog None AddCustomFontFile

    | SelectProfilePath ->
        let dialog = Dialogs.openFileDialog "Select Game Profile" Dialogs.profileFilter
        state, Cmd.OfAsync.perform dialog None AddProfilePath

    | SelectTestFolderPath ->
        let dialog = Dialogs.openFolderDialog "Select Test Folder"
        state, Cmd.OfAsync.perform dialog None AddTestFolderPath

    | SelectProjectsFolderPath ->
        let dialog = Dialogs.openFolderDialog "Select Projects Base Folder"
        state, Cmd.OfAsync.perform dialog None AddProjectsFolderPath

    | SelectSaveProjectTarget ->
        let intialFileName =
            state.OpenProjectFile
            |> Option.map IO.Path.GetFileName
            |> Option.orElse
                (let fn =
                    sprintf "%s_%s" state.Project.ArtistName.Value state.Project.Title.Value
                    |> StringValidator.fileName
                sprintf "%s.rs2dlc" fn
                |> Some)
        let initialDir =
            state.OpenProjectFile
            |> Option.map IO.Path.GetDirectoryName
            |> Option.orElse (Option.ofString state.Config.ProjectsFolderPath)
        let dialog = Dialogs.saveFileDialog "Select Target" Dialogs.projectFilter intialFileName
        state, Cmd.OfAsync.perform dialog initialDir SaveProject

    | SelectOpenProjectFile ->
        let dialog = Dialogs.openFileDialog "Select Project File" Dialogs.projectFilter
        state, Cmd.OfAsync.perform dialog None OpenProject

    | AddProjectsFolderPath (Some path) ->
        let config = { state.Config with ProjectsFolderPath = path }
        { state with Config = config }, Cmd.none

    | AddTestFolderPath (Some path) ->
        let config = { state.Config with TestFolderPath = path }
        { state with Config = config }, Cmd.none

    | AddProfilePath (Some path) ->
        if not <| path.EndsWith("_PRFLDB", StringComparison.OrdinalIgnoreCase) then
            state, Cmd.none
        else
            let config = { state.Config with ProfilePath = path }
            { state with Config = config }, Cmd.none

    | AddCustomFontFile (Some fileName) ->
        match state.SelectedArrangement with
        | Some (Vocals arr as old) ->
            let updated = Vocals ({ arr with CustomFont = Some fileName})
            updateArrangement old updated state
        | _ -> state, Cmd.none

    | AddAudioFile (Some fileName) ->
        let audioFile = { state.Project.AudioFile with Path = fileName }
        let previewPath =
            let previewPath =
                let dir = IO.Path.GetDirectoryName fileName
                let fn = IO.Path.GetFileNameWithoutExtension fileName
                let ext = IO.Path.GetExtension fileName
                IO.Path.Combine(dir, sprintf "%s_preview%s" fn ext)
            if IO.File.Exists previewPath then
                previewPath
            else
                String.Empty
        let previewFile = { state.Project.AudioPreviewFile with Path = previewPath }
        { state with Project = { state.Project with AudioFile = audioFile; AudioPreviewFile = previewFile } }, Cmd.none

    | AddCoverArt (Some fileName) ->
        state.CoverArt |> Option.iter dispose
        let bm = new Bitmap(fileName)
        { state with CoverArt = Some bm
                     Project = { state.Project with AlbumArtFile = fileName } }, Cmd.none

    | AddArrangements (Some files) ->
        let results = Array.map loadArrangement files

        let shouldInclude arrangements arr =
            match arr with
            // Allow only one show lights arrangement
            | Showlights _ when arrangements |> List.exists (function Showlights _ -> true | _ -> false) -> false

            // Allow max five instrumental arrangements
            | Instrumental _ when (arrangements |> List.choose Arrangement.pickInstrumental).Length = 5 -> false

            // Allow max two instrumental arrangements
            | Vocals _ when (arrangements |> List.choose (function Vocals _ -> Some 1 | _ -> None)).Length = 2 -> false
            | _ -> true

        let arrangements =
            (state.Project.Arrangements, results)
            ||> Array.fold (fun state elem ->
                match elem with
                | Ok (arr, _) when shouldInclude state arr -> arr::state
                | _ -> state)

        let metadata = 
            if state.Project.ArtistName = SortableString.Empty then
                results
                |> Array.tryPick (function Ok (_, md) -> md | Error _ -> None)
            else
                None

        match metadata with
        | Some md ->
            { state with
                Project = { state.Project with
                                DLCKey = DLCKey.create state.Config.CharterName md.ArtistName md.Title
                                ArtistName = SortableString.Create md.ArtistName // Ignore the sort value from the XML
                                Title = SortableString.Create (md.Title, md.TitleSort)
                                AlbumName = SortableString.Create (md.AlbumName, md.AlbumNameSort)
                                Year = md.AlbumYear
                                Arrangements = arrangements } }, Cmd.none
        | None ->
            { state with Project = { state.Project with Arrangements = arrangements } }, Cmd.none

    | ArrangementSelected selected -> { state with SelectedArrangement = selected }, Cmd.none

    | ToneSelected selected -> { state with SelectedTone = selected }, Cmd.none

    | DeleteArrangement ->
        let arrangements =
            match state.SelectedArrangement with
            | None -> state.Project.Arrangements
            | Some selected -> List.remove selected state.Project.Arrangements
        { state with Project = { state.Project with Arrangements = arrangements }
                     SelectedArrangement = None }, Cmd.none

    | DeleteTone ->
        let tones =
            match state.SelectedTone with
            | None -> state.Project.Tones
            | Some selected -> List.remove selected state.Project.Tones
        { state with Project = { state.Project with Tones = tones } }, Cmd.none

    | ImportProfileTones ->
        let result = Profile.importTones state.Config.ProfilePath
        match result with
        | Ok toneArray ->
            { state with Overlay = SelectImportTones toneArray; ImportTones = [] }, Cmd.none
        | Error msg ->
            { state with Overlay = ErrorMessage msg }, Cmd.none

    | PreviewAudioStartChanged time ->
        { state with PreviewStartTime = TimeSpan.FromSeconds time }, Cmd.none

    | CreatePreviewAudio (SetupStartTime) ->
        let totalLength = Audio.Tools.getLength state.Project.AudioFile.Path
        // Remove the length of the preview from the total length
        let length = totalLength - TimeSpan.FromSeconds 28.
        { state with Overlay = SelectPreviewStart length }, Cmd.none

    | CreatePreviewAudio (CreateFile) ->
        let task () = async { return Audio.Tools.createPreview state.Project.AudioFile.Path state.PreviewStartTime }
        { state with Overlay = NoOverlay }, Cmd.OfAsync.either task () (FileCreated >> CreatePreviewAudio) ErrorOccurred

    | CreatePreviewAudio (FileCreated previewPath) ->
        let previewFile = { state.Project.AudioPreviewFile with Path = previewPath }
        { state with Project = { state.Project with AudioPreviewFile = previewFile } }, Cmd.none

    | ShowSortFields shown -> { state with ShowSortFields = shown }, Cmd.none
    
    | ShowJapaneseFields shown -> { state with ShowJapaneseFields = shown }, Cmd.none

    | ShowConfigEditor -> { state with Overlay = ConfigEditor }, Cmd.none
    
    | SaveConfiguration ->
        { state with Overlay = NoOverlay }, Cmd.OfAsync.attempt Configuration.save state.Config ErrorOccurred

    | SetConfiguration config -> { state with Config = config }, Cmd.none

    | SaveProject (Some target) ->
        let task() = DLCProject.save target state.Project
        state, Cmd.OfAsync.attempt task () ErrorOccurred

    | OpenProject (Some fileName) ->
        let task() = DLCProject.load fileName
        state, Cmd.OfAsync.either task () (fun p -> ProjectLoaded(p, fileName)) ErrorOccurred

    | ProjectLoaded (project, fileName) ->
        state.CoverArt |> Option.iter dispose
        let bm =
            if IO.File.Exists project.AlbumArtFile then
                new Bitmap(project.AlbumArtFile)
            else
                loadPlaceHolderAlbumArt()

        { state with CoverArt = Some bm
                     Project = project
                     OpenProjectFile = Some fileName
                     SelectedArrangement = None
                     SelectedTone = None }, Cmd.none

    | EditInstrumental edit ->
        match state.SelectedArrangement with
        | Some (Instrumental arr as old) ->
            let updated = Instrumental (edit arr)
            updateArrangement old updated state
        | _ -> state, Cmd.none

    | EditVocals edit ->
        match state.SelectedArrangement with
        | Some (Vocals arr as old) ->
            let updated = Vocals (edit arr)
            updateArrangement old updated state
        | _ -> state, Cmd.none

    | EditTone edit ->
        match state.SelectedTone with
        | Some old ->
            let updated = edit old
            updateTone old updated state
        | _ -> state, Cmd.none

    | EditProject edit -> { state with Project = edit state.Project }, Cmd.none
    | EditConfig edit -> { state with Config = edit state.Config }, Cmd.none

    | BuildTest ->
        let testDir = state.Config.TestFolderPath
        let path = IO.Path.Combine(testDir, state.Project.DLCKey.ToLowerInvariant())
        let task () = PackageBuilder.buildPackages path [ state.CurrentPlatform ] state.Project
        { state with BuildInProgress = true }, Cmd.OfAsync.either task () BuildComplete ErrorOccurred

    | BuildRelease ->
        let releaseDir = IO.Path.GetDirectoryName (Option.get state.OpenProjectFile)
        let fn =
            sprintf "%s_%s_v%s" state.Project.ArtistName.Value state.Project.Title.Value (state.Project.Version.Replace('.', '_'))
            |> StringValidator.fileName
        let path = IO.Path.Combine(releaseDir, fn)
        let task () = PackageBuilder.buildPackages path state.Config.ReleasePlatforms state.Project
        { state with BuildInProgress = true }, Cmd.OfAsync.either task () BuildComplete ErrorOccurred

    | BuildComplete _ -> { state with BuildInProgress = false }, Cmd.none
   
    | ErrorOccurred e -> { state with Overlay = ErrorMessage e.Message }, Cmd.none
    
    // When the user canceled any of the dialogs
    | AddArrangements None | AddCoverArt None | AddAudioFile None | AddCustomFontFile None
    | AddProfilePath None | AddTestFolderPath None | AddProjectsFolderPath None
    | SaveProject None | OpenProject None ->
        state, Cmd.none

let view (state: State) dispatch =
    Grid.create [
        Grid.children [
            Grid.create [
                Grid.columnDefinitions "2*,*,2*"
                Grid.rowDefinitions "3*,2*"
                //Grid.showGridLines true
                Grid.children [
                    ProjectDetails.view state dispatch

                    DockPanel.create [
                        Grid.column 1
                        DockPanel.children [
                            Button.create [
                                DockPanel.dock Dock.Top
                                Button.margin 5.0
                                Button.padding (15.0, 5.0)
                                Button.horizontalAlignment HorizontalAlignment.Left
                                Button.content "Add Arrangement"
                                Button.onClick (fun _ -> dispatch SelectOpenArrangement)
                            ]

                            ListBox.create [
                                ListBox.virtualizationMode ItemVirtualizationMode.None
                                ListBox.margin 2.
                                ListBox.dataItems state.Project.Arrangements
                                match state.SelectedArrangement with
                                | Some a -> ListBox.selectedItem a
                                | None -> ()
                                ListBox.onSelectedItemChanged ((fun item ->
                                    match item with
                                    | :? Arrangement as arr ->
                                        dispatch (ArrangementSelected (Some arr))
                                    | null when state.Project.Arrangements.Length = 0 -> dispatch (ArrangementSelected None)
                                    | _ -> ()), SubPatchOptions.OnChangeOf state)
                                ListBox.onKeyDown (fun k ->
                                    if k.Key = Key.Delete then
                                        k.Handled <- true
                                        dispatch DeleteArrangement)
                            ]
                        ]
                    ]

                    StackPanel.create [
                        Grid.column 2
                        StackPanel.margin 8.
                        StackPanel.children [
                            match state.SelectedArrangement with
                            | None ->
                                TextBlock.create [
                                    TextBlock.text "Select an arrangement to edit its details"
                                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                                ]

                            | Some arr ->
                                // Arrangement name
                                TextBlock.create [
                                    TextBlock.fontSize 17.
                                    TextBlock.text (Arrangement.getName arr false)
                                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                                ]

                                // Arrangement filename
                                TextBlock.create [
                                    TextBlock.text (IO.Path.GetFileName (Arrangement.getFile arr))
                                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                                ]

                                match arr with
                                | Showlights _ -> ()
                                | Instrumental i -> InstrumentalDetails.view state dispatch i
                                | Vocals v -> VocalsDetails.view state dispatch v
                        ]
                    ]

                    DockPanel.create [
                        Grid.column 1
                        Grid.row 1
                        DockPanel.children [
                            StackPanel.create [
                                DockPanel.dock Dock.Top
                                StackPanel.orientation Orientation.Horizontal
                                StackPanel.spacing 4.
                                StackPanel.margin 5.
                                StackPanel.children [
                                    Button.create [
                                        Button.padding (15.0, 5.0)
                                        Button.horizontalAlignment HorizontalAlignment.Left
                                        Button.content "From Profile"
                                        Button.onClick (fun _ -> dispatch ImportProfileTones)
                                        Button.isEnabled (IO.File.Exists state.Config.ProfilePath)
                                    ]
                                    Button.create [
                                        Button.padding (15.0, 5.0)
                                        Button.horizontalAlignment HorizontalAlignment.Left
                                        Button.content "Import"
                                    ]
                                ]
                            ]

                            ListBox.create [
                                ListBox.margin 2.
                                ListBox.dataItems state.Project.Tones
                                match state.SelectedTone with
                                | Some t -> ListBox.selectedItem t
                                | None -> ()
                                ListBox.onSelectedItemChanged ((fun item ->
                                    match item with
                                    | :? Tone as t -> dispatch (ToneSelected (Some t))
                                    | null when state.Project.Tones.Length = 0 -> dispatch (ToneSelected None)
                                    | _ -> ()), SubPatchOptions.OnChangeOf state)
                                ListBox.onKeyDown (fun k -> if k.Key = Key.Delete then dispatch DeleteTone)
                            ]
                        ]
                    ]

                    StackPanel.create [
                        Grid.column 2
                        Grid.row 1
                        StackPanel.margin 8.
                        StackPanel.children [
                            match state.SelectedTone with
                            | None ->
                                TextBlock.create [
                                    TextBlock.text "Select a tone to edit its details"
                                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                                ]
                            | Some tone -> ToneDetails.view state dispatch tone
                        ]
                    ]
                ]
            ]

            match state.Overlay with
            | NoOverlay -> ()
            | _ ->
                Grid.create [
                    Grid.children [
                        Rectangle.create [
                            Rectangle.fill "#77000000"
                            Rectangle.onTapped (fun _ -> CloseOverlay |> dispatch)
                        ]
                        Border.create [
                            Border.padding 20.0
                            Border.cornerRadius 5.0
                            Border.horizontalAlignment HorizontalAlignment.Center
                            Border.verticalAlignment VerticalAlignment.Center
                            Border.background "#444444"
                            Border.child (
                                match state.Overlay with
                                | NoOverlay -> failwith "This can not happen."
                                | ErrorMessage msg -> ErrorMessage.view dispatch msg
                                | SelectPreviewStart audioLength -> SelectPreviewStart.view state dispatch audioLength
                                | SelectImportTones tones -> SelectImportTones.view state dispatch tones
                                | ConfigEditor -> ConfigEditor.view state dispatch
                            )
                        ]
                    ]
                ]
        ]
    ]