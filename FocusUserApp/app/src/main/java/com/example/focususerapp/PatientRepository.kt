package com.example.focususerapp

import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.database.DataSnapshot
import com.google.firebase.database.FirebaseDatabase
import com.google.android.gms.tasks.Tasks
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

class PatientRepository(
    private val auth: FirebaseAuth = FirebaseAuth.getInstance(),
    private val database: FirebaseDatabase = FirebaseDatabase.getInstance()
) {
    fun currentUserId(): String? = auth.currentUser?.uid

    fun loadCurrentPatient(
        onSuccess: (PatientProfile, List<TestResult>) -> Unit,
        onError: (Exception) -> Unit
    ) {
        val uid = currentUserId() ?: return onError(IllegalStateException("No authenticated user"))
        val patientRef = database.reference.child("patients").child(uid)
        val profileKeys = listOf("name", "surname", "email", "phone", "birthDate", "nfc", "setup")
        val tasks = profileKeys.map { key -> patientRef.child(key).get() } + patientRef.child("testResults").get()

        Tasks.whenAllSuccess<DataSnapshot>(tasks)
            .addOnSuccessListener { snapshots ->
                val byKey = snapshots.associateBy { it.key.orEmpty() }
                if (profileKeys.any { byKey[it]?.exists() == true }) {
                    onSuccess(parseProfileFields(byKey), parseTests(byKey["testResults"] ?: return@addOnSuccessListener))
                } else {
                    loadLegacyPatient(uid, onSuccess, onError)
                }
            }
            .addOnFailureListener(onError)
    }

    fun updateSetupProfile(
        birthDate: String,
        phone: String,
        doctorEmail: String,
        doctorPhone: String,
        onSuccess: () -> Unit,
        onError: (Exception) -> Unit
    ) {
        val uid = currentUserId() ?: return onError(IllegalStateException("No authenticated user"))
        val updates = mapOf(
            "birthDate" to birthDate,
            "phone" to phone,
            "doctorEmail" to doctorEmail,
            "doctorPhone" to doctorPhone,
            "setup" to true
        )
        database.reference.child("patients").child(uid)
            .updateChildren(updates)
            .addOnSuccessListener { onSuccess() }
            .addOnFailureListener(onError)
    }

    fun saveGameResult(
        game: String,
        duration: String,
        difficulty: String,
        score: Double,
        extra: Map<String, Any?> = emptyMap(),
        onSuccess: () -> Unit = {},
        onError: (Exception) -> Unit = {}
    ) {
        val uid = currentUserId() ?: return onError(IllegalStateException("No authenticated user"))
        val payload = linkedMapOf<String, Any?>(
            "game" to game,
            "dateTime" to SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US).format(Date()),
            "duration" to duration,
            "difficulty" to difficulty,
            "scor" to kotlin.math.round(score * 100.0) / 100.0
        )
        payload.putAll(extra)

        val patientRef = database.reference
            .child("patients")
            .child(uid)
            .child("activityResults")
            .push()

        patientRef.setValue(payload)
            .addOnSuccessListener {
                val activityId = patientRef.key
                if (activityId != null) {
                    database.reference.child("activityResults").child(uid).child(activityId).setValue(payload)
                    database.reference.child(uid).child("activities").child(activityId).setValue(payload)
                }
                onSuccess()
            }
            .addOnFailureListener(onError)
    }

    fun updateNfcTag(
        tag: String,
        onSuccess: () -> Unit,
        onError: (Exception) -> Unit
    ) {
        val uid = currentUserId() ?: return onError(IllegalStateException("No authenticated user"))
        database.reference.child("patients").child(uid).child("nfc")
            .setValue(tag)
            .addOnSuccessListener { onSuccess() }
            .addOnFailureListener(onError)
    }

    fun loadCurrentGameResults(
        onSuccess: (List<GameResult>) -> Unit,
        onError: (Exception) -> Unit
    ) {
        val uid = currentUserId() ?: return onError(IllegalStateException("No authenticated user"))
        database.reference.child("patients").child(uid).child("activityResults")
            .orderByKey()
            .limitToLast(12)
            .get()
            .addOnSuccessListener { snapshot ->
                onSuccess(parseGameResults(snapshot))
            }
            .addOnFailureListener(onError)
    }

    fun createPatientProfile(
        uid: String,
        name: String,
        surname: String,
        email: String,
        onSuccess: () -> Unit,
        onError: (Exception) -> Unit
    ) {
        val profileData = mapOf(
            "name" to name,
            "surname" to surname,
            "birthDate" to "",
            "email" to email,
            "phone" to "",
            "setup" to false
        )
        database.reference.child("patients").child(uid)
            .setValue(profileData)
            .addOnSuccessListener { onSuccess() }
            .addOnFailureListener(onError)
    }

    fun isSetupComplete(
        uid: String,
        onSuccess: (Boolean) -> Unit,
        onError: (Exception) -> Unit
    ) {
        database.reference.child("patients").child(uid).child("setup").get()
            .addOnSuccessListener { patientSetup ->
                if (patientSetup.exists()) {
                    onSuccess(patientSetup.getValue(Boolean::class.java) ?: false)
                } else {
                    database.reference.child(uid).child("profile").child("setup").get()
                        .addOnSuccessListener { legacySetup ->
                            onSuccess(legacySetup.getValue(Boolean::class.java) ?: false)
                        }
                        .addOnFailureListener(onError)
                }
            }
            .addOnFailureListener(onError)
    }

    private fun loadLegacyPatient(
        uid: String,
        onSuccess: (PatientProfile, List<TestResult>) -> Unit,
        onError: (Exception) -> Unit
    ) {
        database.reference.child(uid).get()
            .addOnSuccessListener { root ->
                val profile = parseLegacyProfile(root.child("profile"))
                val tests = parseTests(root.child("tests"))
                onSuccess(profile, tests)
            }
            .addOnFailureListener(onError)
    }

    private fun parseProfile(snapshot: DataSnapshot): PatientProfile {
        return PatientProfile(
            name = snapshot.stringValue("name"),
            surname = snapshot.stringValue("surname"),
            email = snapshot.stringValue("email"),
            phone = snapshot.stringValue("phone"),
            birthDate = snapshot.stringValue("birthDate"),
            nfc = snapshot.stringValue("nfc"),
            setup = snapshot.child("setup").getValue(Boolean::class.java) ?: false
        )
    }

    private fun parseProfileFields(fields: Map<String, DataSnapshot>): PatientProfile {
        return PatientProfile(
            name = fields.stringValue("name"),
            surname = fields.stringValue("surname"),
            email = fields.stringValue("email"),
            phone = fields.stringValue("phone"),
            birthDate = fields.stringValue("birthDate"),
            nfc = fields.stringValue("nfc"),
            setup = fields["setup"]?.getValue(Boolean::class.java) ?: false
        )
    }

    private fun parseLegacyProfile(snapshot: DataSnapshot): PatientProfile {
        return PatientProfile(
            name = snapshot.stringValue("name"),
            surname = snapshot.stringValue("surname"),
            email = snapshot.stringValue("email"),
            phone = snapshot.stringValue("phone-number"),
            birthDate = snapshot.stringValue("birth-date"),
            nfc = snapshot.stringValue("nfc"),
            setup = snapshot.child("setup").getValue(Boolean::class.java) ?: false
        )
    }

    private fun parseTests(snapshot: DataSnapshot): List<TestResult> {
        return snapshot.children.map { test ->
            TestResult(
                id = test.key.orEmpty(),
                dateTime = test.stringValue("dateTime"),
                duration = test.stringValue("duration").ifBlank { test.stringValue("durata") },
                dist = parseNumberSeries(test.child("dist").value),
                ecg = parseNumberSeries(test.child("ecg").value),
                hr = parseNumberSeries(test.child("hr").value),
                mapPoints = parseMapPoints(test.child("map").value),
                precizieGonogo = test.floatValue("precizieGonogo")
                    ?: test.floatValue("precizia gonogo")
                    ?: test.floatValue("precizie_gonogo"),
                scor = test.floatValue("scor"),
                spo2 = parseNumberSeries(test.child("spo2").value),
                tr2 = test.floatValue("tr2")
            )
        }.sortedByDescending { it.id }
    }

    private fun parseGameResults(snapshot: DataSnapshot): List<GameResult> {
        return snapshot.children.map { item ->
            val details = item.children
                .filter { child -> child.key !in setOf("game", "dateTime", "duration", "difficulty", "scor") }
                .associate { child -> child.key.orEmpty() to child.value?.toString().orEmpty() }

            GameResult(
                id = item.key.orEmpty(),
                game = item.stringValue("game"),
                dateTime = item.stringValue("dateTime"),
                duration = item.stringValue("duration"),
                difficulty = item.stringValue("difficulty"),
                scor = item.floatValue("scor"),
                details = details
            )
        }.sortedByDescending { it.id }
    }

    private fun DataSnapshot.stringValue(key: String): String {
        return child(key).getValue(String::class.java) ?: child(key).value?.toString().orEmpty()
    }

    private fun Map<String, DataSnapshot>.stringValue(key: String): String {
        val value = this[key]?.value
        return value?.toString().orEmpty()
    }

    private fun DataSnapshot.floatValue(key: String): Float? {
        val value = child(key).value ?: return null
        return when (value) {
            is Number -> value.toFloat()
            is String -> parseFloat(value)
            else -> parseFloat(value)
        }
    }

    private fun parseNumberSeries(value: Any?): List<Float> {
        return when (value) {
            null -> emptyList()
            is List<*> -> value.mapNotNull { parseFloat(it) }
            is Map<*, *> -> value.values.mapNotNull { parseFloat(it) }
            else -> value.toString()
                .split(',', ';', ' ')
                .mapNotNull { parseFloat(it.trim()) }
        }
    }

    private fun parseMapPoints(value: Any?): List<Pair<Float, Float>> {
        return when (value) {
            null -> emptyList()
            is List<*> -> value.mapNotNull { it.toPointOrNull() }
            is Map<*, *> -> value.values.mapNotNull { it.toPointOrNull() }
            else -> value.toString()
                .split(';')
                .mapNotNull { chunk ->
                    val coords = chunk.split(',')
                    if (coords.size < 2) return@mapNotNull null
                    val x = parseFloat(coords[0].trim())
                    val y = parseFloat(coords[1].trim())
                    if (x != null && y != null) x to y else null
                }
        }
    }

    private fun parseFloat(value: Any?): Float? {
        return when (value) {
            null -> null
            is Number -> value.toFloat()
            is String -> value.replace(',', '.').trim().takeIf { it.isNotEmpty() }?.let {
                runCatching { java.lang.Float.parseFloat(it) }.getOrNull()
            }
            else -> value.toString().replace(',', '.').trim().takeIf { it.isNotEmpty() }?.let {
                runCatching { java.lang.Float.parseFloat(it) }.getOrNull()
            }
        }
    }

    private fun Any?.toPointOrNull(): Pair<Float, Float>? {
        return when (this) {
            is Map<*, *> -> {
                val x = parseFloat(this["x"]) ?: parseFloat(this["lat"])
                val y = parseFloat(this["y"]) ?: parseFloat(this["lng"])
                if (x != null && y != null) x to y else null
            }
            is List<*> -> {
                val x = parseFloat(getOrNull(0))
                val y = parseFloat(getOrNull(1))
                if (x != null && y != null) x to y else null
            }
            else -> null
        }
    }
}
