package com.example.focususerapp

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.util.AttributeSet
import android.view.View

class GridMapView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
    defStyleAttr: Int = 0
) : View(context, attrs, defStyleAttr) {

    private var points: List<Pair<Float, Float>> = emptyList()

    private val linePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#FF6B6B")
        strokeWidth = 5f
        style = Paint.Style.STROKE
        strokeJoin = Paint.Join.ROUND
        strokeCap = Paint.Cap.ROUND
    }

    private val pointPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#2ECC71")
        style = Paint.Style.FILL
    }

    private val gridPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#25384F")
        strokeWidth = 1.4f
    }

    private val emptyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#7F8FA6")
        textSize = 28f
        textAlign = Paint.Align.CENTER
    }

    fun setPoints(newPoints: List<Pair<Float, Float>>) {
        points = newPoints
        invalidate()
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val chartWidth = width.toFloat()
        val chartHeight = height.toFloat()
        val padding = 18f

        for (i in 1 until 4) {
            val x = chartWidth * i / 4f
            val y = chartHeight * i / 4f
            canvas.drawLine(x, padding, x, chartHeight - padding, gridPaint)
            canvas.drawLine(padding, y, chartWidth - padding, y, gridPaint)
        }

        if (points.isEmpty()) {
            canvas.drawText("No map data", chartWidth / 2f, chartHeight / 2f, emptyPaint)
            return
        }

        val minX = points.minOf { it.first }
        val maxX = points.maxOf { it.first }
        val minY = points.minOf { it.second }
        val maxY = points.maxOf { it.second }
        val rangeX = (maxX - minX).takeIf { it > 0f } ?: 1f
        val rangeY = (maxY - minY).takeIf { it > 0f } ?: 1f
        val usableWidth = chartWidth - padding * 2f
        val usableHeight = chartHeight - padding * 2f

        val pixelPoints = points.map { (x, y) ->
            val px = padding + ((x - minX) / rangeX) * usableWidth
            val py = chartHeight - padding - ((y - minY) / rangeY) * usableHeight
            px to py
        }

        for (i in 0 until pixelPoints.lastIndex) {
            canvas.drawLine(
                pixelPoints[i].first,
                pixelPoints[i].second,
                pixelPoints[i + 1].first,
                pixelPoints[i + 1].second,
                linePaint
            )
        }

        pixelPoints.forEach { (x, y) ->
            canvas.drawCircle(x, y, 7f, pointPaint)
        }
    }
}
