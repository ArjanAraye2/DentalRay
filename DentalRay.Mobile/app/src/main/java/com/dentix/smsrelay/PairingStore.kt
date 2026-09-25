package com.dentix.smsrelay

import android.content.Context

/**
 * اطلاعات جفت‌شدن با سرور دنتیکس.
 *
 * پس از اسکن کیوآرکد، آدرس سرور و کلید دستگاه اینجا ذخیره می‌شود؛ فقط همین
 * دو مقدار لازم است تا گوشی بتواند پیامک‌ها را برای مطب بفرستد. کلید از سمت
 * دنتیکس قابل قطع است، پس اگر گوشی گم شود مطب می‌تواند دسترسی را ببندد.
 */
object PairingStore {

    private const val PREFS = "dentix_pairing"
    private const val KEY_SERVER = "server"
    private const val KEY_TOKEN = "token"
    private const val KEY_PAIRED_AT = "pairedAt"

    private fun prefs(context: Context) =
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    fun save(context: Context, server: String, token: String) {
        prefs(context).edit()
            .putString(KEY_SERVER, server.trimEnd('/'))
            .putString(KEY_TOKEN, token)
            .putLong(KEY_PAIRED_AT, System.currentTimeMillis())
            .apply()
    }

    fun isPaired(context: Context): Boolean =
        !prefs(context).getString(KEY_TOKEN, "").isNullOrEmpty()

    fun server(context: Context): String =
        prefs(context).getString(KEY_SERVER, "") ?: ""

    fun token(context: Context): String =
        prefs(context).getString(KEY_TOKEN, "") ?: ""

    /** زمان جفت‌شدن؛ پیامک‌های قدیمی‌تر از این لحظه خوانده نمی‌شوند. */
    fun pairedAt(context: Context): Long =
        prefs(context).getLong(KEY_PAIRED_AT, 0L)

    fun clear(context: Context) {
        prefs(context).edit().clear().apply()
    }
}
