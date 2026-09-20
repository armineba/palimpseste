using NUnit.Framework;
using Palimpseste.Game.Service;

namespace Palimpseste.Game.Tests
{
    public sealed class LabApiErrorTests
    {
        [Test]
        public void ApiProblemDisplaysOnlyItsReadableMessageAndKeepsCodeForRecovery()
        {
            const string body = "{\"code\":\"generation_quota_exceeded\",\"message\":\"Votre quota quotidien de générations est atteint. Réessayez plus tard.\",\"trace_id\":\"private-trace\",\"retryable\":true}";
            Assert.IsTrue(ApiErrorText.TryReadProblem(body, out var code, out var message));
            Assert.AreEqual("generation_quota_exceeded", code);
            Assert.AreEqual("Votre quota quotidien de générations est atteint. Réessayez plus tard.", message);
            Assert.AreEqual(message, ApiErrorText.Display(body, "HTTP/1.1 429"));
            StringAssert.DoesNotContain("trace_id", ApiErrorText.Display(body, "HTTP/1.1 429"));
        }

        [Test]
        public void UnknownResponseIsBoundedAndNetworkErrorRemainsReadable()
        {
            Assert.AreEqual(300, ApiErrorText.Display(new string('X', 500), null).Length);
            Assert.AreEqual("Connexion impossible", ApiErrorText.Display(null, "Connexion impossible"));
            Assert.AreEqual("Erreur réseau", ApiErrorText.Display(null, null));
        }
    }
}
