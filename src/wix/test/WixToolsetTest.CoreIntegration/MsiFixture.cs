// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolsetTest.CoreIntegration
{
    using System;
    using System.IO;
    using System.Linq;
    using Example.Extension;
    using WixBuildTools.TestSupport;
    using WixToolset.Core.TestPackage;
    using WixToolset.Data;
    using WixToolset.Data.Symbols;
    using WixToolset.Data.WindowsInstaller;
    using Xunit;

    public class MsiFixture
    {
        [Fact]
        public void CanBuildSingleFile()
        {
            var folder = TestData.Get(@"TestData\SingleFile");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.wixpdb")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\PFiles\MsiPackage\test.txt")));

                var intermediate = Intermediate.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"));

                Assert.False(intermediate.HasLevel(WixToolset.Data.IntermediateLevels.Compiled));
                Assert.True(intermediate.HasLevel(WixToolset.Data.IntermediateLevels.Linked));
                Assert.True(intermediate.HasLevel(WixToolset.Data.IntermediateLevels.Resolved));
                Assert.True(intermediate.HasLevel(WixToolset.Data.WindowsInstaller.IntermediateLevels.FullyBound));

                var section = intermediate.Sections.Single();

                var fileSymbol = section.Symbols.OfType<FileSymbol>().First();
                Assert.Equal(Path.Combine(folder, @"data\test.txt"), fileSymbol[FileSymbolFields.Source].AsPath().Path);
                Assert.Equal(@"test.txt", fileSymbol[FileSymbolFields.Source].PreviousValue.AsPath().Path);
            }
        }

        [Fact]
        public void CanBuildSingleFileCompressed()
        {
            var folder = TestData.Get(@"TestData\SingleFileCompressed");

            using (var fs = new DisposableFileSystem())
            {
                var intermediateFolder = fs.GetFolder();

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\example.cab")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.wixpdb")));

                var intermediate = Intermediate.Load(Path.Combine(intermediateFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                var fileSymbol = section.Symbols.OfType<FileSymbol>().Single();
                Assert.Equal(Path.Combine(folder, @"data\test.txt"), fileSymbol[FileSymbolFields.Source].AsPath().Path);
                Assert.Equal(@"test.txt", fileSymbol[FileSymbolFields.Source].PreviousValue.AsPath().Path);
            }
        }

        [Fact]
        public void CanBuildSingleFileCompressedWithMediaTemplate()
        {
            var folder = TestData.Get(@"TestData\SingleFileCompressed");

            using (var fs = new DisposableFileSystem())
            {
                var intermediateFolder = fs.GetFolder();

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-d", "MediaTemplateCompressionLevel",
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\cab1.cab")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.wixpdb")));
            }
        }

        [Fact]
        public void CanBuildSingleFileCompressedWithMediaTemplateWithLowCompression()
        {
            var folder = TestData.Get(@"TestData\SingleFileCompressed");

            using (var fs = new DisposableFileSystem())
            {
                var intermediateFolder = fs.GetFolder();

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-d", "MediaTemplateCompressionLevel=low",
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\low1.cab")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.wixpdb")));
            }
        }

        [Fact]
        public void CanBuildMultipleFilesCompressed()
        {
            var folder = TestData.Get(@"TestData\MultiFileCompressed");

            using (var fs = new DisposableFileSystem())
            {
                var intermediateFolder = fs.GetFolder();

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    "-sw1079", // TODO: why does this test need to create a second cab which is empty?
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\example1.cab")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\example2.cab")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.wixpdb")));
            }
        }

        [Fact]
        public void CanFailBuildMissingFile()
        {
            var folder = TestData.Get(@"TestData\SingleFile");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "does-not-exist"),
                    "-bindpath", Path.Combine(folder, "also-does-not-exist"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                }, out var messages);
                Assert.Equal(103, result);

                var error = messages.Single(m => m.Level == MessageLevel.Error);
                var errorMessage = error.ToString();
                var checkedPaths = errorMessage.Substring(errorMessage.IndexOf(':') + 1).Split(new[] { ',' }).Select(s => s.Trim()).ToArray();
                Assert.Equal(new[]
                {
                    "test.txt",
                    Path.Combine(folder, "does-not-exist", "test.txt"),
                    Path.Combine(folder, "also-does-not-exist", "test.txt"),
                }, checkedPaths);
            }
        }

        [Fact]
        public void CanBuildWithErrorTable()
        {
            var folder = TestData.Get(@"TestData\ErrorsInUI");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.wixpdb")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\PFiles\MsiPackage\test.txt")));

                var intermediate = Intermediate.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                var errors = section.Symbols.OfType<ErrorSymbol>().ToDictionary(t => t.Id.Id);
                Assert.Equal("Category 55 Emergency Doomsday Crisis", errors["1234"].Message.Trim());
                Assert.Equal(" ", errors["5678"].Message);

                var customAction1 = section.Symbols.OfType<CustomActionSymbol>().Where(t => t.Id.Id == "CanWeReferenceAnError_YesWeCan").Single();
                Assert.Equal("1234", customAction1.Target);

                var customAction2 = section.Symbols.OfType<CustomActionSymbol>().Where(t => t.Id.Id == "TextErrorsWorkOKToo").Single();
                Assert.Equal("If you see this, something went wrong.", customAction2.Target);
            }
        }

        [Fact]
        public void CanLoadPdbGeneratedByBuild()
        {
            var folder = TestData.Get(@"TestData\MultiFileCompressed");

            using (var fs = new DisposableFileSystem())
            {
                var intermediateFolder = fs.GetFolder();

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-d", "MediaTemplateCompressionLevel",
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\cab1.cab")));

                var pdbPath = Path.Combine(intermediateFolder, @"bin\test.wixpdb");
                Assert.True(File.Exists(pdbPath));

                var output = WindowsInstallerData.Load(pdbPath, suppressVersionCheck: true);
                Assert.NotNull(output);
            }
        }

        [Fact]
        public void CanLoadPdbGeneratedByBuildViaWixOutput()
        {
            var folder = TestData.Get(@"TestData\MultiFileCompressed");

            using (var fs = new DisposableFileSystem())
            {
                var intermediateFolder = fs.GetFolder();

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-d", "MediaTemplateCompressionLevel",
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\cab1.cab")));

                var pdbPath = Path.Combine(intermediateFolder, @"bin\test.wixpdb");
                Assert.True(File.Exists(pdbPath));

                var wixOutput = WixOutput.Read(pdbPath);
                var output = WindowsInstallerData.Load(wixOutput, suppressVersionCheck: true);
                Assert.NotNull(output);
            }
        }

        [Fact]
        public void CanBuildManualUpgrade()
        {
            var folder = TestData.Get(@"TestData\ManualUpgrade");

            using (var fs = new DisposableFileSystem())
            {
                var intermediateFolder = fs.GetFolder();

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                }, out var messages);

                Assert.Equal(0, result);

                var pdbPath = Path.Combine(intermediateFolder, @"bin\test.wixpdb");
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\test.msi")));
                Assert.True(File.Exists(pdbPath));
                Assert.True(File.Exists(Path.Combine(intermediateFolder, @"bin\PFiles\MsiPackage\test.txt")));

                var intermediate = Intermediate.Load(pdbPath);
                var section = intermediate.Sections.Single();

                var upgradeSymbol = section.Symbols.OfType<UpgradeSymbol>().Single();
                Assert.False(upgradeSymbol.ExcludeLanguages);
                Assert.True(upgradeSymbol.IgnoreRemoveFailures);
                Assert.False(upgradeSymbol.VersionMaxInclusive);
                Assert.True(upgradeSymbol.VersionMinInclusive);
                Assert.Equal("13.0.0", upgradeSymbol.VersionMax);
                Assert.Equal("12.0.0", upgradeSymbol.VersionMin);
                Assert.False(upgradeSymbol.OnlyDetect);
                Assert.Equal("BLAHBLAHBLAH", upgradeSymbol.ActionProperty);

                var pdb = WindowsInstallerData.Load(pdbPath, suppressVersionCheck: false);
                var secureProperties = pdb.Tables["Property"].Rows.Where(row => row.GetKey() == "SecureCustomProperties").Single();
                Assert.Contains("BLAHBLAHBLAH", secureProperties.FieldAsString(1));
            }
        }

        [Fact]
        public void CanBuildWixipl()
        {
            var folder = TestData.Get(@"TestData\SingleFile");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.wixipl")
                }, out var messages);

                Assert.Equal(0, result);

                var builtFiles = Directory.GetFiles(Path.Combine(baseFolder, @"bin"));

                Assert.Equal(new[]{
                    "test.wixipl"
                }, builtFiles.Select(Path.GetFileName).ToArray());
            }
        }

        [Fact]
        public void CanBuildWixlib()
        {
            var folder = TestData.Get(@"TestData\SingleFile");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.wixlib")
                }, out var messages);

                Assert.Equal(0, result);

                var builtFiles = Directory.GetFiles(Path.Combine(baseFolder, @"bin"));

                Assert.Equal(new[]{
                    "test.wixlib"
                }, builtFiles.Select(Path.GetFileName).ToArray());
            }
        }

        [Fact]
        public void CanBuildBinaryWixlib()
        {
            var folder = TestData.Get(@"TestData\SingleFile");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-bindfiles",
                    "-o", Path.Combine(baseFolder, @"bin\test.wixlib"));

                result.AssertSuccess();

                using (var wixout = WixOutput.Read(Path.Combine(baseFolder, @"bin\test.wixlib")))
                {
                    Assert.NotNull(wixout.GetDataStream("wix-ir.json"));

                    var text = wixout.GetData("wix-ir/test.txt");
                    Assert.Equal("This is test.txt.", text);
                }
            }
        }

        [Fact]
        public void CanBuildBinaryWixlibWithCollidingFilenames()
        {
            var folder = TestData.Get(@"TestData\SameFileFolders");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(
                    "build",
                    Path.Combine(folder, "TestComponents.wxs"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-bindfiles",
                    "-o", Path.Combine(baseFolder, @"bin\test.wixlib"));

                result.AssertSuccess();

                using (var wixout = WixOutput.Read(Path.Combine(baseFolder, @"bin\test.wixlib")))
                {
                    Assert.NotNull(wixout.GetDataStream("wix-ir.json"));

                    var text = wixout.GetData("wix-ir/test.txt");
                    Assert.Equal(@"This is a\test.txt.", text);

                    var text2 = wixout.GetData("wix-ir/test.txt-1");
                    Assert.Equal(@"This is b\test.txt.", text2);

                    var text3 = wixout.GetData("wix-ir/test.txt-2");
                    Assert.Equal(@"This is c\test.txt.", text3);
                }
            }
        }

        [Fact]
        public void CanBuildWithIncludePath()
        {
            var folder = TestData.Get(@"TestData\IncludePath");
            var bindpath = Path.Combine(folder, "data");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", bindpath,
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.msi"),
                    "-i", bindpath);

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.wixpdb")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\PFiles\MsiPackage\test.txt")));

                var intermediate = Intermediate.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                var fileSymbol = section.Symbols.OfType<FileSymbol>().Single();
                Assert.Equal(Path.Combine(folder, @"data\test.txt"), fileSymbol[FileSymbolFields.Source].AsPath().Path);
                Assert.Equal(@"test.txt", fileSymbol[FileSymbolFields.Source].PreviousValue.AsPath().Path);
            }
        }

        [Fact]
        public void CanBuildWithAssembly()
        {
            var folder = TestData.Get(@"TestData\Assembly");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.wixpdb")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\PFiles\AssemblyMsiPackage\candle.exe")));

                var intermediate = Intermediate.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                var fileSymbol = section.Symbols.OfType<FileSymbol>().Single();
                Assert.Equal(Path.Combine(folder, @"data\candle.exe"), fileSymbol[FileSymbolFields.Source].AsPath().Path);
                Assert.Equal(@"candle.exe", fileSymbol[FileSymbolFields.Source].PreviousValue.AsPath().Path);

                var msiAssemblyNameSymbols = section.Symbols.OfType<MsiAssemblyNameSymbol>();
                Assert.Equal(new[]
                {
                    "culture",
                    "fileVersion",
                    "name",
                    "processorArchitecture",
                    "publicKeyToken",
                    "version"
                }, msiAssemblyNameSymbols.OrderBy(a => a.Name).Select(a => a.Name).ToArray());

                Assert.Equal(new[]
                {
                    "neutral",
                    "3.11.11810.0",
                    "candle",
                    "x86",
                    "256B3414DFA97718",
                    "3.0.0.0"
                }, msiAssemblyNameSymbols.OrderBy(a => a.Name).Select(a => a.Value).ToArray());
            }
        }

        [Fact]
        public void CanBuildWithNet1xAssembly()
        {
            var folder = TestData.Get(@"TestData\Assembly1x");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.wixpdb")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\PFiles\AssemblyMsiPackage\candle.exe")));

                var intermediate = Intermediate.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                var fileSymbol = section.Symbols.OfType<FileSymbol>().Single();
                Assert.Equal(Path.Combine(folder, @"data\candle.exe"), fileSymbol[FileSymbolFields.Source].AsPath().Path);
                Assert.Equal(@"candle.exe", fileSymbol[FileSymbolFields.Source].PreviousValue.AsPath().Path);

                var msiAssemblyNameSymbols = section.Symbols.OfType<MsiAssemblyNameSymbol>();
                Assert.Equal(new[]
                {
                    "culture",
                    "fileVersion",
                    "name",
                    "publicKeyToken",
                    "version"
                }, msiAssemblyNameSymbols.OrderBy(a => a.Name).Select(a => a.Name).ToArray());

                Assert.Equal(new[]
                {
                    "neutral",
                    "2.0.5805.0",
                    "candle",
                    "CE35F76FCDA82BAD",
                    "2.0.5805.0",
                }, msiAssemblyNameSymbols.OrderBy(a => a.Name).Select(a => a.Value).ToArray());
            }
        }

        [Fact]
        public void CanBuild64bit()
        {
            var folder = TestData.Get(@"TestData\SingleFile");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-arch", "x64",
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                var intermediate = Intermediate.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                var platformSummary = section.Symbols.OfType<SummaryInformationSymbol>().Single(s => s.PropertyId == SummaryInformationType.PlatformAndLanguage);
                Assert.Equal("x64;1033", platformSummary.Value);
            }
        }

        [Fact]
        public void CanBuildSharedComponent()
        {
            var folder = TestData.Get(@"TestData\SingleFile");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-arch", "x64",
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                var intermediate = Intermediate.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                // Only one component is shared.
                var sharedComponentSymbols = section.Symbols.OfType<ComponentSymbol>();
                Assert.Equal(1, sharedComponentSymbols.Sum(t => t.Shared ? 1 : 0));

                // And it is this one.
                var sharedComponentSymbol = sharedComponentSymbols.Single(t => t.Id.Id == "Shared.dll");
                Assert.True(sharedComponentSymbol.Shared);
            }
        }

        [Fact]
        public void CanBuildSetProperty()
        {
            var folder = TestData.Get(@"TestData\SetProperty");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                var output = WindowsInstallerData.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"), false);
                var caRows = output.Tables["CustomAction"].Rows.Single();
                Assert.Equal("SetINSTALLLOCATION", caRows.FieldAsString(0));
                Assert.Equal("51", caRows.FieldAsString(1));
                Assert.Equal("INSTALLLOCATION", caRows.FieldAsString(2));
                Assert.Equal("[INSTALLFOLDER]", caRows.FieldAsString(3));
            }
        }

        [Fact]
        public void CanBuildVersionIndependentProgId()
        {
            var folder = TestData.Get(@"TestData\ProgId");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.msi")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.wixpdb")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\PFiles\MsiPackage\Foo.exe")));

                var intermediate = Intermediate.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                var progids = section.Symbols.OfType<ProgIdSymbol>().OrderBy(symbol => symbol.ProgId).ToList();
                Assert.Equal(new[]
                {
                    "Foo.File.hol",
                    "Foo.File.hol.15"
                }, progids.Select(p => p.ProgId).ToArray());

                Assert.Equal(new[]
                {
                    "Foo.File.hol.15",
                    null
                }, progids.Select(p => p.ParentProgIdRef).ToArray());
            }
        }

        [Fact]
        public void CanBuildInstanceTransform()
        {
            var folder = TestData.Get(@"TestData\InstanceTransform");

            using (var fs = new DisposableFileSystem())
            {
                var intermediateFolder = fs.GetFolder();

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                var output = WindowsInstallerData.Load(Path.Combine(intermediateFolder, @"bin\test.wixpdb"), false);
                var substorage = output.SubStorages.Single();
                Assert.Equal("I1", substorage.Name);

                var data = substorage.Data;
                Assert.Equal(new[]
                {
                    "_SummaryInformation",
                    "Property",
                    "Upgrade"
                }, data.Tables.Select(t => t.Name).ToArray());

                Assert.Equal(new[]
                {
                    "INSTANCEPROPERTY\tI1",
                    "ProductName\tMsiPackage (Instance 1)",
                }, JoinRows(data.Tables["Property"]));

                Assert.Equal(new[]
                {
                    "{047730A5-30FE-4A62-A520-DA9381B8226A}\t\t1.0.0.0\t1033\t1\t\tWIX_UPGRADE_DETECTED",
                    "{047730A5-30FE-4A62-A520-DA9381B8226A}\t\t1.0.0.0\t1033\t1\t0\t0",
                    "{047730A5-30FE-4A62-A520-DA9381B8226A}\t1.0.0.0\t\t1033\t2\t\tWIX_DOWNGRADE_DETECTED",
                    "{047730A5-30FE-4A62-A520-DA9381B8226A}\t1.0.0.0\t\t1033\t2\t0\t0"
                }, JoinRows(data.Tables["Upgrade"]));
            }
        }

        [Fact(Skip = "Test demonstrates failure")]
        public void FailsBuildAtLinkTimeForMissingEnsureTable()
        {
            var folder = TestData.Get(@"TestData");
            var extensionPath = Path.GetFullPath(new Uri(typeof(ExampleExtensionFactory).Assembly.CodeBase).LocalPath);

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");
                var msiPath = Path.Combine(baseFolder, @"bin\test.msi");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "BadEnsureTable", "BadEnsureTable.wxs"),
                    Path.Combine(folder, "ProductWithComponentGroupRef", "MinimalComponentGroup.wxs"),
                    Path.Combine(folder, "ProductWithComponentGroupRef", "Product.wxs"),
                    "-ext", extensionPath,
                    "-bindpath", Path.Combine(folder, "SingleFile", "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", msiPath
                });
                Assert.Collection(result.Messages,
                    first =>
                    {
                        Assert.Equal(MessageLevel.Error, first.Level);
                        Assert.Equal("The identifier 'WixCustomTable:TableDefinitionNotExposedByExtension' could not be found. Ensure you have typed the reference correctly and that all the necessary inputs are provided to the linker.", first.ToString());
                    });

                Assert.False(File.Exists(msiPath));
            }
        }

        private static string[] JoinRows(Table table)
        {
            return table.Rows.Select(r => JoinFields(r.Fields)).ToArray();

            string JoinFields(Field[] fields)
            {
                return String.Join('\t', fields.Select(f => f.ToString()));
            }
        }
    }
}
