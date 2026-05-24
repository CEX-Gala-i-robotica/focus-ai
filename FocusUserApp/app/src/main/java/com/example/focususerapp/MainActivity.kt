package com.example.focususerapp

import android.content.Intent
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.view.View
import android.view.animation.DecelerateInterpolator
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.cardview.widget.CardView
import androidx.core.view.WindowCompat
import com.google.firebase.auth.FirebaseAuth
import java.util.Locale

class MainActivity : AppCompatActivity() {

    private lateinit var brandContainer: LinearLayout
    private lateinit var tabAccount: TextView
    private lateinit var tabTests: TextView
    private lateinit var tabGames: TextView
    private lateinit var accountPage: LinearLayout
    private lateinit var testsPage: LinearLayout
    private lateinit var scrollContent: ScrollView
    private lateinit var profileFields: LinearLayout
    private lateinit var testResultsContainer: LinearLayout
    private lateinit var tvName: TextView
    private lateinit var tvEmail: TextView
    private lateinit var tvInitials: TextView
    private lateinit var btnSignOut: TextView
    private lateinit var cardProfile: CardView

    private val auth = FirebaseAuth.getInstance()
    private val repository = PatientRepository()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        setContentView(R.layout.activity_main)
        window.statusBarColor = Color.TRANSPARENT
        window.decorView.systemUiVisibility =
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE or View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN

        if (auth.currentUser == null) {
            startActivity(Intent(this, LoginActivity::class.java))
            finish()
            return
        }

        initViews()
        setupListeners()
        showPage(Page.Account)
        loadPatient()
        playEntranceAnimation()
    }

    private fun initViews() {
        brandContainer = findViewById(R.id.brandContainer)
        tabAccount = findViewById(R.id.tabAccount)
        tabTests = findViewById(R.id.tabTests)
        tabGames = findViewById(R.id.tabGames)
        accountPage = findViewById(R.id.accountPage)
        testsPage = findViewById(R.id.testsPage)
        scrollContent = findViewById(R.id.scrollContent)
        profileFields = findViewById(R.id.profileFields)
        testResultsContainer = findViewById(R.id.testResultsContainer)
        tvName = findViewById(R.id.tvName)
        tvEmail = findViewById(R.id.tvEmail)
        tvInitials = findViewById(R.id.tvInitials)
        btnSignOut = findViewById(R.id.btnSignOut)
        cardProfile = findViewById(R.id.cardProfile)
    }

    private fun setupListeners() {
        tabAccount.setOnClickListener { showPage(Page.Account) }
        tabTests.setOnClickListener { showPage(Page.Tests) }
        tabGames.setOnClickListener { startActivity(Intent(this, GamesActivity::class.java)) }
        btnSignOut.setOnClickListener {
            auth.signOut()
            startActivity(Intent(this, LoginActivity::class.java))
            finish()
        }
    }

    private fun loadPatient() {
        repository.loadCurrentPatient(
            onSuccess = { profile, tests ->
                renderProfile(profile)
                renderTests(tests)
            },
            onError = {
                Toast.makeText(this, "Error loading account data", Toast.LENGTH_SHORT).show()
            }
        )
    }

    private fun renderProfile(profile: PatientProfile) {
        tvName.text = profile.fullName.ifBlank { "Unnamed patient" }
        tvEmail.text = profile.email.ifBlank { "No email" }
        tvInitials.text = buildString {
            profile.name.firstOrNull()?.let { append(it.uppercaseChar()) }
            profile.surname.firstOrNull()?.let { append(it.uppercaseChar()) }
        }.ifBlank { "?" }

        profileFields.removeAllViews()
        profileFields.addView(infoRow("Name", profile.name))
        profileFields.addView(infoRow("Surname", profile.surname))
        profileFields.addView(infoRow("Email", profile.email))
        profileFields.addView(infoRow("Phone", profile.phone))
        profileFields.addView(infoRow("Birth date", profile.birthDate))
    }

    private fun renderTests(tests: List<TestResult>) {
        testResultsContainer.removeAllViews()

        if (tests.isEmpty()) {
            testResultsContainer.addView(emptyState("No test results available"))
            return
        }

        tests.forEachIndexed { index, result ->
            testResultsContainer.addView(testCard(result, tests.size - index))
        }
    }

    private fun testCard(result: TestResult, displayNumber: Int): View {
        val card = CardView(this).apply {
            radius = 16f
            cardElevation = 0f
            setCardBackgroundColor(Color.parseColor("#0D1B2A"))
            isClickable = true
            isFocusable = true
            layoutParams = LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT
            ).apply {
                bottomMargin = dp(16)
            }
        }

        val content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(20), dp(20), dp(20), dp(20))
        }

        val details = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            visibility = View.GONE
        }

        val status = TextView(this).apply {
            text = "View details"
            setTextColor(Color.parseColor("#4DA3FF"))
            textSize = 12f
            typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
            gravity = android.view.Gravity.END
            layoutParams = LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT
            ).apply { topMargin = dp(10) }
        }

        content.addView(testCardHeader(result, displayNumber))
        content.addView(status)

        details.addView(infoRow("Duration", result.duration))
        details.addView(infoRow("Average distance", result.averageDistance.formatValue()))
        details.addView(infoRow("Go/No-Go accuracy", result.precizieGonogo.formatPercent()))

        details.addView(sectionTitle("ECG"))
        details.addView(LineChartView(this).apply {
            setData(result.ecg, Color.parseColor("#FF6B6B"))
            layoutParams = LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                dp(150)
            ).apply { topMargin = dp(8) }
        })

        details.addView(sectionTitle("SpO2"))
        details.addView(LineChartView(this).apply {
            setData(result.spo2, Color.parseColor("#2ECC71"))
            layoutParams = LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                dp(150)
            ).apply { topMargin = dp(8) }
        })

        details.addView(sectionTitle("Map"))
        details.addView(GridMapView(this).apply {
            setPoints(result.mapPoints)
            layoutParams = LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                dp(190)
            ).apply { topMargin = dp(8) }
            background = roundedBackground("#08131F")
        })

        content.addView(details)
        card.setOnClickListener {
            val expanded = details.visibility == View.VISIBLE
            details.visibility = if (expanded) View.GONE else View.VISIBLE
            status.text = if (expanded) "View details" else "Hide details"
        }

        card.addView(content)
        return card
    }

    private fun testCardHeader(result: TestResult, displayNumber: Int): View {
        return LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = android.view.Gravity.CENTER_VERTICAL

            addView(LinearLayout(context).apply {
                orientation = LinearLayout.VERTICAL
                layoutParams = LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f)

                addView(TextView(context).apply {
                    text = "Test $displayNumber"
                    setTextColor(Color.WHITE)
                    textSize = 17f
                    typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                })

                addView(TextView(context).apply {
                    text = result.dateTime.ifBlank { "No date" }
                    setTextColor(Color.parseColor("#7F8FA6"))
                    textSize = 13f
                    typeface = Typeface.create("sans-serif-light", Typeface.NORMAL)
                    layoutParams = LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.WRAP_CONTENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT
                    ).apply { topMargin = dp(4) }
                })
            })

            addView(TextView(context).apply {
                text = result.scor.formatValue()
                setTextColor(Color.WHITE)
                textSize = 18f
                typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                gravity = android.view.Gravity.END
                background = roundedBackground("#17304A")
                setPadding(dp(12), dp(8), dp(12), dp(8))
            })
        }
    }

    private fun infoRow(label: String, value: String): View {
        return LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = android.view.Gravity.CENTER_VERTICAL
            layoutParams = LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT
            ).apply { topMargin = dp(12) }

            addView(TextView(context).apply {
                text = label
                setTextColor(Color.parseColor("#7F8FA6"))
                textSize = 13f
                typeface = Typeface.create("sans-serif-light", Typeface.NORMAL)
                layoutParams = LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f)
            })

            addView(TextView(context).apply {
                text = value.ifBlank { "Not set" }
                setTextColor(Color.parseColor("#E8F0FE"))
                textSize = 13f
                typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                gravity = android.view.Gravity.END
                layoutParams = LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1.2f)
            })
        }
    }

    private fun sectionTitle(title: String): View {
        return TextView(this).apply {
            text = title
            setTextColor(Color.parseColor("#4DA3FF"))
            textSize = 12f
            typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
            layoutParams = LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT
            ).apply { topMargin = dp(20) }
        }
    }

    private fun emptyState(message: String): View {
        return TextView(this).apply {
            text = message
            setTextColor(Color.parseColor("#7F8FA6"))
            textSize = 14f
            gravity = android.view.Gravity.CENTER
            background = roundedBackground("#0D1B2A")
            setPadding(dp(20), dp(28), dp(20), dp(28))
        }
    }

    private fun showPage(page: Page) {
        val isAccount = page == Page.Account
        accountPage.visibility = if (isAccount) View.VISIBLE else View.GONE
        testsPage.visibility = if (isAccount) View.GONE else View.VISIBLE
        tabAccount.setTextColor(Color.parseColor(if (isAccount) "#FFFFFF" else "#7F8FA6"))
        tabTests.setTextColor(Color.parseColor(if (isAccount) "#7F8FA6" else "#FFFFFF"))
        tabGames.setTextColor(Color.parseColor("#7F8FA6"))
        tabAccount.background = if (isAccount) roundedBackground("#17304A") else null
        tabTests.background = if (isAccount) null else roundedBackground("#17304A")
        tabGames.background = null
        scrollContent.post { scrollContent.scrollTo(0, 0) }
    }

    private fun playEntranceAnimation() {
        brandContainer.alpha = 0f
        cardProfile.alpha = 0f
        cardProfile.translationY = 50f
        brandContainer.animate().alpha(1f).setDuration(500).setStartDelay(100).start()
        cardProfile.animate().alpha(1f).translationY(0f).setDuration(500).setStartDelay(200)
            .setInterpolator(DecelerateInterpolator(2f)).start()
    }

    private fun roundedBackground(color: String): GradientDrawable {
        return GradientDrawable().apply {
            setColor(Color.parseColor(color))
            cornerRadius = dp(12).toFloat()
        }
    }

    private fun Float?.formatValue(): String {
        return this?.let { String.format(Locale.US, "%.2f", it) } ?: "Not set"
    }

    private fun Float?.formatPercent(): String {
        return this?.let { String.format(Locale.US, "%.2f%%", it) } ?: "Not set"
    }

    private fun dp(value: Int): Int {
        return (value * resources.displayMetrics.density).toInt()
    }

    private enum class Page {
        Account,
        Tests
    }
}
