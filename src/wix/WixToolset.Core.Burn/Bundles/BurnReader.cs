// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.Burn.Bundles
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;
    using WixToolset.Core.Native;
    using WixToolset.Extensibility.Services;

    /// <summary>
    /// Burn PE reader for the WiX toolset.
    /// </summary>
    /// <remarks>This class encapsulates reading from a stub EXE with containers attached
    /// for dissecting bundled/chained setup packages.</remarks>
    /// <example>
    /// using (BurnReader reader = BurnReader.Open(fileExe, this.core, guid))
    /// {
    ///     reader.ExtractUXContainer(file1, tempFolder);
    /// }
    /// </example>
    internal class BurnReader : BurnCommon
    {
        private bool disposed;

        private bool invalidBundle;
        private BinaryReader binaryReader;
        private readonly List<DictionaryEntry> attachedContainerPayloadNames;

        /// <summary>
        /// Creates a BurnReader for reading a PE file.
        /// </summary>
        /// <param name="messaging"></param>
        /// <param name="fileExe">File to read.</param>
        private BurnReader(IMessaging messaging, string fileExe)
            : base(messaging, fileExe)
        {
            this.attachedContainerPayloadNames = new List<DictionaryEntry>();
        }

        /// <summary>
        /// Gets the underlying stream.
        /// </summary>
        public Stream Stream => this.binaryReader?.BaseStream;

        internal static BurnReader Open(object inputFilePath)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Opens a Burn reader.
        /// </summary>
        /// <param name="messaging"></param>
        /// <param name="fileExe">Path to file.</param>
        /// <returns>Burn reader.</returns>
        public static BurnReader Open(IMessaging messaging, string fileExe)
        {
            var reader = new BurnReader(messaging, fileExe);

            reader.binaryReader = new BinaryReader(File.Open(fileExe, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete));
            if (!reader.Initialize(reader.binaryReader))
            {
                reader.invalidBundle = true;
            }

            return reader;
        }

        /// <summary>
        /// Gets the UX container from the exe and extracts its contents to the output directory.
        /// </summary>
        /// <param name="outputDirectory">Directory to write extracted files to.</param>
        /// <param name="tempDirectory">Scratch directory.</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ExtractUXContainer(string outputDirectory, string tempDirectory)
        {
            // No UX container to extract
            if (this.AttachedContainers.Count == 0)
            {
                return false;
            }

            if (this.invalidBundle)
            {
                return false;
            }

            Directory.CreateDirectory(outputDirectory);
            string tempCabPath = Path.Combine(tempDirectory, "ux.cab");
            string manifestOriginalPath = Path.Combine(outputDirectory, "0");
            string manifestPath = Path.Combine(outputDirectory, "manifest.xml");
            var uxContainerSlot = this.AttachedContainers[0];

            this.binaryReader.BaseStream.Seek(this.UXAddress, SeekOrigin.Begin);
            using (Stream tempCab = File.Open(tempCabPath, FileMode.Create, FileAccess.Write))
            {
                BurnCommon.CopyStream(this.binaryReader.BaseStream, tempCab, (int)uxContainerSlot.Size);
            }

            var cabinet = new Cabinet(tempCabPath);
            cabinet.Extract(outputDirectory);

            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            FileSystem.MoveFile(manifestOriginalPath, manifestPath);

            XmlDocument document = new XmlDocument();
            document.Load(manifestPath);
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(document.NameTable);
            namespaceManager.AddNamespace("burn", BurnCommon.BurnNamespace);
            XmlNodeList uxPayloads = document.SelectNodes("/burn:BurnManifest/burn:UX/burn:Payload", namespaceManager);
            XmlNodeList payloads = document.SelectNodes("/burn:BurnManifest/burn:Payload", namespaceManager);

            foreach (XmlNode uxPayload in uxPayloads)
            {
                XmlNode sourcePathNode = uxPayload.Attributes.GetNamedItem("SourcePath");
                XmlNode filePathNode = uxPayload.Attributes.GetNamedItem("FilePath");

                string sourcePath = Path.Combine(outputDirectory, sourcePathNode.Value);
                string destinationPath = Path.Combine(outputDirectory, filePathNode.Value);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                FileSystem.MoveFile(sourcePath, destinationPath);
            }

            foreach (XmlNode payload in payloads)
            {
                XmlNode packagingNode = payload.Attributes.GetNamedItem("Packaging");

                string packaging = packagingNode.Value;

                if (packaging.Equals("embedded", StringComparison.OrdinalIgnoreCase))
                {
                    XmlNode sourcePathNode = payload.Attributes.GetNamedItem("SourcePath");
                    XmlNode filePathNode = payload.Attributes.GetNamedItem("FilePath");
                    XmlNode containerNode = payload.Attributes.GetNamedItem("Container");

                    string sourcePath = sourcePathNode.Value;
                    string destinationPath = Path.Combine(containerNode.Value, filePathNode.Value);

                    this.attachedContainerPayloadNames.Add(new DictionaryEntry(sourcePath, destinationPath));
                }
            }

            return true;
        }

        internal void ExtractUXContainer(string uxExtractPath, object intermediateFolder)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets each non-UX attached container from the exe and extracts its contents to the output directory.
        /// </summary>
        /// <param name="outputDirectory">Directory to write extracted files to.</param>
        /// <param name="tempDirectory">Scratch directory.</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ExtractAttachedContainers(string outputDirectory, string tempDirectory)
        {
            // No attached containers to extract
            if (this.AttachedContainers.Count < 2)
            {
                return false;
            }

            if (this.invalidBundle)
            {
                return false;
            }

            Directory.CreateDirectory(outputDirectory);
            uint nextAddress = this.EngineSize;
            for (int i = 1; i < this.AttachedContainers.Count; i++)
            {
                ContainerSlot cntnr = this.AttachedContainers[i];
                string tempCabPath = Path.Combine(tempDirectory, $"a{i}.cab");

                this.binaryReader.BaseStream.Seek(nextAddress, SeekOrigin.Begin);
                using (Stream tempCab = File.Open(tempCabPath, FileMode.Create, FileAccess.Write))
                {
                    BurnCommon.CopyStream(this.binaryReader.BaseStream, tempCab, (int)cntnr.Size);
                }

                var cabinet = new Cabinet(tempCabPath);
                cabinet.Extract(outputDirectory);

                nextAddress += cntnr.Size;
            }

            foreach (DictionaryEntry entry in this.attachedContainerPayloadNames)
            {
                string sourcePath = Path.Combine(outputDirectory, (string)entry.Key);
                string destinationPath = Path.Combine(outputDirectory, (string)entry.Value);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                FileSystem.MoveFile(sourcePath, destinationPath);
            }

            return true;
        }

        /// <summary>
        /// Dispose object.
        /// </summary>
        /// <param name="disposing">True when releasing managed objects.</param>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing && this.binaryReader != null)
                {
                    this.binaryReader.Close();
                    this.binaryReader = null;
                }

                this.disposed = true;
            }
        }
    }
}
