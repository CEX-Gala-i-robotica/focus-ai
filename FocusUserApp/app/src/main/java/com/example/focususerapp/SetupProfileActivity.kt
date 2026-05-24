package com.example.focususerapp

import android.app.DatePickerDialog
import android.content.Intent
import android.graphics.Color
import android.os.Bundle
import android.view.View
import android.view.animation.DecelerateInterpolator
import android.widget.*
import androidx.appcompat.app.AppCompatActivity
import androidx.cardview.widget.CardView
import androidx.core.view.WindowCompat
import com.google.firebase.auth.FirebaseAuth
import java.util.*

class SetupProfileActivity : AppCompatActivity() {

    private lateinit var etBirthDate: EditText
    private lateinit var etPhoneNumber: EditText
    private lateinit var etDoctorEmail: EditText
    private lateinit var etDoctorPhone: EditText
    private lateinit var btnSave: Button
    private lateinit var cardSetup: CardView
    private lateinit var brandContainer: LinearLayout
    private lateinit var tvWelcomeName: TextView

    private val auth     = FirebaseAuth.getInstance()
    private val repository = PatientRepository()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        setContentView(R.layout.activity_setup_profile)
        window.statusBarColor = Color.TRANSPARENT
        window.decorView.systemUiVisibility =
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE or View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN

        initViews()
        AppAppearance.apply(this)
        loadUserName()
        setupListeners()
        playEntranceAnimation()
    }

    private fun initViews() {
        etBirthDate    = findViewById(R.id.etBirthDate)
        etPhoneNumber  = findViewById(R.id.etPhoneNumber)
        etDoctorEmail  = findViewById(R.id.etDoctorEmail)
        etDoctorPhone  = findViewById(R.id.etDoctorPhone)
        btnSave        = findViewById(R.id.btnSave)
        cardSetup      = findViewById(R.id.cardSetup)
        brandContainer = findViewById(R.id.brandContainer)
        tvWelcomeName  = findViewById(R.id.tvWelcomeName)
    }

    private fun loadUserName() {
        repository.loadCurrentPatient(
            onSuccess = { profile, _ ->
                if (profile.name.isNotEmpty()) tvWelcomeName.text = "${AppText.get(this, "Hello")}, ${profile.name}"
            },
            onError = {}
        )
    }

    private fun setupListeners() {
        etBirthDate.setOnClickListener {
            val cal = Calendar.getInstance()
            DatePickerDialog(this, { _, year, month, day ->
                etBirthDate.setText(String.format("%02d.%02d.%04d", day, month + 1, year))
            }, cal.get(Calendar.YEAR) - 18, cal.get(Calendar.MONTH), cal.get(Calendar.DAY_OF_MONTH)).show()
        }

        btnSave.setOnClickListener {
            val birthDate   = etBirthDate.text.toString().trim()
            val phone       = etPhoneNumber.text.toString().trim()
            val doctorEmail = etDoctorEmail.text.toString().trim()
            val doctorPhone = etDoctorPhone.text.toString().trim()
            if (validateInput(birthDate, phone)) saveProfile(birthDate, phone, doctorEmail, doctorPhone)
        }
    }

    private fun validateInput(birthDate: String, phone: String): Boolean {
        if (birthDate.isEmpty()) { etBirthDate.error = AppText.get(this, "Required"); etBirthDate.requestFocus(); return false }
        if (phone.isEmpty()) { etPhoneNumber.error = AppText.get(this, "Required"); etPhoneNumber.requestFocus(); return false }
        return true
    }

    private fun saveProfile(birthDate: String, phone: String, doctorEmail: String, doctorPhone: String) {
        val uid = auth.currentUser?.uid ?: return
        btnSave.isEnabled = false
        btnSave.text = AppText.get(this, "SAVING...")

        repository.updateSetupProfile(
            birthDate = birthDate,
            phone = phone,
            doctorEmail = doctorEmail,
            doctorPhone = doctorPhone,
            onSuccess = {
                btnSave.isEnabled = true
                btnSave.text = AppText.get(this, "SAVE & CONTINUE")
                startActivity(Intent(this, MainActivity::class.java))
                finish()
            },
            onError = { e ->
                btnSave.isEnabled = true
                btnSave.text = AppText.get(this, "SAVE & CONTINUE")
                Toast.makeText(this, e.message, Toast.LENGTH_LONG).show()
            }
        )
    }

    private fun playEntranceAnimation() {
        brandContainer.alpha = 0f
        cardSetup.alpha = 0f
        cardSetup.translationY = 60f
        brandContainer.animate().alpha(1f).setDuration(700).setStartDelay(200).start()
        cardSetup.animate().alpha(1f).translationY(0f).setDuration(600).setStartDelay(400)
            .setInterpolator(DecelerateInterpolator(2f)).start()
    }
}
