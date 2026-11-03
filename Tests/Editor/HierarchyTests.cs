using JCommonUtils.Hierarchy;
using NUnit.Framework;
using UnityEngine;

namespace JCommonUtils.Tests
{
    public sealed class HierarchyTests
    {
        private Transform _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("Root").transform;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
        }

        [Test]
        public void RootInclusionIsExplicit()
        {
            Transform child = Child(_root, "Root");
            Assert.That(TransformSearch.FindChildRecursive(_root, "Root"), Is.SameAs(child));
            Assert.That(TransformSearch.FindSelfOrChildRecursive(_root, "Root"), Is.SameAs(_root));
        }

        [Test]
        public void DuplicateNamesUseDepthFirstHierarchyOrder()
        {
            Transform firstBranch = Child(_root, "Branch");
            Transform deepMatch = Child(firstBranch, "Target");
            Transform shallowMatch = Child(_root, "Target");
            Assert.That(TransformSearch.FindChildRecursive(_root, "Target"), Is.SameAs(deepMatch));
            shallowMatch.SetSiblingIndex(0);
            Assert.That(TransformSearch.FindChildRecursive(_root, "Target"), Is.SameAs(shallowMatch));
        }

        [Test]
        public void InactiveBranchesAreIncluded()
        {
            Transform branch = Child(_root, "Inactive");
            Transform match = Child(branch, "Target");
            branch.gameObject.SetActive(false);
            match.gameObject.SetActive(false);
            Assert.That(TransformSearch.FindChildRecursive(_root, "Target"), Is.SameAs(match));
        }

        [Test]
        public void RenameReparentAndDestroyAreObservedWithoutCache()
        {
            Transform match = Child(_root, "Target");
            Assert.That(TransformSearch.FindChildRecursive(_root, "Target"), Is.SameAs(match));
            match.name = "Renamed";
            Assert.That(TransformSearch.FindChildRecursive(_root, "Target"), Is.Null);
            Transform branch = Child(_root, "Branch");
            match.SetParent(branch, false);
            Assert.That(TransformSearch.FindChildRecursive(_root, "Renamed"), Is.SameAs(match));
            Object.DestroyImmediate(match.gameObject);
            Assert.That(TransformSearch.FindChildRecursive(_root, "Renamed"), Is.Null);
        }

        [Test]
        public void NamesAreExactAndEmptyNamesRemainSearchable()
        {
            Transform match = Child(_root, "target");
            Transform unnamed = Child(_root, string.Empty);
            Assert.That(TransformSearch.FindChildRecursive(_root, "Target"), Is.Null);
            Assert.That(TransformSearch.FindChildRecursive(_root, "target"), Is.SameAs(match));
            Assert.That(TransformSearch.FindChildRecursive(_root, string.Empty), Is.SameAs(unnamed));
            Assert.That(TransformSearch.FindChildRecursive(_root, null), Is.Null);
        }

        [Test]
        public void NullAndDestroyedRootsReturnNull()
        {
            Assert.That(TransformSearch.FindChildRecursive(null, "Target"), Is.Null);
            Assert.That(TransformSearch.FindSelfOrChildRecursive(null, "Target"), Is.Null);
            Object.DestroyImmediate(_root.gameObject);
            Assert.That(TransformSearch.FindChildRecursive(_root, "Root"), Is.Null);
            Assert.That(TransformSearch.FindSelfOrChildRecursive(_root, "Root"), Is.Null);
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
