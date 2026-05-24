package com.example.focususerapp

import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.text.InputType
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.EditText
import android.widget.FrameLayout
import android.widget.GridLayout
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.cardview.widget.CardView
import androidx.core.view.WindowCompat
import java.util.Locale
import kotlin.math.max
import kotlin.math.min
import kotlin.math.round
import kotlin.random.Random

class GamesActivity : AppCompatActivity() {

    private val repository = PatientRepository()
    private val handler = Handler(Looper.getMainLooper())
    private lateinit var content: LinearLayout
    private var ticker: Runnable? = null
    private var activeGame = false
    private var gameStartedAt = 0L

    private val colors = listOf(
        ColorChoice("Red", "#FF3366", "RED"),
        ColorChoice("Green", "#00FF66", "GREEN"),
        ColorChoice("Blue", "#00E5FF", "BLUE"),
        ColorChoice("Yellow", "#EAB308", "YELLOW"),
        ColorChoice("Orange", "#FFB020", "ORANGE"),
        ColorChoice("Purple", "#7C3DFF", "PURPLE")
    )

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        window.statusBarColor = Color.TRANSPARENT
        window.decorView.systemUiVisibility =
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE or View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN

        val p = AppAppearance.palette(this)
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(p.root)
        }
        root.addView(header())

        val scroll = ScrollView(this).apply {
            overScrollMode = View.OVER_SCROLL_NEVER
            isFillViewport = true
            layoutParams = LinearLayout.LayoutParams(match, 0, 1f)
        }
        content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(24), dp(16), dp(24), dp(32))
        }
        scroll.addView(content)
        root.addView(scroll)
        setContentView(root)

        showMenu()
    }

    override fun onDestroy() {
        stopTicker()
        handler.removeCallbacksAndMessages(null)
        super.onDestroy()
    }

    override fun onBackPressed() {
        if (activeGame) showMenu() else super.onBackPressed()
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
                background = rounded(p.surfaceAlt, 24)
                setOnClickListener {
                    if (activeGame) showMenu() else finish()
                }
            })

            addView(TextView(context).apply {
                text = AppText.get(context, "Games")
                setTextColor(p.text)
                textSize = 21f
                typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                gravity = Gravity.CENTER
                layoutParams = LinearLayout.LayoutParams(0, wrap, 1f)
            })

            addView(TextView(context).apply {
                text = "Focus"
                setTextColor(p.muted)
                textSize = 13f
            })
        }
    }

    private fun animateChildrenEntrance() {
        for (i in 0 until content.childCount) {
            val child = content.getChildAt(i)
            child.alpha = 0f
            child.translationY = 40f
            child.animate()
                .alpha(1f)
                .translationY(0f)
                .setDuration(400)
                .setStartDelay(i * 45L)
                .setInterpolator(android.view.animation.DecelerateInterpolator(1.5f))
                .start()
        }
    }

    private fun showMenu() {
        activeGame = false
        stopTicker()
        handler.removeCallbacksAndMessages(null)
        content.removeAllViews()

        content.addView(title("Choose a game", "Play quick cognitive exercises and save every result in Firebase."))
        
        addGameCard("Quick Math", "Solve operations in 60 seconds.", "#00E5FF", "🧮") {
            showDifficulty("Quick Math", listOf("Easy", "Medium", "Hard")) { startQuickMath(it) }
        }
        addGameCard("Memory", "Find all matching card pairs.", "#7C3DFF", "🧠") {
            showDifficulty("Memory", listOf("Easy", "Medium", "Hard")) { startMemory(it) }
        }
        addGameCard("Sequences", "Watch and repeat the growing sequence.", "#00FF66", "🔢") {
            showDifficulty("Sequences", listOf("Easy", "Medium", "Hard")) { startSequences(it) }
        }
        addGameCard("Stroop Test", "Pick the text color, not the word.", "#FF3366", "🎨") {
            showDifficulty("Stroop Test", listOf("Easy", "Medium", "Hard")) { startStroop(it) }
        }
        addGameCard("Visual Search", "Find the odd character before time runs out.", "#FFB020", "🔍") {
            showDifficulty("Visual Search", listOf("Easy", "Medium", "Hard")) { startVisualSearch(it) }
        }

        content.addView(section("History"))
        val historyContainer = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        content.addView(historyContainer)
        
        // Initial entrance animation
        animateChildrenEntrance()

        repository.loadCurrentGameResults(
            onSuccess = { results ->
                historyContainer.removeAllViews()
                if (results.isEmpty()) {
                    historyContainer.addView(emptyState("No games played yet"))
                } else {
                    results.take(12).forEach { historyContainer.addView(historyCard(it)) }
                }
                
                // Animate history cards sliding in beautifully
                for (i in 0 until historyContainer.childCount) {
                    val child = historyContainer.getChildAt(i)
                    child.alpha = 0f
                    child.translationY = 30f
                    child.animate()
                        .alpha(1f)
                        .translationY(0f)
                        .setDuration(350)
                        .setStartDelay(i * 30L)
                        .setInterpolator(android.view.animation.DecelerateInterpolator(1.2f))
                        .start()
                }
            },
            onError = { 
                historyContainer.removeAllViews()
                historyContainer.addView(emptyState("Could not load game history")) 
            }
        )
    }

    private fun addGameCard(name: String, subtitle: String, accent: String, iconStr: String, onClick: () -> Unit) {
        val p = AppAppearance.palette(this)
        content.addView(CardView(this).apply {
            radius = dp(24).toFloat()
            cardElevation = dp(8).toFloat()
            setCardBackgroundColor(p.surface)
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { bottomMargin = dp(14) }
            isClickable = true
            setOnClickListener { onClick() }
            
            addView(LinearLayout(context).apply {
                orientation = LinearLayout.HORIZONTAL
                gravity = Gravity.CENTER_VERTICAL
                setPadding(dp(18), dp(18), dp(18), dp(18))
                
                // Emoji Icon View
                addView(FrameLayout(context).apply {
                    background = rounded(accent, 14)
                    layoutParams = LinearLayout.LayoutParams(dp(44), dp(44)).apply { rightMargin = dp(14) }
                    
                    addView(TextView(context).apply {
                        text = iconStr
                        textSize = 20f
                        gravity = Gravity.CENTER
                        layoutParams = FrameLayout.LayoutParams(match, match)
                    })
                })

                addView(LinearLayout(context).apply {
                    orientation = LinearLayout.VERTICAL
                    layoutParams = LinearLayout.LayoutParams(0, wrap, 1f)
                    
                    addView(TextView(context).apply {
                        text = AppText.get(context, name)
                        setTextColor(p.text)
                        textSize = 17f
                        typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                    })
                    
                    addView(TextView(context).apply {
                        text = AppText.get(context, subtitle)
                        setTextColor(p.muted)
                        textSize = 13f
                        setPadding(0, dp(4), 0, 0)
                    })
                })

                addView(TextView(context).apply {
                    text = AppText.get(context, "Play")
                    setTextColor(p.accent)
                    textSize = 13f
                    typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                })
            })
        })
    }

    private fun showDifficulty(game: String, options: List<String>, start: (String) -> Unit) {
        activeGame = true
        stopTicker()
        content.removeAllViews()
        val p = AppAppearance.palette(this)
        content.addView(title(game, "Choose difficulty"))
        options.forEach { diff ->
            content.addView(Button(this).apply {
                text = AppText.get(context, diff)
                textSize = 16f
                setTextColor(p.text)
                background = rounded(p.surfaceAlt, 24)
                layoutParams = LinearLayout.LayoutParams(match, dp(56)).apply { bottomMargin = dp(12) }
                setOnClickListener { start(diff) }
            })
        }
        animateChildrenEntrance()
    }

    private fun startQuickMath(difficulty: String) {
        activeGame = true
        content.removeAllViews()
        gameStartedAt = now()
        val p = AppAppearance.palette(this)
        val durationSec = 60
        var secondsLeft = durationSec
        var score = 0
        var correct = 0
        var wrong = 0
        var streak = 0
        var bestStreak = 0
        var answer = 0

        val scoreText = statText("Score: 0")
        val timerText = statText("60s")
        
        val question = TextView(this).apply {
            setTextColor(p.text)
            textSize = 44f
            typeface = Typeface.DEFAULT_BOLD
            gravity = Gravity.CENTER
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { topMargin = dp(36); bottomMargin = dp(24) }
        }
        
        val input = EditText(this).apply {
            inputType = InputType.TYPE_CLASS_NUMBER or InputType.TYPE_NUMBER_FLAG_SIGNED
            textSize = 24f
            gravity = Gravity.CENTER
            setTextColor(p.text)
            setHintTextColor(p.muted)
            hint = AppText.get(this@GamesActivity, "Answer")
            background = rounded(p.surface, 24, p.accent)
            setPadding(dp(16), 0, dp(16), 0)
            layoutParams = LinearLayout.LayoutParams(match, dp(60)).apply { bottomMargin = dp(14) }
        }
        
        val feedback = TextView(this).apply {
            setTextColor(p.muted)
            textSize = 15f
            gravity = Gravity.CENTER
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { bottomMargin = dp(14) }
        }

        fun nextQuestion() {
            val maxVal = when (difficulty) {
                "Easy" -> 20
                "Medium" -> 50
                else -> 100
            }
            val ops = when (difficulty) {
                "Easy" -> listOf("+", "-")
                "Medium" -> listOf("+", "-", "x")
                else -> listOf("+", "-", "x", "/")
            }
            val op = ops.random()
            val a: Int
            val b: Int
            if (op == "/") {
                val divisor = Random.nextInt(2, max(3, maxVal / 4))
                val multiplier = Random.nextInt(2, max(3, maxVal / divisor))
                a = divisor * multiplier
                b = divisor
            } else if (op == "-") {
                a = Random.nextInt(1, maxVal + 1)
                b = Random.nextInt(1, a + 1)
            } else {
                a = Random.nextInt(1, maxVal + 1)
                b = Random.nextInt(1, if (op == "x") min(maxVal, 12) + 1 else maxVal + 1)
            }
            answer = when (op) {
                "+" -> a + b
                "-" -> a - b
                "x" -> a * b
                else -> a / b
            }
            question.text = "$a  $op  $b"
            input.setText("")
        }

        fun finishGame() {
            stopTicker()
            val total = correct + wrong
            val accuracy = if (total > 0) correct.toDouble() / total else 0.0
            val finalScore = min(100.0, accuracy * 60.0 + correct * 1.2 + bestStreak * 0.5)
            saveAndShowResult(
                "Quick Math",
                difficulty,
                finalScore,
                mapOf("correct" to correct, "wrong" to wrong, "streak" to bestStreak),
                "${AppText.get(this, "Correct")}: $correct  ${AppText.get(this, "Wrong")}: $wrong  ${AppText.get(this, "Best streak")}: $bestStreak"
            )
        }

        content.addView(title("Quick Math", "Solve as many operations as possible."))
        content.addView(row(scoreText, timerText))
        content.addView(question)
        content.addView(input)
        content.addView(primaryButton("Submit") {
            val typed = input.text.toString().toIntOrNull() ?: return@primaryButton
            if (typed == answer) {
                correct++
                streak++
                bestStreak = max(bestStreak, streak)
                score += 10 + if (streak >= 5) 5 else 0 + if (streak >= 10) 5 else 0
                feedback.text = AppText.get(this, "Correct")
                feedback.setTextColor(p.positive)
            } else {
                wrong++
                streak = 0
                score = max(0, score - 3)
                feedback.text = "${AppText.get(this, "Wrong. Answer:")} $answer"
                feedback.setTextColor(p.negative)
            }
            scoreText.text = "${AppText.get(this, "Score")}: $score"
            nextQuestion()
        })
        content.addView(feedback)
        
        nextQuestion()
        animateChildrenEntrance()

        startTicker {
            secondsLeft--
            timerText.text = "${secondsLeft}s"
            if (secondsLeft <= 0) finishGame()
        }
    }

    private fun startMemory(difficulty: String) {
        activeGame = true
        content.removeAllViews()
        gameStartedAt = now()
        val p = AppAppearance.palette(this)
        
        val cols = when (difficulty) {
            "Easy" -> 4
            "Medium" -> 4
            else -> 4
        }
        val rows = when (difficulty) {
            "Easy" -> 4
            "Medium" -> 5
            else -> 6
        }
        val totalPairs = rows * cols / 2
        
        // Emojis instead of letters for high-fidelity gaming
        val symbols = listOf("🍎", "🍌", "🍒", "🍉", "🍇", "🍓", "🍍", "🥑", "🍑", "🍋", "🥕", "🌽")
            .take(totalPairs)
            .flatMap { listOf(it, it) }
            .shuffled()

        var first: Button? = null
        var firstIndex = -1
        var locked = false
        var moves = 0
        var pairs = 0
        var seconds = 0
        val matched = BooleanArray(symbols.size)
        
        val movesText = statText("Moves: 0")
        val timeText = statText("00:00")
        val pairsText = statText("Pairs: 0/$totalPairs")
        
        val grid = GridLayout(this).apply {
            columnCount = cols
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { topMargin = dp(24) }
        }

        fun finishGame() {
            stopTicker()
            val maxMoves = totalPairs * 2.5
            val movePenalty = max(0.0, (moves - totalPairs) / maxMoves * 40.0)
            val timePenalty = min(40.0, seconds / 3.0)
            val finalScore = max(0.0, 100.0 - movePenalty - timePenalty)
            saveAndShowResult(
                "Memory",
                difficulty,
                finalScore,
                mapOf("moves" to moves, "pairs" to totalPairs),
                "${AppText.get(this, "Time")}: ${formatSeconds(seconds)}  ${AppText.get(this, "Moves")}: $moves"
            )
        }

        symbols.forEachIndexed { index, symbol ->
            val btn = Button(this).apply {
                text = "?"
                textSize = 22f
                setTextColor(p.text)
                background = rounded(p.surfaceAlt, 18)
                layoutParams = GridLayout.LayoutParams().apply {
                    width = 0
                    height = dp(70)
                    columnSpec = GridLayout.spec(GridLayout.UNDEFINED, 1f)
                    setMargins(dp(4), dp(4), dp(4), dp(4))
                }
                setOnClickListener {
                    if (locked || matched[index] || text != "?") return@setOnClickListener
                    text = symbol
                    background = rounded(p.accent, 18)
                    setTextColor(Color.WHITE)
                    if (first == null) {
                        first = this
                        firstIndex = index
                    } else {
                        moves++
                        movesText.text = "${AppText.get(context, "Moves")}: $moves"
                        val second = this
                        if (symbols[firstIndex] == symbol) {
                            matched[firstIndex] = true
                            matched[index] = true
                            pairs++
                            pairsText.text = "${AppText.get(context, "Pairs")}: $pairs/$totalPairs"
                            first = null
                            firstIndex = -1
                            if (pairs == totalPairs) finishGame()
                        } else {
                            locked = true
                            handler.postDelayed({
                                first?.text = "?"
                                first?.background = rounded(p.surfaceAlt, 18)
                                first?.setTextColor(p.text)
                                second.text = "?"
                                second.background = rounded(p.surfaceAlt, 18)
                                second.setTextColor(p.text)
                                first = null
                                firstIndex = -1
                                locked = false
                            }, 700)
                        }
                    }
                }
            }
            grid.addView(btn)
        }

        content.addView(title("Memory", "Find every pair."))
        content.addView(row(timeText, movesText, pairsText))
        content.addView(grid)
        
        animateChildrenEntrance()

        startTicker {
            seconds++
            timeText.text = formatSeconds(seconds)
        }
    }

    private fun startSequences(difficulty: String) {
        activeGame = true
        content.removeAllViews()
        gameStartedAt = now()
        val p = AppAppearance.palette(this)
        val maxLevels = 20
        val flashMs = when (difficulty) {
            "Easy" -> 650L
            "Medium" -> 450L
            else -> 300L
        }
        var level = 1
        var lives = 3
        val sequence = mutableListOf<Int>()
        var inputIndex = 0
        var waiting = false
        val levelText = statText("Level: 1/$maxLevels")
        val scoreText = statText("Score: 0/100")
        val livesText = statText("Lives: 3")
        
        val status = TextView(this).apply {
            text = AppText.get(context, "Watch the sequence")
            setTextColor(p.text)
            textSize = 18f
            gravity = Gravity.CENTER
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { topMargin = dp(24); bottomMargin = dp(18) }
        }
        
        val grid = GridLayout(this).apply {
            columnCount = 3
            layoutParams = LinearLayout.LayoutParams(match, wrap)
        }
        val buttons = mutableListOf<Button>()

        fun currentScore(): Double {
            val completed = max(0, level - 1)
            return min(100.0, completed * (3 + lives) / (maxLevels * 6.0) * 100.0)
        }

        fun updateHud() {
            levelText.text = "${AppText.get(this, "Level")}: ${min(level, maxLevels)}/$maxLevels"
            scoreText.text = "${AppText.get(this, "Score")}: ${currentScore().toInt()}/100"
            livesText.text = "${AppText.get(this, "Lives")}: $lives"
        }

        fun finishGame() {
            saveAndShowResult(
                "Sequences",
                difficulty,
                currentScore(),
                mapOf("rawScore" to round(currentScore()).toInt(), "maxLevel" to max(0, level - 1)),
                "${AppText.get(this, "Level reached")}: ${max(0, level - 1)}/$maxLevels  ${AppText.get(this, "Lives")}: $lives"
            )
        }

        fun setEnabled(enabled: Boolean) = buttons.forEach { it.isEnabled = enabled }
        
        fun flash(index: Int, done: () -> Unit = {}) {
            val btn = buttons[index]
            btn.background = rounded(p.accent, 12)
            handler.postDelayed({
                btn.background = rounded(sequenceColor(index), 12)
                done()
            }, flashMs)
        }

        fun playSequence(pos: Int = 0) {
            waiting = false
            setEnabled(false)
            status.text = AppText.get(this, "Watch the sequence")
            if (pos >= sequence.size) {
                inputIndex = 0
                waiting = true
                status.text = AppText.get(this, "Repeat it")
                setEnabled(true)
                return
            }
            flash(sequence[pos]) { handler.postDelayed({ playSequence(pos + 1) }, 160) }
        }

        fun nextRound() {
            if (level > maxLevels) {
                finishGame()
                return
            }
            sequence.add(Random.nextInt(0, 9))
            updateHud()
            playSequence()
        }

        repeat(9) { idx ->
            val btn = Button(this).apply {
                text = ""
                background = rounded(sequenceColor(idx), 12)
                isEnabled = false
                layoutParams = GridLayout.LayoutParams().apply {
                    width = 0
                    height = dp(88)
                    columnSpec = GridLayout.spec(GridLayout.UNDEFINED, 1f)
                    setMargins(dp(5), dp(5), dp(5), dp(5))
                }
                setOnClickListener {
                    if (!waiting) return@setOnClickListener
                    flash(idx)
                    if (idx == sequence[inputIndex]) {
                        inputIndex++
                        if (inputIndex == sequence.size) {
                            level++
                            waiting = false
                            setEnabled(false)
                            status.text = AppText.get(context, "Correct")
                            handler.postDelayed({ nextRound() }, 700)
                        }
                    } else {
                        lives--
                        updateHud()
                        waiting = false
                        setEnabled(false)
                        if (lives <= 0) {
                            finishGame()
                        } else {
                            status.text = AppText.get(context, "Wrong. Watch again")
                            handler.postDelayed({ playSequence() }, 900)
                        }
                    }
                }
            }
            buttons.add(btn)
            grid.addView(btn)
        }

        content.addView(title("Sequences", "Memorize and repeat."))
        content.addView(row(levelText, scoreText, livesText))
        content.addView(status)
        content.addView(grid)
        
        nextRound()
        animateChildrenEntrance()
    }

    private fun startStroop(difficulty: String) {
        activeGame = true
        content.removeAllViews()
        gameStartedAt = now()
        val p = AppAppearance.palette(this)
        val totalRounds = 30
        val activeColors = colors.take(if (difficulty == "Easy") 4 else if (difficulty == "Medium") 5 else 6)
        val limitMs = when (difficulty) {
            "Easy" -> 0L
            "Medium" -> 5000L
            else -> 3500L
        }
        var round = 0
        var score = 0
        var correct = 0
        var wrong = 0
        var streak = 0
        var maxStreak = 0
        var correctKey = ""
        var roundStart = 0L
        val progress = statText("Round: 0/$totalRounds")
        val scoreText = statText("Score: 0")
        
        val word = TextView(this).apply {
            textSize = 56f
            typeface = Typeface.DEFAULT_BOLD
            gravity = Gravity.CENTER
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { topMargin = dp(46); bottomMargin = dp(38) }
        }
        val grid = GridLayout(this).apply { columnCount = 2 }

        fun finishGame() {
            val finalScore = min(100.0, score / (totalRounds * 100.0) * 100.0)
            saveAndShowResult(
                "Stroop Test",
                difficulty,
                finalScore,
                mapOf("correct" to correct, "wrong" to wrong, "streak" to maxStreak),
                "$correct ${AppText.get(this, "correct")}  $wrong ${AppText.get(this, "wrong")}  ${AppText.get(this, "Best streak")}: $maxStreak"
            )
        }

        fun nextRound() {
            if (round >= totalRounds) {
                finishGame()
                return
            }
            handler.removeCallbacks(ticker ?: Runnable {})
            round++
            progress.text = "${AppText.get(this, "Round")}: $round/$totalRounds"
            val ink = activeColors.random()
            val label = activeColors.filter { it.key != ink.key }.random()
            correctKey = ink.key
            word.text = AppText.get(this, label.display)
            word.setTextColor(Color.parseColor(ink.hex))
            val options = (listOf(ink, label) + activeColors.filter { it.key != ink.key && it.key != label.key }.shuffled())
                .distinctBy { it.key }
                .take(4)
                .shuffled()
            grid.removeAllViews()
            roundStart = now()
            options.forEach { option ->
                grid.addView(Button(this).apply {
                    text = AppText.get(context, option.display)
                    setTextColor(Color.parseColor(option.hex))
                    textSize = 15f
                    typeface = Typeface.DEFAULT_BOLD
                    background = rounded(p.surface, 12, Color.parseColor(option.hex))
                    layoutParams = GridLayout.LayoutParams().apply {
                        width = 0
                        height = dp(58)
                        columnSpec = GridLayout.spec(GridLayout.UNDEFINED, 1f)
                        setMargins(dp(5), dp(5), dp(5), dp(5))
                    }
                    setOnClickListener {
                        val elapsed = (now() - roundStart) / 1000.0
                        if (option.key == correctKey) {
                            correct++
                            streak++
                            maxStreak = max(maxStreak, streak)
                            val bonus = if (limitMs > 0) max(0.0, 1.0 - elapsed / (limitMs / 1000.0)) else max(0.0, 1.0 - elapsed / 3.0)
                            score += ((50 + 50 * bonus) * if (streak >= 3) 1.2 else 1.0).toInt()
                        } else {
                            wrong++
                            streak = 0
                        }
                        scoreText.text = "${AppText.get(context, "Score")}: $score"
                        handler.postDelayed({ nextRound() }, 350)
                    }
                })
            }
            if (limitMs > 0) handler.postDelayed({
                if (activeGame && progress.text == "${AppText.get(this, "Round")}: $round/$totalRounds") {
                    wrong++
                    streak = 0
                    nextRound()
                }
            }, limitMs)
        }

        content.addView(title("Stroop Test", "Choose the ink color."))
        content.addView(row(progress, scoreText))
        content.addView(word)
        content.addView(grid)
        
        nextRound()
        animateChildrenEntrance()
    }

    private fun startVisualSearch(difficulty: String) {
        activeGame = true
        content.removeAllViews()
        gameStartedAt = now()
        val p = AppAppearance.palette(this)
        val totalRounds = 8
        val timeLimit = when (difficulty) {
            "Easy" -> 25
            "Medium" -> 20
            else -> 15
        }
        val sets = listOf("S" to "5", "O" to "0", "B" to "8", "Z" to "2", "E" to "3", "I" to "1", "W" to "M")
        var round = 0
        var score = 0
        var lives = 3
        var targetIndex = -1
        var secondsLeft = timeLimit
        val roundText = statText("Round: 0/$totalRounds")
        val scoreText = statText("Score: 0")
        val livesText = statText("Lives: 3")
        val timerText = statText("${timeLimit}s")
        val grid = GridLayout(this).apply { layoutParams = LinearLayout.LayoutParams(match, wrap).apply { topMargin = dp(18) } }

        fun finishGame(won: Boolean) {
            stopTicker()
            val normalized = min(100.0, score / (totalRounds * 575.0) * 100.0)
            saveAndShowResult(
                "Visual Search",
                difficulty,
                normalized,
                mapOf("rawScore" to score, "round" to round, "totalRounds" to totalRounds, "completed" to won),
                "${AppText.get(this, "Score")}: $score  ${AppText.get(this, "Round")}: $round/$totalRounds"
            )
        }

        fun nextRound() {
            stopTicker()
            if (round >= totalRounds) {
                finishGame(true)
                return
            }
            round++
            secondsLeft = timeLimit
            val cols = when (round) {
                1, 2 -> 6
                3, 4 -> 8
                5, 6 -> 10
                else -> 12
            }
            val rows = when (round) {
                1, 2 -> 5
                3, 4 -> 6
                5, 6 -> 7
                else -> 8
            }
            val pair = sets.random()
            val total = rows * cols
            targetIndex = Random.nextInt(total)
            grid.columnCount = cols
            grid.removeAllViews()
            roundText.text = "${AppText.get(this, "Round")}: $round/$totalRounds"
            scoreText.text = "${AppText.get(this, "Score")}: $score"
            livesText.text = "${AppText.get(this, "Lives")}: $lives"
            timerText.text = "${secondsLeft}s"
            repeat(total) { index ->
                grid.addView(Button(this).apply {
                    text = if (index == targetIndex) pair.second else pair.first
                    textSize = if (cols <= 8) 18f else 14f
                    setTextColor(p.text)
                    background = rounded(p.surface, 7)
                    layoutParams = GridLayout.LayoutParams().apply {
                        width = 0
                        height = dp(if (cols <= 8) 46 else 36)
                        columnSpec = GridLayout.spec(GridLayout.UNDEFINED, 1f)
                        setMargins(dp(2), dp(2), dp(2), dp(2))
                    }
                    setOnClickListener {
                        if (index == targetIndex) {
                            val pts = 200 + secondsLeft * 15 + round * 30
                            score += pts
                            nextRound()
                        } else {
                            lives--
                            livesText.text = "${AppText.get(context, "Lives")}: $lives"
                            background = rounded("#FF3366", 7)
                            if (lives <= 0) finishGame(false)
                        }
                    }
                })
            }
            startTicker {
                secondsLeft--
                timerText.text = "${secondsLeft}s"
                if (secondsLeft <= 0) {
                    lives--
                    if (lives <= 0) finishGame(false) else nextRound()
                }
            }
        }

        content.addView(title("Visual Search", "Find the odd character."))
        content.addView(row(roundText, scoreText, livesText, timerText))
        content.addView(grid)
        
        nextRound()
        animateChildrenEntrance()
    }

    private fun saveAndShowResult(
        game: String,
        difficulty: String,
        score: Double,
        extra: Map<String, Any?>,
        summary: String
    ) {
        activeGame = false
        stopTicker()
        val duration = formatMillis(now() - gameStartedAt)
        val p = AppAppearance.palette(this)
        repository.saveGameResult(
            game = game,
            duration = duration,
            difficulty = difficulty,
            score = score,
            extra = extra,
            onSuccess = { Toast.makeText(this, AppText.get(this, "Result saved"), Toast.LENGTH_SHORT).show() },
            onError = { Toast.makeText(this, AppText.get(this, "Could not save result"), Toast.LENGTH_SHORT).show() }
        )

        content.removeAllViews()
        content.addView(title("Result", game))
        content.addView(CardView(this).apply {
            radius = dp(24).toFloat()
            cardElevation = dp(8).toFloat()
            setCardBackgroundColor(p.surface)
            layoutParams = LinearLayout.LayoutParams(match, wrap)
            addView(LinearLayout(context).apply {
                orientation = LinearLayout.VERTICAL
                gravity = Gravity.CENTER_HORIZONTAL
                setPadding(dp(24), dp(28), dp(24), dp(28))
                addView(TextView(context).apply {
                    text = String.format(Locale.US, "%.1f / 100", score)
                    setTextColor(p.text)
                    textSize = 32f
                    typeface = Typeface.DEFAULT_BOLD
                })
                addView(TextView(context).apply {
                    text = "$summary\n${AppText.get(context, "Duration")}: $duration  ${AppText.get(context, "Choose difficulty")}: ${AppText.get(context, difficulty)}"
                    setTextColor(p.muted)
                    textSize = 14f
                    gravity = Gravity.CENTER
                    setPadding(0, dp(12), 0, dp(18))
                })
                addView(primaryButton("Back to Games") { showMenu() })
            })
        })
        animateChildrenEntrance()
    }

    private fun historyCard(result: GameResult): View {
        val p = AppAppearance.palette(this)
        return CardView(this).apply {
            radius = dp(24).toFloat()
            cardElevation = dp(6).toFloat()
            setCardBackgroundColor(p.surface)
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { bottomMargin = dp(10) }
            addView(LinearLayout(context).apply {
                orientation = LinearLayout.VERTICAL
                setPadding(dp(16), dp(14), dp(16), dp(14))
                addView(row(TextView(context).apply {
                    text = AppText.get(context, result.game.ifBlank { "Game" })
                    setTextColor(p.text)
                    textSize = 15f
                    typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
                }, TextView(context).apply {
                    text = result.scor?.let { String.format(Locale.US, "%.1f", it) } ?: "-"
                    setTextColor(p.text)
                    textSize = 15f
                    typeface = Typeface.DEFAULT_BOLD
                    gravity = Gravity.END
                }))
                addView(TextView(context).apply {
                    text = "${result.dateTime.ifBlank { AppText.get(context, "No date") }}  ${result.duration}  ${AppText.get(context, result.difficulty)}"
                    setTextColor(p.muted)
                    textSize = 12f
                    setPadding(0, dp(6), 0, 0)
                })
            })
        }
    }

    private fun title(text: String, subtitle: String): View {
        val p = AppAppearance.palette(this)
        return LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            layoutParams = LinearLayout.LayoutParams(match, wrap).apply { bottomMargin = dp(18) }
            addView(TextView(context).apply {
                this.text = AppText.get(context, text)
                setTextColor(p.text)
                textSize = 24f
                typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
            })
            addView(TextView(context).apply {
                this.text = AppText.get(context, subtitle)
                setTextColor(p.muted)
                textSize = 14f
                setPadding(0, dp(5), 0, 0)
            })
        }
    }

    private fun section(text: String): View = TextView(this).apply {
        this.text = AppText.get(context, text)
        setTextColor(AppAppearance.palette(context).text)
        textSize = 18f
        typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
        setPadding(0, dp(12), 0, dp(12))
    }

    private fun statText(textValue: String): TextView = TextView(this).apply {
        text = localizeStat(textValue)
        setTextColor(AppAppearance.palette(context).text)
        textSize = 13f
        gravity = Gravity.CENTER
        background = rounded(AppAppearance.palette(context).surfaceAlt, 18)
        setPadding(dp(10), dp(10), dp(10), dp(10))
    }

    private fun localizeStat(textValue: String): String {
        val separator = textValue.indexOf(':')
        return if (separator > 0) {
            AppText.get(this, textValue.substring(0, separator)) + textValue.substring(separator)
        } else {
            AppText.get(this, textValue)
        }
    }

    private fun row(vararg views: View): LinearLayout = LinearLayout(this).apply {
        orientation = LinearLayout.HORIZONTAL
        gravity = Gravity.CENTER_VERTICAL
        views.forEachIndexed { index, view ->
            view.layoutParams = LinearLayout.LayoutParams(0, wrap, 1f).apply {
                if (index > 0) leftMargin = dp(8)
            }
            addView(view)
        }
    }

    private fun primaryButton(textValue: String, onClick: () -> Unit): Button = Button(this).apply {
        text = AppText.get(context, textValue)
        setTextColor(Color.WHITE)
        textSize = 15f
        typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
        background = AppAppearance.gradientButton(context)
        layoutParams = LinearLayout.LayoutParams(match, dp(54)).apply { bottomMargin = dp(12) }
        setOnClickListener { onClick() }
    }

    private fun emptyState(message: String): View = TextView(this).apply {
        text = AppText.get(context, message)
        setTextColor(AppAppearance.palette(context).muted)
        textSize = 14f
        gravity = Gravity.CENTER
        background = rounded(AppAppearance.palette(context).surface, 24)
        setPadding(dp(18), dp(24), dp(18), dp(24))
    }

    private fun startTicker(tick: () -> Unit) {
        stopTicker()
        ticker = object : Runnable {
            override fun run() {
                tick()
                if (ticker === this && activeGame) handler.postDelayed(this, 1000)
            }
        }
        handler.postDelayed(ticker!!, 1000)
    }

    private fun stopTicker() {
        ticker?.let { handler.removeCallbacks(it) }
        ticker = null
    }

    private fun sequenceColor(index: Int): String {
        return listOf("#FF3366", "#00E5FF", "#00FF66", "#FFB020", "#7C3DFF", "#FF3366", "#00E5FF", "#00FF66", "#FFB020")[index]
    }

    private fun rounded(color: String, radius: Int, stroke: String? = null): GradientDrawable {
        return rounded(Color.parseColor(color), radius, stroke?.let { Color.parseColor(it) })
    }

    private fun rounded(color: Int, radius: Int, strokeColor: Int? = null): GradientDrawable {
        return GradientDrawable().apply {
            setColor(color)
            cornerRadius = dp(radius).toFloat()
            if (strokeColor != null) setStroke(dp(1), strokeColor)
        }
    }

    private fun formatSeconds(seconds: Int): String = "%02d:%02d".format(seconds / 60, seconds % 60)

    private fun formatMillis(ms: Long): String {
        val total = max(0L, ms / 1000L).toInt()
        return formatSeconds(total)
    }

    private fun now(): Long = System.currentTimeMillis()

    private fun dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()

    private val match = LinearLayout.LayoutParams.MATCH_PARENT
    private val wrap = LinearLayout.LayoutParams.WRAP_CONTENT

    private data class ColorChoice(val key: String, val hex: String, val display: String)
}
