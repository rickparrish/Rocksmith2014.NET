﻿module Rocksmith2014.SNG.Tests.ReadWriteUnpacked

open Expecto
open Rocksmith2014.SNG
open System.IO

let testFileLevels = 12

let testRead file expectedLevels =
    let sng = SNGFile.readUnpacked file
    Expect.equal sng.Levels.Length expectedLevels (sprintf "Read %i levels" expectedLevels)

[<Tests>]
let readWriteTests =
  testList "Read/Write Unpacked Files" [

    testCase "Can read unpacked SNG file" <| fun _ ->
      testRead "unpacked.sng" testFileLevels

    testCase "Can write unpacked SNG file" <| fun _ ->
      let sng = SNGFile.readUnpacked "unpacked.sng"
      SNGFile.saveUnpacked "test_write_unpacked.sng" sng

      testRead "test_write_unpacked.sng" testFileLevels
      Expect.equal (FileInfo("test_write_unpacked.sng").Length) (FileInfo("unpacked.sng").Length) "Same size file written"
  ]