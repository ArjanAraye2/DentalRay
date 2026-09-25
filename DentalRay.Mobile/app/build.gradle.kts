// DentalRay Mobile - پیکربندی ماژول برنامه
plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.dentix.smsrelay"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.dentix.smsrelay"
        minSdk = 26          // اندروید ۸ به بالا؛ اکثر گوشی‌های فعلی
        targetSdk = 34
        versionCode = 1
        versionName = "1.0"
    }

    buildTypes {
        release {
            // امضا با کلید خودی؛ برای نصب آزمایشی همان نسخهٔ debug کافی است.
            isMinifyEnabled = false
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("com.google.android.material:material:1.12.0")
    implementation("androidx.constraintlayout:constraintlayout:2.1.4")

    // دوربین برای اسکن کیوآرکد (نام درست این کتابخانه camera-camera2 است)
    implementation("androidx.camera:camera-core:1.3.4")
    implementation("androidx.camera:camera-camera2:1.3.4")
    implementation("androidx.camera:camera-lifecycle:1.3.4")
    implementation("androidx.camera:camera-view:1.3.4")

    // رمزگشایی کیوآرکد (بدون نیاز به سرویس‌های گوگل روی گوشی)
    implementation("com.google.zxing:core:3.5.3")

    // کار در پس‌زمینه با صف ارسال
    implementation("androidx.work:work-runtime-ktx:2.9.1")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.4")
}
