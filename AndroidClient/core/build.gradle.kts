plugins {
    id("org.jetbrains.kotlin.jvm")
}

kotlin {
    jvmToolchain(17)
}

sourceSets {
    named("main") {
        kotlin.srcDir("src/main/kotlin")
    }
    create("tool") {
        kotlin.srcDir("src/tool/kotlin")
        compileClasspath += sourceSets["main"].output + configurations["runtimeClasspath"]
        runtimeClasspath += output + compileClasspath
    }
}

dependencies {
    testImplementation("junit:junit:4.13.2")
    testImplementation(kotlin("test"))
}

tasks.test {
    systemProperty("lmx.vectors", rootProject.file("../Protocol/testvectors").absolutePath)
}
