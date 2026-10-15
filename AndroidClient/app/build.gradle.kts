import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

val versionProps = Properties().apply { rootProject.file("version.properties").inputStream().use { load(it) } }

android {
    namespace = "com.lanmessengerx.android"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.lanmessengerx.android"
        minSdk = 26
        targetSdk = 34
        versionCode = versionProps.getProperty("versionCode").toInt()
        versionName = versionProps.getProperty("versionName")
    }

    signingConfigs {
        create("release") {
            storeFile = rootProject.file("signing/lanmessengerx-release.jks")
            storePassword = "lanmessengerx"
            keyAlias = "lanmessengerx"
            keyPassword = "lanmessengerx"
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            proguardFiles(rootProject.file("tools/proguard-rules.pro"))
            signingConfig = signingConfigs.getByName("release")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }
    sourceSets["main"].java.srcDir("src/main/kotlin")
    // translations and stock pictures are shared with the desktop client
    sourceSets["main"].assets.srcDir(layout.buildDirectory.dir("generated/shared-assets"))
}

val sharedAssets = rootProject.file("../DesktopClient/src/LanMessengerX.App/Assets")
val copySharedAssets by tasks.registering(Copy::class) {
    from(sharedAssets) { include("lang/*.json", "avatars/[0-9]*.png") }
    into(layout.buildDirectory.dir("generated/shared-assets"))
}
tasks.matching { it.name == "preBuild" }.configureEach { dependsOn(copySharedAssets) }

dependencies {
    implementation(project(":core"))
}
