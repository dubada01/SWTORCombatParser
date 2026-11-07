using NUnit.Framework;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser_Test
{
    [TestFixture]
    class TestEncryption
    {
        [Test]
        public void TextEncryption()
        {
            const string secret = "test-key";
            const string input = "A representative connection string";
            var encryptedOutput = Crypto.EncryptStringAES(input, secret);
            var output = Crypto.DecryptStringAES(encryptedOutput, secret);
            Assert.That(output, Is.EqualTo(input));
        }
    }
}
