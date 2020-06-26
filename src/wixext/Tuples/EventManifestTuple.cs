// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Util
{
    using WixToolset.Data;
    using WixToolset.Util.Symbols;

    public static partial class UtilSymbolDefinitions
    {
        public static readonly IntermediateSymbolDefinition EventManifest = new IntermediateSymbolDefinition(
            UtilSymbolDefinitionType.EventManifest.ToString(),
            new[]
            {
                new IntermediateFieldDefinition(nameof(EventManifestSymbolFields.ComponentRef), IntermediateFieldType.String),
                new IntermediateFieldDefinition(nameof(EventManifestSymbolFields.File), IntermediateFieldType.String),
            },
            typeof(EventManifestSymbol));
    }
}

namespace WixToolset.Util.Symbols
{
    using WixToolset.Data;

    public enum EventManifestSymbolFields
    {
        ComponentRef,
        File,
    }

    public class EventManifestSymbol : IntermediateSymbol
    {
        public EventManifestSymbol() : base(UtilSymbolDefinitions.EventManifest, null, null)
        {
        }

        public EventManifestSymbol(SourceLineNumber sourceLineNumber, Identifier id = null) : base(UtilSymbolDefinitions.EventManifest, sourceLineNumber, id)
        {
        }

        public IntermediateField this[EventManifestSymbolFields index] => this.Fields[(int)index];

        public string ComponentRef
        {
            get => this.Fields[(int)EventManifestSymbolFields.ComponentRef].AsString();
            set => this.Set((int)EventManifestSymbolFields.ComponentRef, value);
        }

        public string File
        {
            get => this.Fields[(int)EventManifestSymbolFields.File].AsString();
            set => this.Set((int)EventManifestSymbolFields.File, value);
        }
    }
}