package com.example.focususerapp

import android.app.Activity
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Build
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.EditText
import android.widget.TextView
import androidx.cardview.widget.CardView

object AppAppearance {
    data class Palette(
        val root: Int,
        val surface: Int,
        val surfaceAlt: Int,
        val text: Int,
        val muted: Int,
        val accent: Int,
        val accentEnd: Int,
        val positive: Int,
        val negative: Int,
        val divider: Int
    )

    fun palette(context: android.content.Context): Palette {
        return if (AppSettings.isDark(context)) {
            Palette(
                root = Color.parseColor("#12141D"),
                surface = Color.parseColor("#1A1D29"),
                surfaceAlt = Color.parseColor("#202433"),
                text = Color.WHITE,
                muted = Color.parseColor("#8A8F9E"),
                accent = Color.parseColor("#00E5FF"),
                accentEnd = Color.parseColor("#0055FF"),
                positive = Color.parseColor("#00FF66"),
                negative = Color.parseColor("#FF3366"),
                divider = Color.parseColor("#263044")
            )
        } else {
            Palette(
                root = Color.parseColor("#EEF3FF"),
                surface = Color.parseColor("#FFFFFF"),
                surfaceAlt = Color.parseColor("#E2E9F8"),
                text = Color.parseColor("#101423"),
                muted = Color.parseColor("#667085"),
                accent = Color.parseColor("#0055FF"),
                accentEnd = Color.parseColor("#00B8D9"),
                positive = Color.parseColor("#00B85C"),
                negative = Color.parseColor("#E42355"),
                divider = Color.parseColor("#D7DEEC")
            )
        }
    }

    fun apply(activity: Activity) {
        val p = palette(activity)
        activity.window.statusBarColor = Color.TRANSPARENT
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            activity.window.navigationBarColor = p.root
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            activity.window.decorView.outlineAmbientShadowColor = p.accent
            activity.window.decorView.outlineSpotShadowColor = p.accent
        }
        val root = activity.window.decorView.rootView
        val content = activity.findViewById<ViewGroup>(android.R.id.content)
        if (content.childCount > 0) content.getChildAt(0).setBackgroundColor(p.root)
        tintTree(root, p)
        localizeTree(activity, root)
    }

    fun gradientButton(context: android.content.Context): GradientDrawable {
        val p = palette(context)
        return GradientDrawable(
            GradientDrawable.Orientation.TL_BR,
            intArrayOf(p.accent, p.accentEnd)
        ).apply {
            cornerRadius = dp(context, 24).toFloat()
        }
    }

    fun rounded(context: android.content.Context, color: Int, radius: Int = 24, strokeColor: Int? = null): GradientDrawable {
        return GradientDrawable().apply {
            setColor(color)
            cornerRadius = dp(context, radius).toFloat()
            if (strokeColor != null) setStroke(dp(context, 1), strokeColor)
        }
    }

    fun oval(context: android.content.Context, color: Int, strokeColor: Int? = null): GradientDrawable {
        return GradientDrawable().apply {
            shape = GradientDrawable.OVAL
            setColor(color)
            if (strokeColor != null) setStroke(dp(context, 2), strokeColor)
        }
    }

    private fun tintTree(view: View, p: Palette) {
        when (view) {
            else -> {
                val name = runCatching { view.resources.getResourceEntryName(view.id) }.getOrNull()
                if (name == "gradientOverlay") {
                    view.background = GradientDrawable(
                        GradientDrawable.Orientation.TL_BR,
                        intArrayOf(p.surfaceAlt, p.root)
                    )
                }
                if (name != null && name.startsWith("inputBackground")) {
                    view.background = rounded(view.context, p.surfaceAlt, 24, p.divider)
                }
                if (name == "tabContainer") {
                    view.background = rounded(view.context, p.surface, 24)
                }
                if (name == "avatarBackground") {
                    view.background = oval(view.context, p.surfaceAlt, p.accent)
                }
                if (name == "btnSignOut") {
                    view.background = rounded(view.context, p.surfaceAlt, 24, p.divider)
                }
                if (name == "btnGoogle") {
                    view.background = rounded(view.context, p.surfaceAlt, 24, p.divider)
                }
                if (name == "accentCircle") {
                    view.background = oval(view.context, p.accent)
                    view.alpha = if (AppSettings.isDark(view.context)) 0.15f else 0.08f
                }
            }
        }

        when (view) {
            is CardView -> {
                view.radius = dp(view.context, 24).toFloat()
                view.cardElevation = dp(view.context, 8).toFloat()
                view.setCardBackgroundColor(p.surface)
            }
            is Button -> {
                view.setTextColor(Color.WHITE)
                view.typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                view.background = gradientButton(view.context)
                view.elevation = dp(view.context, 8).toFloat()
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                    view.outlineAmbientShadowColor = p.accent
                    view.outlineSpotShadowColor = p.accent
                }
            }
            is EditText -> {
                view.setTextColor(p.text)
                view.setHintTextColor(p.muted)
            }
            is TextView -> {
                val current = view.currentTextColor
                val accentLike = near(current, Color.parseColor("#00E5FF")) || near(current, Color.parseColor("#0055FF"))
                val mutedLike = near(current, Color.parseColor("#8A8F9E")) || near(current, Color.parseColor("#667085"))
                view.setTextColor(
                    when {
                        accentLike -> p.accent
                        mutedLike -> p.muted
                        else -> p.text
                    }
                )
                if (view.textSize >= 20f * view.resources.displayMetrics.scaledDensity) {
                    if (AppSettings.isDark(view.context)) {
                        view.setShadowLayer(10f, 0f, 0f, Color.argb(120, Color.red(p.accent), Color.green(p.accent), Color.blue(p.accent)))
                    } else {
                        view.setShadowLayer(0f, 0f, 0f, Color.TRANSPARENT)
                    }
                }
            }
        }

        if (view is ViewGroup) {
            if (view.parent == null) view.setBackgroundColor(p.root)
            for (i in 0 until view.childCount) tintTree(view.getChildAt(i), p)
        }
    }

    private fun localizeTree(activity: Activity, view: View) {
        if (view is EditText) {
            val hint = view.hint?.toString().orEmpty()
            if (hint.isNotBlank()) view.hint = AppText.get(activity, hint)
        }
        if (view is TextView) {
            val original = view.text?.toString().orEmpty()
            if (original.isNotBlank()) view.text = AppText.get(activity, original)
        }
        if (view is ViewGroup) {
            for (i in 0 until view.childCount) localizeTree(activity, view.getChildAt(i))
        }
    }

    private fun near(a: Int, b: Int): Boolean {
        return kotlin.math.abs(Color.red(a) - Color.red(b)) < 35 &&
            kotlin.math.abs(Color.green(a) - Color.green(b)) < 35 &&
            kotlin.math.abs(Color.blue(a) - Color.blue(b)) < 35
    }

    private fun dp(context: android.content.Context, value: Int): Int {
        return (value * context.resources.displayMetrics.density).toInt()
    }
}
