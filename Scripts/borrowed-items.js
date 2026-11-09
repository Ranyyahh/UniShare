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
        it.pausedMs = null;

        if (it.status === 'Pending') {
            it.ownerConfirmed = false;
            it.borrowerConfirmed = false;
        }
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
    function isAwaiting(it) { return it.status === 'Pending'; }
    function isInUse(it) { return it.status === 'Ongoing' && it.handover; }
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

    function getFakeRating(id) { return (id % 3 === 0) ? '4.8' : (id % 2 === 0 ? '4.5' : '4.9'); }
    function getFakeOwnerRating(id) { return (id % 3 === 0) ? '4.9' : (id % 2 === 0 ? '4.7' : '5.0'); }

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

    var photos = {};
    [].slice.call(document.querySelectorAll('#bi-images img[data-item]')).forEach(function (img) {
        photos[img.getAttribute('data-item')] = { src: img.getAttribute('src'), alt: img.getAttribute('alt') };
    });
    function media(it) {
        var p = photos[it.id];
        if (!p) { return '<span class="bi-nophoto">No photo</span>'; }
        return '<img class="bi-photo" src="' + esc(p.src) + '" alt="' + esc(p.alt || it.name) + '" loading="lazy" style="object-fit: cover;" />';
    }
    function tile(it, large) {
        return '<div class="bi-tile bi-tile--' + esc(it.theme) + (large ? ' bi-tile--lg' : '') + '">' + media(it) + '</div>';
    }
    function statusPill(it, solid) {
        var st = lower(it.status);
        return '<span class="bi-status bi-status--' + st + (solid ? ' bi-status--solid' : '') + '"><i></i>' + esc(it.status) + '</span>';
    }

    function facts(it) {
        function row(k, v, cls) { return '<div><dt>' + k + '</dt><dd' + (cls ? ' class="' + cls + '"' : '') + '>' + esc(v) + '</dd></div>'; }
        return '<dl class="bi-facts">' + row('Rental Period', it.period) + row('Pick-up Point', it.pickup) +
            row('Transaction ID', it.txn) + row('Total Paid', PESO + it.total, 'bi-facts__total') + '</dl>';
    }

    function factsFull(it) {
        var late = lateInfo(it), fee = it.price * it.hours, id = it.id;
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
        var st = lower(it.status);
        var tag = '<span class="bi-duration">' + SVG_CLOCK + it.hours + ' hrs</span>';
        var foot = '';

        if (isAwaiting(it)) {
            // NEW: Check if borrower confirmed, show disabled button if true
            if (it.borrowerConfirmed) {
                foot = '<button type="button" class="us-btn us-btn--primary us-btn--block" disabled>' + SVG_CHECK + ' Handover Confirmed</button>';
            } else {
                foot = '<button type="button" class="us-btn us-btn--primary us-btn--block" data-act="handover" data-id="' + it.id + '">' + SVG_CHECK + ' Confirm Handover</button>';
            }
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
            '<span class="card-rating"><span class="star-icon">&#9733;</span> ' + getFakeRating(it.id) + '</span>' +
            '</h3>' +
            '<div class="bi-meta" style="margin-bottom: 4px;"><span class="bi-price">' + PESO + it.price + '<small>/hr</small></span>' +
            '<span class="bi-lender" style="display:flex; align-items:center; gap:4px;">Lender: <strong>' + esc(it.lender) + '</strong> <span class="owner-rating"><span class="star-icon">&#9733;</span> ' + getFakeOwnerRating(it.id) + '</span></span></div>' +
            '<div class="bi-foot">' + foot + '</div></div></article>';
    }

    function stepsHTML(it) {
        var late = lateInfo(it);
        var handoverDate = it.steps.handover;
        if (isInUse(it) && !handoverDate) { handoverDate = today(); }
        var seq = [{ l: 'Requested', d: it.steps.requested }, { l: 'Approved', d: it.steps.approved }, { l: 'Handover', d: handoverDate }];
        if (late.ms > 0) { seq.push({ l: 'Overdue', late: true }); }
        seq.push({ l: 'Completed', d: it.steps.completed });
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
        if (isAwaiting(it)) {
            if (it.borrowerConfirmed) {
                // Waiting for owner state
                actions = '<div class="bi-notice bi-notice--block" style="background:#f8fafc; border-color:#e2e8f0; color:#64748b;"><strong>Waiting for Owner</strong><p>You have confirmed the handover. Waiting for the owner to confirm as well to start the timer.</p></div>';
            } else {
                // REMOVED the Confirm Handover button, left only the Report a problem button
                actions = '<div class="bi-modal-actions"><button type="button" class="us-btn us-btn--danger" data-act="report" data-id="' + it.id + '">Report a problem</button></div>';
            }
        } else if (isInUse(it)) {
            actions = '<div class="bi-modal-actions"><button type="button" class="us-btn us-btn--danger" data-act="report" data-id="' + it.id + '">Report a problem</button></div>';
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
            '<span style="font-size:12px; color:var(--text-secondary); font-weight:600; display:flex; align-items:center; gap:2px;"><span style="color:#f59e0b; font-size:14px;">&#9733;</span> ' + getFakeOwnerRating(it.id) + '</span>' +
            '</div></div>' +
            '<button type="button" class="us-btn us-btn--outline us-btn--block" data-msg-lender="' + esc(it.lender) + '" data-msg-item="' + esc(it.name) + '">' + SVG_MSG + ' Message Lender</button></div>' +
            '<div class="bi-about"><h4>About this item</h4><p>' + esc(it.about) + '</p></div></aside>' +
            '<section class="bi-dialog__main">' + statusPill(it, true) + '<span class="bi-cat">' + esc(it.category) + '</span>' +
            '<h3 class="bi-dialog__name" style="display:flex; justify-content:space-between; align-items:flex-start;">' +
            '<span>' + esc(it.name) + '</span>' +
            '<span style="font-size:16px; color:var(--text-secondary); font-weight:600; display:flex; align-items:center; gap:2px;"><span style="color:#f59e0b;">&#9733;</span> ' + getFakeRating(it.id) + '</span>' +
            '</h3>' +
            '<div class="bi-dialog__price">' + PESO + it.price + '<small>/hr</small></div>' +
            '<div class="bi-factbox">' + factsFull(it) + '</div>' + timerPanel(it) +
            '<h4 class="bi-h4">Rental Progress</h4>' + stepsHTML(it) + actions + '</section></div>';
    }

    function matches(it) {
        if (state.status && it.status !== state.status) { return false; }
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
            var n = s ? items.filter(function (i) { return i.status === s; }).length : items.length;
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

        var completedItems = items.filter(function (i) { return i.status === 'Completed'; });

        if (completedItems.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-secondary); padding: 40px;">No transactions found.</td></tr>';
            return;
        }

        tbody.innerHTML = completedItems.map(function (it) {
            return '<tr>' +
                '<td><strong>' + esc(it.txn) + '</strong></td>' + // Changed to it.txn
                '<td>' + esc(it.period.split('–')[0].trim()) + '</td>' +
                '<td><strong>' + esc(it.name) + '</strong></td>' +
                '<td>' + esc(it.lender) + '</td>' +
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
                [].slice.call(root.querySelectorAll('[data-ltotal="' + id + '"]')).forEach(function (el) { el.textContent = money(it.price * it.hours + late.fee); });
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

        it.borrowerConfirmed = true;

        if (it.ownerConfirmed) {
            it.status = 'Ongoing';
            it.handover = true;
            it.endsAt = Date.now() + it.hours * 3600000;
            it.steps.handover = today();
            render();
            toast('Both parties confirmed. Your rental timer has started.');
        } else {
            render();
            toast('You confirmed the handover. Waiting for the owner to confirm.');
        }
    }

    function doReturn(id) {
        var it = byId(id);
        if (!it || !isInUse(it)) { return; }
        var lateNow = lateInfo(it);
        if (lateNow.ms > 0) { it.lateMs = lateNow.ms; }
        it.status = 'Completed';
        it.endsAt = null;
        it.steps.completed = today();
        it.returnedOn = new Date().toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
        render();
        var lt = lateInfo(it);
        toast(lt.over ? 'Item returned ' + human(lt.ms) + ' late. A late fee of ' + money(lt.fee) + ' applies.'
            : (lt.ms > 0 ? 'Item returned within the grace period. No late fee.' : 'Item returned. This rental is now completed and ready to rate.'));
    }

    function doDispute(id, reason, details) {
        var it = byId(id);
        if (!it || it.status === 'Disputed' || it.status === 'Completed') { return; }
        if (it.endsAt !== null) {
            var left = it.endsAt - Date.now();
            if (left >= 0) { it.pausedMs = left; } else { it.pausedMs = 0; it.lateMs = -left; }
            it.endsAt = null;
        }
        it.status = 'Disputed';
        it.dispute = details ? reason + ': ' + details : reason;
        render();
        toast('Dispute sent to the admin. The rental timer is paused.');
    }

    function doRate(id, itemRating, ownerRating, review) {
        var it = byId(id);
        if (!it || it.status !== 'Completed' || it.rating) { return; }
        it.rating = itemRating;
        it.review = review || '';
        render();
        toast('Thanks for reviewing ' + it.name + ' and the owner!');
    }

    function act(name, id) {
        if (name === 'handover') { openAsk('bi-confirm', 'bi-confirm-form', id, [['bi-confirm-item', function (i) { return i.name; }], ['bi-confirm-lender', function (i) { return i.lender; }]]); }
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
        var btn = e.target.closest('[data-act]');
        if (btn) { e.stopPropagation(); act(btn.getAttribute('data-act'), parseInt(btn.getAttribute('data-id'), 10)); return; }
        if (e.target.closest('[data-stop]') || e.target.closest('.bi-rate-form')) { return; }
        var card = e.target.closest('.bi-card');
        if (card && grid.contains(card)) { openDetail(parseInt(card.getAttribute('data-id'), 10)); }
    });
    grid.addEventListener('keydown', function (e) {
        if ((e.key === 'Enter' || e.key === ' ') && e.target.classList && e.target.classList.contains('bi-card')) {
            e.preventDefault();
            openDetail(parseInt(e.target.getAttribute('data-id'), 10));
        }
    });

    root.addEventListener('submit', function (e) {
        var form = e.target;
        if (form.hasAttribute('data-rate-form')) {
            e.preventDefault();
            var picked = form.querySelector('input[name="rating"]:checked');
            if (!picked) { return; }
            var ta = form.querySelector('textarea');
            doRate(parseInt(form.getAttribute('data-id'), 10), parseInt(picked.value, 10), parseInt(picked.value, 10), ta ? ta.value.trim() : '');
        }
    });

    $('bi-confirm-form').addEventListener('submit', function (e) { e.preventDefault(); closeDialog($('bi-confirm')); doHandover(state.pendingId); });
    $('bi-return-form').addEventListener('submit', function (e) { e.preventDefault(); closeDialog($('bi-return')); doReturn(state.pendingId); });
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