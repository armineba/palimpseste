using System;
using NUnit.Framework;
using Palimpseste.Game.Service;

namespace Palimpseste.Game.Tests
{
    public sealed class WindowsCredentialStoreTests
    {
        [Test]
        public void CredentialSurvivesReadIsScopedToServiceAndCanBeDeleted()
        {
            var id = Guid.NewGuid().ToString("N");
            var service = "https://credential-test.invalid/" + id;
            var differentService = "https://credential-test.invalid/other-" + id;
            var secret = "credential-fixture-" + id;
            try
            {
                Assert.IsTrue(WindowsCredentialStore.TryWrite(service, secret, out var writeError), writeError);
                Assert.IsTrue(WindowsCredentialStore.TryRead(service, out var read, out var readError), readError);
                Assert.AreEqual(secret, read);
                Assert.IsFalse(WindowsCredentialStore.TryRead(differentService, out var wrong, out var wrongError), wrongError);
                Assert.IsNull(wrong);
                Assert.IsTrue(WindowsCredentialStore.TryDelete(service, out var deleteError), deleteError);
                Assert.IsFalse(WindowsCredentialStore.TryRead(service, out var deleted, out var absentError), absentError);
                Assert.IsNull(deleted);
            }
            finally
            {
                WindowsCredentialStore.TryDelete(service, out _);
            }
        }
    }
}
