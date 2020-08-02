﻿namespace Rocksmith2014.PSARC

open System.IO
open Rocksmith2014.Common.Interfaces

type NamedEntry =
    { Name: string
      Data: Stream }

type Entry =
    { NameDigest : byte[]
      zIndexBegin : uint32
      Length : uint64
      Offset : uint64
      ID : int32 }

    member this.Write (writer: IBinaryWriter) =
        writer.WriteBytes this.NameDigest
        writer.WriteUInt32 this.zIndexBegin
        writer.WriteUInt40 this.Length
        writer.WriteUInt40 this.Offset

    static member Read (reader: IBinaryReader) index =
        { ID = index
          NameDigest = reader.ReadBytes(16)
          zIndexBegin = reader.ReadUInt32()
          Length = reader.ReadUInt40()
          Offset = reader.ReadUInt40() }

    /// Creates a "proto-entry" (no offset) from the given named entry.
    static member CreateProto (nEntry: NamedEntry) zBegin id =
        { NameDigest = Cryptography.md5Hash nEntry.Name
          zIndexBegin = zBegin
          Length = uint64 nEntry.Data.Length
          Offset = 0UL // Will be set later
          ID = id }
