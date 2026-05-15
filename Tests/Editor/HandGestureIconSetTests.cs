using Aoyon.FaceTune.Gui;
using NUnit.Framework;
using System;
using UnityEngine;

namespace Aoyon.FaceTune.Tests
{
    public class HandGestureIconSetTests
    {
        [Test]
        public void IconNamesMatchHandGestureEnumOrder()
        {
            var icons = HandGestureIconSet.Icons;

            Assert.That(icons, Has.Length.EqualTo(Enum.GetValues(typeof(HandGesture)).Length));
            Assert.That(icons[(int)HandGesture.Neutral].TextureName, Is.Null);
            Assert.That(icons[(int)HandGesture.Neutral].FallbackText, Is.EqualTo("N"));
            Assert.That(icons[(int)HandGesture.Fist].TextureName, Is.EqualTo("oncoming-fist.png"));
            Assert.That(icons[(int)HandGesture.HandOpen].TextureName, Is.EqualTo("raised-hand.png"));
            Assert.That(icons[(int)HandGesture.FingerPoint].TextureName, Is.EqualTo("backhand-index-pointing-right.png"));
            Assert.That(icons[(int)HandGesture.Victory].TextureName, Is.EqualTo("victory-hand.png"));
            Assert.That(icons[(int)HandGesture.RockNRoll].TextureName, Is.EqualTo("sign-of-the-horns.png"));
            Assert.That(icons[(int)HandGesture.HandGun].TextureName, Is.EqualTo("love-you-gesture.png"));
            Assert.That(icons[(int)HandGesture.ThumbsUp].TextureName, Is.EqualTo("thumbs-up.png"));
        }

        [Test]
        public void CreateTintedTextureUsesSourceAlphaAndRequestedColor()
        {
            var source = new Texture2D(2, 1, TextureFormat.RGBA32, false);
            source.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.25f));
            source.SetPixel(1, 0, new Color(1f, 1f, 1f, 1f));
            source.Apply();

            var tinted = HandGestureIconSet.CreateTintedTexture(source, new Color(0.2f, 0.4f, 0.6f, 1f));

            Assert.That(tinted.GetPixel(0, 0).r, Is.EqualTo(0.2f).Within(0.01f));
            Assert.That(tinted.GetPixel(0, 0).g, Is.EqualTo(0.4f).Within(0.01f));
            Assert.That(tinted.GetPixel(0, 0).b, Is.EqualTo(0.6f).Within(0.01f));
            Assert.That(tinted.GetPixel(0, 0).a, Is.EqualTo(0.25f).Within(0.01f));
            Assert.That(tinted.GetPixel(1, 0).a, Is.EqualTo(1f).Within(0.01f));

            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(tinted);
        }
    }
}
