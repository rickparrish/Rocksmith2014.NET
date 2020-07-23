﻿module TestApp.Tools

open Avalonia
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Threading
open Rocksmith2014.SNG
open Rocksmith2014.Conversion
open System.Threading.Tasks
open System.IO

let private window =
    lazy ((Application.Current.ApplicationLifetime :?> ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime).MainWindow)

let sngFilters =
    let filter = FileDialogFilter(Extensions = ResizeArray(seq { "sng" }), Name = "SNG Files")
    ResizeArray(seq { filter })

let xmlFilters =
    let filter = FileDialogFilter(Extensions = ResizeArray(seq { "xml" }), Name = "XML Files")
    ResizeArray(seq { filter })

let openFileDialogSingle title filters dispatch = 
    Dispatcher.UIThread.InvokeAsync(
        fun () ->
            OpenFileDialog(Title = title, AllowMultiple = false, Filters = filters)
               .ShowAsync(window.Force())
               .ContinueWith(fun (t: Task<string[]>) -> 
                   match t.Result with
                   | [| file |] -> file |> dispatch
                   | _ -> ())
        ) |> ignore

let ofdSng = openFileDialogSingle "Select File" sngFilters
let ofdXml = openFileDialogSingle "Select File" xmlFilters

type State = { Status:string; Platform:Platform }

let init = { Status = ""; Platform = PC }

type Msg =
    | UnpackFile of file:string
    | ConvertVocals of file:string
    | ConvertInstrumentalSNGtoXML of file:string
    | ConvertInstrumentalXMLtoSNG of file:string
    | RoundTrip of file:string
    | ChangePlatform of Platform

let update (msg: Msg) (state: State) : State =
    match msg with
    | RoundTrip file ->
        try
            SNGFile.readPacked file state.Platform
            |> SNGFile.savePacked (file + "re") state.Platform
            state
        with e -> { state with Status = e.Message }

    | UnpackFile file ->
        try
            SNGFile.unpackFile file state.Platform; state
        with e -> { state with Status = e.Message }

    | ConvertVocals file ->
        try
            let target = Path.ChangeExtension(file, "xml")
            ConvertVocals.sngFileToXml file target state.Platform
            state
        with e -> { state with Status = e.Message }

    | ConvertInstrumentalSNGtoXML file ->
        try
            let targetFile = Path.ChangeExtension(file, "xml")
            ConvertInstrumental.sngFileToXml file targetFile state.Platform
            state
        with e -> { state with Status = e.Message }

    | ConvertInstrumentalXMLtoSNG file ->
        try
            let targetFile = Path.ChangeExtension(file, "sng")
            ConvertInstrumental.xmlFileToSng file targetFile state.Platform
            state
        with e -> { state with Status = e.Message }

    | ChangePlatform platform ->
        { state with Platform = platform }

let view (state: State) dispatch =
    StackPanel.create [
        StackPanel.margin 5.0
        StackPanel.spacing 5.0
        StackPanel.children [
            StackPanel.create [
                StackPanel.orientation Orientation.Horizontal
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.children [
                    RadioButton.create [
                        RadioButton.groupName "Platform"
                        RadioButton.content "PC"
                        RadioButton.isChecked (state.Platform = PC)
                        RadioButton.onIsPressedChanged (fun p -> if p then dispatch (ChangePlatform PC))
                    ]
                    RadioButton.create [
                        RadioButton.groupName "Platform"
                        RadioButton.content "Mac"
                        RadioButton.isChecked (state.Platform = Mac)
                        RadioButton.onIsPressedChanged (fun p -> if p then dispatch (ChangePlatform Mac))
                    ]
                ]
            ]
            Button.create [
                Button.onClick (fun _ ->  ofdSng (RoundTrip >> dispatch))
                Button.content "Round-trip Packed File..."
            ]

            Button.create [
                Button.onClick (fun _ -> ofdSng (UnpackFile >> dispatch))
                Button.content "Unpack SNG File..."
            ]

            Button.create [
                Button.onClick (fun _ -> ofdSng (ConvertVocals >> dispatch))
                Button.content "Convert Vocals SNG to XML..."
            ]
            
            Button.create [
                Button.onClick (fun _ -> ofdSng (ConvertInstrumentalSNGtoXML >> dispatch))
                Button.content "Convert Instrumental SNG to XML..."
            ]

            Button.create [
                Button.onClick (fun _ -> ofdXml (ConvertInstrumentalXMLtoSNG >> dispatch))
                Button.content "Convert Instrumental XML to SNG..."
            ]

            TextBlock.create [
                TextBlock.fontSize 28.0
                TextBlock.verticalAlignment VerticalAlignment.Center
                TextBlock.horizontalAlignment HorizontalAlignment.Center
                TextBlock.text (string state.Status)
            ]
        ]
    ]
