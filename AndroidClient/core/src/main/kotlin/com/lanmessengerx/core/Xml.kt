package com.lanmessengerx.core

/**
 * Minimal, hardened XML reader/writer for the LAN Messenger message envelope:
 *
 *   <lmcmessage><head><from/>...</head><body><message/>...</body></lmcmessage>
 *
 * The original client uses Qt's QDom and looks values up with
 * elementsByTagName(), i.e. "first descendant with that tag name, text() of it".
 * [XmlMessage] reproduces exactly those lookup rules. DOCTYPE/entity
 * declarations are rejected outright (untrusted LAN input).
 */
class XmlElement(val name: String) {
    /** Child content in document order: either String (text) or XmlElement. */
    val content = ArrayList<Any>()

    /** Concatenated text of this element and all descendants (QDomElement::text()). */
    fun text(): String {
        val sb = StringBuilder()
        appendText(sb)
        return sb.toString()
    }

    private fun appendText(sb: StringBuilder) {
        for (c in content) {
            if (c is String) sb.append(c) else (c as XmlElement).appendText(sb)
        }
    }

    /** All descendant elements in document order. */
    fun descendants(): List<XmlElement> {
        val out = ArrayList<XmlElement>()
        collect(out)
        return out
    }

    private fun collect(out: MutableList<XmlElement>) {
        for (c in content) if (c is XmlElement) {
            out.add(c)
            c.collect(out)
        }
    }

    fun firstDescendant(tag: String): XmlElement? = descendants().firstOrNull { it.name == tag }
}

object XmlParser {
    private const val MAX_DEPTH = 64
    private const val MAX_NODES = 20000

    /** Returns the root element, or null if the text is not acceptable XML. */
    fun parse(src: String): XmlElement? {
        var i = 0
        val n = src.length
        val stack = ArrayList<XmlElement>()
        var root: XmlElement? = null
        var nodes = 0
        val text = StringBuilder()

        fun flushText() {
            if (text.isNotEmpty()) {
                stack.lastOrNull()?.content?.add(text.toString())
                text.setLength(0)
            }
        }

        while (i < n) {
            val ch = src[i]
            if (ch != '<') {
                if (ch == '&') {
                    val semi = src.indexOf(';', i)
                    if (semi < 0 || semi - i > 12) return null
                    val decoded = decodeEntity(src.substring(i + 1, semi)) ?: return null
                    text.append(decoded)
                    i = semi + 1
                } else {
                    // Outside the root element only whitespace is legal.
                    if (stack.isEmpty() && !ch.isWhitespace() && ch != '﻿') return null
                    text.append(ch)
                    i++
                }
                continue
            }
            if (src.startsWith("<!--", i)) {
                val end = src.indexOf("-->", i + 4)
                if (end < 0) return null
                i = end + 3
            } else if (src.startsWith("<![CDATA[", i)) {
                val end = src.indexOf("]]>", i + 9)
                if (end < 0 || stack.isEmpty()) return null
                text.append(src, i + 9, end)
                i = end + 3
            } else if (src.startsWith("<?", i)) {
                val end = src.indexOf("?>", i + 2)
                if (end < 0) return null
                i = end + 2
            } else if (src.startsWith("<!", i)) {
                return null // DOCTYPE and friends
            } else if (src.startsWith("</", i)) {
                val end = src.indexOf('>', i + 2)
                if (end < 0) return null
                val name = src.substring(i + 2, end).trim()
                flushText()
                val top = stack.removeLastOrNull() ?: return null
                if (top.name != name) return null
                i = end + 1
                if (stack.isEmpty()) {
                    // Only whitespace/comments may follow the root.
                    for (k in i until n) {
                        if (!src[k].isWhitespace() && src[k] != '<') return null
                    }
                    return root
                }
            } else {
                // start tag
                var j = i + 1
                val nameStart = j
                while (j < n && !src[j].isWhitespace() && src[j] != '>' && src[j] != '/') j++
                if (j >= n || j == nameStart) return null
                val name = src.substring(nameStart, j)
                // skip attributes, honouring quotes
                var selfClosing = false
                while (j < n) {
                    val c = src[j]
                    if (c == '"' || c == '\'') {
                        val q = src.indexOf(c, j + 1)
                        if (q < 0) return null
                        j = q + 1
                    } else if (c == '/' && j + 1 < n && src[j + 1] == '>') {
                        selfClosing = true
                        j += 2
                        break
                    } else if (c == '>') {
                        j++
                        break
                    } else {
                        j++
                    }
                }
                if (j > n) return null
                if (++nodes > MAX_NODES || stack.size >= MAX_DEPTH) return null
                flushText()
                val el = XmlElement(name)
                if (stack.isEmpty()) {
                    if (root != null) return null
                    root = el
                } else {
                    stack.last().content.add(el)
                }
                if (!selfClosing) stack.add(el)
                else if (stack.isEmpty()) return root
                i = j
            }
        }
        return null // unterminated document
    }

    private fun decodeEntity(e: String): String? = when {
        e == "amp" -> "&"
        e == "lt" -> "<"
        e == "gt" -> ">"
        e == "quot" -> "\""
        e == "apos" -> "'"
        e.startsWith("#x") || e.startsWith("#X") -> codePoint(e.substring(2).toIntOrNull(16))
        e.startsWith("#") -> codePoint(e.substring(1).toIntOrNull(10))
        else -> null
    }

    private fun codePoint(cp: Int?): String? {
        if (cp == null || cp < 0 || cp > 0x10FFFF || cp in 0xD800..0xDFFF) return null
        return String(Character.toChars(cp))
    }
}

object XmlText {
    /** Drops characters that are illegal in XML 1.0 (they would make Qt peers reject the whole message). */
    fun sanitize(s: String): String {
        var needs = false
        for (ch in s) if (!legal(ch)) { needs = true; break }
        if (!needs) return s
        val sb = StringBuilder(s.length)
        var i = 0
        while (i < s.length) {
            val ch = s[i]
            if (Character.isHighSurrogate(ch) && i + 1 < s.length && Character.isLowSurrogate(s[i + 1])) {
                sb.append(ch).append(s[i + 1])
                i += 2
                continue
            }
            if (legal(ch) && !Character.isSurrogate(ch)) sb.append(ch)
            i++
        }
        return sb.toString()
    }

    private fun legal(ch: Char): Boolean =
        ch == '\t' || ch == '\n' || ch == '\r' || (ch in ' '..'퟿') || (ch in ''..'�') ||
            Character.isSurrogate(ch)

    fun escape(s: String): String {
        val sb = StringBuilder(s.length + 16)
        for (ch in sanitize(s)) {
            when (ch) {
                '&' -> sb.append("&amp;")
                '<' -> sb.append("&lt;")
                '>' -> sb.append("&gt;")
                '\r' -> sb.append("&#13;")
                else -> sb.append(ch)
            }
        }
        return sb.toString()
    }
}

/** A parsed or to-be-sent protocol message: ordered header and body name/value pairs. */
class XmlMessage {
    val head = ArrayList<Pair<String, String>>()
    val body = ArrayList<Pair<String, String>>()

    fun addHeader(name: String, value: String) = apply { head.add(name to value) }
    fun addData(name: String, value: String) = apply { body.add(name to value) }
    fun header(name: String): String = head.firstOrNull { it.first == name }?.second ?: ""
    fun data(name: String): String = body.firstOrNull { it.first == name }?.second ?: ""
    fun dataExists(name: String) = body.any { it.first == name }
    fun removeHeader(name: String) {
        val idx = head.indexOfFirst { it.first == name }
        if (idx >= 0) head.removeAt(idx)
    }

    fun removeData(name: String) {
        val idx = body.indexOfFirst { it.first == name }
        if (idx >= 0) body.removeAt(idx)
    }

    /** Replaces (or adds) a body value. */
    fun setData(name: String, value: String) = apply {
        removeData(name)
        addData(name, value)
    }

    fun copy(): XmlMessage = XmlMessage().also { it.head.addAll(head); it.body.addAll(body) }

    fun toXml(): String {
        val sb = StringBuilder(256)
        sb.append('<').append(Xn.ROOT).append('>')
        sb.append("<head>")
        for ((k, v) in head) sb.append('<').append(k).append('>').append(XmlText.escape(v)).append("</").append(k).append('>')
        sb.append("</head><body>")
        for ((k, v) in body) sb.append('<').append(k).append('>').append(XmlText.escape(v)).append("</").append(k).append('>')
        sb.append("</body></").append(Xn.ROOT).append('>')
        return sb.toString()
    }

    override fun toString() = toXml()

    companion object {
        /** Null if [text] is not a valid `lmcmessage` document. */
        fun parse(text: String): XmlMessage? {
            val root = XmlParser.parse(text) ?: return null
            if (root.name != Xn.ROOT) return null
            val msg = XmlMessage()
            root.firstDescendant(Xn.HEAD)?.descendants()?.forEach { msg.head.add(it.name to it.text()) }
            root.firstDescendant(Xn.BODY)?.descendants()?.forEach { msg.body.add(it.name to it.text()) }
            return msg
        }
    }
}
