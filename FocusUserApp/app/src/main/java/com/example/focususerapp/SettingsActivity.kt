package com.example.focususerapp

import android.graphics.Color
import android.graphics.Typeface
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.View
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.cardview.widget.CardView
import androidx.core.view.WindowCompat

class SettingsActivity : AppCompatActivity() {
    private lateinit var root: LinearLayout
    private lateinit var content: LinearLayout

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        buildUi()
        AppAppearance.apply(this)
    }

    private fun buildUi() {
        val p = AppAppearance.palette(this)
        root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(p.root)
        }
        root.addView(header())

        val scroll = ScrollView(this).apply {
            isFillViewport = true
            overScrollMode = View.OVER_SCROLL_NEVER
            layoutParams = LinearLayout.LayoutParams(match, 0, 1f)
        }
        content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(24), dp(16), dp(24), dp(32))
        }
        scroll.addView(content)
        root.addView(scroll)
        setContentView(root)

        renderSettings()
    }

    private fun header(): View {
        val p = AppAppearance.palette(this)
        return LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(dp(24), dp(48), dp(24), dp(14))
            setBackgroundColor(p.root)

            addView(TextView(context).apply {
                text = AppText.get(context, "Back")
                setTextColor(p.accent)
                textSize = 14f
                typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                setPadding(dp(14), dp(8), dp(14), dp(8))
                background = AppAppearance.rounded(context, p.surfaceAlt, 24)
                setOnClickListener { finish() }
            })

            addView(TextView(context).apply {
                text = AppText.get(context, "Settings")
                setTextColor(p.text)
                textSize = 22f
                typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                gravity = Gravity.CENTER
                setShadowLayer(10f, 0f, 0f, Color.argb(120, Color.red(p.accent), Color.green(p.accent), Color.blue(p.accent)))
                layoutParams = LinearLayout.LayoutParams(0, wrap, 1f)
            })

            addView(TextView(context).apply {
                text = "Focus"
                setTextColor(p.muted)
                textSize = 13f
            })
        }
    }

    private fun renderSettings() {
        content.removeAllViews()
        content.addView(title("App settings", "Choose the theme and language used by the app."))
        content.addView(settingsCard("Appearance") {
            addView(optionRow("🌙", "Dark mode", AppSettings.isDark(context)) {
                AppSettings.setTheme(context, AppSettings.THEME_DARK)
                Toast.makeText(context, AppText.get(context, "Theme updated"), Toast.LENGTH_SHORT).show()
                refreshUi()
            })
            addView(optionRow("☀️", "Light mode", !AppSettings.isDark(context)) {
                AppSettings.setTheme(context, AppSettings.THEME_LIGHT)
                Toast.makeText(context, AppText.get(context, "Theme updated"), Toast.LENGTH_SHORT).show()
                refreshUi()
            })
        })
        content.addView(settingsCard("Language") {
            val currentLang = AppSettings.language(context)
            addView(optionRow("🇬🇧", "English", currentLang == AppSettings.LANG_EN) {
                AppSettings.setLanguage(context, AppSettings.LANG_EN)
                Toast.makeText(context, AppText.get(context, "Language updated"), Toast.LENGTH_SHORT).show()
                refreshUi()
            })
            addView(optionRow("🇷🇴", "Romanian", currentLang == AppSettings.LANG_RO) {
                AppSettings.setLanguage(context, AppSettings.LANG_RO)
                Toast.makeText(context, AppText.get(context, "Language updated"), Toast.LENGTH_SHORT).show()
                refreshUi()
            })
            addView(optionRow("🇹🇷", "Turkish", currentLang == AppSettings.LANG_TR) {
                AppSettings.setLanguage(context, AppSettings.LANG_TR)
                Toast.makeText(context, AppText.get(context, "Language updated"), Toast.LENGTH_SHORT).show()
                refreshUi()
            })
        })
    }

    private fun refreshUi() {
        buildUi()
        AppAppearance.apply(this)
    }

    private fun title(title: String, subtitle: String): View {
        val p = AppAppearance.palette(this)
        return LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { bottomMargin = dp(18) }
            addView(TextView(context).apply {
                text = AppText.get(context, title)
                setTextColor(p.text)
                textSize = 26f
                typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
            })
            addView(TextView(context).apply {
                text = AppText.get(context, subtitle)
                setTextColor(p.muted)
                textSize = 14f
                setPadding(0, dp(6), 0, 0)
            })
        }
    }

    private fun settingsCard(title: String, children: LinearLayout.() -> Unit): View {
        val p = AppAppearance.palette(this)
        return CardView(this).apply {
            radius = dp(24).toFloat()
            cardElevation = dp(8).toFloat()
            setCardBackgroundColor(p.surface)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                outlineAmbientShadowColor = p.accent
                outlineSpotShadowColor = p.accent
            }
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { bottomMargin = dp(16) }
            addView(LinearLayout(context).apply {
                orientation = LinearLayout.VERTICAL
                setPadding(dp(20), dp(18), dp(20), dp(20))
                addView(TextView(context).apply {
                    text = AppText.get(context, title)
                    setTextColor(p.text)
                    textSize = 18f
                    typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                    setPadding(0, 0, 0, dp(12))
                })
                children()
            })
        }
    }

    private fun optionRow(icon: String, label: String, selected: Boolean, onClick: () -> Unit): View {
        val p = AppAppearance.palette(this)
        val translated = AppText.get(this, label)
        return TextView(this).apply {
            text = if (selected) "$icon  $translated  ✓" else "$icon  $translated"
            setTextColor(if (selected) Color.WHITE else p.text)
            textSize = 15f
            typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
            gravity = Gravity.CENTER_VERTICAL
            background = if (selected) AppAppearance.gradientButton(context) else AppAppearance.rounded(context, p.surfaceAlt, 20)
            setPadding(dp(16), 0, dp(16), 0)
            elevation = if (selected) dp(8).toFloat() else 0f
            layoutParams = LinearLayout.LayoutParams(match, dp(54)).apply { bottomMargin = dp(10) }
            setOnClickListener { onClick() }
        }
    }

    private fun dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()
    private val match = LinearLayout.LayoutParams.MATCH_PARENT
    private val wrap = LinearLayout.LayoutParams.WRAP_CONTENT
}
