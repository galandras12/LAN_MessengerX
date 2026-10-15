using System.Security.Cryptography;
using LanMessengerX.Core;
using Xunit;

namespace LanMessengerX.Tests;

internal static class Vectors
{
    public static string Dir
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Protocol", "testvectors"))) d = d.Parent;
            return Path.Combine(d?.FullName ?? throw new DirectoryNotFoundException("Protocol/testvectors not found"), "Protocol", "testvectors");
        }
    }

    public static string Text(string name) => File.ReadAllText(Path.Combine(Dir, name));
    public static byte[] Hex(string name) => Convert.FromHexString(Text(name).Trim());
    public static byte[] Range(int from, int to) => Enumerable.Range(from, to - from).Select(i => (byte)i).ToArray();
}

public class XmlTests
{
    [Fact]
    public void RoundTripKeepsOrderAndText()
    {
        var m = new XmlMessage();
        m.AddHeader(Xn.From, "A1B2").AddHeader(Xn.To, "C3D4").AddHeader(Xn.MessageId, "7").AddHeader(Xn.Type, "message");
        const string text = "hello <b>&amp; \"quotes\" 'x' > y\nnew line  two spaces ";
        m.AddData(Xn.Message, text);
        var parsed = XmlMessage.Parse(m.ToXml())!;
        Assert.Equal("A1B2", parsed.Header(Xn.From));
        Assert.Equal("7", parsed.Header(Xn.MessageId));
        Assert.Equal(text, parsed.Data(Xn.Message));
    }

    [Fact]
    public void ParsesWhatQtDomWrites()
    {
        // QDomDocument::toString() indents with newlines and leaves '>' unescaped in text
        var qt = "<lmcmessage>\n <head>\n  <from>AA</from>\n  <type>message</type>\n </head>\n <body>\n  <message>a &lt;&amp;> b</message>\n  <note/>\n </body>\n</lmcmessage>\n";
        var m = XmlMessage.Parse(qt)!;
        Assert.Equal("AA", m.Header(Xn.From));
        Assert.Equal("a <&> b", m.Data(Xn.Message));
        Assert.Equal("", m.Data(Xn.Note));
        Assert.True(m.DataExists(Xn.Note));
    }

    [Theory]
    [InlineData("<other><head/><body/></other>")]
    [InlineData("not xml")]
    [InlineData("<lmcmessage><head></body></lmcmessage>")]
    [InlineData("<!DOCTYPE x [<!ENTITY a \"b\">]><lmcmessage><head/><body/></lmcmessage>")]
    [InlineData("<lmcmessage><head/><body><m>&unknown;</m></body></lmcmessage>")]
    public void RejectsForeignAndHostileDocuments(string text) => Assert.Null(XmlMessage.Parse(text));

    [Fact]
    public void IllegalXmlCharactersAreDropped()
    {
        var m = new XmlMessage().AddData(Xn.Message, "a\u0000b\u0008c\ud83d\ude00d");
        Assert.Equal("abc\ud83d\ude00d", XmlMessage.Parse(m.ToXml())!.Data(Xn.Message));
    }

    [Fact]
    public void CarriageReturnsSurvive()
    {
        var m = new XmlMessage().AddData(Xn.Message, "a\r\nb");
        Assert.Equal("a\r\nb", XmlMessage.Parse(m.ToXml())!.Data(Xn.Message));
    }

    [Fact]
    public void LookupIsFirstMatchLikeQDom()
    {
        var m = XmlMessage.Parse("<lmcmessage><head><x>1</x></head><body><v>first</v><v>second</v></body></lmcmessage>")!;
        Assert.Equal("first", m.Data("v"));
    }
}

public class CryptoTests
{
    [Fact]
    public void EvpBytesToKeyMatchesOpenSsl()
    {
        var expected = Vectors.Text("evp-bytes-to-key.hex").Trim();
        var got = LmxCrypto.EvpBytesToKey(Vectors.Range(0, 32), 5, 32, 16);
        Assert.Equal(expected, Convert.ToHexString(got).ToLowerInvariant());
    }

    [Fact]
    public void Aes256CbcMatchesOpenSsl()
    {
        var cipher = new SessionCipher(Vectors.Range(0, 32).Concat(Vectors.Range(0xa0, 0xb0)).ToArray());
        var expected = Vectors.Hex("aes256cbc-ciphertext.hex");
        var plain = System.Text.Encoding.UTF8.GetBytes("LAN Messenger X interop éő");
        Assert.Equal(expected, cipher.Encrypt(plain));
        Assert.Equal(plain, cipher.Decrypt(expected));
    }

    [Fact]
    public void DecryptWithWrongKeyFailsCleanly()
    {
        var a = new SessionCipher(LmxCrypto.NewSessionMaterial());
        var b = new SessionCipher(LmxCrypto.NewSessionMaterial());
        Assert.Null(b.Decrypt(a.Encrypt(new byte[40])));
    }

    [Fact]
    public void RsaOaepSha1DecryptsOpenSslCiphertext()
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(Vectors.Text("rsa-private-pkcs8.pem"));
        Assert.Equal(Vectors.Range(0, 48), LmxCrypto.RsaDecrypt(rsa, Vectors.Hex("oaep-sha1-ciphertext.hex")));
    }

    [Fact]
    public void Pkcs1PublicKeyPemIsReadAndWrittenBack()
    {
        var pem = System.Text.Encoding.ASCII.GetBytes(Vectors.Text("rsa-public-pkcs1.pem"));
        using var key = LmxCrypto.PublicKeyFromPem(pem);
        Assert.Equal(2048, key.KeySize);
        using var keys = new LmxCrypto.RsaKeys(key);
        Assert.Equal(Vectors.Text("rsa-public-pkcs1.pem").Trim().Replace("\r\n", "\n"), System.Text.Encoding.ASCII.GetString(keys.PublicPem).Trim());
    }

    [Fact]
    public void HandshakeRoundTrip()
    {
        using var server = LmxCrypto.GenerateRsa(1024); // the original uses 1024 bit keys
        using var pub = LmxCrypto.PublicKeyFromPem(server.PublicPem);
        var material = LmxCrypto.NewSessionMaterial();
        Assert.Equal(48, material.Length);
        Assert.Equal(material, LmxCrypto.RsaDecrypt(server.Rsa, LmxCrypto.RsaEncrypt(pub, material)));
        var pem = System.Text.Encoding.ASCII.GetString(server.PublicPem);
        Assert.StartsWith("-----BEGIN RSA PUBLIC KEY-----\n", pem);
        Assert.EndsWith("-----END RSA PUBLIC KEY-----\n", pem);
    }
}

public class ProtocolTests
{
    [Fact]
    public void VersionComparisonMatchesTheOriginal()
    {
        Assert.True(Proto.CompareVersions("2.0.12", "1.2.30") > 0);
        Assert.Equal(0, Proto.CompareVersions("1.2.10", "1.2.10"));
        Assert.True(Proto.CompareVersions("1.2.9", "1.2.10") < 0);
        Assert.True(Proto.CompareVersions(Proto.Version, "1.2.30") > 0);
    }

    [Fact]
    public void UserIdIsMacPlusLogonCappedForOldReceivers()
    {
        Assert.Equal("A1B2C3D4E5F6anna", Messenger.BuildUserId("a1:b2:c3:d4:e5:f6", "anna"));
        var id = Messenger.BuildUserId("AABBCCDDEEFF", "a-very-long-logon-name-indeed");
        Assert.Equal(Proto.MaxUserIdLength, id.Length);
        Assert.DoesNotContain(':', id);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Windows\\evil.exe", "evil.exe")]
    [InlineData("...", "file")]
    [InlineData("a<b>c|d?.txt", "abcd.txt")]
    public void FileNamesCannotEscapeTheDownloadFolder(string input, string expected) =>
        Assert.Equal(expected, Messenger.SanitizeFileName(input));

    [Fact]
    public void WireNamesAreTheOriginalOnes()
    {
        Assert.Equal("userdata", MessageType.UserData.Name());
        Assert.Equal("groupmessage", MessageType.GroupMessage.Name());
        Assert.Equal("PUBKEY", DatagramType.PublicKey.Tag());
        Assert.Equal(DatagramType.Handshake, DatagramTags.FromTag("HNDSHK"));
        Assert.Equal(new[] { "chat", "busy", "dnd", "brb", "away", "gone" }, Proto.StatusCodes);
        Assert.Equal(MessageType.Folder, Wire.ParseMessageType("folder"));
        Assert.Equal(FileOp.Accept, Wire.ParseFileOp("accept"));
        Assert.Null(Wire.ParseFileOp("bogus"));
    }
}

public class DiscoveryTests
{
    private static LanInterface Lan(string ip, int prefix) => new("eth0", "test", System.Net.IPAddress.Parse(ip), prefix, 1, "");

    [Fact]
    public void SubnetHostsCoverTheWholeSlash24WithoutNetworkAndBroadcast()
    {
        var hosts = NetUtil.SubnetHosts(Lan("192.168.1.77", 24)).ToList();
        Assert.Equal(254, hosts.Count);
        Assert.Equal("192.168.1.1", hosts[0]);
        Assert.Equal("192.168.1.254", hosts[^1]);
    }

    [Fact]
    public void SubnetHostsOfAHugeNetworkAreCutDownToTheSlash24()
    {
        var hosts = NetUtil.SubnetHosts(Lan("10.20.30.40", 8)).ToList();
        Assert.Equal(254, hosts.Count);
        Assert.All(hosts, h => Assert.StartsWith("10.20.30.", h));
    }

    [Fact]
    public void SubnetHostsOfASlash22AreAllScanned()
    {
        var hosts = NetUtil.SubnetHosts(Lan("10.0.5.9", 22)).ToList();
        Assert.Equal(1022, hosts.Count);
        Assert.Equal("10.0.4.1", hosts[0]);
        Assert.Equal("10.0.7.254", hosts[^1]);
    }
}
