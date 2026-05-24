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

    private var xCoords = FloatArray(0)
    private var yCoords = FloatArray(0)
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
        points = newPoints
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
            return
        }

        if (xCoords.size != points.size) {
            xCoords = FloatArray(points.size)
            yCoords = FloatArray(points.size)
        }

        val chartWidth = cachedWidth.toFloat()
        val chartHeight = cachedHeight.toFloat()
        val padding = 18f

        val minX = points.minOf { it.first }
        val maxX = points.maxOf { it.first }
        val minY = points.minOf { it.second }
        val maxY = points.maxOf { it.second }
        val rangeX = (maxX - minX).takeIf { it > 0f } ?: 1f
        val rangeY = (maxY - minY).takeIf { it > 0f } ?: 1f
        val usableWidth = chartWidth - padding * 2f
        val usableHeight = chartHeight - padding * 2f

        points.forEachIndexed { index, (x, y) ->
            xCoords[index] = padding + ((x - minX) / rangeX) * usableWidth
            yCoords[index] = chartHeight - padding - ((y - minY) / rangeY) * usableHeight
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

        linePaint.setShadowLayer(14f, 0f, 0f, linePaint.color)
        pointPaint.setShadowLayer(12f, 0f, 0f, pointPaint.color)
        setLayerType(LAYER_TYPE_SOFTWARE, linePaint)

        for (i in 0 until points.lastIndex) {
            canvas.drawLine(xCoords[i], yCoords[i], xCoords[i + 1], yCoords[i + 1], linePaint)
        }

        for (i in points.indices) {
            canvas.drawCircle(xCoords[i], yCoords[i], 7f, pointPaint)
        }
    }
}
