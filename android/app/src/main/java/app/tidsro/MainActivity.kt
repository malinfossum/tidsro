package app.tidsro

import android.Manifest
import android.app.AlarmManager
import android.content.Context
import android.content.DialogInterface
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import android.view.LayoutInflater
import android.view.View
import android.widget.AdapterView
import android.widget.ArrayAdapter
import android.widget.EditText
import android.widget.ImageButton
import android.widget.LinearLayout
import android.widget.Spinner
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import com.google.android.material.button.MaterialButton
import com.google.android.material.chip.Chip
import com.google.android.material.chip.ChipGroup
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import com.google.android.material.materialswitch.MaterialSwitch
import com.google.android.material.snackbar.Snackbar
import java.time.Instant
import java.time.ZoneId
import java.time.ZonedDateTime

class MainActivity : AppCompatActivity() {

    private val handler = Handler(Looper.getMainLooper())
    private var timers = mutableListOf<CountdownTimer>()
    private var alarms = mutableListOf<Alarm>()
    private val timerRows = mutableMapOf<String, View>()
    private var lastTimersRaw = ""
    private var lastAlarmsRaw = ""

    private lateinit var inputDuration: EditText
    private lateinit var inputTimerLabel: EditText
    private lateinit var spinnerTimerSound: Spinner
    private lateinit var textTimerError: TextView
    private lateinit var listTimers: LinearLayout
    private lateinit var textTimersEmpty: TextView

    private lateinit var inputAlarmTime: EditText
    private lateinit var inputAlarmLabel: EditText
    private lateinit var spinnerAlarmSound: Spinner
    private lateinit var spinnerRepeat: Spinner
    private lateinit var chipsDays: ChipGroup
    private lateinit var switchWarn: MaterialSwitch
    private lateinit var textAlarmError: TextView
    private lateinit var listAlarms: LinearLayout
    private lateinit var textAlarmsEmpty: TextView

    private val tick = object : Runnable {
        override fun run() {
            if (Store.timersRaw(this@MainActivity) != lastTimersRaw) {
                timers = Store.loadTimers(this@MainActivity)
                rebuildTimerRows()
            } else {
                updateTimerTexts()
            }
            if (Store.alarmsRaw(this@MainActivity) != lastAlarmsRaw) {
                rebuildAlarmRows()
            }
            handler.postDelayed(this, 500)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        inputDuration = findViewById(R.id.input_duration)
        inputTimerLabel = findViewById(R.id.input_timer_label)
        spinnerTimerSound = findViewById(R.id.spinner_timer_sound)
        textTimerError = findViewById(R.id.text_timer_error)
        listTimers = findViewById(R.id.list_timers)
        textTimersEmpty = findViewById(R.id.text_timers_empty)

        inputAlarmTime = findViewById(R.id.input_alarm_time)
        inputAlarmLabel = findViewById(R.id.input_alarm_label)
        spinnerAlarmSound = findViewById(R.id.spinner_alarm_sound)
        spinnerRepeat = findViewById(R.id.spinner_repeat)
        chipsDays = findViewById(R.id.chips_days)
        switchWarn = findViewById(R.id.switch_warn)
        textAlarmError = findViewById(R.id.text_alarm_error)
        listAlarms = findViewById(R.id.list_alarms)
        textAlarmsEmpty = findViewById(R.id.text_alarms_empty)

        val defaultSound = Store.defaultSound(this).coerceIn(0, Sound.values().size - 1)
        spinnerTimerSound.adapter = soundAdapter()
        spinnerTimerSound.setSelection(defaultSound)
        spinnerTimerSound.onSelected { Store.setDefaultSound(this, it) }
        spinnerAlarmSound.adapter = soundAdapter()
        spinnerAlarmSound.setSelection(defaultSound)
        spinnerAlarmSound.onSelected { Store.setDefaultSound(this, it) }

        spinnerRepeat.adapter = repeatAdapter()
        spinnerRepeat.onSelected { pos ->
            chipsDays.visibility = if (pos == 4) View.VISIBLE else View.GONE
        }
        buildDayChips(chipsDays)

        findViewById<MaterialButton>(R.id.btn_preset5).setOnClickListener { startPreset(5) }
        findViewById<MaterialButton>(R.id.btn_preset30).setOnClickListener { startPreset(30) }
        findViewById<MaterialButton>(R.id.btn_preset60).setOnClickListener { startPreset(60) }
        findViewById<MaterialButton>(R.id.btn_start).setOnClickListener { startFromInput() }
        findViewById<MaterialButton>(R.id.btn_add_alarm).setOnClickListener { addAlarm() }

        requestPermissionsIfNeeded()
    }

    override fun onResume() {
        super.onResume()
        timers = Store.loadTimers(this)
        rebuildTimerRows()
        rebuildAlarmRows()
        handler.removeCallbacks(tick)
        handler.postDelayed(tick, 500)
    }

    override fun onPause() {
        super.onPause()
        handler.removeCallbacks(tick)
    }

    private fun requestPermissionsIfNeeded() {
        if (Build.VERSION.SDK_INT >= 33 &&
            ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) !=
            PackageManager.PERMISSION_GRANTED
        ) {
            ActivityCompat.requestPermissions(this, arrayOf(Manifest.permission.POST_NOTIFICATIONS), 1)
        }
        if (Build.VERSION.SDK_INT >= 31) {
            val am = getSystemService(Context.ALARM_SERVICE) as AlarmManager
            if (!am.canScheduleExactAlarms()) {
                Snackbar.make(findViewById(android.R.id.content), R.string.exact_alarm_hint, Snackbar.LENGTH_LONG)
                    .setAction(R.string.allow) {
                        startActivity(
                            Intent(
                                Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM,
                                Uri.parse("package:$packageName")
                            )
                        )
                    }
                    .show()
            }
        }
    }

    // ---- Timers ----

    private fun startPreset(minutes: Int) {
        textTimerError.visibility = View.GONE
        startTimer(minutes * 60L * 1000L)
    }

    private fun startFromInput() {
        val parsed = Rules.parseDuration(inputDuration.text.toString())
        if (parsed.ms == null) {
            textTimerError.text = parsed.error
            textTimerError.visibility = View.VISIBLE
            return
        }
        textTimerError.visibility = View.GONE
        startTimer(parsed.ms)
        inputDuration.text.clear()
    }

    private fun startTimer(ms: Long) {
        val t = CountdownTimer(
            label = inputTimerLabel.text.toString().trim().ifEmpty { null },
            originalMs = ms,
            endsAt = System.currentTimeMillis() + ms,
            sound = spinnerTimerSound.selectedItemPosition
        )
        timers.add(t)
        persistTimers()
        AlarmScheduler.armTimer(this, t)
        rebuildTimerRows()
        inputTimerLabel.text.clear()
    }

    private fun pauseTimer(t: CountdownTimer) {
        t.pausedRemainingMs = t.remainingMs(System.currentTimeMillis())
        t.endsAt = null
        AlarmScheduler.cancelTimer(this, t.id)
        persistTimers()
        rebuildTimerRows()
    }

    private fun resumeTimer(t: CountdownTimer) {
        val rem = t.pausedRemainingMs ?: return
        t.endsAt = System.currentTimeMillis() + rem
        t.pausedRemainingMs = null
        AlarmScheduler.armTimer(this, t)
        persistTimers()
        rebuildTimerRows()
    }

    private fun resetTimer(t: CountdownTimer) {
        if (t.endsAt != null) {
            t.endsAt = System.currentTimeMillis() + t.originalMs
            AlarmScheduler.armTimer(this, t)
        } else {
            t.pausedRemainingMs = t.originalMs
        }
        persistTimers()
        rebuildTimerRows()
    }

    private fun cancelTimer(t: CountdownTimer) {
        val remaining = t.remainingMs(System.currentTimeMillis())
        val wasRunning = t.running
        timers.removeAll { it.id == t.id }
        AlarmScheduler.cancelTimer(this, t.id)
        persistTimers()
        rebuildTimerRows()
        Snackbar.make(findViewById(android.R.id.content), R.string.timer_cancelled, Snackbar.LENGTH_LONG)
            .setAction(R.string.undo) {
                if (wasRunning) {
                    t.endsAt = System.currentTimeMillis() + remaining
                    t.pausedRemainingMs = null
                    AlarmScheduler.armTimer(this, t)
                }
                timers.add(t)
                persistTimers()
                rebuildTimerRows()
            }
            .show()
    }

    private fun persistTimers() {
        Store.saveTimers(this, timers)
        lastTimersRaw = Store.timersRaw(this)
    }

    private fun rebuildTimerRows() {
        lastTimersRaw = Store.timersRaw(this)
        listTimers.removeAllViews()
        timerRows.clear()
        val sorted = timers.sortedWith(compareBy({ !it.running }, { it.endsAt ?: Long.MAX_VALUE }))
        textTimersEmpty.visibility = if (sorted.isEmpty()) View.VISIBLE else View.GONE
        for (t in sorted) {
            val row = LayoutInflater.from(this).inflate(R.layout.item_timer, listTimers, false)
            row.alpha = if (t.running) 1f else 0.6f
            val lbl = row.findViewById<TextView>(R.id.timer_label)
            lbl.text = t.label ?: ""
            val pauseBtn = row.findViewById<MaterialButton>(R.id.btn_pause)
            pauseBtn.text = getString(if (t.running) R.string.pause else R.string.resume)
            pauseBtn.setOnClickListener { if (t.running) pauseTimer(t) else resumeTimer(t) }
            row.findViewById<MaterialButton>(R.id.btn_reset).setOnClickListener { resetTimer(t) }
            row.findViewById<MaterialButton>(R.id.btn_cancel).setOnClickListener { cancelTimer(t) }
            timerRows[t.id] = row
            listTimers.addView(row, rowParams())
        }
        updateTimerTexts()
    }

    private fun updateTimerTexts() {
        val now = System.currentTimeMillis()
        for (t in timers) {
            val row = timerRows[t.id] ?: continue
            val tv = row.findViewById<TextView>(R.id.timer_remaining)
            tv.text = Rules.formatMs(t.remainingMs(now))
            tv.setTextColor(ContextCompat.getColor(this, if (t.running) R.color.accent else R.color.text_muted))
            // The wall-clock finish time, hidden while paused since it depends on when the timer resumes.
            val fv = row.findViewById<TextView>(R.id.timer_finish)
            val ends = t.endsAt
            if (ends != null) {
                val z = ZonedDateTime.ofInstant(Instant.ofEpochMilli(ends), ZoneId.systemDefault())
                fv.text = String.format("done %02d:%02d", z.hour, z.minute)
                fv.visibility = View.VISIBLE
            } else {
                fv.visibility = View.GONE
            }
        }
    }

    // ---- Schedule ----

    private fun addAlarm() {
        val parsed = Rules.parseClockTime(inputAlarmTime.text.toString())
        if (parsed.time == null) {
            textAlarmError.text = parsed.error
            textAlarmError.visibility = View.VISIBLE
            return
        }
        val days = daysForRepeatPosition(spinnerRepeat.selectedItemPosition, chipsDays)
        if (spinnerRepeat.selectedItemPosition == 4 && days == 0) {
            textAlarmError.text = getString(R.string.pick_a_day)
            textAlarmError.visibility = View.VISIBLE
            return
        }
        textAlarmError.visibility = View.GONE
        val a = Alarm(
            hour = parsed.time.hour,
            minute = parsed.time.minute,
            days = days,
            label = inputAlarmLabel.text.toString().trim().ifEmpty { null },
            sound = spinnerAlarmSound.selectedItemPosition,
            warnBefore = switchWarn.isChecked,
            enabled = true
        )
        a.nextFireAt = Rules.nextFireAtFor(a, System.currentTimeMillis())
        alarms.add(a)
        persistAlarms()
        AlarmScheduler.armAlarm(this, a)
        rebuildAlarmRows()
        inputAlarmTime.text.clear()
        inputAlarmLabel.text.clear()
    }

    private fun deleteAlarm(a: Alarm) {
        alarms.removeAll { it.id == a.id }
        AlarmScheduler.cancelAlarm(this, a.id)
        persistAlarms()
        rebuildAlarmRows()
        Snackbar.make(findViewById(android.R.id.content), R.string.alarm_deleted, Snackbar.LENGTH_LONG)
            .setAction(R.string.undo) {
                if (a.enabled) {
                    a.nextFireAt = Rules.nextFireAtFor(a, System.currentTimeMillis())
                    AlarmScheduler.armAlarm(this, a)
                }
                alarms.add(a)
                persistAlarms()
                rebuildAlarmRows()
            }
            .show()
    }

    private fun persistAlarms() {
        Store.saveAlarms(this, alarms)
        lastAlarmsRaw = Store.alarmsRaw(this)
    }

    private fun rebuildAlarmRows() {
        alarms = Store.loadAlarms(this)
        lastAlarmsRaw = Store.alarmsRaw(this)
        listAlarms.removeAllViews()
        val sorted = alarms.sortedWith(compareBy({ !it.enabled }, { it.nextFireAt }))
        textAlarmsEmpty.visibility = if (sorted.isEmpty()) View.VISIBLE else View.GONE
        for (a in sorted) {
            val row = LayoutInflater.from(this).inflate(R.layout.item_alarm, listAlarms, false)
            row.alpha = if (a.enabled) 1f else 0.5f
            row.findViewById<TextView>(R.id.alarm_time).text =
                String.format("%02d:%02d  %s", a.hour, a.minute, Rules.cadenceLabel(a.days))
            val subParts = mutableListOf<String>()
            a.label?.let { subParts.add(it) }
            subParts.add(Sound.from(a.sound).label)
            if (a.warnBefore) subParts.add(getString(R.string.warns_before_short))
            row.findViewById<TextView>(R.id.alarm_sub).text = subParts.joinToString(" - ")
            val sw = row.findViewById<MaterialSwitch>(R.id.alarm_enabled)
            sw.isChecked = a.enabled
            sw.setOnCheckedChangeListener { _, checked ->
                a.enabled = checked
                if (checked) {
                    a.nextFireAt = Rules.nextFireAtFor(a, System.currentTimeMillis())
                    AlarmScheduler.armAlarm(this, a)
                } else {
                    AlarmScheduler.cancelAlarm(this, a.id)
                }
                persistAlarms()
                rebuildAlarmRows()
            }
            row.findViewById<ImageButton>(R.id.btn_edit).setOnClickListener { showEditDialog(a) }
            row.findViewById<ImageButton>(R.id.btn_delete).setOnClickListener { deleteAlarm(a) }
            listAlarms.addView(row, rowParams())
        }
    }

    private fun showEditDialog(a: Alarm) {
        val v = LayoutInflater.from(this).inflate(R.layout.dialog_edit_alarm, null)
        val time = v.findViewById<EditText>(R.id.edit_time)
        val label = v.findViewById<EditText>(R.id.edit_label)
        val soundSpinner = v.findViewById<Spinner>(R.id.edit_sound)
        val repeatSpinner = v.findViewById<Spinner>(R.id.edit_repeat)
        val chips = v.findViewById<ChipGroup>(R.id.edit_chips_days)
        val warn = v.findViewById<MaterialSwitch>(R.id.edit_warn)
        val error = v.findViewById<TextView>(R.id.edit_error)

        time.setText(String.format("%02d:%02d", a.hour, a.minute))
        label.setText(a.label ?: "")
        soundSpinner.adapter = soundAdapter()
        soundSpinner.setSelection(a.sound.coerceIn(0, Sound.values().size - 1))
        repeatSpinner.adapter = repeatAdapter()
        buildDayChips(chips)
        val repeatIdx = when (a.days) {
            0 -> 0
            Rules.ALL_DAYS -> 1
            Rules.WEEKDAYS -> 2
            Rules.WEEKENDS -> 3
            else -> 4
        }
        repeatSpinner.setSelection(repeatIdx)
        if (repeatIdx == 4) {
            chips.visibility = View.VISIBLE
            setChips(chips, a.days)
        }
        repeatSpinner.onSelected { pos ->
            chips.visibility = if (pos == 4) View.VISIBLE else View.GONE
        }
        warn.isChecked = a.warnBefore

        val dialog = MaterialAlertDialogBuilder(this)
            .setTitle(R.string.edit_alarm)
            .setView(v)
            .setPositiveButton(R.string.save, null)
            .setNegativeButton(R.string.cancel, null)
            .create()
        dialog.show()
        dialog.getButton(DialogInterface.BUTTON_POSITIVE)?.setOnClickListener {
            val parsed = Rules.parseClockTime(time.text.toString())
            if (parsed.time == null) {
                error.text = parsed.error
                error.visibility = View.VISIBLE
                return@setOnClickListener
            }
            val days = daysForRepeatPosition(repeatSpinner.selectedItemPosition, chips)
            if (repeatSpinner.selectedItemPosition == 4 && days == 0) {
                error.text = getString(R.string.pick_a_day)
                error.visibility = View.VISIBLE
                return@setOnClickListener
            }
            a.hour = parsed.time.hour
            a.minute = parsed.time.minute
            a.days = days
            a.label = label.text.toString().trim().ifEmpty { null }
            a.sound = soundSpinner.selectedItemPosition
            a.warnBefore = warn.isChecked
            a.nextFireAt = Rules.nextFireAtFor(a, System.currentTimeMillis())
            persistAlarms()
            if (a.enabled) AlarmScheduler.armAlarm(this, a) else AlarmScheduler.cancelAlarm(this, a.id)
            rebuildAlarmRows()
            dialog.dismiss()
        }
    }

    // ---- Helpers ----

    private fun rowParams(): LinearLayout.LayoutParams {
        val lp = LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT,
            LinearLayout.LayoutParams.WRAP_CONTENT
        )
        lp.bottomMargin = (8 * resources.displayMetrics.density).toInt()
        return lp
    }

    private fun soundAdapter(): ArrayAdapter<String> =
        ArrayAdapter(this, android.R.layout.simple_spinner_item, Sound.labels()).also {
            it.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item)
        }

    private fun repeatAdapter(): ArrayAdapter<String> =
        ArrayAdapter(
            this, android.R.layout.simple_spinner_item,
            listOf("Once", "Daily", "Weekdays", "Weekends", "Custom")
        ).also {
            it.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item)
        }

    private fun daysForRepeatPosition(pos: Int, chips: ChipGroup): Int = when (pos) {
        0 -> 0
        1 -> Rules.ALL_DAYS
        2 -> Rules.WEEKDAYS
        3 -> Rules.WEEKENDS
        else -> daysFrom(chips)
    }

    private fun buildDayChips(group: ChipGroup) {
        val names = listOf("Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun")
        group.removeAllViews()
        for ((i, name) in names.withIndex()) {
            val chip = Chip(this)
            chip.text = name
            chip.isCheckable = true
            chip.tag = 1 shl i
            group.addView(chip)
        }
    }

    private fun daysFrom(group: ChipGroup): Int {
        var mask = 0
        for (i in 0 until group.childCount) {
            val chip = group.getChildAt(i) as Chip
            if (chip.isChecked) mask = mask or (chip.tag as Int)
        }
        return mask
    }

    private fun setChips(group: ChipGroup, days: Int) {
        for (i in 0 until group.childCount) {
            val chip = group.getChildAt(i) as Chip
            chip.isChecked = (days and (chip.tag as Int)) != 0
        }
    }

    private fun Spinner.onSelected(cb: (Int) -> Unit) {
        onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                cb(position)
            }

            override fun onNothingSelected(parent: AdapterView<*>?) {}
        }
    }
}
