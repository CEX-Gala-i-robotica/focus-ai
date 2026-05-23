package com.example.focususerapp

import android.content.Intent
import android.graphics.Color
import android.os.Bundle
import android.text.InputType
import android.view.View
import android.view.animation.DecelerateInterpolator
import android.widget.*
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.cardview.widget.CardView
import androidx.core.view.WindowCompat
import com.google.android.gms.auth.api.signin.GoogleSignIn
import com.google.android.gms.auth.api.signin.GoogleSignInClient
import com.google.android.gms.auth.api.signin.GoogleSignInOptions
import com.google.android.gms.common.api.ApiException
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.GoogleAuthProvider
import com.google.firebase.database.FirebaseDatabase

class LoginActivity : AppCompatActivity() {

    private lateinit var etEmail: EditText
    private lateinit var etPassword: EditText
    private lateinit var ivTogglePassword: ImageView
    private lateinit var btnLogin: Button
    private lateinit var btnGoogle: LinearLayout
    private lateinit var tvGoogleText: TextView
    private lateinit var tvForgotPassword: TextView
    private lateinit var tvSignUp: TextView
    private lateinit var cardLogin: CardView
    private lateinit var brandContainer: LinearLayout

    private lateinit var auth: FirebaseAuth
    private lateinit var googleSignInClient: GoogleSignInClient
    private val database = FirebaseDatabase.getInstance()
    private val repository = PatientRepository()
    private var isPasswordVisible = false

    private val googleLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        val task = GoogleSignIn.getSignedInAccountFromIntent(result.data)
        try {
            val account = task.getResult(ApiException::class.java)
            firebaseAuthWithGoogle(account.idToken!!)
        } catch (e: ApiException) {
            setGoogleLoading(false)
            Toast.makeText(this, "Google error: ${e.message}", Toast.LENGTH_SHORT).show()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        setContentView(R.layout.activity_login)
        window.statusBarColor = Color.TRANSPARENT
        window.decorView.systemUiVisibility =
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE or View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN

        auth = FirebaseAuth.getInstance()

        val gso = GoogleSignInOptions.Builder(GoogleSignInOptions.DEFAULT_SIGN_IN)
            .requestIdToken(getString(R.string.default_web_client_id))
            .requestEmail()
            .build()
        googleSignInClient = GoogleSignIn.getClient(this, gso)

        if (auth.currentUser != null) {
            checkSetupAndRedirect(auth.currentUser!!.uid)
            return
        }

        initViews()
        setupListeners()
        playEntranceAnimation()
    }

    private fun initViews() {
        etEmail          = findViewById(R.id.etEmail)
        etPassword       = findViewById(R.id.etPassword)
        ivTogglePassword = findViewById(R.id.ivTogglePassword)
        btnLogin         = findViewById(R.id.btnLogin)
        btnGoogle        = findViewById(R.id.btnGoogle)
        tvGoogleText     = findViewById(R.id.tvGoogleText)
        tvForgotPassword = findViewById(R.id.tvForgotPassword)
        tvSignUp         = findViewById(R.id.tvSignUp)
        cardLogin        = findViewById(R.id.cardLogin)
        brandContainer   = findViewById(R.id.brandContainer)
    }

    private fun setupListeners() {
        ivTogglePassword.setOnClickListener {
            isPasswordVisible = !isPasswordVisible
            etPassword.inputType = if (isPasswordVisible)
                InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_VISIBLE_PASSWORD
            else
                InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD
            ivTogglePassword.setImageResource(
                if (isPasswordVisible) R.drawable.ic_eye_off else R.drawable.ic_eye
            )
            etPassword.setSelection(etPassword.text.length)
        }

        btnLogin.setOnClickListener {
            val email = etEmail.text.toString().trim()
            val password = etPassword.text.toString().trim()
            if (!validateInput(email, password)) return@setOnClickListener
            btnLogin.isEnabled = false
            btnLogin.text = "SIGNING IN..."
            auth.signInWithEmailAndPassword(email, password)
                .addOnCompleteListener { task ->
                    if (task.isSuccessful) {
                        checkSetupAndRedirect(auth.currentUser!!.uid)
                    } else {
                        btnLogin.isEnabled = true
                        btnLogin.text = "SIGN IN"
                        Toast.makeText(this, task.exception?.message, Toast.LENGTH_LONG).show()
                    }
                }
        }

        btnGoogle.setOnClickListener {
            setGoogleLoading(true)
            googleSignInClient.signOut().addOnCompleteListener {
                googleLauncher.launch(googleSignInClient.signInIntent)
            }
        }

        tvSignUp.setOnClickListener {
            startActivity(Intent(this, SignUpActivity::class.java))
            overridePendingTransition(android.R.anim.fade_in, android.R.anim.fade_out)
        }

        tvForgotPassword.setOnClickListener {
            val email = etEmail.text.toString().trim()
            if (email.isEmpty()) { etEmail.error = "Enter email first"; return@setOnClickListener }
            auth.sendPasswordResetEmail(email)
                .addOnSuccessListener { Toast.makeText(this, "Reset email sent!", Toast.LENGTH_SHORT).show() }
                .addOnFailureListener { Toast.makeText(this, it.message, Toast.LENGTH_SHORT).show() }
        }
    }

    private fun firebaseAuthWithGoogle(idToken: String) {
        auth.signInWithCredential(GoogleAuthProvider.getCredential(idToken, null))
            .addOnCompleteListener { task ->
                setGoogleLoading(false)
                if (task.isSuccessful) {
                    val uid = auth.currentUser!!.uid
                    database.reference.child("patients").child(uid).get()
                        .addOnSuccessListener { snapshot ->
                            if (!snapshot.exists()) {
                                val displayName = auth.currentUser?.displayName ?: ""
                                val parts = displayName.trim().split(" ")
                                val profileData = mapOf(
                                    "name"         to (parts.firstOrNull() ?: ""),
                                    "surname"      to (if (parts.size > 1) parts.drop(1).joinToString(" ") else ""),
                                    "birthDate"    to "",
                                    "doctorEmail"  to "",
                                    "doctorPhone"  to "",
                                    "phone"        to "",
                                    "email"        to (auth.currentUser?.email ?: ""),
                                    "setup"        to false
                                )
                                database.reference.child("patients").child(uid)
                                    .setValue(profileData)
                                    .addOnSuccessListener {
                                        startActivity(Intent(this, SetupProfileActivity::class.java))
                                        finish()
                                    }
                            } else {
                                checkSetupAndRedirect(uid)
                            }
                        }
                } else {
                    Toast.makeText(this, task.exception?.message, Toast.LENGTH_LONG).show()
                }
            }
    }

    private fun checkSetupAndRedirect(uid: String) {
        repository.isSetupComplete(
            uid = uid,
            onSuccess = { setupDone ->
                if (setupDone) {
                    startActivity(Intent(this, MainActivity::class.java))
                } else {
                    startActivity(Intent(this, SetupProfileActivity::class.java))
                }
                finish()
            },
            onError = {
                startActivity(Intent(this, SetupProfileActivity::class.java))
                finish()
            }
        )
    }

    private fun validateInput(email: String, password: String): Boolean {
        if (email.isEmpty() || !android.util.Patterns.EMAIL_ADDRESS.matcher(email).matches()) {
            etEmail.error = "Invalid email"; etEmail.requestFocus(); return false
        }
        if (password.length < 6) {
            etPassword.error = "Minimum 6 characters"; etPassword.requestFocus(); return false
        }
        return true
    }

    private fun setGoogleLoading(loading: Boolean) {
        btnGoogle.isEnabled = !loading
        tvGoogleText.text = if (loading) "Connecting..." else "Continue with Google"
    }

    private fun playEntranceAnimation() {
        brandContainer.alpha = 0f
        cardLogin.alpha = 0f
        cardLogin.translationY = 60f
        brandContainer.animate().alpha(1f).setDuration(700).setStartDelay(200).start()
        cardLogin.animate().alpha(1f).translationY(0f).setDuration(600).setStartDelay(400)
            .setInterpolator(DecelerateInterpolator(2f)).start()
    }
}
