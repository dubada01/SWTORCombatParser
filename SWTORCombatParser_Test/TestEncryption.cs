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
            var secret = "obscureButNotSecure";
            var input = "Host=swtorparse-free.cagglk8w6mwm.us-west-2.rds.amazonaws.com;Port=5432;Username=leaderboard_user;Password=d5525endLbu!;Database=swtor-parse";
            var encryptedOutput = Crypto.EncryptStringAES(input, secret);
            var output = Crypto.DecryptStringAES(encryptedOutput, secret);

        }
    }
}
