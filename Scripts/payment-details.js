(function () {
    'use strict';

    var root = document.getElementById('payment-details');
    if (!root) return;

    var bookingId = root.getAttribute('data-booking-id');
    var token = root.querySelector('input[name="__RequestVerificationToken"]');
    var message = document.getElementById('payment-message');
    var chargeDetails = document.getElementById('payment-charge-details');
    var entryPanel = document.getElementById('payment-entry-panel');
    var chargeSelect = document.getElementById('payment-charge-type');
    var methodSelect = document.getElementById('payment-method');
    var referenceField = document.getElementById('payment-reference-field');
    var referenceInput = document.getElementById('payment-reference');
    var historyBody = document.getElementById('payment-history-body');
    var finalizePanel = document.getElementById('late-fee-owner-panel');
    var returnConfirmPanel = document.getElementById('owner-return-confirm-panel');
    var state = null;

    function value(obj, name) {
        if (!obj) return undefined;
        return obj[name] !== undefined ? obj[name] : obj[name.charAt(0).toLowerCase() + name.slice(1)];
    }

    function esc(input) {
        return String(input === null || input === undefined ? '' : input)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function money(amount) {
        var n = Number(amount || 0);
        return '₱' + n.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function dateText(raw) {
        if (!raw) return '—';
        var match = /\/Date\((-?\d+)\)\//.exec(String(raw));
        var date = new Date(match ? Number(match[1]) : raw);
        return isNaN(date.getTime()) ? '—' : date.toLocaleString('en-PH', { dateStyle: 'medium', timeStyle: 'short' });
    }

    function showMessage(text, isError) {
        message.textContent = text;
        message.className = 'alert ' + (isError ? 'alert-danger' : 'alert-success');
        message.hidden = false;
    }

    function get(url) {
        return fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } }).then(function (response) {
            return response.json().then(function (result) {
                if (!response.ok || (result && result.ok === false)) throw new Error(result.message || 'Could not load payment details.');
                return result;
            });
        });
    }

    function post(url, values) {
        var body = new URLSearchParams();
        body.append('__RequestVerificationToken', token ? token.value : '');
        Object.keys(values).forEach(function (key) { body.append(key, values[key] === null || values[key] === undefined ? '' : values[key]); });
        return fetch(url, {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8', Accept: 'application/json' },
            body: body.toString()
        }).then(function (response) {
            return response.json().then(function (result) {
                if (!response.ok || !result.ok) throw new Error(result.message || 'Could not save the payment change.');
                return result;
            });
        });
    }

    function findPayment(chargeType) {
        var payments = value(state, 'Payments') || [];
        for (var i = 0; i < payments.length; i++) {
            if (String(value(payments[i], 'ChargeType') || '').toUpperCase() === chargeType) return payments[i];
        }
        return null;
    }

    function paymentCanBeSubmitted(payment) {
        if (!payment) return true;
        var status = String(value(payment, 'Status') || '').toUpperCase();
        return status === 'UNPAID';
    }

    function renderCharges() {
        var rentalFee = Number(value(state, 'RentalFee') || 0);
        var lateFee = value(state, 'LateFee');
        var finalized = lateFee && value(lateFee, 'FinalizedAmount') !== null && value(lateFee, 'FinalizedAmount') !== undefined;
        var estimate = lateFee ? Number(value(lateFee, 'EstimatedAmount') || 0) : 0;
        var finalizedAmount = finalized ? Number(value(lateFee, 'FinalizedAmount')) : null;
        var rentalDescription = value(state, 'CanSubmitRentalPayment')
            ? 'The owner has accepted this request. Payment can be recorded.'
            : 'Payment can be recorded after the owner accepts this request.';
        var lateDescription = !lateFee ? 'Late fee details will appear after the rental starts.' :
            (finalized ? 'Finalized by the owner.' : 'Estimate only. The owner finalizes the amount after confirming the return.');
        chargeDetails.innerHTML =
            '<div class="col-md-6"><div class="border rounded p-3 h-100"><h3 class="h5">Rental fee</h3>' +
            '<p class="fs-5 mb-1">' + money(rentalFee) + '</p><p class="text-muted mb-0">' + esc(rentalDescription) + '</p></div></div>' +
            '<div class="col-md-6"><div class="border rounded p-3 h-100"><h3 class="h5">Late fee</h3>' +
            '<p class="mb-1">Estimated: <strong>' + money(estimate) + '</strong></p>' +
            (finalized ? '<p class="mb-1">Finalized: <strong>' + money(finalizedAmount) + '</strong></p>' : '') +
            (lateFee ? '<p class="small text-muted mb-1">' + esc(value(lateFee, 'BillableHours') || 0) + ' billable hour(s) · ' +
                esc(value(lateFee, 'GracePeriodMinutes') || 0) + ' minute grace · ' + money(value(lateFee, 'HourlyRate') || 0) + '/hour</p>' : '') +
            '<p class="text-muted mb-0">' + esc(lateDescription) + '</p></div></div>';

        var choices = [];
        if (value(state, 'CanSubmitRentalPayment') && rentalFee > 0 && paymentCanBeSubmitted(findPayment('RENTAL_FEE'))) {
            choices.push('<option value="RENTAL_FEE">Rental fee — ' + money(rentalFee) + '</option>');
        }
        var lateAmount = finalized ? finalizedAmount : 0;
        if (finalized && lateAmount > 0 && paymentCanBeSubmitted(findPayment('LATE_FEE'))) {
            choices.push('<option value="LATE_FEE">Late fee — ' + money(lateAmount) + '</option>');
        }
        entryPanel.hidden = Boolean(value(state, 'IsOwner')) || choices.length === 0;
        chargeSelect.innerHTML = choices.join('');

        var returnedAt = lateFee && value(lateFee, 'ReturnedAt');
        finalizePanel.hidden = !value(state, 'IsOwner') || !lateFee || !returnedAt;
        var bookingStatus = String(value(state, 'BookingStatus') || '').toUpperCase();
        returnConfirmPanel.hidden = !value(state, 'IsOwner') || (bookingStatus !== 'RETURN_REQUESTED' && bookingStatus !== 'RETURN PENDING');
        var finalInput = document.getElementById('late-fee-final-amount');
        if (lateFee && (document.activeElement !== finalInput || finalInput.value === '')) {
            var defaultFinal = finalized ? finalizedAmount : estimate;
            finalInput.value = Number(defaultFinal || 0).toFixed(2);
        }
    }

    function renderHistory() {
        var payments = value(state, 'Payments') || [];
        if (!payments.length) {
            historyBody.innerHTML = '<tr><td colspan="6" class="text-muted">No payment records have been submitted for this rental.</td></tr>';
            return;
        }
        var isOwner = Boolean(value(state, 'IsOwner'));
        historyBody.innerHTML = payments.map(function (payment) {
            var status = String(value(payment, 'Status') || 'UNPAID').toUpperCase();
            var submittedAt = value(payment, 'SubmittedAt');
            var statusText = status === 'UNPAID' && submittedAt ? 'AWAITING OWNER CONFIRMATION' : status;
            var action = '';
            if (isOwner && status === 'UNPAID' && submittedAt) {
                action = '<button type="button" class="btn btn-sm btn-success" data-confirm-payment="' + esc(value(payment, 'Id')) + '">Confirm received</button>';
            } else if (!isOwner && submittedAt && status !== 'DISPUTED' && status !== 'REFUNDED') {
                action = '<button type="button" class="btn btn-sm btn-outline-danger" data-dispute-payment="' + esc(value(payment, 'Id')) + '">Dispute</button>';
            }
            var method = value(payment, 'PaymentMethod') || '—';
            var reference = value(payment, 'TransactionReference');
            return '<tr><td>' + esc(String(value(payment, 'ChargeType') || '').replace(/_/g, ' ')) + '</td>' +
                '<td>' + money(value(payment, 'Amount')) + '</td>' +
                '<td>' + esc(method) + (reference ? '<br><small>Ref: ' + esc(reference) + '</small>' : '') + '</td>' +
                '<td>' + esc(statusText) + '</td><td>' + esc(dateText(submittedAt || value(payment, 'CreatedAt'))) + '</td>' +
                '<td>' + action + '</td></tr>';
        }).join('');
    }

    function render() {
        renderCharges();
        renderHistory();
    }

    function refresh() {
        var url = root.getAttribute('data-details-url') + '?bookingId=' + encodeURIComponent(bookingId);
        return get(url).then(function (details) { state = details; render(); }).catch(function (error) {
            historyBody.innerHTML = '<tr><td colspan="6" class="text-danger">' + esc(error.message) + '</td></tr>';
        });
    }

    methodSelect.addEventListener('change', function () {
        var useReference = methodSelect.value === 'GCASH';
        referenceField.hidden = !useReference;
        if (!useReference) referenceInput.value = '';
    });

    document.getElementById('payment-entry-form').addEventListener('submit', function (event) {
        event.preventDefault();
        var button = this.querySelector('button[type="submit"]');
        button.disabled = true;
        post(root.getAttribute('data-submit-url'), {
            BookingId: bookingId,
            ChargeType: chargeSelect.value,
            PaymentMethod: methodSelect.value,
            TransactionReference: referenceInput.value.trim()
        }).then(function () {
            showMessage('Payment details saved. The owner still needs to confirm receipt.', false);
            return refresh();
        }).catch(function (error) { showMessage(error.message, true); })
          .then(function () { button.disabled = false; });
    });

    document.getElementById('late-fee-finalize-form').addEventListener('submit', function (event) {
        event.preventDefault();
        if (!state || !value(state, 'RentalId')) { showMessage('No rental record is available for this late fee.', true); return; }
        var button = this.querySelector('button[type="submit"]');
        button.disabled = true;
        post(root.getAttribute('data-finalize-url'), {
            RentalId: value(state, 'RentalId'),
            FinalAmount: document.getElementById('late-fee-final-amount').value
        }).then(function () {
            showMessage('Final late fee saved.', false);
            return refresh();
        }).catch(function (error) { showMessage(error.message, true); })
          .then(function () { button.disabled = false; });
    });

    document.getElementById('owner-confirm-return-button').addEventListener('click', function () {
        if (!window.confirm('Confirm that you received the returned item? The late fee will be finalized from the recorded return time.')) return;
        var button = this;
        button.disabled = true;
        post(root.getAttribute('data-confirm-return-url'), { id: bookingId }).then(function (result) {
            showMessage(result.message || 'Return confirmed and late fee finalized.', false);
            return refresh();
        }).catch(function (error) { showMessage(error.message, true); button.disabled = false; });
    });

    historyBody.addEventListener('click', function (event) {
        var confirmButton = event.target.closest('[data-confirm-payment]');
        if (confirmButton) {
            if (!window.confirm('Confirm that you actually received this payment?')) return;
            confirmButton.disabled = true;
            post(root.getAttribute('data-confirm-url'), { PaymentId: confirmButton.getAttribute('data-confirm-payment') })
                .then(function () { showMessage('Receipt confirmed and payment marked paid.', false); return refresh(); })
                .catch(function (error) { showMessage(error.message, true); confirmButton.disabled = false; });
            return;
        }
        var disputeButton = event.target.closest('[data-dispute-payment]');
        if (disputeButton) {
            var reason = window.prompt('Why are you disputing this payment?');
            if (!reason || !reason.trim()) return;
            disputeButton.disabled = true;
            post(root.getAttribute('data-dispute-url'), { PaymentId: disputeButton.getAttribute('data-dispute-payment'), Reason: reason.trim() })
                .then(function () { showMessage('Payment dispute recorded.', false); return refresh(); })
                .catch(function (error) { showMessage(error.message, true); disputeButton.disabled = false; });
        }
    });

    refresh();
}());
