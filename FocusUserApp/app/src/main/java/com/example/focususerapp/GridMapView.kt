package com.example.focususerapp

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import android.util.AttributeSet
import android.view.View

class GridMapView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
    defStyleAttr: Int = 0
) : View(context, attrs, defStyleAttr) {

    private companion object {
        const val MAX_RENDER_POINTS = 500
        const val MAX_DRAWN_DOTS = 160
    }

    private var points: List<Pair<Float, Float>> = emptyList()

    private var xCoords = FloatArray(0)
    private var yCoords = FloatArray(0)
    private val routePath = Path()
    private var isCoordsDirty = true
    private var cachedWidth = 0
    private var cachedHeight = 0

    private val linePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#FF3366")
        strokeWidth = 5f
        style = Paint.Style.STROKE
        strokeJoin = Paint.Join.ROUND
        strokeCap = Paint.Cap.ROUND
    }

    private val pointPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.parseColor("#00FF66")
        style = Paint.Style.FILL
    }

    private val gridPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        strokeWidth = 1.4f
    }

    private val emptyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        textSize = 28f
        textAlign = Paint.Align.CENTER
    }

    fun setPoints(newPoints: List<Pair<Float, Float>>) {
        points = newPoints.downsample(MAX_RENDER_POINTS)
        isCoordsDirty = true
        invalidate()
    }

    override fun onSizeChanged(w: Int, h: Int, oldw: Int, oldh: Int) {
        super.onSizeChanged(w, h, oldw, oldh)
        cachedWidth = w
        cachedHeight = h
        isCoordsDirty = true
    }

    private fun updateCoordinates() {
        isCoordsDirty = false
        if (points.isEmpty() || cachedWidth == 0 || cachedHeight == 0) {
            xCoords = FloatArray(0)
            yCoords = FloatArray(0)
            routePath.reset()
            return
        }

        if (xCoords.size != points.size) {
            xCoords = FloatArray(points.size)
            yCoords = FloatArray(points.size)
        }

        val chartWidth = cachedWidth.toFloat()
        val chartHeight = cachedHeight.toFloat()
        val padding = 18f

        var minX = Float.MAX_VALUE
        var maxX = -Float.MAX_VALUE
        var minY = Float.MAX_VALUE
        var maxY = -Float.MAX_VALUE
        for ((x, y) in points) {
            if (x < minX) minX = x
            if (x > maxX) maxX = x
            if (y < minY) minY = y
            if (y > maxY) maxY = y
        }
        val rangeX = (maxX - minX).takeIf { it > 0f } ?: 1f
        val rangeY = (maxY - minY).takeIf { it > 0f } ?: 1f
        val usableWidth = chartWidth - padding * 2f
        val usableHeight = chartHeight - padding * 2f

        routePath.reset()
        points.forEachIndexed { index, (x, y) ->
            xCoords[index] = padding + ((x - minX) / rangeX) * usableWidth
            yCoords[index] = chartHeight - padding - ((y - minY) / rangeY) * usableHeight
            if (index == 0) {
                routePath.moveTo(xCoords[index], yCoords[index])
            } else {
                routePath.lineTo(xCoords[index], yCoords[index])
            }
        }
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val chartWidth = width.toFloat()
        val chartHeight = height.toFloat()
        val padding = 18f

        val palette = AppAppearance.palette(context)

        gridPaint.color = palette.divider
        gridPaint.alpha = 50
        for (i in 1 until 4) {
            val x = chartWidth * i / 4f
            val y = chartHeight * i / 4f
            canvas.drawLine(x, padding, x, chartHeight - padding, gridPaint)
            canvas.drawLine(padding, y, chartWidth - padding, y, gridPaint)
        }

        if (points.isEmpty()) {
            emptyPaint.color = palette.muted
            canvas.drawText(AppText.get(context, "No map data"), chartWidth / 2f, chartHeight / 2f, emptyPaint)
            return
        }

        if (isCoordsDirty) {
            updateCoordinates()
        }

        canvas.drawPath(routePath, linePaint)

        val dotStep = (points.size / MAX_DRAWN_DOTS).coerceAtLeast(1)
        for (i in points.indices step dotStep) {
            canvas.drawCircle(xCoords[i], yCoords[i], 7f, pointPaint)
        }
    }

    private fun List<Pair<Float, Float>>.downsample(maxPoints: Int): List<Pair<Float, Float>> {
        if (size <= maxPoints) return this
        val sampled = ArrayList<Pair<Float, Float>>(maxPoints)
        val step = (size - 1).toFloat() / (maxPoints - 1).toFloat()
        repeat(maxPoints) { index ->
            sampled.add(this[(index * step).toInt().coerceAtMost(lastIndex)])
        }
        return sampled
    }
}
