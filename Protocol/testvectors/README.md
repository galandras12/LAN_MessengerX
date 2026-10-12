# Crypto test vectors

Produced with the OpenSSL command line (3.0) and used by the Kotlin and C# unit tests to prove that
both implementations agree with OpenSSL, which is what the original LAN Messenger uses.

| File | What it is |
|---|---|
| `rsa-public-pkcs1.pem` | RSA-2048 public key as `-----BEGIN RSA PUBLIC KEY-----` (PKCS#1), the format the original sends in the `PUBKEY` frame |
| `rsa-private-pkcs8.pem` | the matching private key |
| `oaep-sha1-ciphertext.hex` | `bytes(0..47)` encrypted with that key using `openssl pkeyutl -encrypt -pkeyopt rsa_padding_mode:oaep -pkeyopt rsa_oaep_md:sha1 -pkeyopt rsa_mgf1_md:sha1` (the `HNDSHK` payload layout: 32 byte key + 16 byte IV) |
| `evp-bytes-to-key.hex` | `EVP_BytesToKey(SHA-1, no salt, 5 rounds)` of `bytes(0..31)`: 32 byte key followed by 16 byte IV |
| `aes256cbc-ciphertext.hex` | `"LAN Messenger X interop éő"` (UTF-8) encrypted with `openssl enc -aes-256-cbc`, key `bytes(0..31)`, IV `bytes(0xa0..0xaf)` |

The key is for testing only.
