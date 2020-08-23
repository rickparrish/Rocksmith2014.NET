﻿namespace DLCBuilder

open System.Collections.Generic
open Microsoft.Extensions.FileProviders
open System.Reflection
open System.Text.Json

type Locale =
    { Name : string; ShortName : string }

    override this.ToString() =
        this.Name

module Locales =
    let English = { Name = "English"; ShortName = "en" }
    let Finnish = { Name = "Suomi"; ShortName = "fi" }

    let fromShortName name = 
        match name with
        | "en" -> English
        | "fi" -> Finnish
        | _ -> English

type Localization(locale: Locale) =
    let defaultLocale = Locales.English

    static let loadDictionary name =
        let embeddedProvider = EmbeddedFileProvider(Assembly.GetExecutingAssembly())
        use defLoc = embeddedProvider.GetFileInfo(name).CreateReadStream()
        JsonSerializer.DeserializeAsync<Dictionary<string, string>>(defLoc).AsAsync()
        |> Async.RunSynchronously

    static let defaultDictionary: Dictionary<string, string> = loadDictionary "i18n/default.json"

    let localeDictionary =
        if locale = defaultLocale then
            defaultDictionary
        else
            sprintf "i18n/%s.json" locale.ShortName
            |> loadDictionary

    member _.GetString (key: string) =
        let found, str = localeDictionary.TryGetValue key
        if found then str
        else
            let found, str = defaultDictionary.TryGetValue key
            if found then str else sprintf "!!%s!!" key
