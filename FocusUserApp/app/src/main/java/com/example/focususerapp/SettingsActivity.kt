package com.example.focususerapp

import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.View
import android.widget.GridLayout
import android.widget.HorizontalScrollView
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

    private val themeSwatches = listOf(
        "#00E5FF", "#0055FF", "#7C3DFF", "#FF3366", "#FF7A59", "#FFB020",
        "#00FF66", "#00B8D9", "#111827", "#FFFFFF", "#12141D", "#EEF3FF"
    )

    private val surfaceSwatches = listOf(
        "#12141D", "#1A1D29", "#202433", "#06100B", "#0C1C13", "#190F18",
        "#261723", "#061821", "#0B2430", "#EEF3FF", "#FFFFFF", "#E2E9F8"
    )

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
            addView(optionRow("Moon", "Dark mode", AppSettings.isDark(context) && !AppSettings.customThemeEnabled(context)) {
                AppSettings.setCustomThemeEnabled(context, false)
                AppSettings.setTheme(context, AppSettings.THEME_DARK)
                themeUpdated()
            })
            addView(optionRow("Sun", "Light mode", !AppSettings.isDark(context) && !AppSettings.customThemeEnabled(context)) {
                AppSettings.setCustomThemeEnabled(context, false)
                AppSettings.setTheme(context, AppSettings.THEME_LIGHT)
                themeUpdated()
            })
            addView(optionRow("Mix", "Custom theme", AppSettings.customThemeEnabled(context)) {
                AppSettings.setCustomThemeEnabled(context, true)
                themeUpdated()
            })
        })
        content.addView(themePreviewCard())
        content.addView(settingsCard("Theme presets") {
            addView(presetScroller())
        })
        content.addView(settingsCard("Custom colors") {
            addView(colorRole("Accent", "accent", themeSwatches))
            addView(colorRole("Accent gradient", "accentEnd", themeSwatches))
            addView(colorRole("Background", "root", surfaceSwatches))
            addView(colorRole("Cards", "surface", surfaceSwatches))
            addView(colorRole("Inputs", "surfaceAlt", surfaceSwatches))
            addView(colorRole("Text", "text", listOf("#FFFFFF", "#F8FAFC", "#EAFBFF", "#101423", "#111827", "#06100B")))
            addView(colorRole("Muted text", "muted", listOf("#8A8F9E", "#9AA3B8", "#91B8C8", "#D6A8B8", "#667085", "#475467")))
            addView(colorRole("Success", "positive", listOf("#00FF66", "#2DFFB3", "#72F2A1", "#00B85C", "#94FFB0")))
            addView(colorRole("Danger", "negative", listOf("#FF3366", "#FF477E", "#FF4B6E", "#E42355", "#FF6B6B")))
            addView(resetButton())
        })
        content.addView(settingsCard("Language") {
            val currentLang = AppSettings.language(context)
            addView(optionRow("EN", "English", currentLang == AppSettings.LANG_EN) {
                AppSettings.setLanguage(context, AppSettings.LANG_EN)
                languageUpdated()
            })
            addView(optionRow("RO", "Romanian", currentLang == AppSettings.LANG_RO) {
                AppSettings.setLanguage(context, AppSettings.LANG_RO)
                languageUpdated()
            })
            addView(optionRow("TR", "Turkish", currentLang == AppSettings.LANG_TR) {
                AppSettings.setLanguage(context, AppSettings.LANG_TR)
                languageUpdated()
            })
        })
    }

    private fun themeUpdated() {
        Toast.makeText(this, AppText.get(this, "Theme updated"), Toast.LENGTH_SHORT).show()
        refreshUi()
    }

    private fun languageUpdated() {
        Toast.makeText(this, AppText.get(this, "Language updated"), Toast.LENGTH_SHORT).show()
        refreshUi()
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

    private fun themePreviewCard(): View {
        val p = AppAppearance.palette(this)
        return CardView(this).apply {
            radius = dp(24).toFloat()
            cardElevation = dp(10).toFloat()
            setCardBackgroundColor(p.surface)
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { bottomMargin = dp(16) }
            addView(LinearLayout(context).apply {
                orientation = LinearLayout.VERTICAL
                background = previewBackground(p)
                setPadding(dp(20), dp(20), dp(20), dp(20))
                addView(TextView(context).apply {
                    text = AppText.get(context, "Theme preview")
                    setTextColor(p.text)
                    textSize = 20f
                    typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                })
                addView(TextView(context).apply {
                    text = AppText.get(context, "Your colors update instantly across the app.")
                    setTextColor(p.muted)
                    textSize = 13f
                    setPadding(0, dp(4), 0, dp(16))
                })
                addView(LinearLayout(context).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    addView(View(context).apply {
                        background = AppAppearance.oval(context, p.accent)
                        layoutParams = LinearLayout.LayoutParams(dp(42), dp(42)).apply { rightMargin = dp(12) }
                    })
                    addView(LinearLayout(context).apply {
                        orientation = LinearLayout.VERTICAL
                        layoutParams = LinearLayout.LayoutParams(0, wrap, 1f)
                        addView(TextView(context).apply {
                            text = AppText.get(context, "Primary action")
                            setTextColor(p.text)
                            textSize = 15f
                            typeface = Typeface.DEFAULT_BOLD
                        })
                        addView(TextView(context).apply {
                            text = "#${Integer.toHexString(p.accent).takeLast(6).uppercase()}"
                            setTextColor(p.muted)
                            textSize = 12f
                        })
                    })
                    addView(TextView(context).apply {
                        text = AppText.get(context, "Play")
                        setTextColor(Color.WHITE)
                        textSize = 13f
                        gravity = Gravity.CENTER
                        background = AppAppearance.gradientButton(context)
                        layoutParams = LinearLayout.LayoutParams(dp(88), dp(42))
                    })
                })
            })
        }
    }

    private fun presetScroller(): View {
        val selected = AppSettings.customTheme(this).preset
        return HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            overScrollMode = View.OVER_SCROLL_NEVER
            addView(LinearLayout(context).apply {
                orientation = LinearLayout.HORIZONTAL
                AppSettings.presets.forEach { preset ->
                    addView(presetChip(preset, selected == preset.id))
                }
            })
        }
    }

    private fun presetChip(preset: AppSettings.ThemePreset, selected: Boolean): View {
        return LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.CENTER
            setPadding(dp(12), dp(12), dp(12), dp(10))
            background = AppAppearance.rounded(context, preset.surface, 18, if (selected) preset.accent else preset.divider)
            layoutParams = LinearLayout.LayoutParams(dp(132), dp(116)).apply { rightMargin = dp(10) }
            isClickable = true
            isFocusable = true
            addView(LinearLayout(context).apply {
                orientation = LinearLayout.HORIZONTAL
                gravity = Gravity.CENTER
                addView(colorDot(preset.root))
                addView(colorDot(preset.surfaceAlt))
                addView(colorDot(preset.accent))
                addView(colorDot(preset.accentEnd))
            })
            addView(TextView(context).apply {
                text = preset.label
                setTextColor(preset.text)
                textSize = 13f
                typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                gravity = Gravity.CENTER
                setPadding(0, dp(10), 0, 0)
            })
            setOnClickListener {
                AppSettings.applyPreset(context, preset.id)
                themeUpdated()
            }
        }
    }

    private fun colorRole(label: String, role: String, colors: List<String>): View {
        val p = AppAppearance.palette(this)
        return LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { bottomMargin = dp(14) }
            addView(TextView(context).apply {
                text = AppText.get(context, label)
                setTextColor(p.text)
                textSize = 13f
                typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                setPadding(0, 0, 0, dp(8))
            })
            addView(GridLayout(context).apply {
                columnCount = 6
                colors.forEach { hex -> addView(colorSwatch(hex, role)) }
            })
        }
    }

    private fun colorSwatch(hex: String, role: String): View {
        val color = Color.parseColor(hex)
        val p = AppAppearance.palette(this)
        return TextView(this).apply {
            background = AppAppearance.oval(context, color, p.divider)
            isClickable = true
            isFocusable = true
            layoutParams = GridLayout.LayoutParams().apply {
                width = dp(38)
                height = dp(38)
                setMargins(0, 0, dp(10), dp(10))
            }
            setOnClickListener {
                AppSettings.setCustomColor(context, role, color)
                refreshUi()
            }
        }
    }

    private fun resetButton(): View {
        return TextView(this).apply {
            text = AppText.get(context, "Reset custom theme")
            setTextColor(Color.WHITE)
            textSize = 14f
            typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
            gravity = Gravity.CENTER
            background = AppAppearance.gradientButton(context)
            layoutParams = LinearLayout.LayoutParams(match, dp(50)).apply { topMargin = dp(4) }
            setOnClickListener {
                AppSettings.resetCustomTheme(context)
                themeUpdated()
            }
        }
    }

    private fun optionRow(icon: String, label: String, selected: Boolean, onClick: () -> Unit): View {
        val p = AppAppearance.palette(this)
        val translated = AppText.get(this, label)
        return TextView(this).apply {
            text = if (selected) "$icon  $translated  ${AppText.get(context, "selected")}" else "$icon  $translated"
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

    private fun colorDot(color: Int): View = View(this).apply {
        background = AppAppearance.oval(context, color)
        layoutParams = LinearLayout.LayoutParams(dp(18), dp(18)).apply { rightMargin = dp(4) }
    }

    private fun previewBackground(p: AppAppearance.Palette): GradientDrawable {
        return GradientDrawable(
            GradientDrawable.Orientation.TL_BR,
            intArrayOf(p.surface, p.surfaceAlt)
        ).apply { cornerRadius = dp(24).toFloat() }
    }

    private fun dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()
    private val match = LinearLayout.LayoutParams.MATCH_PARENT
    private val wrap = LinearLayout.LayoutParams.WRAP_CONTENT
}
