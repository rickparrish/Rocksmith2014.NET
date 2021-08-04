[<AutoOpen>]
module Common

open DLCBuilder
open Rocksmith2014.Common
open Rocksmith2014.DLCProject
open System

let initialState =
    { Project = DLCProject.Empty
      SavedProject = DLCProject.Empty
      RecentFiles = []
      Config = { Configuration.Default with ShowAdvanced = true }
      SelectedArrangementIndex = -1
      SelectedToneIndex = -1
      SelectedGear = None
      SelectedGearSlot = ToneGear.Amp
      SelectedImportTones = List.empty
      ManuallyEditingKnobKey = None
      ShowSortFields = false
      ShowJapaneseFields = false
      Overlay = NoOverlay
      RunningTasks = Set.empty
      StatusMessages = []
      CurrentPlatform = if OperatingSystem.IsMacOS() then Mac else PC
      OpenProjectFile = None
      AvailableUpdate = None
      ArrangementIssues = Map.empty
      ToneGearRepository = None }
