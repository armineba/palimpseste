using System;
using System.IO;
using NUnit.Framework;
using Palimpseste.Game.Service;
using UnityEngine;

namespace Palimpseste.Game.Tests
{
    public sealed class ServiceAccessTests
    {
        [Test]
        public void PrivateInvitationIsAcceptedOnlyForItsConfiguredHttpsService()
        {
            var root = Path.GetFullPath(Application.temporaryCachePath);
            var directory = Path.Combine(root, "palimpseste-access-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var config = Path.Combine(directory, "service.json");
                var invitation = Path.Combine(directory, "access.json");
                const string service = "https://laboratoire.example.test";
                var code = new string('A', 64);
                File.WriteAllText(config, "{\"service_url\":\"" + service + "\"}");
                File.WriteAllText(invitation, "{\"service_url\":\"" + service +
                    "\",\"invitation_code\":\"" + code + "\"}");
                Assert.IsTrue(ServiceAccess.TryLoadServiceUrl(config, out var loaded));
                Assert.AreEqual(service, loaded);
                Assert.IsTrue(ServiceAccess.TryLoadInvitation(invitation, loaded, out var invitationUrl, out var loadedCode));
                Assert.AreEqual(service, invitationUrl);
                Assert.AreEqual(code, loadedCode);
                Assert.IsFalse(ServiceAccess.TryLoadInvitation(invitation, "https://autre.example.test", out _, out _));

                File.WriteAllText(invitation, "{\"service_url\":\"" + service +
                    "\",\"invitation_code\":\"" + new string('É', 64) + "\"}");
                Assert.IsFalse(ServiceAccess.TryLoadInvitation(invitation, loaded, out _, out _),
                    "Only the backend's ASCII base64url invitation alphabet is accepted");
                File.WriteAllText(config, "{\"service_url\":\"http://public.example.test\"}");
                Assert.IsFalse(ServiceAccess.TryLoadServiceUrl(config, out _),
                    "Player access must not send credentials over non-loopback HTTP");

                var identity = Path.Combine(directory, "identity.json");
                var principal = Guid.NewGuid().ToString("N");
                ServiceAccess.SavePrincipalId(identity, service, "opaque-token-A", principal);
                Assert.IsTrue(ServiceAccess.TryLoadPrincipalId(identity, service, "opaque-token-A", out var loadedPrincipal));
                Assert.AreEqual(principal, loadedPrincipal);
                Assert.IsFalse(ServiceAccess.TryLoadPrincipalId(identity, service, "opaque-token-B", out _),
                    "Another player's credential cannot unlock the prior player's local library");
                Assert.IsFalse(ServiceAccess.TryLoadPrincipalId(identity, "https://autre.example.test", "opaque-token-A", out _));
            }
            finally
            {
                var full = Path.GetFullPath(directory);
                Assert.IsTrue(full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                if (Directory.Exists(full)) Directory.Delete(full, true);
            }
        }
    }
}
