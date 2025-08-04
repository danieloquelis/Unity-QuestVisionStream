plugins {
    id("com.android.library") version "8.2.0"
    id("org.jetbrains.kotlin.android") version "1.9.22"
}

android {
    namespace = "com.xreducation.questvisionstreamplugin"
    compileSdk = 35

    defaultConfig {
        minSdk = 32
        consumerProguardFiles("consumer-rules.pro")
    }

    buildTypes {
        release {
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

repositories {
    google()
    mavenCentral()
}

dependencies {
// AndroidX
    implementation("androidx.core:core-ktx:1.12.0")
    implementation("androidx.appcompat:appcompat:1.6.1")

    // WebRTC
    implementation("io.github.webrtc-sdk:android:125.6422.06.1")

    // OkHttp for signaling
    implementation("com.squareup.okhttp3:okhttp:4.11.0")
}
