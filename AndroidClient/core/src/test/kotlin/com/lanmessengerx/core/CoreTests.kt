package com.lanmessengerx.core

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.security.KeyFactory
import java.security.interfaces.RSAPublicKey
import java.security.spec.PKCS8EncodedKeySpec
import java.util.Base64

private fun vectors(name: String): File {
    val dir = System.getProperty("lmx.vectors") ?: "../../Protocol/testvectors"
    return File(dir, name)
}

private fun hex(s: String): ByteArray = s.trim().chunked(2).map { it.toInt(16).toByte() }.toByteArray()
private fun ByteArray.toHex(): String = joinToString("") { "%02x".format(it) }
private fun range(from: Int, to: Int) = ByteArray(to - from) { (from + it).toByte() }

class XmlTests {
    @Test
    fun roundTripKeepsOrderAndText() {
        val m = XmlMessage()
        m.addHeader(Xn.FROM, "A1B2").addHeader(Xn.TO, "C3D4").addHeader(Xn.MESSAGE_ID, "7").addHeader(Xn.TYPE, "message")
        m.addData(Xn.MESSAGE, "hello <b>&amp; \"quotes\" 'x' > y\nnew line  two spaces ")
        val parsed = XmlMessage.parse(m.toXml())!!
        assertEquals("A1B2", parsed.header(Xn.FROM))
        assertEquals("7", parsed.header(Xn.MESSAGE_ID))
        assertEquals("hello <b>&amp; \"quotes\" 'x' > y\nnew line  two spaces ", parsed.data(Xn.MESSAGE))
    }

    @Test
    fun parsesWhatQtDomWrites() {
        // QDomDocument::toString() indents with newlines and leaves '>' unescaped in text
        val qt = "<lmcmessage>\n <head>\n  <from>AA</from>\n  <type>message</type>\n </head>\n <body>\n  <message>a &lt;&amp;> b</message>\n  <note/>\n </body>\n</lmcmessage>\n"
        val m = XmlMessage.parse(qt)!!
        assertEquals("AA", m.header(Xn.FROM))
        assertEquals("a <&> b", m.data(Xn.MESSAGE))
        assertEquals("", m.data(Xn.NOTE))
        assertTrue(m.dataExists(Xn.NOTE))
    }

    @Test
    fun rejectsForeignAndHostileDocuments() {
        assertNull(XmlMessage.parse("<other><head/><body/></other>"))
        assertNull(XmlMessage.parse("not xml"))
        assertNull(XmlMessage.parse("<lmcmessage><head></body></lmcmessage>"))
        assertNull(XmlMessage.parse("<!DOCTYPE x [<!ENTITY a \"b\">]><lmcmessage><head/><body/></lmcmessage>"))
        assertNull(XmlMessage.parse("<lmcmessage><head/><body><m>&unknown;</m></body></lmcmessage>"))
        assertNull(XmlMessage.parse("<a>".repeat(100) + "</a>".repeat(100)))
    }

    @Test
    fun illegalXmlCharactersAreDropped() {
        val m = XmlMessage().addData(Xn.MESSAGE, "a\u0000b\u0008c\ud83d\ude00d")
        val parsed = XmlMessage.parse(m.toXml())!!
        assertEquals("abc\ud83d\ude00d", parsed.data(Xn.MESSAGE))
    }

    @Test
    fun carriageReturnsSurvive() {
        val m = XmlMessage().addData(Xn.MESSAGE, "a\r\nb")
        assertEquals("a\r\nb", XmlMessage.parse(m.toXml())!!.data(Xn.MESSAGE))
    }

    @Test
    fun lookupIsFirstMatchLikeQDom() {
        val m = XmlMessage.parse("<lmcmessage><head><x>1</x></head><body><v>first</v><v>second</v></body></lmcmessage>")!!
        assertEquals("first", m.data("v"))
    }
}

class CryptoTests {
    @Test
    fun evpBytesToKeyMatchesOpenSsl() {
        val expected = vectors("evp-bytes-to-key.hex").readText().trim()
        val got = LmxCrypto.evpBytesToKey(range(0, 32), 5, 32, 16)
        assertEquals(expected, got.toHex())
    }

    @Test
    fun aes256CbcMatchesOpenSsl() {
        val cipher = SessionCipher(range(0, 32) + range(0xa0, 0xb0))
        val expected = hex(vectors("aes256cbc-ciphertext.hex").readText())
        val plain = "LAN Messenger X interop éő".toByteArray(Charsets.UTF_8)
        assertArrayEquals(expected, cipher.encrypt(plain))
        assertArrayEquals(plain, cipher.decrypt(expected))
    }

    @Test
    fun decryptWithWrongKeyFailsCleanly() {
        val a = SessionCipher(LmxCrypto.newSessionMaterial())
        val b = SessionCipher(LmxCrypto.newSessionMaterial())
        assertNull(b.decrypt(a.encrypt("secret secret secret".toByteArray())))
    }

    private fun privateKey() = KeyFactory.getInstance("RSA").generatePrivate(
        PKCS8EncodedKeySpec(Base64.getDecoder().decode(
            vectors("rsa-private-pkcs8.pem").readLines().filter { !it.startsWith("-----") }.joinToString(""),
        )),
    )

    @Test
    fun rsaOaepSha1DecryptsOpenSslCiphertext() {
        val plain = LmxCrypto.rsaDecrypt(privateKey(), hex(vectors("oaep-sha1-ciphertext.hex").readText()))
        assertArrayEquals(range(0, 48), plain)
    }

    @Test
    fun pkcs1PublicKeyPemIsReadAndWrittenBack() {
        val pemBytes = vectors("rsa-public-pkcs1.pem").readBytes()
        val key = LmxCrypto.publicKeyFromPem(pemBytes) as RSAPublicKey
        assertEquals(2048, key.modulus.bitLength())
        assertEquals(65537L, key.publicExponent.toLong())
        assertEquals(String(pemBytes).trim(), String(LmxCrypto.publicKeyToPem(key)).trim())
    }

    @Test
    fun handshakeRoundTrip() {
        val server = LmxCrypto.generateRsa(1024) // the original uses 1024 bit keys
        val pub = LmxCrypto.publicKeyFromPem(server.publicPem)
        val material = LmxCrypto.newSessionMaterial()
        assertEquals(48, material.size)
        val back = LmxCrypto.rsaDecrypt(server.privateKey, LmxCrypto.rsaEncrypt(pub, material))
        assertArrayEquals(material, back)
        assertTrue(String(server.publicPem).startsWith("-----BEGIN RSA PUBLIC KEY-----\n"))
        assertTrue(String(server.publicPem).endsWith("-----END RSA PUBLIC KEY-----\n"))
    }
}

class ProtocolTests {
    @Test
    fun versionComparisonMatchesTheOriginal() {
        assertTrue(Proto.compareVersions("2.0.12", "1.2.30") > 0)
        assertTrue(Proto.compareVersions("1.2.10", "1.2.10") == 0)
        assertTrue(Proto.compareVersions("1.2.9", "1.2.10") < 0)
        assertTrue(Proto.compareVersions(Proto.VERSION, "1.2.30") > 0)
    }

    @Test
    fun userIdIsMacPlusLogonCappedForOldReceivers() {
        assertEquals("A1B2C3D4E5F6anna", Messenger.buildUserId("a1:b2:c3:d4:e5:f6", "anna"))
        val long = Messenger.buildUserId("AABBCCDDEEFF", "a-very-long-logon-name-indeed")
        assertEquals(Proto.MAX_USER_ID_LENGTH, long.length)
        assertFalse(long.contains(':'))
    }

    @Test
    fun fileNamesCannotEscapeTheDownloadFolder() {
        assertEquals("passwd", Messenger.sanitizeFileName("../../etc/passwd"))
        assertEquals("evil.exe", Messenger.sanitizeFileName("C:\\Windows\\evil.exe"))
        assertEquals("file", Messenger.sanitizeFileName("..."))
        assertNotNull(Messenger.sanitizeFileName("a<b>c|d?.txt"))
        assertEquals("abcd.txt", Messenger.sanitizeFileName("a<b>c|d?.txt"))
    }

    @Test
    fun wireNamesAreTheOriginalOnes() {
        assertEquals("userdata", MessageType.USER_DATA.wire)
        assertEquals("groupmessage", MessageType.GROUP_MESSAGE.wire)
        assertEquals("PUBKEY", DatagramType.PUBLIC_KEY.tag)
        assertEquals(DatagramType.HANDSHAKE, DatagramType.fromTag("HNDSHK"))
        assertEquals(listOf("chat", "busy", "dnd", "brb", "away", "gone"), Proto.STATUS_CODES)
    }
}
