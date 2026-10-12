// Gradle build for Android Studio. Not used by tools/build-apk.sh (which needs no Gradle at all)
// and not exercised in the environment this project was written in - see AndroidClient/README.md.
plugins {
    id("com.android.application") version "8.7.3" apply false
    id("org.jetbrains.kotlin.android") version "2.0.21" apply false
    id("org.jetbrains.kotlin.jvm") version "2.0.21" apply false
}
