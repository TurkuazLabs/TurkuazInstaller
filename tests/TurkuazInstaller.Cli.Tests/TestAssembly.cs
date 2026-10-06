// 📄 Dosya Yolu: /tests/TurkuazInstaller.Cli.Tests/TestAssembly.cs
// 📌 Amac: Process-global Console stdout/stderr kullanan CLI testlerini deterministic calistirir
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: CLI test assembly'sinde paralel calismayi kapatarak Console.SetOut/SetError yarismalarini engeller
//
// Bagimli Oldugu Katman: Tool | Service

using Xunit;

[assembly: CollectionBehavior(
    DisableTestParallelization = true)]
