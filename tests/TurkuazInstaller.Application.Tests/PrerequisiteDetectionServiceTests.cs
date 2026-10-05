// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/PrerequisiteDetectionServiceTests.cs
// 📌 Amac: Generic prerequisite detector registry yonlendirme ve fail-closed davranisini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.1
// Aciklama: Bilinen detector, bilinmeyen id ve duplicate detector id senaryolarini kapsar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Application.Prerequisites;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Prerequisites;
using Xunit;

namespace TurkuazInstaller.Application.Tests;

public sealed class PrerequisiteDetectionServiceTests
{
    [Fact]
    public async Task IsSatisfiedAsync_KnownId_UsesRegisteredDetector()
    {
        var detector =
            new StubDetector(
                "example-runtime",
                true);

        var service =
            new PrerequisiteDetectionService(
                new[]
                {
                    detector
                });

        Assert.True(
            service.Supports(
                "example-runtime"));

        var result =
            await service.IsSatisfiedAsync(
                new Prerequisite(
                    "example-runtime",
                    ">=1.0.0"),
                CancellationToken.None);

        Assert.True(result);
        Assert.Equal(
            1,
            detector.Calls);
    }

    [Fact]
    public async Task IsSatisfiedAsync_UnknownId_FailsClosed()
    {
        var service =
            new PrerequisiteDetectionService(
                Array.Empty<IPrerequisiteDetector>());

        Assert.False(
            service.Supports(
                "unknown-runtime"));

        var result =
            await service.IsSatisfiedAsync(
                new Prerequisite(
                    "unknown-runtime",
                    ">=1.0.0"),
                CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public void Constructor_DuplicateId_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () =>
                new PrerequisiteDetectionService(
                    new IPrerequisiteDetector[]
                    {
                        new StubDetector(
                            "example-runtime",
                            true),
                        new StubDetector(
                            "EXAMPLE-RUNTIME",
                            false)
                    }));
    }

    private sealed class StubDetector
        : IPrerequisiteDetector
    {
        private readonly bool _result;

        public StubDetector(
            string id,
            bool result)
        {
            Id = id;
            _result = result;
        }

        public string Id { get; }

        public int Calls { get; private set; }

        public Task<bool> IsSatisfiedAsync(
            Prerequisite prerequisite,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(
                _result);
        }
    }
}
