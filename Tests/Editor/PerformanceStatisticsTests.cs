using System;
using JCommonUtils.Editor.Diagnostics;
using NUnit.Framework;

namespace JCommonUtils.Tests
{
    public sealed class PerformanceStatisticsTests
    {
        [Test]
        public void Statistics_PreserveHistoricalPercentilesWithoutChangingInput()
        {
            var values = new[] { 40d, 10d, 30d, 20d };
            PerformanceStatistics result = PerformanceStatistics.Calculate(values);
            Assert.That(result.Samples, Is.EqualTo(4));
            Assert.That(result.Mean, Is.EqualTo(25).Within(1e-12));
            Assert.That(result.P50, Is.EqualTo(30));
            Assert.That(result.P95, Is.EqualTo(40));
            Assert.That(result.Max, Is.EqualTo(40));
            Assert.That(values, Is.EqualTo(new[] { 40d, 10d, 30d, 20d }));

            var hundred = new double[100];
            for (int i = 0; i < hundred.Length; ++i) hundred[i] = i + 1;
            Assert.That(PerformanceStatistics.Calculate(hundred).P95, Is.EqualTo(96));
        }

        [Test]
        public void Statistics_DistinguishMissingZeroAndInvalidData()
        {
            Assert.That(PerformanceStatistics.Calculate(Array.Empty<double>()), Is.Null);
            PerformanceStatistics zero = PerformanceStatistics.Calculate(new[] { 0d });
            Assert.That(zero.Samples, Is.EqualTo(1));
            Assert.That(zero.Mean, Is.Zero);
            Assert.That(zero.P50, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => PerformanceStatistics.Calculate(null));
            Assert.Throws<ArgumentException>(() => PerformanceStatistics.Calculate(new[] { double.NaN }));
            Assert.Throws<ArgumentException>(() => PerformanceStatistics.Calculate(new[] { double.PositiveInfinity }));
            Assert.That(PerformanceStatistics.Calculate(new[] { 1e308, -1e308 }).Mean, Is.Zero);
        }

        [Test]
        public void Capture_RejectsInvalidConfigurationBeforeRegisteringCallbacks()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceCapture.Start(new PerformanceCaptureOptions { DurationSeconds = double.NaN }));
            Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceCapture.Start(new PerformanceCaptureOptions { MaxSamples = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceCapture.Start(new PerformanceCaptureOptions { WarmupSeconds = -1 }));
        }
    }
}
