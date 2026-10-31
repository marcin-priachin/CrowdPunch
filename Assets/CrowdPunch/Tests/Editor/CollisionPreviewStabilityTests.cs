using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Tests
{
    public sealed class CollisionPreviewStabilityTests
    {
        private static LaunchImpactForecast Forecast(float angle = 0f) => new LaunchImpactForecast {
            Source = new Entity { Index = 1, Version = 1 }, Victim = new Entity { Index = 2, Version = 1 },
            Direction = new float3(math.sin(math.radians(angle)), 0, math.cos(math.radians(angle))),
            Confidence = 1, ArrivalSeconds = .1f, Impulse = 10 };

        private static CollisionPreviewSettings Tuning => new CollisionPreviewSettings {
            ConfirmationTime = .1f, StableAngleDegrees = 8, MinimumOpacity = .35f };

        [Test]
        public void Player003_BriefInitialWrongDirectionNeverBecomesVisible()
        {
            var state = new CollisionPreviewState(); var tuning = Tuning;
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(60), tuning, 0, .02f, 1, out _), Is.False);
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(), tuning, .04, .04f, 2, out _), Is.False);
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(), tuning, .1, .06f, 3, out _), Is.False);
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(), tuning, .14, .04f, 4, out float alpha), Is.True);
            Assert.That(state.DisplayedDirection, Is.EqualTo(math.forward())); Assert.That(alpha, Is.EqualTo(1));
        }

        [Test]
        public void Player003_ContinuousDriftMustSettleAgainstTheOriginalAnchor()
        {
            var state = new CollisionPreviewState(); var tuning = Tuning;
            CollisionPreviewStability.Update(ref state, Forecast(), tuning, 0, .02f, 1, out _);
            CollisionPreviewStability.Update(ref state, Forecast(6), tuning, .05, .05f, 2, out _);
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(12), tuning, .09, .04f, 3, out _), Is.False);
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(12), tuning, .1, .01f, 4, out _), Is.False);
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(12), tuning, .19, .09f, 5, out _), Is.True);
        }

        [Test]
        public void Player003_MajorChangeFadesOutThenConfirmsWithoutRotatingThroughWrongDirections()
        {
            var state = new CollisionPreviewState(); var tuning = Tuning;
            tuning.ConfirmationTime = 0; tuning.FadeOutTime = .04f;
            CollisionPreviewStability.Update(ref state, Forecast(), tuning, 0, .02f, 1, out _);
            tuning.ConfirmationTime = .1f;
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(90), tuning, .01, .01f, 2, out float alpha), Is.True);
            Assert.That(alpha, Is.EqualTo(.75f).Within(.001f)); Assert.That(state.DisplayedDirection, Is.EqualTo(math.forward()));
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(90), tuning, .05, .04f, 3, out _), Is.False);
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(90), tuning, .1, .05f, 4, out _), Is.False);
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(90), tuning, .11, .01f, 5, out _), Is.True);
            Assert.That(math.distance(state.DisplayedDirection, math.right()), Is.LessThan(.001f));
        }

        [Test]
        public void Player003_SmallJitterSmoothsWithoutRestartingConfirmation()
        {
            var state = new CollisionPreviewState(); var tuning = Tuning;
            tuning.ConfirmationTime = 0; tuning.SmoothingTime = .1f;
            CollisionPreviewStability.Update(ref state, Forecast(), tuning, 0, .02f, 1, out _);
            Assert.That(CollisionPreviewStability.Update(ref state, Forecast(5), tuning, .02, .02f, 2, out _), Is.True);
            Assert.That(state.Confirming, Is.Zero);
            Assert.That(math.degrees(math.atan2(state.DisplayedDirection.x, state.DisplayedDirection.z)), Is.InRange(.1f, 4.9f));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Player003_PoolingRepunchAndFollowupReuseHideOldPredictionImmediately(int change)
        {
            var state = new CollisionPreviewState(); var tuning = Tuning;
            tuning.ConfirmationTime = 0;
            var next = Forecast();
            CollisionPreviewStability.Update(ref state, next, tuning, 0, .02f, 1, out _);
            tuning.ConfirmationTime = .1f; tuning.FadeOutTime = 1;
            if (change == 0) next.SourceLifetime++;
            if (change == 1) next.VictimLifetime++;
            if (change == 2) next.SourceLaunchSequence++;
            if (change == 3) next.FollowupLifetime++;
            Assert.That(CollisionPreviewStability.Update(ref state, next, tuning, .01, .01f, 2, out _), Is.False);
        }

        [Test]
        public void Player003_ConfidenceMultipliesOpacityWithoutChangingTheArrowColour()
        {
            var state = new CollisionPreviewState(); var tuning = Tuning;
            tuning.ConfirmationTime = 0; var forecast = Forecast(); forecast.Confidence = 0;
            Assert.That(CollisionPreviewStability.Update(ref state, forecast, tuning, 0, .02f, 1, out float alpha), Is.True);
            Assert.That(alpha, Is.EqualTo(.35f));
        }

        [Test]
        public void Player003_FadeInAndZeroDurationControlsAreIndependent()
        {
            var state = new CollisionPreviewState(); var tuning = Tuning;
            tuning.ConfirmationTime = 0; tuning.FadeInTime = .1f;
            CollisionPreviewStability.Update(ref state, Forecast(), tuning, 0, .02f, 1, out float alpha);
            Assert.That(alpha, Is.EqualTo(.2f).Within(.001f));
            tuning.FadeInTime = 0;
            CollisionPreviewStability.Update(ref state, Forecast(), tuning, .02, .02f, 2, out alpha);
            Assert.That(alpha, Is.EqualTo(1));
        }

        [Test]
        public void Player003_CompetitionUsesArrivalThenImpulseThenStableSourceIdentity()
        {
            var first = Forecast(); var other = Forecast(90);
            other.Source = new Entity { Index = 9, Version = 1 }; other.ArrivalSeconds = .09f;
            Assert.That(CollisionPreviewStability.Earlier(other, first), Is.True);
            other.ArrivalSeconds = first.ArrivalSeconds; other.Impulse = 20;
            Assert.That(CollisionPreviewStability.Earlier(other, first), Is.True);
            other.Impulse = first.Impulse;
            Assert.That(CollisionPreviewStability.Earlier(other, first), Is.False);
            var tuning = Tuning; tuning.CompetitionWindow = .025f; tuning.ConflictAngleDegrees = 20;
            Assert.That(CollisionPreviewStability.Conflicts(first, other, tuning), Is.True);
            other.ArrivalSeconds = .2f;
            Assert.That(CollisionPreviewStability.Conflicts(first, other, tuning), Is.False);
        }
    }
}
