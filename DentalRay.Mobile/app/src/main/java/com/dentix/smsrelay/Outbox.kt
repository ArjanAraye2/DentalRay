package com.dentix.smsrelay

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.util.UUID

/**
 * صف پیامک‌هایی که هنوز به مطب نرسیده‌اند.
 *
 * اگر گوشی اینترنت نداشته باشد پیامک اینجا می‌ماند و هر بار که وصل شد دوباره
 * فرستاده می‌شود؛ یعنی قطعی شبکه باعث از دست رفتن هیچ پیامکی نمی‌شود.
 * فایل متنی ساده است تا بدون کتابخانهٔ پایگاه‌داده کار کند.
 */
class Outbox(context: Context) {

    private val file = File(context.filesDir, "outbox.json")

    data class Item(
        val id: String,
        val sender: String?,
        val body: String,
        val receivedAt: Long,
        val tries: Int
    ) {
        fun toJson(): JSONObject = JSONObject().apply {
            put("id", id)
            if (sender != null) put("sender", sender) else put("sender", JSONObject.NULL)
            put("body", body)
            put("receivedAt", receivedAt)
            put("tries", tries)
        }
    }

    @Synchronized
    private fun readAll(): MutableList<Item> {
        if (!file.exists()) return mutableListOf()
        return try {
            val array = JSONArray(file.readText())
            (0 until array.length()).map { i ->
                val o = array.getJSONObject(i)
                Item(
                    id = o.optString("id", UUID.randomUUID().toString()),
                    sender = if (o.isNull("sender")) null else o.optString("sender"),
                    body = o.optString("body"),
                    receivedAt = o.optLong("receivedAt"),
                    tries = o.optInt("tries")
                )
            }.toMutableList()
        } catch (_: Exception) {
            mutableListOf()
        }
    }

    @Synchronized
    private fun writeAll(items: List<Item>) {
        val array = JSONArray()
        items.forEach { array.put(it.toJson()) }
        file.parentFile?.mkdirs()
        file.writeText(array.toString())
    }

    /** افزودن با حذف پیامک‌های تکراری (همان متن و همان فرستنده). */
    @Synchronized
    fun add(sender: String?, body: String, receivedAt: Long): Item {
        val items = readAll()
        val duplicate = items.any { it.body == body && it.sender == sender }
        val item = Item(UUID.randomUUID().toString(), sender, body, receivedAt, 0)
        if (!duplicate) {
            items.add(item)
            writeAll(items)
        }
        return item
    }

    @Synchronized
    fun all(): List<Item> = readAll()

    @Synchronized
    fun markSent(ids: Set<String>) {
        val items = readAll().filterNot { it.id in ids }
        writeAll(items)
    }

    @Synchronized
    fun markFailed(ids: Set<String>) {
        val items = readAll().map { if (it.id in ids) it.copy(tries = it.tries + 1) else it }
        writeAll(items)
    }

    @Synchronized
    fun size(): Int = readAll().size

    @Synchronized
    fun clear() = writeAll(emptyList())
}
