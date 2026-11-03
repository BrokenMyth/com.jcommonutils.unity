using JCommonUtils.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace JCommonUtils.Tests
{
    public sealed class HorizontalSectorTests
    {
        private Transform _anchor;

        [SetUp]
        public void SetUp() => _anchor = new GameObject("Sector anchor").transform;

        [TearDown]
        public void TearDown()
        {
            if (_anchor != null) Object.DestroyImmediate(_anchor.gameObject);
        }

        [Test]
        public void RadiusCenterAndAngleBoundariesArePreserved()
        {
            Assert.That(Contains(Vector3.zero), Is.True);
            Assert.That(Contains(Vector3.forward * 2f), Is.True);
            Assert.That(Contains(Vector3.forward * 2.01f), Is.False);
            Assert.That(Contains(Quaternion.Euler(0f, 45f, 0f) * Vector3.forward), Is.True);
            Assert.That(Contains(Quaternion.Euler(0f, -45f, 0f) * Vector3.forward), Is.True);
            Assert.That(Contains(Quaternion.Euler(0f, 45.2f, 0f) * Vector3.forward), Is.False);
            Assert.That(Contains(Vector3.back), Is.False);
        }

        [Test]
        public void VerticalBandUsesWorldYAndCanBeDisabled()
        {
            Assert.That(Contains(new Vector3(0f, 1f, 1f), verticalBand: true), Is.True);
            Assert.That(Contains(new Vector3(0f, -1f, 1f), verticalBand: true), Is.True);
            Assert.That(Contains(new Vector3(0f, 1.01f, 1f), verticalBand: true), Is.False);
            Assert.That(Contains(new Vector3(0f, 100f, 1f)), Is.True);
        }

        [Test]
        public void LocalOffsetAppliesScaleButRadiusIsAlreadyWorldDistance()
        {
            _anchor.SetPositionAndRotation(new Vector3(10f, 20f, -30f), Quaternion.Euler(0f, 90f, 0f));
            _anchor.localScale = new Vector3(2f, 3f, 4f);
            Vector3 offset = new Vector3(.5f, 1f, .25f);
            Vector3 origin = HorizontalSector.GetOrigin(_anchor, offset);
            Assert.That(Vector3.Distance(origin, new Vector3(11f, 23f, -31f)), Is.LessThan(.0001f));
            Assert.That(HorizontalSector.ContainsWorldPoint(_anchor, offset, 2f, 90f, true, 1f, origin + Vector3.right * 1.9f), Is.True);
            Assert.That(HorizontalSector.ContainsWorldPoint(_anchor, offset, 2f, 90f, true, 1f, origin + Vector3.right * 2.1f), Is.False);
            Assert.That(HorizontalSector.ContainsWorldPoint(_anchor, offset, 2f, 90f, true, 1f, origin + Vector3.right + Vector3.up * 1.1f), Is.False);
        }

        [Test]
        public void VerticalFacingFallsBackToWorldForward()
        {
            _anchor.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Assert.That(Contains(Vector3.forward), Is.True);
            Assert.That(Contains(Vector3.right), Is.False);
        }

        [Test]
        public void ClampedAnglesRetainFullCircleAndMinimumCone()
        {
            Assert.That(Contains(Vector3.back, angle: 360f), Is.True);
            Assert.That(Contains(Vector3.back, angle: 720f), Is.True);
            Assert.That(Contains(Quaternion.Euler(0f, .4f, 0f) * Vector3.forward, angle: 0f), Is.True);
            Assert.That(Contains(Quaternion.Euler(0f, 1f, 0f) * Vector3.forward, angle: 0f), Is.False);
        }

        [Test]
        public void RadiusAndHeightUseMinimumExtentBeforeCenterShortcut()
        {
            Assert.That(HorizontalSector.ContainsWorldPoint(_anchor, Vector3.zero, -1f, 90f, true, -1f,
                new Vector3(0f, .009f, .009f)), Is.True);
            Assert.That(HorizontalSector.ContainsWorldPoint(_anchor, Vector3.zero, -1f, 90f, true, -1f,
                new Vector3(0f, .011f, 0f)), Is.False);
            Assert.That(HorizontalSector.ContainsWorldPoint(_anchor, Vector3.zero, -1f, 90f, false, 0f,
                Vector3.forward * .011f), Is.False);
        }

        [Test]
        public void NullAndDestroyedAnchorHaveDocumentedFallbacks()
        {
            Vector3 offset = new Vector3(1f, 2f, 3f);
            Assert.That(HorizontalSector.GetOrigin(null, offset), Is.EqualTo(offset));
            Assert.That(HorizontalSector.ContainsWorldPoint(null, offset, 2f, 90f, false, 0f, offset), Is.False);
            Object.DestroyImmediate(_anchor.gameObject);
            Assert.That(HorizontalSector.GetOrigin(_anchor, offset), Is.EqualTo(offset));
            Assert.That(HorizontalSector.ContainsWorldPoint(_anchor, offset, 2f, 90f, false, 0f, offset), Is.False);
        }

        private bool Contains(Vector3 point, float radius = 2f, float angle = 90f, bool verticalBand = false)
        {
            return HorizontalSector.ContainsWorldPoint(_anchor, Vector3.zero, radius, angle, verticalBand, 1f, point);
        }
    }
}
