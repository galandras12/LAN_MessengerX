package com.lanmessengerx.core

import java.io.ByteArrayOutputStream
import java.math.BigInteger
import java.security.KeyFactory
import java.security.KeyPair
import java.security.KeyPairGenerator
import java.security.MessageDigest
import java.security.PrivateKey
import java.security.PublicKey
import java.security.SecureRandom
import java.security.spec.MGF1ParameterSpec
import java.security.spec.RSAPublicKeySpec
import java.security.spec.X509EncodedKeySpec
import java.security.interfaces.RSAPublicKey
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.OAEPParameterSpec
import javax.crypto.spec.PSource
import javax.crypto.spec.SecretKeySpec

/**
 * The key exchange of the original client, byte for byte:
 *
 *  1. The accepting side sends its RSA public key as a PEM "RSA PUBLIC KEY" (PKCS#1).
 *  2. The connecting side draws 32 random bytes, stretches them with
 *     EVP_BytesToKey(AES-256-CBC, SHA-1, no salt, 5 rounds) into a 32 byte key
 *     and 16 byte IV, and sends key||iv encrypted with RSA/OAEP (SHA-1) as HNDSHK.
 *  3. Every following message is AES-256-CBC/PKCS7 with that fixed key and IV.
 */
object LmxCrypto {
    private val random = SecureRandom()
    const val AES_KEY_LEN = 32
    const val AES_IV_LEN = 16

    class RsaKeys(val keyPair: KeyPair, val publicPem: ByteArray) {
        val privateKey: PrivateKey get() = keyPair.private
    }

    fun generateRsa(bits: Int = 2048): RsaKeys {
        val gen = KeyPairGenerator.getInstance("RSA")
        gen.initialize(bits, random)
        val kp = gen.generateKeyPair()
        return RsaKeys(kp, publicKeyToPem(kp.public as RSAPublicKey))
    }

    /** EVP_BytesToKey with SHA-1, no salt. Returns [keyLen + ivLen] bytes. */
    fun evpBytesToKey(data: ByteArray, rounds: Int, keyLen: Int, ivLen: Int): ByteArray {
        val md = MessageDigest.getInstance("SHA-1")
        val out = ByteArrayOutputStream()
        var prev = ByteArray(0)
        while (out.size() < keyLen + ivLen) {
            md.reset()
            md.update(prev)
            md.update(data)
            var d = md.digest()
            for (r in 1 until rounds) {
                md.reset()
                d = md.digest(d)
            }
            out.write(d)
            prev = d
        }
        return out.toByteArray().copyOf(keyLen + ivLen)
    }

    /** Fresh random session material (key||iv) as the connecting side generates it. */
    fun newSessionMaterial(): ByteArray {
        val seed = ByteArray(32)
        random.nextBytes(seed)
        return evpBytesToKey(seed, 5, AES_KEY_LEN, AES_IV_LEN)
    }

    private fun oaep() = OAEPParameterSpec("SHA-1", "MGF1", MGF1ParameterSpec.SHA1, PSource.PSpecified.DEFAULT)

    fun rsaEncrypt(pub: PublicKey, plain: ByteArray): ByteArray {
        val c = Cipher.getInstance("RSA/ECB/OAEPPadding")
        c.init(Cipher.ENCRYPT_MODE, pub, oaep())
        return c.doFinal(plain)
    }

    fun rsaDecrypt(priv: PrivateKey, cipher: ByteArray): ByteArray {
        val c = Cipher.getInstance("RSA/ECB/OAEPPadding")
        c.init(Cipher.DECRYPT_MODE, priv, oaep())
        return c.doFinal(cipher)
    }

    // ---- PEM / PKCS#1 RSAPublicKey ----------------------------------------------------------

    fun publicKeyToPem(key: RSAPublicKey): ByteArray {
        val n = derInteger(key.modulus)
        val e = derInteger(key.publicExponent)
        val der = derSequence(n + e)
        val b64 = Base64.getEncoder().encodeToString(der)
        val sb = StringBuilder("-----BEGIN RSA PUBLIC KEY-----\n")
        var i = 0
        while (i < b64.length) {
            sb.append(b64, i, minOf(i + 64, b64.length)).append('\n')
            i += 64
        }
        sb.append("-----END RSA PUBLIC KEY-----\n")
        return sb.toString().toByteArray(Charsets.US_ASCII)
    }

    /** Accepts both "RSA PUBLIC KEY" (PKCS#1) and "PUBLIC KEY" (X.509 SPKI) PEM. */
    fun publicKeyFromPem(pem: ByteArray): PublicKey {
        val text = String(pem, Charsets.US_ASCII)
        val isPkcs1 = text.contains("BEGIN RSA PUBLIC KEY")
        val b64 = text.lineSequence().filter { !it.startsWith("-----") }.joinToString("").filter { !it.isWhitespace() }
        val der = Base64.getDecoder().decode(b64)
        val kf = KeyFactory.getInstance("RSA")
        if (!isPkcs1) return kf.generatePublic(X509EncodedKeySpec(der))
        val r = DerReader(der)
        val seq = r.readSequence()
        val n = seq.readInteger()
        val e = seq.readInteger()
        return kf.generatePublic(RSAPublicKeySpec(n, e))
    }

    private fun derLength(len: Int): ByteArray = when {
        len < 0x80 -> byteArrayOf(len.toByte())
        len < 0x100 -> byteArrayOf(0x81.toByte(), len.toByte())
        else -> byteArrayOf(0x82.toByte(), (len shr 8).toByte(), len.toByte())
    }

    private fun derInteger(v: BigInteger): ByteArray {
        val b = v.toByteArray()
        return byteArrayOf(0x02) + derLength(b.size) + b
    }

    private fun derSequence(content: ByteArray): ByteArray = byteArrayOf(0x30) + derLength(content.size) + content

    private class DerReader(private val d: ByteArray, private var pos: Int = 0, private val end: Int = d.size) {
        private fun readLen(): Int {
            val b = d[pos++].toInt() and 0xFF
            if (b < 0x80) return b
            val n = b and 0x7F
            require(n in 1..3) { "bad DER length" }
            var len = 0
            repeat(n) { len = (len shl 8) or (d[pos++].toInt() and 0xFF) }
            return len
        }

        fun readSequence(): DerReader {
            require(d[pos++].toInt() == 0x30) { "expected DER SEQUENCE" }
            val len = readLen()
            require(pos + len <= end) { "truncated DER" }
            return DerReader(d, pos, pos + len).also { pos += len }
        }

        fun readInteger(): BigInteger {
            require(d[pos++].toInt() == 0x02) { "expected DER INTEGER" }
            val len = readLen()
            require(pos + len <= end) { "truncated DER" }
            val v = BigInteger(1, d.copyOfRange(pos, pos + len))
            pos += len
            return v
        }
    }
}

/** AES-256-CBC with a fixed key and IV (the IV is reset for every message, as in the original). */
class SessionCipher(material: ByteArray) {
    private val key = SecretKeySpec(material.copyOfRange(0, LmxCrypto.AES_KEY_LEN), "AES")
    private val iv = IvParameterSpec(material.copyOfRange(LmxCrypto.AES_KEY_LEN, LmxCrypto.AES_KEY_LEN + LmxCrypto.AES_IV_LEN))

    init {
        require(material.size >= LmxCrypto.AES_KEY_LEN + LmxCrypto.AES_IV_LEN) { "session material too short" }
    }

    fun encrypt(plain: ByteArray): ByteArray {
        val c = Cipher.getInstance("AES/CBC/PKCS5Padding")
        c.init(Cipher.ENCRYPT_MODE, key, iv)
        return c.doFinal(plain)
    }

    /** Returns null if the data cannot be decrypted with this key. */
    fun decrypt(cipher: ByteArray): ByteArray? = try {
        val c = Cipher.getInstance("AES/CBC/PKCS5Padding")
        c.init(Cipher.DECRYPT_MODE, key, iv)
        c.doFinal(cipher)
    } catch (e: Exception) {
        null
    }
}
