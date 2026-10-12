# Shrink only: no obfuscation and no optimisation, so the release build behaves exactly like the debug one.
-dontobfuscate
-dontoptimize
-keep class com.lanmessengerx.android.MainActivity { *; }
-keep class com.lanmessengerx.android.MessengerService { *; }
-keepattributes SourceFile,LineNumberTable
-dontwarn javax.annotation.**
-dontwarn org.jetbrains.annotations.**
