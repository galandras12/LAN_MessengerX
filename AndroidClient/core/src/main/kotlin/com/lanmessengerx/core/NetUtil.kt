package com.lanmessengerx.core

import java.net.Inet4Address
import java.net.InetAddress
import java.net.NetworkInterface

/** The LAN interface the messenger is bound to. */
data class LanInterface(
    val name: String,
    val iface: NetworkInterface,
    val address: Inet4Address,
    val prefixLength: Int,
) {
    val ip: String get() = address.hostAddress ?: ""

    /** Subnet directed broadcast address (host bits all ones). */
    val broadcast: Inet4Address
        get() {
            val a = address.address
            val mask = if (prefixLength <= 0) 0 else (-1 shl (32 - prefixLength.coerceAtMost(32)))
            val ip = ((a[0].toInt() and 0xFF) shl 24) or ((a[1].toInt() and 0xFF) shl 16) or
                ((a[2].toInt() and 0xFF) shl 8) or (a[3].toInt() and 0xFF)
            val b = ip or mask.inv()
            val bytes = byteArrayOf((b ushr 24).toByte(), (b ushr 16).toByte(), (b ushr 8).toByte(), b.toByte())
            return InetAddress.getByAddress(bytes) as Inet4Address
        }

    /** Same interface, same address? Used to detect that the network changed. */
    fun sameAs(o: LanInterface?) = o != null && o.name == name && o.address == address
}

object NetUtil {
    private val SKIP_PREFIXES = listOf(
        "lo", "tun", "tap", "ppp", "rmnet", "ccmni", "dummy", "docker", "veth", "br-", "virbr", "vmnet",
        "vboxnet", "p2p", "sit", "ip6", "wg", "tailscale", "zt", "utun", "awdl", "llw", "ap",
    )
    private val PREFERRED_PREFIXES = listOf("wlan", "wlp", "wl", "eth", "enp", "eno", "ens", "en", "usb")

    /**
     * Picks the interface used for LAN discovery: an up, non-loopback, multicast capable
     * interface with an IPv4 address; Wi-Fi/Ethernet preferred over everything else and
     * private (RFC1918) addresses preferred over public ones. Returns null if there is none.
     */
    fun findLanInterface(preferredName: String? = null): LanInterface? {
        val candidates = ArrayList<Pair<Int, LanInterface>>()
        val all = try {
            NetworkInterface.getNetworkInterfaces()?.toList() ?: emptyList()
        } catch (e: Exception) {
            emptyList()
        }
        for (ni in all) {
            try {
                if (!ni.isUp || ni.isLoopback || ni.isPointToPoint) continue
                val lname = ni.name.lowercase()
                var score = 0
                if (preferredName != null && ni.name == preferredName) score += 1000
                if (SKIP_PREFIXES.any { lname.startsWith(it) }) score -= 100
                if (PREFERRED_PREFIXES.any { lname.startsWith(it) }) score += 20
                if (!ni.supportsMulticast()) score -= 10
                for (ia in ni.interfaceAddresses) {
                    val addr = ia.address
                    if (addr !is Inet4Address || addr.isLoopbackAddress || addr.isLinkLocalAddress) continue
                    var s = score
                    if (addr.isSiteLocalAddress) s += 30
                    candidates.add(s to LanInterface(ni.name, ni, addr, ia.networkPrefixLength.toInt()))
                }
            } catch (_: Exception) {
            }
        }
        return candidates.maxByOrNull { it.first }?.second
    }

    fun hex(b: ByteArray): String = b.joinToString("") { "%02X".format(it) }
}
