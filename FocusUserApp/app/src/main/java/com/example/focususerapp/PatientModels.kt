package com.example.focususerapp

data class PatientProfile(
    val name: String = "",
    val surname: String = "",
    val email: String = "",
    val phone: String = "",
    val birthDate: String = "",
    val setup: Boolean = false
) {
    val fullName: String
        get() = listOf(name, surname).filter { it.isNotBlank() }.joinToString(" ")
}

data class TestResult(
    val id: String,
    val dateTime: String = "",
    val duration: String = "",
    val dist: List<Float> = emptyList(),
    val ecg: List<Float> = emptyList(),
    val hr: List<Float> = emptyList(),
    val mapPoints: List<Pair<Float, Float>> = emptyList(),
    val precizieGonogo: Float? = null,
    val scor: Float? = null,
    val spo2: List<Float> = emptyList(),
    val tr2: Float? = null
) {
    val averageDistance: Float?
        get() = dist.takeIf { it.isNotEmpty() }?.average()?.toFloat()
}
