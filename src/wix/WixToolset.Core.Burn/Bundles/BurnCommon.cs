// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.Burn.Bundles
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using WixToolset.Data;
    using WixToolset.Extensibility.Services;

    /// <summary>
    /// Common functionality for Burn PE Writer &amp; Reader for the WiX toolset.
    /// </summary>
    /// <remarks>This class encapsulates common functionality related to 
    /// bundled/chained setup packages.</remarks>
    /// <example>
    /// </example>
    internal abstract class BurnCommon : IDisposable
    {
        public const string BurnNamespace = "http://wixtoolset.org/schemas/v4/2008/Burn";
        public const string BurnUXContainerEmbeddedIdFormat = "u{0}";
        public const string BurnAuthoredContainerEmbeddedIdFormat = "a{0}";

        public const string BADataFileName = "BootstrapperApplicationData.xml";
        public const string BADataNamespace = "http://wixtoolset.org/schemas/v4/BootstrapperApplicationData";

        public const string BundleExtensionDataFileName = "BundleExtensionData.xml";
        public const string BundleExtensionDataNamespace = "http://wixtoolset.org/schemas/v4/BundleExtensionData";

        // See WinNT.h for details about the PE format, including the
        // structure and offsets for IMAGE_DOS_HEADER, IMAGE_NT_HEADERS32,
        // IMAGE_FILE_HEADER, etc.
        protected const UInt32 IMAGE_DOS_HEADER_SIZE = 64;
        protected const UInt32 IMAGE_DOS_HEADER_OFFSET_MAGIC = 0;
        protected const UInt32 IMAGE_DOS_HEADER_OFFSET_NTHEADER = 60;

        protected const UInt32 IMAGE_NT_HEADER_SIZE = 24; // signature DWORD (4) + IMAGE_FILE_HEADER (20)
        protected const UInt32 IMAGE_NT_HEADER_OFFSET_SIGNATURE = 0;
        protected const UInt32 IMAGE_NT_HEADER_OFFSET_NUMBEROFSECTIONS = 6;
        protected const UInt32 IMAGE_NT_HEADER_OFFSET_SIZEOFOPTIONALHEADER = 20;

        protected const UInt32 IMAGE_OPTIONAL_OFFSET_CHECKSUM = 4 * 16; // checksum is 16 DWORDs into IMAGE_OPTIONAL_HEADER which is right after the IMAGE_NT_HEADER.
        protected const UInt32 IMAGE_OPTIONAL_NEGATIVE_OFFSET_CERTIFICATETABLE = (IMAGE_DATA_DIRECTORY_SIZE * (IMAGE_NUMBEROF_DIRECTORY_ENTRIES - IMAGE_DIRECTORY_ENTRY_SECURITY));

        protected const UInt32 IMAGE_SECTION_HEADER_SIZE = 40;
        protected const UInt32 IMAGE_SECTION_HEADER_OFFSET_NAME = 0;
        protected const UInt32 IMAGE_SECTION_HEADER_OFFSET_VIRTUALSIZE = 8;
        protected const UInt32 IMAGE_SECTION_HEADER_OFFSET_SIZEOFRAWDATA = 16;
        protected const UInt32 IMAGE_SECTION_HEADER_OFFSET_POINTERTORAWDATA = 20;

        protected const UInt32 IMAGE_DATA_DIRECTORY_SIZE = 8; // struct of two DWORDs.
        protected const UInt32 IMAGE_DIRECTORY_ENTRY_SECURITY = 4;
        protected const UInt32 IMAGE_NUMBEROF_DIRECTORY_ENTRIES = 16;

        protected const UInt16 IMAGE_DOS_SIGNATURE = 0x5A4D;
        protected const UInt32 IMAGE_NT_SIGNATURE = 0x00004550;
        protected const UInt64 IMAGE_SECTION_WIXBURN_NAME = 0x6E7275627869772E; // ".wixburn", as a qword.

        // The ".wixburn" section contains:
        //    0- 3:  magic number
        //    4- 7:  version
        //    8-23:  bundle GUID
        //   24-27:  engine (stub) size
        //   28-31:  original checksum
        //   32-35:  original signature offset
        //   36-39:  original signature size
        //   40-43:  container type (1 = CAB)
        //   44-47:  container count
        //   48-51:  byte count of manifest + UX container
        //   52-512:  byte count of attached containers (4 bytes for each container)
        protected const UInt32 BURN_SECTION_OFFSET_MAGIC = 0;
        protected const UInt32 BURN_SECTION_OFFSET_VERSION = 4;
        protected const UInt32 BURN_SECTION_OFFSET_BUNDLEGUID = 8;
        protected const UInt32 BURN_SECTION_OFFSET_STUBSIZE = 24;
        protected const UInt32 BURN_SECTION_OFFSET_ORIGINALCHECKSUM = 28;
        protected const UInt32 BURN_SECTION_OFFSET_ORIGINALSIGNATUREOFFSET = 32;
        protected const UInt32 BURN_SECTION_OFFSET_ORIGINALSIGNATURESIZE = 36;
        protected const UInt32 BURN_SECTION_OFFSET_FORMAT = 40;
        protected const UInt32 BURN_SECTION_OFFSET_COUNT = 44;
        protected const UInt32 BURN_SECTION_OFFSET_UXSIZE = 48;
        protected const UInt32 BURN_SECTION_OFFSET_ATTACHEDCONTAINERSIZE0 = 52;
        protected const UInt32 BURN_SECTION_MIN_SIZE = BURN_SECTION_OFFSET_ATTACHEDCONTAINERSIZE0;

        protected const UInt32 BURN_SECTION_MAGIC = 0x00f14300;
        protected const UInt32 BURN_SECTION_VERSION = 0x00000003;
        protected const UInt32 BURN_SECTION_COMPATIBLE_VERSION = 0x00000002;

        protected string fileExe;
        protected UInt32 peOffset = UInt32.MaxValue;
        protected UInt16 sections = UInt16.MaxValue;
        protected UInt32 firstSectionOffset = UInt32.MaxValue;
        protected UInt32 checksumOffset;
        protected UInt32 certificateTableSignatureOffset;
        protected UInt32 certificateTableSignatureSize;
        protected UInt32 wixburnDataOffset = UInt32.MaxValue;
        protected UInt32 wixburnRawDataSize;
        protected UInt32 wixburnMaxContainers;

        // TODO: does this enum exist in another form somewhere?
        /// <summary>
        /// The types of attached containers that BurnWriter supports.
        /// </summary>
        public enum Container
        {
            Nothing = 0,
            UX,
            Attached
        }

        /// <summary>
        /// Creates a BurnCommon for re-writing a PE file.
        /// </summary>
        /// <param name="messaging"></param>
        /// <param name="fileExe">File to modify in-place.</param>
        public BurnCommon(IMessaging messaging, string fileExe)
        {
            this.Messaging = messaging;
            this.fileExe = fileExe;
            this.AttachedContainers = new List<ContainerSlot>();
        }

        public UInt32 Checksum { get; protected set; }
        public UInt32 SignatureOffset { get; protected set; }
        public UInt32 SignatureSize { get; protected set; }
        public UInt32 Version { get; protected set; }
        public UInt32 StubSize { get; protected set; }
        public UInt32 OriginalChecksum { get; protected set; }
        public UInt32 OriginalSignatureOffset { get; protected set; }
        public UInt32 OriginalSignatureSize { get; protected set; }
        public UInt32 EngineSize { get; protected set; }
        public UInt32 UXAddress {  get { return this.StubSize; } }
        public List<ContainerSlot> AttachedContainers { get; protected set; }

        protected IMessaging Messaging { get; }

        public void Dispose()
        {
            this.Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Copies one stream to another.
        /// </summary>
        /// <param name="input">Input stream.</param>
        /// <param name="output">Output stream.</param>
        /// <param name="size">Optional count of bytes to copy. 0 indicates whole input stream from current should be copied.</param>
        protected static int CopyStream(Stream input, Stream output, int size)
        {
            var bytes = new byte[4096];
            var total = 0;
            do
            {
                var read = Math.Min(bytes.Length, size - total);
                read = input.Read(bytes, 0, read);
                if (0 == read)
                {
                    break;
                }

                output.Write(bytes, 0, read);
                total += read;
            } while (0 == size || total < size);

            return total;
        }

        /// <summary>
        /// Initialize the common information about a Burn engine.
        /// </summary>
        /// <param name="reader">Binary reader open against a Burn engine.</param>
        /// <returns>True if initialized.</returns>
        protected bool Initialize(BinaryReader reader)
        {
            if (!this.GetWixburnSectionInfo(reader))
            {
                return false;
            }

            reader.BaseStream.Seek(this.wixburnDataOffset, SeekOrigin.Begin);
            byte[] bytes = reader.ReadBytes((int)this.wixburnRawDataSize);
            UInt32 uint32 = 0;

            uint32 = BurnCommon.ReadUInt32(bytes, BURN_SECTION_OFFSET_MAGIC);
            if (BURN_SECTION_MAGIC != uint32)
            {
                this.Messaging.Write(ErrorMessages.InvalidBundle(this.fileExe));
                return false;
            }

            this.Version = BurnCommon.ReadUInt32(bytes, BURN_SECTION_OFFSET_VERSION);
            if ((BURN_SECTION_VERSION != this.Version) && (BURN_SECTION_COMPATIBLE_VERSION != this.Version))
            {
                this.Messaging.Write(BurnBackendErrors.IncompatibleWixBurnSection(this.fileExe, this.Version));
                return false;
            }

            uint32 = BurnCommon.ReadUInt32(bytes, BURN_SECTION_OFFSET_FORMAT); // We only know how to deal with CABs right now
            if (1 != uint32)
            {
                this.Messaging.Write(ErrorMessages.InvalidBundle(this.fileExe));
                return false;
            }

            this.StubSize = BurnCommon.ReadUInt32(bytes, BURN_SECTION_OFFSET_STUBSIZE);
            this.OriginalChecksum = BurnCommon.ReadUInt32(bytes, BURN_SECTION_OFFSET_ORIGINALCHECKSUM);
            this.OriginalSignatureOffset = BurnCommon.ReadUInt32(bytes, BURN_SECTION_OFFSET_ORIGINALSIGNATUREOFFSET);
            this.OriginalSignatureSize = BurnCommon.ReadUInt32(bytes, BURN_SECTION_OFFSET_ORIGINALSIGNATURESIZE);

            uint containerCount = BurnCommon.ReadUInt32(bytes, BURN_SECTION_OFFSET_COUNT);
            uint uxSize = 0;
            if (this.wixburnMaxContainers < containerCount)
            {
                this.Messaging.Write(ErrorMessages.InvalidBundle(this.fileExe));
                return false;
            }
            else if (containerCount > 0)
            {
                this.AttachedContainers.Clear();
                for (uint i = 0; i < containerCount; ++i)
                {
                    uint sizeOffset = BURN_SECTION_OFFSET_UXSIZE + (i * 4);
                    uint size = BurnCommon.ReadUInt32(bytes, sizeOffset);
                    this.AttachedContainers.Add(new ContainerSlot(size));
                }
                uxSize = this.AttachedContainers[0].Size;
            }

            // If there is an original signature use that to determine the engine size.
            if (0 < this.OriginalSignatureOffset)
            {
                this.EngineSize = this.OriginalSignatureOffset + this.OriginalSignatureSize;
            }
            else if (0 < this.SignatureOffset && 2 > containerCount) // if there is a signature and no attached containers, use the current signature.
            {
                this.EngineSize = this.SignatureOffset + this.SignatureSize;
            }
            else // just use the stub and UX container as the size of the engine.
            {
                this.EngineSize = this.UXAddress + uxSize;
            }

            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Finds the ".wixburn" section in the current exe.
        /// </summary>
        /// <returns>true if the ".wixburn" section is successfully found; false otherwise</returns>
        private bool GetWixburnSectionInfo(BinaryReader reader)
        {
            if (UInt32.MaxValue == this.wixburnDataOffset)
            {
                if (!this.EnsureNTHeader(reader))
                {
                    return false;
                }

                UInt32 wixburnSectionOffset = UInt32.MaxValue;
                byte[] bytes = new byte[IMAGE_SECTION_HEADER_SIZE];

                reader.BaseStream.Seek(this.firstSectionOffset, SeekOrigin.Begin);
                for (UInt16 sectionIndex = 0; sectionIndex < this.sections; ++sectionIndex)
                {
                    reader.Read(bytes, 0, bytes.Length);

                    if (IMAGE_SECTION_WIXBURN_NAME == BurnCommon.ReadUInt64(bytes, IMAGE_SECTION_HEADER_OFFSET_NAME))
                    {
                        wixburnSectionOffset = this.firstSectionOffset + (IMAGE_SECTION_HEADER_SIZE * sectionIndex);
                        break;
                    }
                }

                if (UInt32.MaxValue == wixburnSectionOffset)
                {
                    this.Messaging.Write(ErrorMessages.StubMissingWixburnSection(this.fileExe));
                    return false;
                }

                this.wixburnRawDataSize = BurnCommon.ReadUInt32(bytes, IMAGE_SECTION_HEADER_OFFSET_SIZEOFRAWDATA);

                // we need 52 bytes for the manifest header, which is always going to fit in 
                // the smallest alignment (512 bytes), but just to be paranoid...
                if (BURN_SECTION_MIN_SIZE > this.wixburnRawDataSize)
                {
                    this.Messaging.Write(ErrorMessages.StubWixburnSectionTooSmall(this.fileExe));
                    return false;
                }

                this.wixburnMaxContainers = (this.wixburnRawDataSize - BURN_SECTION_OFFSET_UXSIZE) / sizeof(UInt32);
                this.wixburnDataOffset = BurnCommon.ReadUInt32(bytes, IMAGE_SECTION_HEADER_OFFSET_POINTERTORAWDATA);
            }

            return true;
        }

        /// <summary>
        /// Checks for a valid Windows PE signature (IMAGE_NT_SIGNATURE) in the current exe.
        /// </summary>
        /// <returns>true if the exe is a Windows executable; false otherwise</returns>
        private bool EnsureNTHeader(BinaryReader reader)
        {
            if (UInt32.MaxValue == this.firstSectionOffset)
            {
                if (!this.EnsureDosHeader(reader))
                {
                    return false;
                }

                reader.BaseStream.Seek(this.peOffset, SeekOrigin.Begin);
                byte[] bytes = reader.ReadBytes((int)IMAGE_NT_HEADER_SIZE);

                // Verify the NT signature...
                if (IMAGE_NT_SIGNATURE != BurnCommon.ReadUInt32(bytes, IMAGE_NT_HEADER_OFFSET_SIGNATURE))
                {
                    this.Messaging.Write(ErrorMessages.InvalidStubExe(this.fileExe));
                    return false;
                }

                ushort sizeOptionalHeader = BurnCommon.ReadUInt16(bytes, IMAGE_NT_HEADER_OFFSET_SIZEOFOPTIONALHEADER);

                this.sections = BurnCommon.ReadUInt16(bytes, IMAGE_NT_HEADER_OFFSET_NUMBEROFSECTIONS);
                this.firstSectionOffset = this.peOffset + IMAGE_NT_HEADER_SIZE + sizeOptionalHeader;

                this.checksumOffset = this.peOffset + IMAGE_NT_HEADER_SIZE + IMAGE_OPTIONAL_OFFSET_CHECKSUM;
                this.certificateTableSignatureOffset = this.peOffset + IMAGE_NT_HEADER_SIZE + sizeOptionalHeader - IMAGE_OPTIONAL_NEGATIVE_OFFSET_CERTIFICATETABLE;
                this.certificateTableSignatureSize = this.certificateTableSignatureOffset + 4; // size is in the DWORD after the offset.

                bytes = reader.ReadBytes(sizeOptionalHeader);
                this.Checksum = BurnCommon.ReadUInt32(bytes, IMAGE_OPTIONAL_OFFSET_CHECKSUM);
                this.SignatureOffset = BurnCommon.ReadUInt32(bytes, sizeOptionalHeader - IMAGE_OPTIONAL_NEGATIVE_OFFSET_CERTIFICATETABLE);
                this.SignatureSize = BurnCommon.ReadUInt32(bytes, sizeOptionalHeader - IMAGE_OPTIONAL_NEGATIVE_OFFSET_CERTIFICATETABLE + 4);
            }

            return true;
        }

        /// <summary>
        /// Checks for a valid DOS header in the current exe.
        /// </summary>
        /// <returns>true if the exe starts with a DOS stub; false otherwise</returns>
        private bool EnsureDosHeader(BinaryReader reader)
        {
            if (UInt32.MaxValue == this.peOffset)
            {
                byte[] bytes = reader.ReadBytes((int)IMAGE_DOS_HEADER_SIZE);

                // Verify the DOS 'MZ' signature.
                if (IMAGE_DOS_SIGNATURE != BurnCommon.ReadUInt16(bytes, IMAGE_DOS_HEADER_OFFSET_MAGIC))
                {
                    this.Messaging.Write(ErrorMessages.InvalidStubExe(this.fileExe));
                    return false;
                }

                this.peOffset = BurnCommon.ReadUInt32(bytes, IMAGE_DOS_HEADER_OFFSET_NTHEADER);
            }

            return true;
        }

        /// <summary>
        /// Reads a UInt16 value in little-endian format from an offset in an array of bytes.
        /// </summary>
        /// <param name="bytes">Array from which to read.</param>
        /// <param name="offset">Beginning offset from which to read.</param>
        /// <returns>value at offset</returns>
        internal static UInt16 ReadUInt16(byte[] bytes, UInt32 offset)
        {
            Debug.Assert(offset + 2 <= bytes.Length);
            return (UInt16)(bytes[offset] + (bytes[offset + 1] << 8));
        }

        /// <summary>
        /// Reads a UInt32 value in little-endian format from an offset in an array of bytes.
        /// </summary>
        /// <param name="bytes">Array from which to read.</param>
        /// <param name="offset">Beginning offset from which to read.</param>
        /// <returns>value at offset</returns>
        internal static UInt32 ReadUInt32(byte[] bytes, UInt32 offset)
        {
            Debug.Assert(offset + 4 <= bytes.Length);
            return BurnCommon.ReadUInt16(bytes, offset) + ((UInt32)BurnCommon.ReadUInt16(bytes, offset + 2) << 16);
        }

        /// <summary>
        /// Reads a UInt64 value in little-endian format from an offset in an array of bytes.
        /// </summary>
        /// <param name="bytes">Array from which to read.</param>
        /// <param name="offset">Beginning offset from which to read.</param>
        /// <returns>value at offset</returns>
        internal static UInt64 ReadUInt64(byte[] bytes, UInt32 offset)
        {
            Debug.Assert(offset + 8 <= bytes.Length);
            return BurnCommon.ReadUInt32(bytes, offset) + ((UInt64)BurnCommon.ReadUInt32(bytes, offset + 4) << 32);
        }
    }

    internal struct ContainerSlot
    {
        public ContainerSlot(uint size) : this()
        {
            this.Size = size;
        }

        public uint Size { get; set; }
    }
}
