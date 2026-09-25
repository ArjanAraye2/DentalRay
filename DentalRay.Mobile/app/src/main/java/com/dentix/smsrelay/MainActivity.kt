package com.dentix.smsrelay

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.view.View
import android.widget.Button
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * صفحهٔ اصلی: آیا گوشی جفت شده، سرویس در حال کار است چند پیامک فرستاده شده،
 * و کلیدهای کنترل. عمداً ساده نگه داشته شده تا منشی مطب بتواند بدون آموزش
 * از آن استفاده کند.
 */
class MainActivity : AppCompatActivity() {

    private lateinit var statusTitle: TextView
    private lateinit var statusServer: TextView
    private lateinit var statusDetail: TextView
    private lateinit var statSent: TextView
    private lateinit var statQueued: TextView
    private lateinit var statLast: TextView
    private lateinit var statError: TextView
    private lateinit var btnPair: Button
    private lateinit var btnStart: Button
    private lateinit var btnStop: Button
    private lateinit var btnManual: Button
    private lateinit var btnUnpair: Button
    private lateinit var permissionNote: TextView

    private var unsubscribe: (() -> Unit)? = null

    private val askSms =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
            if (granted) {
                if (PairingStore.isPaired(this)) SmsRelayService.start(this)
                Toast.makeText(this, "مجوز خواندن پیامک داده شد.", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(
                    this,
                    "بدون مجوز خواندن پیامک، برنامه نمی‌تواند چیزی بفرستد.",
                    Toast.LENGTH_LONG
                ).show()
            }
            render()
        }

    private val askNotifications =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { render() }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        statusTitle = findViewById(R.id.statusTitle)
        statusServer = findViewById(R.id.statusServer)
        statusDetail = findViewById(R.id.statusDetail)
        statSent = findViewById(R.id.statSent)
        statQueued = findViewById(R.id.statQueued)
        statLast = findViewById(R.id.statLast)
        statError = findViewById(R.id.statError)
        btnPair = findViewById(R.id.btnPair)
        btnStart = findViewById(R.id.btnStart)
        btnStop = findViewById(R.id.btnStop)
        btnManual = findViewById(R.id.btnManual)
        btnUnpair = findViewById(R.id.btnUnpair)
        permissionNote = findViewById(R.id.permissionNote)

        btnPair.setOnClickListener { startActivity(Intent(this, PairingActivity::class.java)) }
        btnStart.setOnClickListener { startFlow() }
        btnStop.setOnClickListener {
            SmsRelayService.stop(this)
            RelayState.running = false
            RelayState.notifyChange()
            render()
        }
        btnManual.setOnClickListener { showManualPairing() }
        btnUnpair.setOnClickListener { confirmUnpair() }

        requestNotificationsIfNeeded()
        render()
    }

    override fun onResume() {
        super.onResume()
        unsubscribe = RelayState.subscribe { runOnUiThread { render() } }
        render()
    }

    override fun onPause() {
        unsubscribe?.invoke()
        unsubscribe = null
        super.onPause()
    }

    // ------------------------------------------------------------

    /** اول مجوزها، بعد روشن کردن سرویس. */
    private fun startFlow() {
        when {
            !PairingStore.isPaired(this) ->
                Toast.makeText(this, "اول گوشی را جفت کنید.", Toast.LENGTH_SHORT).show()

            !SmsReader.hasPermission(this) ->
                askSms.launch(Manifest.permission.READ_SMS)

            else -> {
                SmsRelayService.start(this)
                render()
            }
        }
    }

    private fun requestNotificationsIfNeeded() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) !=
            PackageManager.PERMISSION_GRANTED
        ) {
            askNotifications.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    private fun showManualPairing() {
        val fields = layoutInflater.inflate(R.layout.dialog_manual_pairing, null)
        val serverInput = fields.findViewById<TextView>(R.id.manualServer)
        val tokenInput = fields.findViewById<TextView>(R.id.manualToken)
        serverInput.text = PairingStore.server(this)
        AlertDialog.Builder(this)
            .setTitle("ثبت دستی اتصال")
            .setView(fields)
            .setPositiveButton("ثبت") { _, _ ->
                val server = serverInput.text.toString().trim()
                val token = tokenInput.text.toString().trim()
                if (server.isEmpty() || token.isEmpty()) {
                    Toast.makeText(this, "هر دو مورد لازم است.", Toast.LENGTH_SHORT).show()
                } else {
                    PairingStore.save(this, server, token)
                    Toast.makeText(this, "اتصال ثبت شد.", Toast.LENGTH_SHORT).show()
                    render()
                }
            }
            .setNegativeButton("انصراف", null)
            .show()
    }

    private fun confirmUnpair() {
        AlertDialog.Builder(this)
            .setTitle("قطع اتصال")
            .setMessage("گوشی دیگر پیامکی برای مطب نمی‌فرستد. مطمئنید؟")
            .setPositiveButton("قطع اتصال") { _, _ ->
                SmsRelayService.stop(this)
                PairingStore.clear(this)
                Outbox(this).clear()
                RelayState.sentCount = 0
                RelayState.lastError = null
                RelayState.lastInfo = null
                render()
            }
            .setNegativeButton("انصراف", null)
            .show()
    }

    private fun render() {
        val paired = PairingStore.isPaired(this)
        statusTitle.text = if (paired) "گوشی جفت شده است" else "گوشی جفت نشده است"
        statusServer.text = if (paired) {
            "سرور: ${PairingStore.server(this)}"
        } else {
            "برای شروع، «جفت‌سازی با Dentix» را بزنید و کیوآرکد مطب را اسکن کنید."
        }
        statusDetail.text = when {
            !paired -> "بعد از جفت‌سازی، پیامک‌های حاوی لینک رادیولوژی خودکار برای مطب فرستاده می‌شود."
            !SmsReader.hasPermission(this) -> "مجوز خواندن پیامک هنوز داده نشده است."
            RelayState.running -> "در حال کار در پس‌زمینه"
            else -> "متوقف است؛ دکمهٔ «شروع ارسال» را بزنید."
        }

        statSent.text = RelayState.sentCount.toString()
        statQueued.text = RelayState.queued.toString()
        statLast.text = if (RelayState.lastSentAt > 0) {
            SimpleDateFormat("HH:mm:ss", Locale.US).format(Date(RelayState.lastSentAt))
        } else "—"

        val error = RelayState.lastError
        statError.visibility = if (error.isNullOrEmpty()) View.GONE else View.VISIBLE
        statError.text = error ?: ""

        val info = RelayState.lastInfo
        permissionNote.visibility = if (info.isNullOrEmpty()) View.GONE else View.VISIBLE
        permissionNote.text = info ?: ""

        btnPair.visibility = if (paired) View.GONE else View.VISIBLE
        btnManual.visibility = if (paired) View.GONE else View.VISIBLE
        btnStart.visibility = if (paired && !RelayState.running) View.VISIBLE else View.GONE
        btnStop.visibility = if (paired && RelayState.running) View.VISIBLE else View.GONE
        btnUnpair.visibility = if (paired) View.VISIBLE else View.GONE
    }
}
