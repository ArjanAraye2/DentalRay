package com.dentix.smsrelay

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/**
 * پس از راه‌اندازی مجدد گوشی، سرویس ارسال را دوباره روشن می‌کند تا پیامکی
 * که در آن فاصله رسیده از دست نرود.
 */
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Intent.ACTION_BOOT_COMPLETED) return
        if (!PairingStore.isPaired(context)) return
        runCatching { SmsRelayService.start(context) }
    }
}
