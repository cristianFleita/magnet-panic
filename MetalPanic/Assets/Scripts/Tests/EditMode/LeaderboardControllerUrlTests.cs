using NUnit.Framework;

namespace MagnetPanic.UI.Tests
{
    public sealed class LeaderboardControllerUrlTests
    {
        [Test]
        public void ResolveLeaderboardUrlUsesBrowserValueWhenPresent()
        {
            Assert.That(
                LeaderboardController.ResolveLeaderboardUrl("https://api.example.test/leaderboard"),
                Is.EqualTo("https://api.example.test/leaderboard"));
        }

        [Test]
        public void ResolveLeaderboardUrlAppendsLeaderboardPathToBackendOrigin()
        {
            Assert.That(
                LeaderboardController.ResolveLeaderboardUrl("https://api.example.test"),
                Is.EqualTo("https://api.example.test/leaderboard"));
        }

        [Test]
        public void ResolveLeaderboardUrlFallsBackToLocalhostWhenBrowserValueIsBlank()
        {
            Assert.That(
                LeaderboardController.ResolveLeaderboardUrl(" "),
                Is.EqualTo("http://localhost:3000/leaderboard"));
        }
    }
}
