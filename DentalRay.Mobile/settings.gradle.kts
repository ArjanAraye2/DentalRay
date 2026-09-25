// DentalRay Mobile - ساختار پروژهٔ برنامهٔ اندروید
pluginManagement {
    repositories {
        // آینه‌ها اول؛ اگر دسترسی مستقیم به سرور گوگل بود، google() هم جواب می‌دهد.
        maven("https://maven.aliyun.com/repository/google")
        maven("https://maven.aliyun.com/repository/gradle-plugin")
        maven("https://maven.aliyun.com/repository/public")
        gradlePluginPortal()
        google()
        mavenCentral()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        maven("https://maven.aliyun.com/repository/google")
        maven("https://maven.aliyun.com/repository/public")
        google()
        mavenCentral()
    }
}

rootProject.name = "DentalRayMobile"
include(":app")
