package com.dentix.smsrelay

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat

/**
 * وضعیت زندهٔ سرویس؛ صفحهٔ اصلی با شنیدن همین، آمار را به‌روز نشان می‌دهد.
 */
object RelayState {
    @Volatile var running = false
    @Volatile var lastError: String? = null
    @Volatile var lastInfo: String? = null
    @Volatile var sentCount = 0
    @Volatile var queued = 0
    @Volatile var lastSentAt = 0L

    private val listeners = java.util.concurrent.CopyOnWriteArrayList<() -> Unit>()

    fun subscribe(listener: () -> Unit): () -> Unit {
        listeners.add(listener)
        return { listeners.remove(listener) }
    }

    fun notifyChange() {
        listeners.forEach { runCatching { it() } }
    }
}

/**
 * سرویس پس‌زمینه: هر چند ثانیه پیامک‌های تازه را می‌خواند، در صف می‌گذارد و
 * هر آنچه در صف است به مطب می‌فرستد. کار در نوتیفیکیشن ادامه دارد تا اندروید
 * برنامه را در پس‌زمینه متوقف نکند.
 */
class SmsRelayService : Service() {

    private var worker: Thread? = null
    @Volatile private var active = true

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        createChannel()
        startForeground(NOTIFICATION_ID, buildNotification("در حال آماده‌سازی…"))
        RelayState.running = true
        RelayState.notifyChange()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                stopSelf()
                return START_NOT_STICKY
            }
            ACTION_TICK -> { /* یک نوبت اجرا دستی، همان حلقه کافی است */ }
        }

        if (worker?.isAlive != true) {
            active = true
            worker = Thread {
                val since = PairingStore.pairedAt(this)
                while (active) {
                    try { tick(since) } catch (_: Exception) { }
                    Thread.sleep(CYCLE_MS)
                }
            }.apply { isDaemon = true; start() }
        }
        return START_STICKY
    }

    /**
     * نقطه‌ای که تا آن خوانده‌ایم. بدون این، هر نوبت همان صندوق قبلی دوباره
     * خوانده می‌شد و همان پیامک بارها و بارها فرستاده می‌شد.
     */
    @Volatile private var lastReadAt = 0L

    private fun tick(since: Long) {
        val outbox = Outbox(this)

        // ۱) پیامک‌های تازه با لینک را به صف اضافه کن
        if (SmsReader.hasPermission(this)) {
            val fresh = SmsReader.readNew(this, maxOf(since, lastReadAt))
            fresh.forEach { m -> outbox.add(m.sender, m.body, m.receivedAt) }
            if (fresh.isNotEmpty()) {
                lastReadAt = maxOf(lastReadAt, fresh.maxOf { it.receivedAt })
                RelayState.lastInfo = "${fresh.size} پیامک تازه خوانده شد."
            }
        }

        // ۲) هر چه در صف است بفرست
        val pending = outbox.all()
        RelayState.queued = pending.size
        if (pending.isNotEmpty() && PairingStore.isPaired(this)) {
            try {
                val result = DentixApi.send(
                    PairingStore.server(this),
                    PairingStore.token(this),
                    pending
                )
                outbox.markSent(pending.map { it.id }.toSet())
                RelayState.sentCount += pending.size
                RelayState.lastSentAt = System.currentTimeMillis()
                RelayState.lastError = null
                RelayState.lastInfo =
                    "${pending.size} پیامک فرستاده شد (${result.linked} متصل، ${result.pending} در انتظار)"
            } catch (e: Exception) {
                outbox.markFailed(pending.map { it.id }.toSet())
                RelayState.lastError = e.message ?: "ارسال انجام نشد."
            }
        }

        RelayState.queued = outbox.size()
        RelayState.notifyChange()
        updateNotification()
    }

    override fun onDestroy() {
        active = false
        RelayState.running = false
        RelayState.notifyChange()
        super.onDestroy()
    }

    // ------------------------------------------------------------
    // نوتیفیکیشنی که اندروید را مطمئن می‌کند کار در پس‌زمینه ادامه دارد
    // ------------------------------------------------------------
    private fun createChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            manager.createNotificationChannel(
                NotificationChannel(
                    CHANNEL_ID,
                    "ارسال پیامک رادیولوژی",
                    NotificationManager.IMPORTANCE_MIN
                ).apply { description = "دریافت پیامک‌های رادیولوژی و فرستادن آن‌ها به مطب" }
            )
        }
    }

    private fun buildNotification(text: String): Notification {
        val open = PendingIntent.getActivity(
            this, 0, Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.stat_notify_sync)
            .setContentTitle("Dentix")
            .setContentText(text)
            .setContentIntent(open)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .build()
    }

    private fun updateNotification() {
        val text = buildString {
            append("ارسال‌شده: ")
            append(RelayState.sentCount)
            if (RelayState.queued > 0) append("  |  در صف: ").append(RelayState.queued)
            RelayState.lastError?.let { append("  |  خطا: ").append(it) }
        }
        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        manager.notify(NOTIFICATION_ID, buildNotification(text))
    }

    companion object {
        const val CHANNEL_ID = "dentix_relay"
        const val NOTIFICATION_ID = 101
        const val ACTION_STOP = "com.dentix.smsrelay.STOP"
        const val ACTION_TICK = "com.dentix.smsrelay.TICK"
        private const val CYCLE_MS = 20_000L

        fun start(context: Context) {
            val intent = Intent(context, SmsRelayService::class.java)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }

        fun stop(context: Context) {
            val intent = Intent(context, SmsRelayService::class.java).apply { action = ACTION_STOP }
            runCatching { context.startService(intent) }
                .onFailure { context.stopService(Intent(context, SmsRelayService::class.java)) }
        }
    }
}
