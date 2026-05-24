package com.example.focususerapp

import android.content.Intent
import android.graphics.Color
import android.os.Bundle
import android.text.InputType
import android.view.View
import android.view.animation.DecelerateInterpolator
import android.widget.*
import androidx.appcompat.app.AppCompatActivity
import androidx.cardview.widget.CardView
import androidx.core.view.WindowCompat
import com.google.firebase.auth.FirebaseAuth

class SignUpActivity : AppCompatActivity() {

    private lateinit var etFullName: EditText
    private lateinit var etEmail: EditText
    private lateinit var etPassword: EditText
    private lateinit var etConfirmPassword: EditText
    private lateinit var ivTogglePassword: ImageView
    private lateinit var ivToggleConfirm: ImageView
    private lateinit var btnSignUp: Button
    private lateinit var tvLogin: TextView
    private lateinit var ivBack: ImageView
    private lateinit var cardSignUp: CardView
    private lateinit var brandContainer: LinearLayout

    private var isPasswordVisible = false
    private var isConfirmVisible = false

    private lateinit var auth: FirebaseAuth
    private val repository = PatientRepository()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        setContentView(R.layout.activity_sign_up)
        window.statusBarColor = Color.TRANSPARENT
        window.decorView.systemUiVisibility =
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE or View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN

        auth = FirebaseAuth.getInstance()
        initViews()
        AppAppearance.apply(this)
        setupListeners()
        playEntranceAnimation()
    }

    private fun initViews() {
        etFullName        = findViewById(R.id.etFullName)
        etEmail           = findViewById(R.id.etEmail)
        etPassword        = findViewById(R.id.etPassword)
        etConfirmPassword = findViewById(R.id.etConfirmPassword)
        ivTogglePassword  = findViewById(R.id.ivTogglePassword)
        ivToggleConfirm   = findViewById(R.id.ivToggleConfirm)
        btnSignUp         = findViewById(R.id.btnSignUp)
        tvLogin           = findViewById(R.id.tvLogin)
        ivBack            = findViewById(R.id.ivBack)
        cardSignUp        = findViewById(R.id.cardSignUp)
        brandContainer    = findViewById(R.id.brandContainer)
    }

    private fun setupListeners() {
        ivBack.setOnClickListener { finish() }

        ivTogglePassword.setOnClickListener {
            isPasswordVisible = !isPasswordVisible
            etPassword.inputType = if (isPasswordVisible)
                InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_VISIBLE_PASSWORD
            else
                InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD
            ivTogglePassword.setImageResource(if (isPasswordVisible) R.drawable.ic_eye_off else R.drawable.ic_eye)
            etPassword.setSelection(etPassword.text.length)
        }

        ivToggleConfirm.setOnClickListener {
            isConfirmVisible = !isConfirmVisible
            etConfirmPassword.inputType = if (isConfirmVisible)
                InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_VISIBLE_PASSWORD
            else
                InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD
            ivToggleConfirm.setImageResource(if (isConfirmVisible) R.drawable.ic_eye_off else R.drawable.ic_eye)
            etConfirmPassword.setSelection(etConfirmPassword.text.length)
        }

        btnSignUp.setOnClickListener {
            val name     = etFullName.text.toString().trim()
            val email    = etEmail.text.toString().trim()
            val password = etPassword.text.toString().trim()
            val confirm  = etConfirmPassword.text.toString().trim()
            if (validateInput(name, email, password, confirm)) performSignUp(name, email, password)
        }

        tvLogin.setOnClickListener { finish() }
    }

    private fun validateInput(name: String, email: String, password: String, confirm: String): Boolean {
        if (name.isEmpty()) { etFullName.error = AppText.get(this, "Required"); etFullName.requestFocus(); return false }
        if (email.isEmpty() || !android.util.Patterns.EMAIL_ADDRESS.matcher(email).matches()) {
            etEmail.error = AppText.get(this, "Invalid email"); etEmail.requestFocus(); return false
        }
        if (password.length < 6) { etPassword.error = AppText.get(this, "Min 6 chars"); etPassword.requestFocus(); return false }
        if (confirm != password) { etConfirmPassword.error = AppText.get(this, "Passwords don't match"); etConfirmPassword.requestFocus(); return false }
        return true
    }

    private fun performSignUp(name: String, email: String, password: String) {
        btnSignUp.isEnabled = false
        btnSignUp.text = AppText.get(this, "CREATING...")

        auth.createUserWithEmailAndPassword(email, password)
            .addOnCompleteListener { task ->
                if (task.isSuccessful) {
                    val uid = auth.currentUser!!.uid
                    saveUserToDatabase(uid, name, email)
                } else {
                    btnSignUp.isEnabled = true
                    btnSignUp.text = AppText.get(this, "CREATE ACCOUNT")
                    Toast.makeText(this, task.exception?.message, Toast.LENGTH_LONG).show()
                }
            }
    }

    private fun saveUserToDatabase(uid: String, name: String, email: String) {
        val parts = name.trim().split(" ")
        val firstName = parts.firstOrNull() ?: name
        val lastName  = if (parts.size > 1) parts.drop(1).joinToString(" ") else ""

        repository.createPatientProfile(
            uid = uid,
            name = firstName,
            surname = lastName,
            email = email,
            onSuccess = {
                btnSignUp.isEnabled = true
                btnSignUp.text = AppText.get(this, "CREATE ACCOUNT")
                startActivity(Intent(this, SetupProfileActivity::class.java))
                finish()
            },
            onError = { e ->
                btnSignUp.isEnabled = true
                btnSignUp.text = AppText.get(this, "CREATE ACCOUNT")
                Toast.makeText(this, e.message, Toast.LENGTH_LONG).show()
            }
        )
    }

    private fun playEntranceAnimation() {
        brandContainer.alpha = 0f
        cardSignUp.alpha = 0f
        cardSignUp.translationY = 60f
        brandContainer.animate().alpha(1f).setDuration(700).setStartDelay(200).start()
        cardSignUp.animate().alpha(1f).translationY(0f).setDuration(600).setStartDelay(400)
            .setInterpolator(DecelerateInterpolator(2f)).start()
    }
}
