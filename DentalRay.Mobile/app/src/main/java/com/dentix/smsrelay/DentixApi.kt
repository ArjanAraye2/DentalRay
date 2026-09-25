package com.dentix.smsrelay

import org.json.JSONArray
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

/**
 * فرستادن پیامک‌ها به سرور دنتیکس.
 *
 * آدرس سرور و کلید دستگاه از PairingStore می‌آید؛ سرور با همان کلید گوشی را
 * می‌شناسد. سرور خودش کار تشخیص بیمار را انجام می‌دهد، پس اینجا فقط متن و
 * فرستنده فرستاده می‌شود.
 */
object DentixApi {

    data class Result(val received: Int, val linked: Int, val pending: Int)

    fun send(server: String, token: String, messages: List<Outbox.Item>): Result {
        val payload = JSONObject().apply {
            put("deviceToken", token)
            val array = JSONArray()
            messages.forEach { item ->
                array.put(JSONObject().apply {
                    put("body", item.body)
                    if (item.sender != null) put("sender", item.sender)
                    put("receivedAt", isoUtc(item.receivedAt))
                })
            }
            put("messages", array)
        }

        val connection = URL("${server.trimEnd('/')}/api/inbox/sms").openConnection() as HttpURLConnection
        connection.requestMethod = "POST"
        connection.connectTimeout = 20_000
        connection.readTimeout = 60_000
        connection.doOutput = true
        connection.setRequestProperty("Content-Type", "application/json; charset=utf-8")
        connection.setRequestProperty("X-Dentix-Device", token)

        try {
            connection.outputStream.use { out ->
                out.write(payload.toString().toByteArray(Charsets.UTF_8))
            }

            val status = connection.responseCode
            val stream = if (status in 200..299) connection.inputStream else connection.errorStream
            val text = stream?.bufferedReader(Charsets.UTF_8)?.use { it.readText() } ?: ""

            if (status !in 200..299) throw IllegalStateException("سرور پاسخ نداد (کد $status)")
            val json = JSONObject(text)
            if (!json.optBoolean("success")) {
                throw IllegalStateException(json.optString("message", "ارسال انجام نشد."))
            }
            return Result(
                received = json.optInt("received"),
                linked = json.optInt("linked"),
                pending = json.optInt("pending")
            )
        } finally {
            connection.disconnect()
        }
    }

    private fun isoUtc(millis: Long): String {
        val format = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US)
        format.timeZone = TimeZone.getTimeZone("UTC")
        return format.format(Date(millis))
    }
}
