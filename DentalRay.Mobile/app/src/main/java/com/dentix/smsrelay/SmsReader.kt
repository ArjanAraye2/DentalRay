package com.dentix.smsrelay

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.net.Uri
import androidx.core.content.ContextCompat

/**
 * خواندن پیامک‌های تازه از گوشی.
 *
 * فقط پیامک‌هایی برداشته می‌شوند که
 *   - بعد از لحظهٔ جفت‌شدن آمده باشند (پیامک‌های قدیمی کاری ندارند)، و
 *   - حاوی لینک http باشند؛ چون فقط لینک رادیولوژی برای مطب اهمیت دارد.
 */
object SmsReader {

    data class Message(val sender: String?, val body: String, val receivedAt: Long)

    fun hasPermission(context: Context): Boolean =
        ContextCompat.checkSelfPermission(context, Manifest.permission.READ_SMS) ==
            PackageManager.PERMISSION_GRANTED

    fun readNew(context: Context, sinceMillis: Long): List<Message> {
        if (!hasPermission(context)) return emptyList()

        val uri = Uri.parse("content://sms/inbox?limit=80")
        val projection = arrayOf("_id", "address", "body", "date")
        val result = mutableListOf<Message>()

        context.contentResolver.query(uri, projection, null, null, "date DESC")?.use { cursor ->
            val bodyIndex = cursor.getColumnIndex("body")
            val addressIndex = cursor.getColumnIndex("address")
            val dateIndex = cursor.getColumnIndex("date")
            while (cursor.moveToNext()) {
                val date = if (dateIndex >= 0) cursor.getLong(dateIndex) else 0L
                if (date <= sinceMillis) continue
                val body = if (bodyIndex >= 0) cursor.getString(bodyIndex) ?: "" else ""
                if (!body.contains("http://", true) && !body.contains("https://", true)) continue
                val sender = if (addressIndex >= 0) cursor.getString(addressIndex) else null
                result.add(Message(sender = sender, body = body, receivedAt = date))
            }
        }
        return result
    }
}
