(function () {
    'use strict';

    var form = document.getElementById('rental-form');
    if (!form) return;

    var slots = ['09:00 AM', '10:00 AM', '11:00 AM', '12:00 PM', '01:00 PM', '02:00 PM', '03:00 PM', '04:00 PM'];
    var dayNumbers = { Sun: 0, Mon: 1, Tue: 2, Wed: 3, Thu: 4, Fri: 5, Sat: 6 };
    var configuredDays = (form.dataset.availableDays || '').split(',').map(function (day) { return dayNumbers[day]; }).filter(function (day) { return day !== undefined; });
    var ownerDays = configuredDays.length ? configuredDays : [0, 1, 2, 3, 4, 5, 6];
    var selected = new Date(); selected.setHours(0, 0, 0, 0);
    var month = new Date(selected.getFullYear(), selected.getMonth(), 1);
    var unavailable = [];
    var selectedTime = '';
    var requestNumber = 0;
    var isLoading = false;
    var rate = Number(form.dataset.hourlyRate) || 0;
    var dateInput = document.getElementById('handover-date');
    var timeInput = document.getElementById('handover-time');
    var durationInput = document.getElementById('duration');
    var slotContainer = document.getElementById('time-slots');
    var submitButton = form.querySelector('.submit-btn');

    function $(id) { return document.getElementById(id); }
    function pad(value) { return value < 10 ? '0' + value : String(value); }
    function dateKey(date) { return date.getFullYear() + '-' + pad(date.getMonth() + 1) + '-' + pad(date.getDate()); }
    function isOwnerDay(date) { return ownerDays.indexOf(date.getDay()) >= 0; }
    function isSameDate(a, b) { return a && b && dateKey(a) === dateKey(b); }
    function setFirstScheduledDate() {
        for (var i = 0; i < 120; i++) {
            var date = new Date(); date.setHours(0, 0, 0, 0); date.setDate(date.getDate() + i);
            if (isOwnerDay(date)) { selected = date; return; }
        }
        selected = null;
    }
    function formatDate(date) {
        return date.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });
    }
    function selectedDuration() { return Number(durationInput.value) || 2; }
    function updateSummary() {
        var hours = selectedDuration();
        $('duration-label').textContent = hours + ' hour' + (hours === 1 ? '' : 's');
        $('fee-total').textContent = '₱' + (rate * hours).toFixed(2);
        if (selected && selectedTime) {
            var parts = selectedTime.match(/(\d+):(\d+) (AM|PM)/);
            var handover = new Date(selected);
            handover.setHours((Number(parts[1]) % 12) + (parts[3] === 'PM' ? 12 : 0), Number(parts[2]), 0, 0);
            var returnAt = new Date(handover.getTime() + hours * 3600000);
            $('return-time').textContent = formatDate(returnAt) + ' · ' + returnAt.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }) + ' (' + hours + '-hour rental)';
        } else {
            $('return-time').textContent = 'Select an available handover time';
        }
        dateInput.value = selected ? dateKey(selected) : '';
        timeInput.value = selectedTime || '';
        $('time-select').value = selectedTime || '';
        submitButton.disabled = isLoading || !selected || !selectedTime || unavailable.indexOf(selectedTime) >= 0;
    }
    function renderCalendar() {
        var grid = $('calendar-grid');
        var first = new Date(month.getFullYear(), month.getMonth(), 1);
        var daysInMonth = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate();
        $('month-label').textContent = first.toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
        grid.innerHTML = '';
        ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'].forEach(function (label) {
            var heading = document.createElement('span'); heading.textContent = label; grid.appendChild(heading);
        });
        for (var blank = 0; blank < first.getDay(); blank++) grid.appendChild(document.createElement('span'));
        for (var day = 1; day <= daysInMonth; day++) {
            var date = new Date(month.getFullYear(), month.getMonth(), day);
            var button = document.createElement('button');
            button.type = 'button'; button.textContent = day;
            var past = date < new Date(new Date().setHours(0, 0, 0, 0));
            var onSchedule = isOwnerDay(date);
            if (past || !onSchedule) button.disabled = true;
            if (!onSchedule) button.classList.add('owner-off');
            if (isSameDate(date, selected)) button.classList.add('selected');
            button.setAttribute('aria-label', formatDate(date) + (onSchedule ? ', owner handover day' : ', unavailable'));
            button.addEventListener('click', function (chosenDate) {
                return function () { selected = chosenDate; selectedTime = ''; renderCalendar(); refreshAvailability(); };
            }(date));
            grid.appendChild(button);
        }
        var currentMonth = (month.getFullYear() - new Date().getFullYear()) * 12 + month.getMonth() - new Date().getMonth();
        $('prev-month').disabled = currentMonth <= 0;
        $('next-month').disabled = currentMonth >= 3;
    }
    function renderSlots() {
        slotContainer.innerHTML = '';
        slots.forEach(function (slot) {
            var blocked = isLoading || unavailable.indexOf(slot) >= 0;
            var button = document.createElement('button');
            button.type = 'button'; button.textContent = slot;
            button.className = blocked ? 'slot-unavailable' : 'slot-available';
            if (selectedTime === slot && !blocked) button.classList.add('selected');
            button.disabled = blocked;
            button.setAttribute('aria-label', slot + (blocked ? ', unavailable' : selectedTime === slot ? ', selected' : ', available'));
            button.addEventListener('click', function () {
                if (button.disabled) return;
                selectedTime = slot; renderSlots(); updateSummary();
            });
            slotContainer.appendChild(button);
        });
    }
    function refreshAvailability() {
        var status = $('availability-status');
        if (!selected) {
            unavailable = slots.slice(); isLoading = false;
            status.textContent = 'No handover days are available in the owner schedule.';
            renderSlots(); updateSummary(); return;
        }
        var callId = ++requestNumber;
        isLoading = true; unavailable = slots.slice();
        status.textContent = 'Checking current bookings…'; renderSlots(); updateSummary();
        var url = new URL(form.getAttribute('data-availability-url'), window.location.href);
        url.searchParams.set('id', form.querySelector('input[name="id"]').value);
        url.searchParams.set('date', dateKey(selected));
        url.searchParams.set('duration', String(selectedDuration()));
        fetch(url.toString(), { method: 'GET', cache: 'no-store', credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
            .then(function (response) { if (!response.ok) throw new Error('Availability could not be loaded.'); return response.json(); })
            .then(function (data) {
                if (callId !== requestNumber) return;
                unavailable = data.unavailableSlots || [];
                isLoading = false;
                if (!selectedTime || unavailable.indexOf(selectedTime) >= 0) selectedTime = slots.find(function (slot) { return unavailable.indexOf(slot) < 0; }) || '';
                status.textContent = selectedTime ? 'Availability is up to date for ' + formatDate(selected) + '.' : 'No handover times are available for this date and duration.';
                renderSlots(); updateSummary();
            })
            .catch(function () {
                if (callId !== requestNumber) return;
                unavailable = slots.slice(); isLoading = false; selectedTime = '';
                status.textContent = 'Could not check availability. Refresh the page and try again.';
                renderSlots(); updateSummary();
            });
    }

    $('prev-month').addEventListener('click', function () { month.setMonth(month.getMonth() - 1); renderCalendar(); });
    $('next-month').addEventListener('click', function () { month.setMonth(month.getMonth() + 1); renderCalendar(); });
    document.querySelectorAll('.duration-btn').forEach(function (button) {
        button.addEventListener('click', function () {
            document.querySelectorAll('.duration-btn').forEach(function (other) { other.classList.remove('active'); });
            button.classList.add('active'); durationInput.value = button.dataset.hours; updateSummary(); refreshAvailability();
        });
    });
    form.addEventListener('submit', function (event) {
        if (isLoading || !selectedTime || unavailable.indexOf(selectedTime) >= 0) {
            event.preventDefault();
            $('availability-status').textContent = 'Choose an available handover time before submitting.';
        }
    });

    setFirstScheduledDate();
    if (selected) month = new Date(selected.getFullYear(), selected.getMonth(), 1);
    renderCalendar(); updateSummary(); refreshAvailability();
})();
