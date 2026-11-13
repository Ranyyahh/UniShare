(function () {
    'use strict';

    var root = document.querySelector('.bi-page');
    if (!root) { return; }
    window.UniShareBorrowed = true;

    var PESO = '\u20B1';
    var data = { items: [], categories: [] };

    // FIX: Safely check if the JSON data tag exists before reading it
    var dataEl = document.getElementById('bi-data');
    if (dataEl && dataEl.textContent) {
        try { data = JSON.parse(dataEl.textContent); }
        catch (err) { if (window.console) { console.error('Borrowed Items: could not parse page data', err); } }
    } else {
        if (window.console) { console.warn('Borrowed Items: #bi-data tag not found in HTML.'); }
    }

    /* ------------------------------------------------------------------ state */
    var LATE_RATE = Number(data.lateRate) || 1;
    var GRACE_MIN = (data.graceMinutes === undefined || data.graceMinutes === null) ? 30 : Number(data.graceMinutes);
    var items = data.items || [];
    var categoryOrder = data.categories || [];
    var now = Date.now();

    items.forEach(function (it) {
        it.endsAt = (it.remaining !== null && it.remaining !== undefined) ? now + it.remaining * 1000 : null;
        it.pausedMs = (it.pausedMs !== null && it.pausedMs !== undefined) ? Number(it.pausedMs) : null;
        it.lateMs = (it.lateMs !== null && it.lateMs !== undefined) ? Number(it.lateMs) : null;

    });

    items.forEach(function (it) { it.stage = isInUse(it) ? stageOf(it) : 'none'; });
    var state = { status: root.getAttribute('data-initial') || 'Pending', category: '', q: '', detailId: null, pendingId: null };

    /* --------------------------------------------------------------- elements */
    function $(id) { return document.getElementById(id); }
    var grid = $('bi-grid'), emptyBox = $('bi-empty'), resultEl = $('bi-result');
    var search = $('bi-search'), catBtn = $('bi-cat-btn'), catMenu = $('bi-cat-menu'), catLabel = $('bi-cat-label');
    var tabsBox = root.querySelector('.bi-tabs'), thumb = root.querySelector('.bi-tabs__thumb');
    var tabs = [].slice.call(root.querySelectorAll('.bi-tab'));
    var detail = $('bi-detail'), detailBody = $('bi-detail-body');
    var toastEl = $('bi-toast'), toastTimer = null, thumbReady = false;

    /* ---------------------------------------------------------------- helpers */
    function esc(v) {
        return String(v === null || v === undefined ? '' : v).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }
    function pad(n) { return (n < 10 ? '0' : '') + n; }
    function clock(ms) {
        var s = Math.max(0, Math.floor(ms / 1000));
        return pad(Math.floor(s / 3600)) + ':' + pad(Math.floor((s % 3600) / 60)) + ':' + pad(s % 60);
    }
    function today() { return new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric' }); }
    function byId(id) { for (var i = 0; i < items.length; i++) { if (items[i].id === id) { return items[i]; } } return null; }
    function isAwaiting(it) { return !!it.canConfirmHandover; }
    function isInUse(it) { return (it.status === 'Ongoing' || it.status === 'Return Requested') && it.handover; }
    function lower(s) { return String(s).toLowerCase(); }
    function money(n) { return PESO + n.toFixed(2); }
    function human(ms) {
        var total = Math.floor(ms / 60000);
        if (total < 1) { return 'under 1 min'; }
        var h = Math.floor(total / 60), m = total % 60;
        return h ? h + ' hr' + (m ? ' ' + m + ' min' : '') : m + ' min';
    }
    function perMinute(it) { return it.price / 60 * LATE_RATE; }

    function lateInfo(it) {
        var ms = 0;
        if (isInUse(it) && it.endsAt !== null) { ms = Date.now() - it.endsAt; }
        else if (it.lateMs) { ms = it.lateMs; }
        ms = Math.max(0, ms);
        var graceMs = GRACE_MIN * 60000;
        var chargeMs = Math.max(0, ms - graceMs);
        var minutes = Math.floor(chargeMs / 60000);
        var fee = Math.round(minutes * perMinute(it) * 100) / 100;
        return {
            ms: ms, graceMs: graceMs, inGrace: ms > 0 && ms <= graceMs, over: ms > graceMs,
            minutes: minutes, fee: fee, live: isInUse(it)
        };
    }
    function stageOf(it) { var l = lateInfo(it); return l.over ? 'late' : (l.ms > 0 ? 'grace' : 'none'); }

    function ratingText(value) { return value === null || value === undefined || isNaN(Number(value)) ? 'New' : Number(value).toFixed(1); }

    var SVG_CHECK = '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><path d="M5 12l4 4 10-10" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/></svg>';
    var SVG_CLOCK = '<svg viewBox="0 0 24 24" width="13" height="13" aria-hidden="true"><circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" stroke-width="2"/><path d="M12 7v5l3 2" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>';
    var SVG_ALERT = '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" stroke-width="2"/><path d="M12 7v6M12 16.5v.5" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>';
    var SVG_RETURN = '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><path d="M9 7L4 12l5 5M4 12h11a5 5 0 010 10h-2" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" transform="translate(0 -4)"/></svg>';
    var SVG_MSG = '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><path d="M4 5h16v11H9l-5 4z" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/></svg>';

    function timerInfo(it) {
        var full = it.hours * 3600000, late = lateInfo(it), rate = money(perMinute(it));
        if (it.status === 'Disputed') {
            var dnote = 'Paused while the dispute is under review.';
            if (late.over) { dnote = 'Overdue by ' + human(late.ms) + ' before the dispute. Late fee ' + money(late.fee) + ' is on hold.'; }
            else if (late.ms > 0) { dnote = 'Past due (within the grace period) before the dispute. No late fee so far.'; }
            return { label: 'Timer paused', value: clock(it.pausedMs !== null ? it.pausedMs : full), cls: 'paused', note: dnote };
        }
        if (it.status === 'Completed') {
            var cnote = 'Returned on ' + (it.returnedOn || it.steps.completed || 'the agreed date') + '.';
            if (late.over) { cnote += ' Returned ' + human(late.ms) + ' late - late fee ' + money(late.fee) + '.'; }
            else if (late.ms > 0) { cnote += ' Returned within the grace period - no late fee.'; }
            return { label: 'Rental completed', value: clock(full), cls: late.over ? 'late' : 'done', note: cnote };
        }
        if (!it.handover) {
            return { label: 'Rental duration', value: clock(full), cls: 'idle', note: 'The timer starts once both parties confirm the handover.' };
        }
        var left = it.endsAt - Date.now();
        if (left >= 0) {
            return {
                label: 'Time remaining', value: clock(left), cls: left < 600000 ? 'warn' : 'live',
                note: 'Return the item before the timer ends. After that you get a ' + GRACE_MIN + '-minute grace period before late fees start.'
            };
        }
        if (late.inGrace) {
            return {
                label: 'Grace period', value: clock(late.graceMs - late.ms), cls: 'warn',
                note: 'Rental time is over. Return the item within the grace period to avoid a late fee (' + rate + ' per minute after).'
            };
        }
        return {
            label: 'Overdue by', value: clock(late.ms), cls: 'late',
            note: 'Grace period ended. Late fee so far: ' + money(late.fee) + ' (' + rate + ' for every minute after the ' + GRACE_MIN + '-minute grace period). Please return the item now.'
        };
    }

    function starsStatic(n, large) {
        var h = '<span class="bi-stars' + (large ? ' bi-stars--lg' : '') + '" aria-label="Rated ' + n + ' out of 5">';
        for (var i = 1; i <= 5; i++) { h += '<span class="' + (i <= n ? 'is-on' : '') + '">&#9733;</span>'; }
        return h + '</span>';
    }

    function media(it) {
        if (!it.image) { return '<span class="bi-nophoto">No photo</span>'; }
        return '<img class="bi-photo" src="' + esc(it.image) + '" alt="' + esc(it.name) + '" loading="lazy" style="object-fit: cover;" />';
    }
    function tile(it, large) {
        return '<div class="bi-tile bi-tile--' + esc(it.theme) + (large ? ' bi-tile--lg' : '') + '">' + media(it) + '</div>';
    }
    function statusPill(it, solid) {
        var st = lower(it.status).replace(/\s+/g, '-');
        return '<span class="bi-status bi-status--' + st + (solid ? ' bi-status--solid' : '') + '"><i></i>' + esc(it.status) + '</span>';
    }

    function facts(it) {
        function row(k, v, cls) { return '<div><dt>' + k + '</dt><dd' + (cls ? ' class="' + cls + '"' : '') + '>' + esc(v) + '</dd></div>'; }
        return '<dl class="bi-facts">' + row('Rental Period', it.period) + row('Pick-up Point', it.pickup) +
            row('Transaction ID', it.txn) + row('Total Paid', PESO + it.total, 'bi-facts__total') + '</dl>';
    }

    function factsFull(it) {
        var late = lateInfo(it), fee = Number(it.total), id = it.id;
        if (isNaN(fee)) { fee = it.price * it.hours; }
        function row(k, v, cls, attr) { return '<div><dt>' + k + '</dt><dd' + (cls ? ' class="' + cls + '"' : '') + (attr || '') + '>' + v + '</dd></div>'; }
        var overdue, overCls = '';
        if (late.over) {
            overdue = late.live ? '<span data-lover="' + id + '">' + human(late.ms) + ' late</span>' : human(late.ms) + ' late' + (it.status === 'Disputed' ? ' (on hold)' : '');
            overCls = 'bi-facts__late';
        } else if (late.ms > 0) { overdue = late.live ? 'In grace period' : 'Within grace period'; overCls = 'bi-facts__grace'; }
        else if (isAwaiting(it)) { overdue = 'Not started'; }
        else if (isInUse(it)) { overdue = 'Not overdue'; }
        else if (it.status === 'Disputed') { overdue = 'None'; }
        else { overdue = 'Returned on time'; }
        var calc = late.minutes > 0 ? ' <small data-lcalc="' + id + '">(' + late.minutes + ' min x ' + money(perMinute(it)) + ')</small>' : '';
        return '<dl class="bi-facts">' +
            row('Rental Period', esc(it.period)) + row('Pick-up Point', esc(it.pickup)) +
            row('Payment Method', esc(it.payment)) + row('Transaction ID', esc(it.txn)) +
            row('Rental Fee <small>(' + PESO + it.price + '/hr x ' + it.hours + ' hrs)</small>', PESO + fee.toFixed(2)) +
            row('Overdue', overdue, overCls) +
            row('Late Fee' + calc, money(late.fee), late.over ? 'bi-facts__late' : '', late.over && late.live ? ' data-lfee="' + id + '"' : '') +
            row('Total Amount', money(fee + late.fee), 'bi-facts__total', ' data-ltotal="' + id + '"') +
            '</dl><p class="bi-facts__note">Grace period: ' + GRACE_MIN + ' minutes with no late fee. After that: ' + money(perMinute(it)) +
            ' for every minute late. </p>';
    }

    function cardHTML(it) {
        var st = lower(it.status).replace(/\s+/g, '-');
        var tag = '<span class="bi-duration">' + SVG_CLOCK + it.hours + ' hrs</span>';
        var foot = '';

        if (it.status === 'Pending' && it.borrowerHandoverConfirmed) {
            foot = '<div class="bi-notice">' + SVG_CLOCK + ' Waiting for the lender to confirm handover</div>';
        } else if (it.status === 'Pending' && !it.canConfirmHandover) {
            foot = '<div class="bi-notice">' + SVG_CLOCK + ' Waiting for the lender to approve this request</div>';
        } else if (isAwaiting(it)) {
            foot = '<button type="button" class="us-btn us-btn--primary us-btn--block" data-act="handover" data-id="' + it.id + '">' + SVG_CHECK + ' Confirm Handover</button>';
        } else if (it.returnRequested) {
            foot = '<div class="bi-notice">' + SVG_RETURN + ' Return request sent to the lender</div>';
        } else if (isInUse(it)) {
            var t = timerInfo(it);
            foot = '<div class="bi-card__timer bi-card__timer--' + t.cls + '" data-tbox-mini="' + it.id + '">' + SVG_CLOCK +
                '<span data-tlabel="' + it.id + '">' + t.label + '</span><b data-tmini="' + it.id + '">' + t.value + '</b></div>';
        } else if (it.status === 'Completed') {
            if (it.rating) {
                foot = '<div class="bi-review"><span class="bi-review__label">Rating &amp; Review</span><div class="bi-review__row">' + starsStatic(it.rating) + '<span class="bi-review__done">Review submitted</span></div></div>';
            } else {
                foot = '<button type="button" class="us-btn us-btn--primary us-btn--block" data-act="rate" data-id="' + it.id + '">To Rate</button>';
            }
        } else {
            foot = '<div class="bi-notice bi-notice--danger">' + SVG_ALERT + ' Dispute under review by admin</div>';
        }

        return '<article class="bi-card bi-card--' + st + '" tabindex="0" role="button" aria-haspopup="dialog" data-id="' + it.id +
            '" aria-label="View rental details for ' + esc(it.name) + '">' +
            '<div class="bi-tile bi-tile--' + esc(it.theme) + '">' + statusPill(it, false) + tag + media(it) + '</div>' +
            '<div class="bi-body"><span class="bi-cat">' + esc(it.category) + '</span>' +
            '<h3 class="bi-name" style="display:flex; justify-content:space-between; align-items:flex-start;">' +
            '<span>' + esc(it.name) + '</span>' +
            '<span class="card-rating">' + (it.itemRating ? '<span class="star-icon">&#9733;</span> ' + ratingText(it.itemRating) : '') + '</span>' +
            '</h3>' +
            '<div class="bi-meta" style="margin-bottom: 4px;"><span class="bi-price">' + PESO + it.price + '<small>/hr</small></span>' +
            '<span class="bi-lender" style="display:flex; align-items:center; gap:4px;">Lender: <strong>' + esc(it.lender) + '</strong> <span class="owner-rating">' + (it.ownerRating ? '<span class="star-icon">&#9733;</span> ' + ratingText(it.ownerRating) : '') + '</span></span></div>' +
            '<div class="bi-foot">' + foot + '</div></div></article>';
    }

    function stepsHTML(it) {
        var late = lateInfo(it);
        var handoverDate = it.steps.handover;
        if (isInUse(it) && !handoverDate) { handoverDate = today(); }
        var seq = [{ l: 'Requested', d: it.steps.requested }, { l: 'Approved', d: it.steps.approved }, { l: 'Handover', d: handoverDate }];
        if (late.ms > 0) { seq.push({ l: 'Overdue', late: true }); }
        seq.push(it.returnRequested ? { l: 'Return Requested', d: it.returnRequestedOn || 'Awaiting lender confirmation' } : { l: 'Completed', d: it.steps.completed });
        var last = seq.length - 1, firstPending = -1;
        for (var k = 0; k < seq.length; k++) { if (!seq[k].late && !seq[k].d) { firstPending = k; break; } }
        var h = '<ol class="bi-steps">';
        for (var i = 0; i < seq.length; i++) {
            var s = seq[i], stt, label = s.l, when, fee = '', dot = '';
            if (s.late) {
                stt = late.over ? 'late' : 'grace';
                if (late.over) {
                    when = late.live ? '<span data-lstep="' + it.id + '">' + human(late.ms) + ' late</span>' : 'Late ' + human(late.ms);
                    fee = '<small class="bi-step__fee" data-lfee-step="' + it.id + '">' + money(late.fee) + ' late fee</small>';
                } else {
                    when = late.live ? 'In grace period' : 'Within grace period';
                    fee = '<small class="bi-step__fee bi-step__fee--grace">No late fee</small>';
                }
                dot = '<svg viewBox="0 0 24 24" width="12" height="12" aria-hidden="true"><path d="M12 6v8M12 17.5v.5" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"/></svg>';
            } else {
                stt = s.d ? 'done' : (i === firstPending ? 'current' : 'todo');
                when = esc(s.d || (stt === 'current' ? 'In progress' : 'Pending'));
                if (it.status === 'Disputed' && i === last) { stt = 'bad'; label = 'Disputed'; when = 'Under review'; }
                if (stt === 'done') { dot = '<svg viewBox="0 0 24 24" width="12" height="12" aria-hidden="true"><path d="M5 12l4 4 10-10" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/></svg>'; }
                else if (stt === 'bad') { dot = '<svg viewBox="0 0 24 24" width="12" height="12" aria-hidden="true"><path d="M12 6v8M12 17.5v.5" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"/></svg>'; }
            }
            h += '<li class="bi-step bi-step--' + stt + '"><span class="bi-step__dot">' + dot + '</span><strong>' + label + '</strong><small>' + when + '</small>' + fee + '</li>';
        }
        return h + '</ol>';
    }

    function timerPanel(it) {
        var t = timerInfo(it);
        return '<div class="bi-timer bi-timer--' + t.cls + '" data-tbox="' + it.id + '">' +
            '<span class="bi-timer__icon">' + SVG_CLOCK + '</span>' +
            '<div class="bi-timer__main"><span class="bi-timer__label" data-tbox-label="' + it.id + '">' + t.label + '</span>' +
            '<strong class="bi-timer__value" data-tbox-value="' + it.id + '">' + t.value + '</strong></div>' +
            '<span class="bi-timer__note" data-tbox-note="' + it.id + '">' + esc(t.note) + '</span></div>';
    }

    function detailHTML(it) {
        var actions = '';
        if (it.status === 'Pending' && it.borrowerHandoverConfirmed) {
            actions = '<div class="bi-notice bi-notice--block">' + SVG_CLOCK + ' Your handover confirmation is saved. The rental starts after the lender confirms.</div>';
        } else if (it.status === 'Pending' && !it.canConfirmHandover) {
            actions = '<div class="bi-notice bi-notice--block">' + SVG_CLOCK + ' Your request is waiting for the lender to approve it.</div>';
        } else if (isAwaiting(it)) {
            actions = '<div class="bi-modal-actions"><button type="button" class="us-btn us-btn--danger" data-act="report" data-id="' + it.id + '">Report a problem</button>' +
                '<button type="button" class="us-btn us-btn--primary" data-act="handover" data-id="' + it.id + '">' + SVG_CHECK + ' Confirm Handover</button></div>';
        } else if (it.returnRequested) {
            actions = '<div class="bi-notice bi-notice--block">' + SVG_RETURN + ' Return request sent to the lender' + (it.returnRequestedOn ? ' on ' + esc(it.returnRequestedOn) : '') + '. Completion is recorded after lender confirmation.</div>';
        } else if (isInUse(it)) {
            actions = '<div class="bi-modal-actions"><button type="button" class="us-btn us-btn--danger" data-act="report" data-id="' + it.id + '">Report a problem</button>' +
                '<button type="button" class="us-btn us-btn--primary" data-act="return" data-id="' + it.id + '">' + SVG_RETURN + ' Return Item</button></div>';
        } else if (it.status === 'Completed') {
            actions = '<h4 class="bi-h4">Rating &amp; Review</h4>';
            if (it.rating) {
                actions += '<div class="bi-review__row">' + starsStatic(it.rating, true) + '<span class="bi-review__done">Review submitted</span></div>' +
                    (it.review ? '<p class="bi-review__text">&ldquo;' + esc(it.review) + '&rdquo;</p>' : '');
            } else {
                actions += '<button type="button" class="us-btn us-btn--primary us-btn--block" data-act="rate" data-id="' + it.id + '">To Rate</button>';
            }
        } else {
            actions = '<div class="bi-notice bi-notice--danger bi-notice--block"><strong>Dispute under review by admin</strong><p>' + esc(it.dispute) +
                '</p><small>You will be notified once the admin resolves this case.</small></div>';
        }

        return '<div class="bi-dialog__body"><aside class="bi-dialog__side">' + tile(it, true) +
            '<div class="bi-lender-card"><div class="bi-lender-card__top"><span class="us-avatar">' + esc(it.initials) + '</span>' +
            '<div style="flex:1; display:flex; justify-content:space-between; align-items:flex-start;">' +
            '<div><strong>' + esc(it.lender) + '</strong><small>Lender &bull; ' + esc(it.program) + '</small></div>' +
            '<span style="font-size:12px; color:var(--text-secondary); font-weight:600; display:flex; align-items:center; gap:2px;">' + (it.ownerRating ? '<span style="color:#f59e0b; font-size:14px;">&#9733;</span> ' + ratingText(it.ownerRating) : 'New') + '</span>' +
            '</div></div>' +
            '<button type="button" class="us-btn us-btn--outline us-btn--block" data-msg-lender="' + esc(it.lender) + '" data-msg-item="' + esc(it.name) + '" data-owner-id="' + esc(it.ownerId) + '" data-item-id="' + esc(it.itemId) + '">' + SVG_MSG + ' Message Lender</button></div>' +
            '<div class="bi-about"><h4>About this item</h4><p>' + esc(it.about) + '</p></div></aside>' +
            '<section class="bi-dialog__main">' + statusPill(it, true) + '<span class="bi-cat">' + esc(it.category) + '</span>' +
            '<h3 class="bi-dialog__name" style="display:flex; justify-content:space-between; align-items:flex-start;">' +
            '<span>' + esc(it.name) + '</span>' +
            '<span style="font-size:16px; color:var(--text-secondary); font-weight:600; display:flex; align-items:center; gap:2px;">' + (it.itemRating ? '<span style="color:#f59e0b;">&#9733;</span> ' + ratingText(it.itemRating) : '') + '</span>' +
            '</h3>' +
            '<div class="bi-dialog__price">' + PESO + it.price + '<small>/hr</small></div>' +
            '<div class="bi-factbox">' + factsFull(it) + '</div>' + timerPanel(it) +
            '<h4 class="bi-h4">Rental Progress</h4>' + stepsHTML(it) + actions + '</section></div>';
    }

    function matches(it) {
        if (state.status && it.status !== state.status && !(state.status === 'Ongoing' && it.status === 'Return Requested')) { return false; }
        if (state.category && it.category !== state.category) { return false; }
        if (state.q) {
            var hay = lower(it.name + ' ' + it.lender + ' ' + it.txn + ' ' + it.category);
            if (hay.indexOf(state.q) === -1) { return false; }
        }
        return true;
    }

    function renderGrid() {
        var list = items.filter(matches);
        grid.innerHTML = list.map(cardHTML).join('');
        grid.hidden = list.length === 0;
        emptyBox.hidden = list.length !== 0;
        resultEl.textContent = 'Showing ' + list.length + ' of ' + items.length + ' item' + (items.length === 1 ? '' : 's');
    }

    function renderTabs() {
        tabs.forEach(function (t) {
            var s = t.getAttribute('data-status');
            var n = s ? items.filter(function (i) { return i.status === s || (s === 'Ongoing' && i.status === 'Return Requested'); }).length : items.length;
            t.querySelector('span').textContent = n;
            t.setAttribute('aria-selected', s === state.status ? 'true' : 'false');
        });
        moveThumb();
    }

    function renderDetail() {
        if (state.detailId === null) { return; }
        var it = byId(state.detailId);
        if (it) { detailBody.innerHTML = detailHTML(it); }
    }

    function render() { renderTabs(); renderGrid(); renderDetail(); }

    function moveThumb() {
        if (!thumb) { return; }
        var active = root.querySelector('.bi-tab[aria-selected="true"]');
        if (!active || !active.offsetWidth) { return; }
        if (!thumbReady) { thumb.style.transition = 'none'; }
        thumb.style.width = active.offsetWidth + 'px';
        thumb.style.transform = 'translateX(' + active.offsetLeft + 'px)';
        if (!thumbReady) { void thumb.offsetWidth; thumb.style.transition = ''; thumbReady = true; }
    }

    function buildCategoryMenu() {
        var names = categoryOrder.slice();
        items.forEach(function (i) { if (names.indexOf(i.category) === -1) { names.push(i.category); } });
        var html = '<li><button type="button" role="option" class="is-selected" data-value="">All Categories <span>' + items.length + '</span></button></li>';
        names.forEach(function (n) {
            var c = items.filter(function (i) { return i.category === n; }).length;
            if (c) { html += '<li><button type="button" role="option" data-value="' + esc(n) + '">' + esc(n) + ' <span>' + c + '</span></button></li>'; }
        });
        catMenu.innerHTML = html;
    }

    function buildTransactionHistory() {
        var tbody = $('bi-history-body');
        if (!tbody) return;

        var completedItems = items.filter(function (i) { return ['Completed', 'Cancelled', 'Declined', 'Rejected'].indexOf(i.status) >= 0; });

        if (completedItems.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:var(--text-secondary); padding: 40px;">No transactions found.</td></tr>';
            return;
        }

        tbody.innerHTML = completedItems.map(function (it) {
            return '<tr>' +
                '<td><strong>' + esc(it.txn) + '</strong></td>' +
                '<td>' + esc(it.steps.requested || it.period.split('\u2013')[0].trim()) + '</td>' +
                '<td><strong>' + esc(it.name) + '</strong></td>' +
                '<td>' + esc(it.lender) + '</td>' +
                '<td>' + esc(it.status) + '</td>' +
                '<td class="us-right"><span>' + PESO + it.total + '</span></td>' +
                '</tr>';
        }).join('');
    }

    function tick() {
        var changed = false;
        items.forEach(function (it) {
            if (!isInUse(it)) { return; }
            var st = stageOf(it);
            if (st !== it.stage) { it.stage = st; changed = true; }
        });
        if (changed) { render(); return; }
        items.forEach(function (it) {
            if (!isInUse(it)) { return; }
            var t = timerInfo(it), late = lateInfo(it), id = it.id;
            var mini = root.querySelector('[data-tmini="' + id + '"]');
            if (mini) {
                mini.textContent = t.value;
                root.querySelector('[data-tlabel="' + id + '"]').textContent = t.label;
                root.querySelector('[data-tbox-mini="' + id + '"]').className = 'bi-card__timer bi-card__timer--' + t.cls;
            }
            var box = root.querySelector('[data-tbox="' + id + '"]');
            if (box) {
                box.className = 'bi-timer bi-timer--' + t.cls;
                box.querySelector('[data-tbox-label]').textContent = t.label;
                box.querySelector('[data-tbox-value]').textContent = t.value;
                box.querySelector('[data-tbox-note]').textContent = t.note;
            }
            if (late.over) {
                [].slice.call(root.querySelectorAll('[data-lfee="' + id + '"]')).forEach(function (el) { el.textContent = money(late.fee); });
                [].slice.call(root.querySelectorAll('[data-lfee-step="' + id + '"]')).forEach(function (el) { el.textContent = money(late.fee) + ' late fee'; });
                [].slice.call(root.querySelectorAll('[data-lstep="' + id + '"]')).forEach(function (el) { el.textContent = human(late.ms) + ' late'; });
                [].slice.call(root.querySelectorAll('[data-lover="' + id + '"]')).forEach(function (el) { el.textContent = human(late.ms) + ' late'; });
                [].slice.call(root.querySelectorAll('[data-ltotal="' + id + '"]')).forEach(function (el) { el.textContent = money(Number(it.total) + late.fee); });
                [].slice.call(root.querySelectorAll('[data-lcalc="' + id + '"]')).forEach(function (el) { el.textContent = '(' + late.minutes + ' min x ' + money(perMinute(it)) + ')'; });
            }
        });
    }
    setInterval(tick, 1000);

    function toast(msg) {
        toastEl.textContent = msg;
        toastEl.classList.add('is-on');
        try { if (toastEl.showPopover) { if (toastEl.matches(':popover-open')) { toastEl.hidePopover(); } toastEl.showPopover(); } } catch (e) { }
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () {
            toastEl.classList.remove('is-on');
            try { if (toastEl.hidePopover && toastEl.matches(':popover-open')) { toastEl.hidePopover(); } } catch (e) { }
        }, 3800);
    }

    function openDialog(d) { if (d && typeof d.showModal === 'function' && !d.open) { d.showModal(); } }
    function closeDialog(d) { if (d && d.open) { d.close(); } }

    function postAction(url, values, callback) {
        var token = root.querySelector('input[name="__RequestVerificationToken"]');
        var body = new URLSearchParams();
        body.append('__RequestVerificationToken', token ? token.value : '');
        Object.keys(values).forEach(function (key) { body.append(key, values[key] === null || values[key] === undefined ? '' : values[key]); });
        fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' }, body: body.toString() })
            .then(function (response) { return response.json().then(function (result) { if (!response.ok) { throw new Error(result.message || 'The request could not be completed.'); } return result; }); })
            .then(function (result) { if (!result.ok) { throw new Error(result.message || 'The request could not be completed.'); } callback(result); })
            .catch(function (error) { toast(error.message || 'The request could not be completed. Refresh and try again.'); });
    }

    function postCondition(id, phase, formId, callback) {
        var formEl = $(formId);
        var files = formEl.querySelector('[name="conditionPhotos"]').files;
        if (files.length > 5) { toast('Upload no more than five condition photos.'); return; }
        var body = new FormData();
        var token = root.querySelector('input[name="__RequestVerificationToken"]');
        body.append('id', id);
        body.append('phase', phase);
        body.append('condition', formEl.querySelector('[name="condition"]').value);
        body.append('notes', formEl.querySelector('[name="conditionNotes"]').value);
        if (token) body.append('__RequestVerificationToken', token.value);
        Array.prototype.forEach.call(files, function (file) { body.append('photos', file, file.name); });
        fetch(root.getAttribute('data-condition-url'), { method: 'POST', credentials: 'same-origin', body: body })
            .then(function (response) { return response.json().then(function (result) { if (!response.ok || !result.ok) throw new Error(result.message || 'Condition details could not be saved.'); return result; }); })
            .then(callback)
            .catch(function (error) { toast(error.message || 'Condition details could not be saved.'); });
    }

    function openAsk(dialogId, formId, id, fields) {
        var it = byId(id);
        if (!it) { return; }
        state.pendingId = id;
        $(formId).reset();
        fields.forEach(function (f) { $(f[0]).textContent = f[1](it); });
        openDialog($(dialogId));
    }

    function doHandover(id) {
        var it = byId(id);
        if (!it || !isAwaiting(it)) { return; }
        postCondition(id, 'HANDOVER', 'bi-confirm-form', function () {
            postAction(root.getAttribute('data-handover-url'), { id: id, received: true }, function (result) {
                it.borrowerHandoverConfirmed = true; it.canConfirmHandover = false;
                it.handover = !!it.ownerHandoverConfirmed;
                if (it.handover) {
                    it.status = 'Ongoing'; it.endsAt = Date.now() + it.hours * 3600000; it.steps.handover = today();
                } else {
                    it.status = 'Pending'; it.endsAt = null;
                }
                closeDialog($('bi-confirm'));
                render(); toast(result.message);
            });
        });
    }

    function doReturn(id) {
        var it = byId(id);
        if (!it || !isInUse(it)) { return; }
        postCondition(id, 'RETURN', 'bi-return-form', function () {
            postAction(root.getAttribute('data-return-url'), { id: id, returned: true }, function (result) {
                it.status = 'Return Requested'; it.returnRequested = true;
                it.returnRequestedOn = new Date().toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
                closeDialog($('bi-return'));
                render(); toast(result.message);
            });
        });
    }

    function doDispute(id, reason, details) {
        var it = byId(id);
        if (!it || it.status === 'Disputed' || it.status === 'Completed' || it.returnRequested) { return; }
        postAction(root.getAttribute('data-dispute-url'), { id: id, reason: reason, details: details }, function (result) {
            if (it.endsAt !== null) { var left = it.endsAt - Date.now(); it.pausedMs = left >= 0 ? left : 0; if (left < 0) it.lateMs = -left; it.endsAt = null; }
            it.status = 'Disputed'; it.dispute = details ? reason + ': ' + details : reason; render(); toast(result.message);
        });
    }

    function doRate(id, itemRating, ownerRating, review) {
        var it = byId(id);
        if (!it || it.status !== 'Completed' || it.rating) { return; }
        postAction(root.getAttribute('data-rate-url'), { id: id, itemRating: itemRating, ownerRating: ownerRating, review: review || '' }, function (result) {
            it.rating = itemRating; it.itemRating = itemRating; it.review = review || ''; render(); toast(result.message);
        });
    }

    function act(name, id) {
        if (name === 'handover') { openAsk('bi-confirm', 'bi-confirm-form', id, [['bi-confirm-item', function (i) { return i.name; }], ['bi-confirm-lender', function (i) { return i.lender; }]]); }
        else if (name === 'return') { openAsk('bi-return', 'bi-return-form', id, [['bi-return-item', function (i) { return i.name; }], ['bi-return-lender', function (i) { return i.lender; }]]); }
        else if (name === 'report') { openAsk('bi-report', 'bi-report-form', id, [['bi-report-item', function (i) { return i.name; }]]); }
        else if (name === 'rate') {
            var it = byId(id);
            if (!it) return;
            state.pendingId = id;
            $('bi-rate-form').reset();
            $('bi-rate-item').textContent = it.name;
            $('bi-rate-lender').textContent = it.lender;
            openDialog($('bi-rate'));
        }
        else if (name === 'history') {
            buildTransactionHistory();
            openDialog($('bi-history'));
        }
    }

    function openDetail(id) {
        state.detailId = id;
        renderDetail();
        openDialog(detail);
    }

    root.addEventListener('click', function (e) {
        var messageButton = e.target.closest('[data-msg-lender]');
        if (messageButton) {
            e.stopPropagation();
            var params = new URLSearchParams({ userId: messageButton.getAttribute('data-owner-id') || '', itemId: messageButton.getAttribute('data-item-id') || '' });
            window.location.href = root.getAttribute('data-messages-url') + '?' + params.toString();
            return;
        }
        var btn = e.target.closest('[data-act]');
        if (btn) { e.stopPropagation(); act(btn.getAttribute('data-act'), btn.getAttribute('data-id')); return; }
        if (e.target.closest('[data-stop]') || e.target.closest('.bi-rate-form')) { return; }
        var card = e.target.closest('.bi-card');
        if (card && grid.contains(card)) { openDetail(card.getAttribute('data-id')); }
    });
    grid.addEventListener('keydown', function (e) {
        if ((e.key === 'Enter' || e.key === ' ') && e.target.classList && e.target.classList.contains('bi-card')) {
            e.preventDefault();
            openDetail(e.target.getAttribute('data-id'));
        }
    });

    $('bi-confirm-form').addEventListener('submit', function (e) { e.preventDefault(); doHandover(state.pendingId); });
    $('bi-return-form').addEventListener('submit', function (e) { e.preventDefault(); doReturn(state.pendingId); });
    $('bi-report-form').addEventListener('submit', function (e) {
        e.preventDefault();
        var f = e.target;
        closeDialog($('bi-report'));
        doDispute(state.pendingId, f.elements.reason.value, f.elements.details.value.trim());
    });

    $('bi-rate-form').addEventListener('submit', function (e) {
        e.preventDefault();
        var f = e.target;
        var itemRating = f.querySelector('input[name="itemRating"]:checked');
        var ownerRating = f.querySelector('input[name="ownerRating"]:checked');
        if (!itemRating || !ownerRating) return;

        closeDialog($('bi-rate'));
        doRate(state.pendingId, parseInt(itemRating.value, 10), parseInt(ownerRating.value, 10), f.elements.review.value.trim());
    });

    [].slice.call(root.querySelectorAll('dialog.bi-dialog')).forEach(function (d) {
        d.addEventListener('click', function (e) { if (e.target === d || e.target.closest('[data-close]')) { d.close(); } });
    });
    detail.addEventListener('close', function () { state.detailId = null; });

    tabs.forEach(function (t, i) {
        t.addEventListener('click', function () {
            state.status = t.getAttribute('data-status');
            grid.classList.remove('is-switching'); void grid.offsetWidth; grid.classList.add('is-switching'); grid.scrollTop = 0;
            renderTabs(); renderGrid();
        });
        t.addEventListener('keydown', function (e) {
            var next = e.key === 'ArrowRight' ? i + 1 : (e.key === 'ArrowLeft' ? i - 1 : -1);
            if (next < 0 || next >= tabs.length) { return; }
            e.preventDefault(); tabs[next].focus(); tabs[next].click();
        });
    });
    if (tabsBox) { tabsBox.classList.add('bi-tabs--js'); }
    window.addEventListener('resize', moveThumb);
    window.addEventListener('load', moveThumb);
    if (window.ResizeObserver && tabsBox) { new ResizeObserver(moveThumb).observe(tabsBox); }
    if (document.fonts && document.fonts.ready) { document.fonts.ready.then(moveThumb); }

    search.addEventListener('input', function () { state.q = lower(search.value.trim()); grid.scrollTop = 0; renderGrid(); });

    function setMenu(open) {
        catMenu.hidden = !open;
        catBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
        catBtn.classList.toggle('is-open', open);
    }
    catBtn.addEventListener('click', function (e) { e.stopPropagation(); setMenu(catMenu.hidden); });
    catMenu.addEventListener('click', function (e) {
        var opt = e.target.closest('button[data-value]');
        if (!opt) { return; }
        e.stopPropagation();
        [].slice.call(catMenu.querySelectorAll('button')).forEach(function (o) { o.classList.remove('is-selected'); });
        opt.classList.add('is-selected');
        state.category = opt.getAttribute('data-value') || '';
        catLabel.textContent = state.category || 'All Categories';
        setMenu(false);
        grid.classList.remove('is-switching'); void grid.offsetWidth; grid.classList.add('is-switching'); grid.scrollTop = 0;
        renderGrid();
    });
    document.addEventListener('click', function (e) { if (!catMenu.hidden && !catMenu.contains(e.target)) { setMenu(false); } });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !catMenu.hidden) { setMenu(false); catBtn.focus(); } });

    root.addEventListener('error', function (e) {
        var img = e.target;
        if (!img || !img.classList || !img.classList.contains('bi-photo')) { return; }
        var ph = document.createElement('span');
        ph.className = 'bi-nophoto';
        ph.textContent = 'No photo';
        img.parentNode.replaceChild(ph, img);
    }, true);

    /* -------------------------------------------------------- image hover preview */
    var hoverPreview = $('bi-hover-preview');
    var hoverImg = $('bi-hover-img');

    if (hoverPreview && hoverImg) {
        // Show preview on hover
        grid.addEventListener('mouseover', function (e) {
            if (e.target && e.target.classList.contains('bi-photo')) {
                var src = e.target.getAttribute('src');
                hoverImg.src = src;

                // Get mouse coordinates to position the pop-up
                var rect = e.target.getBoundingClientRect();

                // Position the preview right above the image
                hoverPreview.style.left = rect.left + 'px';
                hoverPreview.style.top = (rect.top - 10) + 'px'; // 10px gap above the image

                hoverPreview.classList.add('is-visible');
            }
        });

        // Hide preview on mouseout
        grid.addEventListener('mouseout', function (e) {
            if (e.target && e.target.classList.contains('bi-photo')) {
                hoverPreview.classList.remove('is-visible');
            }
        });
    }

    buildCategoryMenu();
    render();
})();
