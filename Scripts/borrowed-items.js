/* UniShare - Borrowed Items page (runs fully in the browser) */
(function () {
    'use strict';

    var root = document.querySelector('.bi-page');
    if (!root) { return; }
    window.UniShareBorrowed = true;

    var PESO = '\u20B1';
    var data = { items: [], categories: [] };
    try { data = JSON.parse(document.getElementById('bi-data').textContent); }
    catch (err) { if (window.console) { console.error('Borrowed Items: could not read page data', err); } }

    /* ------------------------------------------------------------------ state */
    var items = data.items || [];
    var categoryOrder = data.categories || [];
    var now = Date.now();
    items.forEach(function (it) {
        it.endsAt = (it.remaining !== null && it.remaining !== undefined) ? now + it.remaining * 1000 : null;
        it.pausedMs = null;
    });
    var state = { status: root.getAttribute('data-initial') || '', category: '', q: '', detailId: null, pendingId: null };

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
    function isAwaiting(it) { return it.status === 'Ongoing' && !it.handover; }
    function isInUse(it) { return it.status === 'Ongoing' && it.handover; }
    function lower(s) { return String(s).toLowerCase(); }

    var ICONS = {
        calculator: '<svg class="bi-icon" viewBox="0 0 120 100" aria-hidden="true"><rect x="36" y="8" width="48" height="84" rx="7" fill="#334155"/><rect x="42" y="14" width="36" height="16" rx="3" fill="#c7d2fe"/><g fill="#818cf8"><rect x="42" y="37" width="9" height="8" rx="2"/><rect x="55.5" y="37" width="9" height="8" rx="2"/><rect x="69" y="37" width="9" height="8" rx="2"/></g><g fill="#e2e8f0"><rect x="42" y="50" width="9" height="8" rx="2"/><rect x="55.5" y="50" width="9" height="8" rx="2"/><rect x="69" y="50" width="9" height="8" rx="2"/><rect x="42" y="63" width="9" height="8" rx="2"/><rect x="55.5" y="63" width="9" height="8" rx="2"/><rect x="69" y="63" width="9" height="8" rx="2"/></g><rect x="42" y="76" width="9" height="8" rx="2" fill="#e2e8f0"/><rect x="55.5" y="76" width="22.5" height="8" rx="2" fill="#4f46e5"/></svg>',
        book: '<svg class="bi-icon" viewBox="0 0 120 100" aria-hidden="true"><rect x="22" y="14" width="76" height="20" rx="3" fill="#6366f1"/><rect x="22" y="38" width="76" height="20" rx="3" fill="#818cf8"/><rect x="22" y="62" width="76" height="20" rx="3" fill="#a5b4fc"/><g fill="#e0e7ff"><rect x="28" y="20" width="30" height="3" rx="1.5"/><rect x="28" y="44" width="38" height="3" rx="1.5"/><rect x="28" y="68" width="24" height="3" rx="1.5"/></g></svg>',
        pen: '<svg class="bi-icon" viewBox="0 0 120 100" aria-hidden="true"><g stroke="#334155" stroke-width="7" stroke-linecap="round"><line x1="32" y1="14" x2="88" y2="86"/><line x1="88" y1="14" x2="32" y2="86"/></g><g stroke="#94a3b8" stroke-width="3" stroke-linecap="round"><line x1="46" y1="32" x2="74" y2="68"/><line x1="74" y1="32" x2="46" y2="68"/></g><circle cx="60" cy="50" r="5" fill="#4f46e5"/></svg>',
        ball: '<svg class="bi-icon" viewBox="0 0 120 100" aria-hidden="true"><circle cx="60" cy="50" r="38" fill="#f97316"/><g fill="none" stroke="#7c2d12" stroke-width="3" stroke-linecap="round"><path d="M22 50 H98"/><path d="M60 12 V88"/><path d="M32 22 C48 36 48 64 32 78"/><path d="M88 22 C72 36 72 64 88 78"/></g></svg>',
        'case': '<svg class="bi-icon" viewBox="0 0 120 100" aria-hidden="true"><rect x="32" y="10" width="56" height="80" rx="9" fill="#475569"/><rect x="39" y="17" width="42" height="66" rx="5" fill="#334155"/><rect x="46" y="26" width="28" height="12" rx="3" fill="#94a3b8"/><g fill="#64748b"><rect x="46" y="46" width="12" height="8" rx="2"/><rect x="62" y="46" width="12" height="8" rx="2"/><rect x="46" y="60" width="12" height="8" rx="2"/><rect x="62" y="60" width="12" height="8" rx="2"/></g></svg>',
        board: '<svg class="bi-icon" viewBox="0 0 120 100" aria-hidden="true"><rect x="14" y="22" width="92" height="58" rx="6" fill="#0f766e"/><rect x="14" y="30" width="18" height="22" rx="2" fill="#cbd5e1"/><rect x="42" y="34" width="34" height="16" rx="2" fill="#1e293b"/><g fill="#fbbf24"><rect x="30" y="16" width="60" height="5" rx="1"/><rect x="30" y="81" width="60" height="5" rx="1"/></g><circle cx="92" cy="62" r="5" fill="#99f6e4"/><circle cx="22" cy="70" r="3" fill="#99f6e4"/></svg>'
    };
    var SVG_CHECK = '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><path d="M5 12l4 4 10-10" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/></svg>';
    var SVG_CLOCK = '<svg viewBox="0 0 24 24" width="13" height="13" aria-hidden="true"><circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" stroke-width="2"/><path d="M12 7v5l3 2" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>';
    var SVG_ALERT = '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" stroke-width="2"/><path d="M12 7v6M12 16.5v.5" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>';
    var SVG_RETURN = '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><path d="M9 7L4 12l5 5M4 12h11a5 5 0 010 10h-2" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" transform="translate(0 -4)"/></svg>';
    var SVG_MSG = '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><path d="M4 5h16v11H9l-5 4z" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/></svg>';

    /* -------------------------------------------------------------- timer info */
    function timerInfo(it) {
        var full = it.hours * 3600000;
        if (it.status === 'Disputed') {
            return { label: 'Timer paused', value: clock(it.pausedMs !== null ? it.pausedMs : full), cls: 'paused', note: 'Paused while the dispute is under review.' };
        }
        if (it.status === 'Completed') {
            return { label: 'Rental completed', value: clock(full), cls: 'done', note: 'Returned on ' + (it.returnedOn || it.steps.completed || 'the agreed date') + '.' };
        }
        if (!it.handover) {
            return { label: 'Rental duration', value: clock(full), cls: 'idle', note: 'The timer starts once you confirm the handover.' };
        }
        var left = it.endsAt - Date.now();
        if (left >= 0) {
            return { label: 'Time remaining', value: clock(left), cls: left < 600000 ? 'warn' : 'live', note: 'Return the item before the timer ends.' };
        }
        return { label: 'Overdue by', value: clock(-left), cls: 'late', note: 'The rental time is over. Please return the item now.' };
    }

    /* --------------------------------------------------------------- templates */
    function starsStatic(n, large) {
        var h = '<span class="bi-stars' + (large ? ' bi-stars--lg' : '') + '" aria-label="Rated ' + n + ' out of 5">';
        for (var i = 1; i <= 5; i++) { h += '<span class="' + (i <= n ? 'is-on' : '') + '">&#9733;</span>'; }
        return h + '</span>';
    }
    function rateInputs(prefix, id, large) {
        var h = '<span class="bi-rate' + (large ? ' bi-rate--lg' : '') + '" role="radiogroup" aria-label="Rating">';
        for (var i = 5; i >= 1; i--) {
            var rid = prefix + id + '-' + i;
            h += '<input type="radio" name="rating" value="' + i + '" id="' + rid + '" required />' +
                 '<label for="' + rid + '" title="' + i + (i > 1 ? ' stars' : ' star') + '">&#9733;</label>';
        }
        return h + '</span>';
    }
    /* Item photo container: shows the real picture from it.image. If no picture is set, or the file is
       missing, it falls back to the drawn icon so the card never shows a broken image. */
    function media(it) {
        if (it.image) {
            return '<img class="bi-photo" src="' + esc(it.image) + '" alt="' + esc(it.name) + '" loading="lazy" data-icon="' + esc(it.icon) + '" />';
        }
        return ICONS[it.icon] || '';
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
               row('Payment Method', it.payment) + row('Transaction ID', it.txn) +
               row('Total Paid', PESO + it.total, 'bi-facts__total') + '</dl>';
    }

    function cardHTML(it) {
        var st = lower(it.status);
        var tag = '<span class="bi-duration">' + SVG_CLOCK + it.hours + ' hrs</span>';
        var foot = '';

        if (isAwaiting(it)) {
            foot = '<button type="button" class="us-btn us-btn--primary us-btn--block" data-act="handover" data-id="' + it.id + '">' + SVG_CHECK + ' Confirm Handover</button>';
        } else if (isInUse(it)) {
            var t = timerInfo(it);
            foot = '<div class="bi-card__timer bi-card__timer--' + t.cls + '" data-tbox-mini="' + it.id + '">' + SVG_CLOCK +
                   '<span data-tlabel="' + it.id + '">' + t.label + '</span><b data-tmini="' + it.id + '">' + t.value + '</b></div>' +
                   '<button type="button" class="us-btn us-btn--primary us-btn--block" data-act="return" data-id="' + it.id + '">' + SVG_RETURN + ' Return Item</button>';
        } else if (it.status === 'Completed') {
            var review = '<div class="bi-review"><span class="bi-review__label">Rating &amp; Review</span>';
            if (it.rating) {
                review += '<div class="bi-review__row">' + starsStatic(it.rating) + '<span class="bi-review__done">Review submitted</span></div>';
            } else {
                review += '<form class="bi-review__row" data-rate-form data-id="' + it.id + '" data-stop>' + rateInputs('c', it.id, false) +
                          '<button type="submit" class="us-btn us-btn--primary us-btn--sm">Submit</button></form>';
            }
            foot = review + '</div><button type="button" class="us-btn bi-btn-done us-btn--block" disabled>' + SVG_CHECK + ' Handover Confirmed</button>';
        } else {
            foot = '<div class="bi-notice bi-notice--danger">' + SVG_ALERT + ' Dispute under review by admin</div>' +
                   '<button type="button" class="us-btn bi-btn-done us-btn--block" disabled>Confirm Handover</button>';
        }

        return '<article class="bi-card bi-card--' + st + '" tabindex="0" role="button" aria-haspopup="dialog" data-id="' + it.id +
               '" aria-label="View rental details for ' + esc(it.name) + '">' +
               '<div class="bi-tile bi-tile--' + esc(it.theme) + '">' + statusPill(it, false) + tag + media(it) + '</div>' +
               '<div class="bi-body"><span class="bi-cat">' + esc(it.category) + '</span>' +
               '<h3 class="bi-name">' + esc(it.name) + '</h3>' +
               '<div class="bi-meta"><span class="bi-price">' + PESO + it.price + '<small>/hr</small></span>' +
               '<span class="bi-lender">Lender: <strong>' + esc(it.lender) + '</strong></span></div>' +
               facts(it) + '<div class="bi-foot">' + foot + '</div></div></article>';
    }

    function stepsHTML(it) {
        var labels = ['Requested', 'Approved', 'Handover', 'Completed'];
        var dates = [it.steps.requested, it.steps.approved, it.steps.handover, it.steps.completed];
        if (isInUse(it) && !dates[2]) { dates[2] = today(); }
        var firstPending = -1;
        for (var k = 0; k < dates.length; k++) { if (!dates[k]) { firstPending = k; break; } }
        var h = '<ol class="bi-steps">';
        for (var i = 0; i < 4; i++) {
            var stt = dates[i] ? 'done' : (i === firstPending ? 'current' : 'todo');
            var label = labels[i], when = dates[i] || (stt === 'current' ? 'In progress' : 'Pending');
            if (it.status === 'Disputed' && i === 3) { stt = 'bad'; label = 'Disputed'; when = 'Under review'; }
            var dot = stt === 'done' ? '<svg viewBox="0 0 24 24" width="12" height="12" aria-hidden="true"><path d="M5 12l4 4 10-10" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/></svg>'
                    : (stt === 'bad' ? '<svg viewBox="0 0 24 24" width="12" height="12" aria-hidden="true"><path d="M12 6v8M12 17.5v.5" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"/></svg>' : '');
            h += '<li class="bi-step bi-step--' + stt + '"><span class="bi-step__dot">' + dot + '</span><strong>' + label + '</strong><small>' + esc(when) + '</small></li>';
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
            actions = '<div class="bi-modal-actions"><button type="button" class="us-btn us-btn--danger" data-act="report" data-id="' + it.id + '">Report a problem</button>' +
                      '<button type="button" class="us-btn us-btn--primary" data-act="handover" data-id="' + it.id + '">' + SVG_CHECK + ' Confirm Handover</button></div>';
        } else if (isInUse(it)) {
            actions = '<div class="bi-modal-actions"><button type="button" class="us-btn us-btn--danger" data-act="report" data-id="' + it.id + '">Report a problem</button>' +
                      '<button type="button" class="us-btn us-btn--primary" data-act="return" data-id="' + it.id + '">' + SVG_RETURN + ' Return Item</button></div>';
        } else if (it.status === 'Completed') {
            actions = '<h4 class="bi-h4">Rate your rental</h4>';
            if (it.rating) {
                actions += '<div class="bi-review__row">' + starsStatic(it.rating, true) + '<span class="bi-review__done">Review submitted</span></div>' +
                           (it.review ? '<p class="bi-review__text">&ldquo;' + esc(it.review) + '&rdquo;</p>' : '');
            } else {
                actions += '<form class="bi-rate-form" data-rate-form data-id="' + it.id + '"><div class="bi-review__row">' + rateInputs('d', it.id, true) +
                           '<button type="submit" class="us-btn us-btn--primary">Submit</button></div>' +
                           '<textarea name="review" rows="2" maxlength="300" placeholder="Write a short review (optional)"></textarea></form>';
            }
            actions += '<button type="button" class="us-btn bi-btn-done us-btn--block" disabled>' + SVG_CHECK + ' Handover Confirmed</button>';
        } else {
            actions = '<div class="bi-notice bi-notice--danger bi-notice--block"><strong>Dispute under review by admin</strong><p>' + esc(it.dispute) +
                      '</p><small>You will be notified once the admin resolves this case.</small></div>' +
                      '<button type="button" class="us-btn bi-btn-done us-btn--block" disabled>Confirm Handover</button>';
        }

        return '<div class="bi-dialog__body"><aside class="bi-dialog__side">' + tile(it, true) +
               '<div class="bi-lender-card"><div class="bi-lender-card__top"><span class="us-avatar">' + esc(it.initials) + '</span>' +
               '<div><strong>' + esc(it.lender) + '</strong><small>Lender &bull; ' + esc(it.program) + '</small></div></div>' +
               '<button type="button" class="us-btn us-btn--outline us-btn--block" data-msg-lender="' + esc(it.lender) + '" data-msg-item="' + esc(it.name) + '">' + SVG_MSG + ' Message Lender</button></div>' +
               '<div class="bi-about"><h4>About this item</h4><p>' + esc(it.about) + '</p></div></aside>' +
               '<section class="bi-dialog__main">' + statusPill(it, true) + '<span class="bi-cat">' + esc(it.category) + '</span>' +
               '<h3 class="bi-dialog__name">' + esc(it.name) + '</h3>' +
               '<div class="bi-dialog__price">' + PESO + it.price + '<small>/hr</small></div>' +
               '<div class="bi-factbox">' + facts(it) + '</div>' + timerPanel(it) +
               '<h4 class="bi-h4">Rental Progress</h4>' + stepsHTML(it) + actions + '</section></div>';
    }

    /* ------------------------------------------------------------------ render */
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

    /* ------------------------------------------------------------ live timers */
    function tick() {
        items.forEach(function (it) {
            if (!isInUse(it)) { return; }
            var t = timerInfo(it);
            var id = it.id;
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
        });
    }
    setInterval(tick, 1000);

    /* ------------------------------------------------------------------ toast */
    function toast(msg) {
        toastEl.textContent = msg;
        toastEl.classList.add('is-on');
        try { if (toastEl.showPopover) { if (toastEl.matches(':popover-open')) { toastEl.hidePopover(); } toastEl.showPopover(); } } catch (e) { /* older browsers */ }
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () {
            toastEl.classList.remove('is-on');
            try { if (toastEl.hidePopover && toastEl.matches(':popover-open')) { toastEl.hidePopover(); } } catch (e) { /* ignore */ }
        }, 3800);
    }

    /* ---------------------------------------------------------------- actions */
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
        it.handover = true;
        it.endsAt = Date.now() + it.hours * 3600000;
        it.steps.handover = today();
        render();
        toast('Handover confirmed. Your rental timer has started.');
    }
    function doReturn(id) {
        var it = byId(id);
        if (!it || !isInUse(it)) { return; }
        it.status = 'Completed';
        it.endsAt = null;
        it.steps.completed = today();
        it.returnedOn = new Date().toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
        render();
        toast('Item returned. This rental is now completed and ready to rate.');
    }
    function doDispute(id, reason, details) {
        var it = byId(id);
        if (!it || it.status !== 'Ongoing') { return; }
        if (it.endsAt !== null) { it.pausedMs = Math.max(0, it.endsAt - Date.now()); it.endsAt = null; }
        it.status = 'Disputed';
        it.dispute = details ? reason + ': ' + details : reason;
        render();
        toast('Dispute sent to the admin. The rental timer is paused.');
    }
    function doRate(id, rating, review) {
        var it = byId(id);
        if (!it || it.status !== 'Completed' || it.rating) { return; }   /* rating only after the item is completed */
        it.rating = rating;
        it.review = review || '';
        render();
        toast('Thanks for reviewing ' + it.name + '!');
    }

    /* ----------------------------------------------------------------- events */
    function act(name, id) {
        if (name === 'handover') { openAsk('bi-confirm', 'bi-confirm-form', id, [['bi-confirm-item', function (i) { return i.name; }], ['bi-confirm-lender', function (i) { return i.lender; }]]); }
        else if (name === 'return') { openAsk('bi-return', 'bi-return-form', id, [['bi-return-item', function (i) { return i.name; }], ['bi-return-lender', function (i) { return i.lender; }]]); }
        else if (name === 'report') { openAsk('bi-report', 'bi-report-form', id, [['bi-report-item', function (i) { return i.name; }]]); }
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
            doRate(parseInt(form.getAttribute('data-id'), 10), parseInt(picked.value, 10), ta ? ta.value.trim() : '');
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

    [].slice.call(root.querySelectorAll('dialog.bi-dialog')).forEach(function (d) {
        d.addEventListener('click', function (e) { if (e.target === d || e.target.closest('[data-close]')) { d.close(); } });
    });
    detail.addEventListener('close', function () { state.detailId = null; });

    /* tabs */
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

    /* search */
    search.addEventListener('input', function () { state.q = lower(search.value.trim()); grid.scrollTop = 0; renderGrid(); });

    /* category dropdown */
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

    /* picture file missing -> swap in the drawn icon */
    root.addEventListener('error', function (e) {
        var img = e.target;
        if (!img || !img.classList || !img.classList.contains('bi-photo')) { return; }
        var holder = document.createElement('span');
        holder.innerHTML = ICONS[img.getAttribute('data-icon')] || '';
        if (holder.firstChild) { img.parentNode.replaceChild(holder.firstChild, img); } else { img.parentNode.removeChild(img); }
    }, true);

    /* ------------------------------------------------------------------ start */
    buildCategoryMenu();
    render();
})();
