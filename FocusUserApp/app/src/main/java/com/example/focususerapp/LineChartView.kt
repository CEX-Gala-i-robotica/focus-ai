package com.example.focususerapp

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import android.util.AttributeSet
import android.view.View

class LineChartView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
    defStyleAttr: Int = 0
) : View(context, attrs, defStyleAttr) {

    private var values: List<Float> = emptyList()
    private var lineColor: Int = Color.parseColor("#4DA3FF")

    private val gridPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#25384F")
        strokeWidth = 1.4f
    }

    private val linePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeWidth = 4f
        strokeCap = Paint.Cap.ROUND
        strokeJoin = Paint.Join.ROUND
    }

    private val emptyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#7F8FA6")
        textSize = 28f
        textAlign = Paint.Align.CENTER
    }

    fun setData(points: List<Float>, color: Int = lineColor) {
        values = points
        lineColor = color
        invalidate()
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val chartWidth = width.toFloat()
        val chartHeight = height.toFloat()
        val horizontalPadding = 16f
        val verticalPadding = 18f

        for (i in 1 until 4) {
            val y = chartHeight * i / 4f
            canvas.drawLine(horizontalPadding, y, chartWidth - horizontalPadding, y, gridPaint)
        }

        if (values.size < 2) {
            canvas.drawText("No chart data", chartWidth / 2f, chartHeight / 2f, emptyPaint)
            return
        }

        val minValue = values.minOrNull() ?: 0f
        val maxValue = values.maxOrNull() ?: 0f
        val range = (maxValue - minValue).takeIf { it > 0f } ?: 1f
        val usableWidth = chartWidth - horizontalPadding * 2f
        val usableHeight = chartHeight - verticalPadding * 2f

        val path = Path()
        values.forEachIndexed { index, value ->
            val x = horizontalPadding + (index.toFloat() / (values.lastIndex).toFloat()) * usableWidth
            val y = verticalPadding + (1f - ((value - minValue) / range)) * usableHeight
            if (index == 0) path.moveTo(x, y) else path.lineTo(x, y)
        }

        linePaint.color = lineColor
        canvas.drawPath(path, linePaint)
    }
}
