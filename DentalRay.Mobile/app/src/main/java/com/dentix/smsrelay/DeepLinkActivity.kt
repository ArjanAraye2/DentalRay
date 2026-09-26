package com.dentix.smsrelay

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity

/**
 * لینک جفت‌سازی (dentix://pair?server=...&token=...)
 *
 * وقتی کسی کیوآرکد را با دوربین معمولی گوشی اسکن می‌کند، سیستم‌عامل آدرس را
 * به همین صفحه می‌دهد؛ بدون این، دوربین فقط متن را نشان می‌دهد و هیچ اتفاقی
 * نمی‌افتد. این صفحه کلید را ذخیره می‌کند و برنامه را باز می‌کند.
 */
class DeepLinkActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        handle(intent?.data)
        startActivity(Intent(this, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        finish()
    }

    override fun onNewIntent(intent: Intent?) {
        super.onNewIntent(intent)
        handle(intent?.data)
    }

    private fun handle(data: Uri?) {
        val server = data?.getQueryParameter("server")?.trim().orEmpty()
        val token = data?.getQueryParameter("token")?.trim().orEmpty()
        if (server.isEmpty() || token.isEmpty()) {
            Toast.makeText(this, "لینک جفت‌سازی معتبر نیست.", Toast.LENGTH_LONG).show()
            return
        }
        PairingStore.save(this, server.trimEnd('/'), token)
        Toast.makeText(this, "گوشی جفت شد.", Toast.LENGTH_LONG).show()
    }
}
