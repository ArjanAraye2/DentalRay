package com.dentix.smsrelay

import android.Manifest
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Bundle
import android.widget.Button
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.core.content.ContextCompat
import com.google.zxing.BinaryBitmap
import com.google.zxing.DecodeHintType
import com.google.zxing.MultiFormatReader
import com.google.zxing.NotFoundException
import com.google.zxing.PlanarYUVLuminanceSource
import com.google.zxing.common.HybridBinarizer
import java.util.Collections

/**
 * اسکن کیوآرکدی که دنتیکس نشان می‌دهد.
 *
 * کیوآرکد شامل آدرس سرور و کلید دستگاه است؛ پس از اسکن، همان دو مقدار ذخیره
 * می‌شود و گشت‌و‌نگشت دستی هم به‌عنوان جایگزین پایین صفحه هست.
 */
class PairingActivity : AppCompatActivity() {

    private var lastHandled = 0L
    private var handled = false

    private val askCamera =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
            if (granted) startCamera()
            else {
                Toast.makeText(this, "بدون دوربین، اسکن ممکن نیست.", Toast.LENGTH_LONG).show()
                finish()
            }
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_pairing)

        findViewById<Button>(R.id.btnBack).setOnClickListener { finish() }
        findViewById<Button>(R.id.btnManualSave).setOnClickListener { saveManual() }

        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) ==
            PackageManager.PERMISSION_GRANTED
        ) startCamera() else askCamera.launch(Manifest.permission.CAMERA)
    }

    private fun startCamera() {
        val future = ProcessCameraProvider.getInstance(this)
        future.addListener({
            try {
                val provider = future.get()

                val preview = Preview.Builder().build().also {
                    it.setSurfaceProvider(findViewById<PreviewView>(R.id.previewView).surfaceProvider)
                }

                val analysis = ImageAnalysis.Builder()
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                    .build()
                analysis.setAnalyzer(ContextCompat.getMainExecutor(this)) { image ->
                    val text = decode(image)
                    image.close()
                    if (!text.isNullOrEmpty()) onCode(text)
                }

                provider.unbindAll()
                provider.bindToLifecycle(
                    this, CameraSelector.DEFAULT_BACK_CAMERA, preview, analysis
                )
            } catch (e: Exception) {
                Toast.makeText(this, "دوربین در دسترس نیست.", Toast.LENGTH_LONG).show()
            }
        }, ContextCompat.getMainExecutor(this))
    }

    /** رمزگشایی یک قاب دوربین؛ فقط کیوآرکد معتبر است. */
    private fun decode(image: ImageProxy): String? {
        if (image.format != android.graphics.ImageFormat.YUV_420_888) return null
        val plane = image.planes.firstOrNull() ?: return null
        val buffer = plane.buffer
        val data = ByteArray(buffer.remaining())
        buffer.get(data)

        val source = PlanarYUVLuminanceSource(
            data, image.width, image.height, 0, 0, image.width, image.height, false
        )
        val reader = MultiFormatReader().apply {
            setHints(mapOf(DecodeHintType.POSSIBLE_FORMATS to listOf(com.google.zxing.BarcodeFormat.QR_CODE)))
        }
        return try {
            reader.decodeWithState(BinaryBitmap(HybridBinarizer(source))).text
        } catch (_: NotFoundException) {
            null
        } catch (_: Exception) {
            null
        } finally {
            reader.reset()
        }
    }

    private fun onCode(text: String) {
        if (handled) return
        if (System.currentTimeMillis() - lastHandled < 2000) return
        lastHandled = System.currentTimeMillis()

        val uri = runCatching { Uri.parse(text) }.getOrNull() ?: return
        if (uri.scheme != "dentix") return
        val server = uri.getQueryParameter("server") ?: return
        val token = uri.getQueryParameter("token") ?: return
        if (server.isEmpty() || token.isEmpty()) return

        handled = true
        PairingStore.save(this, server, token)
        Toast.makeText(this, "گوشی جفت شد.", Toast.LENGTH_LONG).show()
        SmsRelayService.start(this)
        finish()
    }

    private fun saveManual() {
        val server = findViewById<TextView>(R.id.manualServer).text.toString().trim()
        val token = findViewById<TextView>(R.id.manualToken).text.toString().trim()
        if (server.isEmpty() || token.isEmpty()) {
            Toast.makeText(this, "هر دو مورد لازم است.", Toast.LENGTH_SHORT).show()
            return
        }
        PairingStore.save(this, server, token)
        Toast.makeText(this, "گوشی جفت شد.", Toast.LENGTH_SHORT).show()
        SmsRelayService.start(this)
        finish()
    }
}
