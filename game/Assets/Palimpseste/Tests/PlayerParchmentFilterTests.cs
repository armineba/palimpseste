using System;
using NUnit.Framework;
using Palimpseste.Game.Library;

namespace Palimpseste.Game.Tests
{
    public sealed class PlayerParchmentFilterTests
    {
        [Test]
        public void LibraryShowsOnlyServerParchmentsOwnedByCurrentPrincipal()
        {
            var id = Guid.NewGuid().ToString("N");
            var owner = Guid.NewGuid().ToString("N");
            var anotherOwner = Guid.NewGuid().ToString("N");
            var fixture = new ParchmentRecord
            {
                parchment_id = "fixture-parchment-001", spell_id = "fixture-spell-braise",
                state = "ready", capture_key = Guid.NewGuid().ToString("N")
            };
            Assert.IsFalse(PlayerParchmentFilter.IsUserParchment(fixture, owner));
            var server = new ParchmentRecord
            {
                parchment_id = id, capture_key = Guid.NewGuid().ToString("N"),
                server_issued = true, state = "ready", owner_id = owner
            };
            Assert.IsTrue(PlayerParchmentFilter.IsUserParchment(server, owner));
            Assert.IsFalse(PlayerParchmentFilter.IsUserParchment(server, anotherOwner));
            Assert.IsFalse(PlayerParchmentFilter.IsUserParchment(server, null));
            var legacyServer = new ParchmentRecord
            {
                parchment_id = id, capture_key = Guid.NewGuid().ToString("N"),
                reference_artifact_id = "server", state = "ready"
            };
            Assert.IsFalse(PlayerParchmentFilter.IsUserParchment(legacyServer, owner),
                "Old records without an owner must not be exposed to another player");
            legacyServer.owner_id = owner;
            Assert.IsTrue(PlayerParchmentFilter.IsUserParchment(legacyServer, owner));
            legacyServer.capture_key = null;
            Assert.IsFalse(PlayerParchmentFilter.IsUserParchment(legacyServer, owner));
        }
    }
}
