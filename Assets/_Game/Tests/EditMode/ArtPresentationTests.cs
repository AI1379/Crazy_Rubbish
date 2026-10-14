using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class ArtPresentationTests
    {
        private static PrototypeAppearance Appearance => AssetDatabase.LoadAssetAtPath<PrototypeAppearance>("Assets/_Game/Data/M1Appearance.asset");

        [Test]
        public void EveryDemoItemAndCharacterPoseResolvesToSuppliedArtwork()
        {
            var art = Appearance;
            Assert.That(art.characters.Length, Is.EqualTo(6));
            foreach (var set in art.characters)
                foreach (var poses in new[] { set.idle, set.carry, set.pickup, set.saw })
                {
                    Assert.That(poses.Length, Is.EqualTo(4));
                    foreach (var sprite in poses) Assert.That(AssetDatabase.GetAssetPath(sprite), Does.StartWith("Assets/_Game/Art/Delivery_20261013/"));
                }
            foreach (ItemId item in System.Enum.GetValues(typeof(ItemId)))
                Assert.That(AssetDatabase.GetAssetPath(art.ItemSprite(item)), Does.StartWith("Assets/_Game/Art/Delivery_20261013/"), item.ToString());
        }

        [Test]
        public void ChangingDirectionAndActionKeepsTheLogicalFeetPositionAndPausePreservesFacing()
        {
            var obj = new GameObject("Art test actor");
            try
            {
                var actor = obj.AddComponent<PrototypeActorView>();
                actor.Configure(Appearance, 0);
                actor.Refresh(new Vector2(8, 6), 0, false, 0);
                actor.Refresh(new Vector2(8.2f, 6), 1, false, 0.25);
                Assert.That(actor.Artwork.sprite, Is.EqualTo(Appearance.characters[0].carry[3]));
                actor.Refresh(new Vector2(8.2f, 6), 2, false, 0.5);
                Assert.That(actor.Artwork.sprite, Is.EqualTo(Appearance.characters[0].pickup[3]));
                Assert.That(actor.transform.position, Is.EqualTo(new Vector3(8.2f, 6, 0)));
                Assert.That(actor.Artwork.transform.localPosition, Is.EqualTo(Vector3.zero));
                actor.Refresh(new Vector2(8.2f, 6), 3, true, 0.5);
                Assert.That(actor.Facing, Is.EqualTo(3));
                Assert.That(actor.Artwork.transform.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void PickupPoseTracksAnActualPendingPickupAndClearsOnCancelOrCompletion()
        {
            var model = new PrototypeSimulation(new PrototypeSettings { automaticInput = false });
            Assert.That(model.PickUp(1, ItemId.WoodHandSaw).Success, Is.True);
            Assert.That(model.PickingUp, Is.True);
            model.CancelInteraction();
            Assert.That(model.PickingUp, Is.False);
            Assert.That(model.PickUp(1, ItemId.WoodHandSaw).Success, Is.True);
            model.Advance(0.55);
            Assert.That(model.PickingUp, Is.False);
            Assert.That(model.Carry.Count(ItemId.WoodHandSaw), Is.EqualTo(1));
        }
    }
}
