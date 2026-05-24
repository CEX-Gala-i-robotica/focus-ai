package com.example.focususerapp

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.LinearGradient
import android.graphics.Paint
import android.graphics.Path
import android.graphics.Shader
import android.util.AttributeSet
import android.view.View

class LineChartView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
    defStyleAttr: Int = 0
) : View(context, attrs, defStyleAttr) {

    private var values: List<Float> = emptyList()
    private var lineColor: Int = Color.parseColor("#00E5FF")
    
    private val linePath = Path()
    private val fillPath = Path()
    private var cachedWidth = 0
    private var cachedHeight = 0
    private var isPathDirty = true
    private var gradientShader: LinearGradient? = null

    private val gridPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        strokeWidth = 1.4f
    }

    private val linePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeWidth = 4f
        strokeCap = Paint.Cap.ROUND
        strokeJoin = Paint.Join.ROUND
    }

    private val fillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
    }

    private val emptyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        textSize = 28f
        textAlign = Paint.Align.CENTER
    }

    fun setData(points: List<Float>, color: Int = lineColor) {
        values = points
        if (lineColor != color) {
            lineColor = color
            gradientShader = null
        }
        isPathDirty = true
        invalidate()
    }

    override fun onSizeChanged(w: Int, h: Int, oldw: Int, oldh: Int) {
        super.onSizeChanged(w, h, oldw, oldh)
        cachedWidth = w
        cachedHeight = h
        gradientShader = null
        isPathDirty = true
    }

    private fun updatePaths() {
        linePath.reset()
        fillPath.reset()
        isPathDirty = false

        if (values.size < 2 || cachedWidth == 0 || cachedHeight == 0) return

        val chartWidth = cachedWidth.toFloat()
        val chartHeight = cachedHeight.toFloat()
        val horizontalPadding = 16f
        val verticalPadding = 18f

        val minValue = values.minOrNull() ?: 0f
        val maxValue = values.maxOrNull() ?: 0f
        val range = (maxValue - minValue).takeIf { it > 0f } ?: 1f
        val usableWidth = chartWidth - horizontalPadding * 2f
        val usableHeight = chartHeight - verticalPadding * 2f

        var firstX = 0f
        var lastX = 0f

        values.forEachIndexed { index, value ->
            val x = horizontalPadding + (index.toFloat() / (values.lastIndex).toFloat()) * usableWidth
            val y = verticalPadding + (1f - ((value - minValue) / range)) * usableHeight
            if (index == 0) {
                linePath.moveTo(x, y)
                fillPath.moveTo(x, y)
                firstX = x
            } else {
                linePath.lineTo(x, y)
                fillPath.lineTo(x, y)
            }
            if (index == values.lastIndex) {
                lastX = x
            }
        }

        fillPath.lineTo(lastX, usableHeight + verticalPadding)
        fillPath.lineTo(firstX, usableHeight + verticalPadding)
        fillPath.close()
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val chartWidth = width.toFloat()
        val chartHeight = height.toFloat()
        val horizontalPadding = 16f
        val verticalPadding = 18f

        val palette = AppAppearance.palette(context)

        gridPaint.color = palette.divider
        gridPaint.alpha = 50
        for (i in 1 until 4) {
            val y = chartHeight * i / 4f
            canvas.drawLine(horizontalPadding, y, chartWidth - horizontalPadding, y, gridPaint)
        }

        if (values.size < 2) {
            emptyPaint.color = palette.muted
            canvas.drawText(AppText.get(context, "No chart data"), chartWidth / 2f, chartHeight / 2f, emptyPaint)
            return
        }

        if (isPathDirty) {
            updatePaths()
        }

        if (gradientShader == null && chartHeight > 0) {
            gradientShader = LinearGradient(
                0f, verticalPadding,
                0f, chartHeight - verticalPadding,
                Color.argb(85, Color.red(lineColor), Color.green(lineColor), Color.blue(lineColor)),
                Color.TRANSPARENT,
                Shader.TileMode.CLAMP
            )
            fillPaint.shader = gradientShader
        }

        canvas.drawPath(fillPath, fillPaint)

        linePaint.color = lineColor
        linePaint.setShadowLayer(14f, 0f, 0f, lineColor)
        setLayerType(LAYER_TYPE_SOFTWARE, linePaint)
        canvas.drawPath(linePath, linePaint)
    }
}
