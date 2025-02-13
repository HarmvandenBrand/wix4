// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.Burn.Inscribe
{
    using System.IO;
    using WixToolset.Core.Burn.Bundles;
    using WixToolset.Core.Native;
    using WixToolset.Extensibility.Data;
    using WixToolset.Extensibility.Services;

    internal class InscribeBundleCommand
    {
        public InscribeBundleCommand(IInscribeContext context)
        {
            this.Context = context;

            this.Messaging = context.ServiceProvider.GetService<IMessaging>();
        }
        
        private IInscribeContext Context { get; }

        public IMessaging Messaging { get; }

        public bool Execute()
        {
            var inscribed = false;
            var tempFile = Path.Combine(this.Context.IntermediateFolder, "bundle_engine_signed.exe");

            using (var reader = BurnReader.Open(this.Context.InputFilePath))
            {
                FileSystem.CopyFile(this.Context.SignedEngineFile, tempFile, allowHardlink: false);
                using (BurnWriter writer = BurnWriter.Open(this.Messaging, tempFile))
                {
                    inscribed = writer.ReattachContainers(reader);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(this.Context.OutputFile));

            FileSystem.MoveFile(tempFile, this.Context.OutputFile);

            return inscribed;
        }
    }
}
