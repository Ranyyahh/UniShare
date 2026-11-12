/* UniShare - Reports & Analytics
   Loads the report for the selected dates from ReportsAnalytics/Data and draws the four
   cards and five charts with plain SVG/DOM (no chart library). ES5, no globals. */
(function () {
    'use strict';

    var page = document.getElementById('reportsPage');
    if (!page) { return; }

    var SVGNS = 'http://www.w3.org/2000/svg';
    var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    var DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    var MAX_DAYS = 366;
    var PESO = '₱';

    var COLORS = {
        revenue: '#4f46e5',
        active: '#4f46e5',
        completed: '#059669',
        disputed: '#dc2626',
        activity: '#0284c7',
        expenses: '#d97706'
    };

    var el = {
        from: document.getElementById('filterFrom'),
        to: document.getElementById('filterTo'),
        form: document.getElementById('reportsFilterForm'),
        reset: document.getElementById('btnResetFilter'),
        apply: document.getElementById('btnApplyFilter'),
        error: document.getElementById('filterError'),
        refresh: document.getElementById('btnRefreshReport'),
        exportBtn: document.getElementById('btnExportReport'),
        notice: document.getElementById('reportsNotice'),
        tooltip: document.getElementById('reportsTooltip'),
        cardRevenue: document.getElementById('cardRevenue'),
        cardRentals: document.getElementById('cardRentals'),
        cardItems: document.getElementById('cardItems'),
        cardBorrowings: document.getElementById('cardBorrowings'),
        cardPendingRentals: document.getElementById('cardPendingRentals'),
        cardActiveRentals: document.getElementById('cardActiveRentals'),
        cardCompletedRentals: document.getElementById('cardCompletedRentals'),
        cardClosedRentals: document.getElementById('cardClosedRentals'),
        cardPendingBorrowings: document.getElementById('cardPendingBorrowings'),
        cardActiveBorrowings: document.getElementById('cardActiveBorrowings'),
        cardCompletedBorrowings: document.getElementById('cardCompletedBorrowings'),
        cardClosedBorrowings: document.getElementById('cardClosedBorrowings'),
        revenueSubtitle: document.getElementById('revenueSubtitle'),
        activitySubtitle: document.getElementById('activitySubtitle'),
        statusFootnote: document.getElementById('statusFootnote'),
        expensesFootnote: document.getElementById('expensesFootnote'),
        chartRevenue: document.getElementById('chartRevenue'),
        chartTopItems: document.getElementById('chartTopItems'),
        chartStatus: document.getElementById('chartStatus'),
        chartActivity: document.getElementById('chartActivity'),
        chartExpenses: document.getElementById('chartExpenses'),
        chartRequestsReceived: document.getElementById('chartRequestsReceived'),
        chartRequestsSubmitted: document.getElementById('chartRequestsSubmitted'),
        transactions: document.getElementById('reportsTransactions'),
        statsRevenue: document.getElementById('statsRevenue'),
        statsActivity: document.getElementById('statsActivity'),
        statsExpenses: document.getElementById('statsExpenses'),
        period: document.getElementById('reportsPeriod')
    };

    var chips = page.querySelectorAll('.reports-chip');
    var dataUrl = page.getAttribute('data-data-url');
    var exportUrl = page.getAttribute('data-export-url');
    var defaultFrom = page.getAttribute('data-default-from');
    var defaultTo = page.getAttribute('data-default-to');
    var baseNotice = (el.notice.textContent || '').replace(/^\s+|\s+$/g, '');

    var applied = { from: defaultFrom, to: defaultTo };
    var lastPayload = null;
    var lastFailure = null;
    var requestId = 0;
    var animate = true;                                    // false while redrawing after a resize
    var uid = 0;
    var reduced = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

    /* ---------- small helpers ---------- */

    function pad(n) { return (n < 10 ? '0' : '') + n; }

    function toIso(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }

    function parseIso(s) {
        var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s || '');
        if (!m) { return null; }
        var d = new Date(+m[1], +m[2] - 1, +m[3]);
        if (d.getFullYear() !== +m[1] || d.getMonth() !== +m[2] - 1 || d.getDate() !== +m[3]) { return null; }
        return d;
    }

    function formatMoney(v) {
        var n = Number(v) || 0;
        return PESO + n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function formatInt(v) { return (Number(v) || 0).toLocaleString('en-US'); }

    function svgEl(tag, attrs, parent) {
        var node = document.createElementNS(SVGNS, tag);
        var k;
        for (k in attrs) { if (attrs.hasOwnProperty(k)) { node.setAttribute(k, attrs[k]); } }
        if (parent) { parent.appendChild(node); }
        return node;
    }

    function htmlEl(tag, className, text, parent) {
        var node = document.createElement(tag);
        if (className) { node.className = className; }
        if (text !== undefined && text !== null) { node.textContent = text; }
        if (parent) { parent.appendChild(node); }
        return node;
    }

    function clear(node) { while (node.firstChild) { node.removeChild(node.firstChild); } }

    function sum(arr) {
        var t = 0, i;
        for (i = 0; i < arr.length; i++) { t += Number(arr[i]) || 0; }
        return t;
    }

    /* ---------- labels ---------- */

    function spansYears(p) { return p && p.from && p.to && p.from.substr(0, 4) !== p.to.substr(0, 4); }

    function shortLabel(iso, gran, withYear) {
        var d = parseIso(iso);
        if (!d) { return iso; }
        if (gran === 'month') { return MONTHS[d.getMonth()] + ' ' + d.getFullYear(); }
        return MONTHS[d.getMonth()] + ' ' + d.getDate() + (withYear ? ', ' + d.getFullYear() : '');
    }

    function longLabel(iso, gran) {
        var d = parseIso(iso);
        if (!d) { return iso; }
        if (gran === 'month') { return MONTHS[d.getMonth()] + ' ' + d.getFullYear(); }
        var base = MONTHS[d.getMonth()] + ' ' + d.getDate() + ', ' + d.getFullYear();
        return gran === 'week' ? 'Week of ' + base : DAYS[d.getDay()] + ', ' + base;
    }

    function rangeLabel(from, to) {
        var a = parseIso(from), b = parseIso(to);
        if (!a || !b) { return ''; }
        function f(d) { return DAYS[d.getDay()] + ', ' + MONTHS[d.getMonth()] + ' ' + d.getDate() + ', ' + d.getFullYear(); }
        return f(a) + ' \u2013 ' + f(b);
    }

    // Number of buckets that have already started; later ones (rest of the current week) are not plotted.
    function elapsedCount(labels) {
        var today = toIso(new Date()), n = 0, i;
        for (i = 0; i < labels.length; i++) { if (labels[i] <= today) { n = i + 1; } }
        return n;
    }

    function granularityWord(g) { return g === 'month' ? 'month' : (g === 'week' ? 'week' : 'day'); }

    /* ---------- tooltip ---------- */

    // rows: [{ color, label, value, note, tone, sub }]  (tone: 'up' | 'down'; sub: a detail line under the summary row)
    function showTip(title, rows, evt) {
        var tip = el.tooltip, i, row, divided = false;
        clear(tip);
        htmlEl('strong', 'reports-tooltip__title', title, tip);
        for (i = 0; i < rows.length; i++) {
            if (rows[i].sub && !divided) { htmlEl('div', 'reports-tooltip__divider', null, tip); divided = true; }
            row = htmlEl('div', 'reports-tooltip__row' + (rows[i].sub ? ' is-sub' : ''), null, tip);
            if (rows[i].color) { htmlEl('span', 'reports-tooltip__swatch', null, row).style.background = rows[i].color; }
            htmlEl('span', 'reports-tooltip__label', rows[i].label, row);
            if (rows[i].value) { htmlEl('span', 'reports-tooltip__value', rows[i].value, row); }
            if (rows[i].note) { htmlEl('div', 'reports-tooltip__note' + (rows[i].tone ? ' is-' + rows[i].tone : ''), rows[i].note, tip); }
        }
        tip.hidden = false;
        var x = evt.clientX + 16, y = evt.clientY + 16;
        var w = tip.offsetWidth, h = tip.offsetHeight;
        if (x + w > window.innerWidth - 8) { x = evt.clientX - w - 16; }
        if (y + h > window.innerHeight - 8) { y = evt.clientY - h - 16; }
        tip.style.left = Math.max(8, x) + 'px';
        tip.style.top = Math.max(8, y) + 'px';
        tip.className = 'reports-tooltip is-visible';
    }

    function hideTip() { el.tooltip.className = 'reports-tooltip'; }

    var MAX_DETAIL_ROWS = 5;

    function shortDate(iso) {
        var d = parseIso(iso);
        return d ? MONTHS[d.getMonth()] + ' ' + d.getDate() : iso;
    }

    function itemLabel(t) { return t.itemName + (t.quantity > 1 ? ' \u00D7' + t.quantity : ''); }

    // Item rows for one chart point. kind 'activity' = borrowed item + status; 'expense' = item + amount paid + transaction.
    function detailRows(list, kind) {
        var rows = [], i, t, shown = Math.min(list.length, MAX_DETAIL_ROWS);
        for (i = 0; i < shown; i++) {
            t = list[i];
            rows.push(kind === 'expense'
                ? { sub: true, label: itemLabel(t), value: formatMoney(t.amount), note: 'Transaction ' + t.transactionId + ' \u00B7 paid ' + shortDate(t.date) }
                : { sub: true, label: itemLabel(t), value: t.status, note: 'Transaction ' + t.transactionId + ' \u00B7 requested ' + shortDate(t.date) });
        }
        if (list.length > shown) {
            rows.push({ sub: true, label: '+ ' + (list.length - shown) + ' more ' + (kind === 'expense' ? 'payment' : 'borrowing') + (list.length - shown === 1 ? '' : 's'), value: '' });
        }
        return rows;
    }

    function detailsText(list, kind) {
        var parts = [], i, t;
        for (i = 0; i < list.length; i++) {
            t = list[i];
            parts.push(itemLabel(t) + ' (' + t.transactionId + (kind === 'expense' ? ', ' + formatMoney(t.amount) : ', ' + t.status) + ')');
        }
        return parts.join('; ');
    }

    function deltaNote(cur, prev, word, lowerIsBetter) {
        if (!(prev > 0)) { return null; }
        var pct = ((cur - prev) / prev) * 100;
        if (Math.abs(pct) < 0.05) { return { text: 'No change vs previous ' + word, tone: null }; }
        var up = pct > 0;
        return {
            text: (up ? '\u25B2 ' : '\u25BC ') + Math.abs(pct).toFixed(1) + '% vs previous ' + word,
            tone: (up !== !!lowerIsBetter) ? 'up' : 'down'
        };
    }

    function pointFromTouch(e) {
        var t = e.touches && e.touches[0];
        return t ? { clientX: t.clientX, clientY: t.clientY } : null;
    }

    /* ---------- shared chart pieces ---------- */

    function niceTicks(max, integerOnly) {
        if (!(max > 0)) { max = 1; }
        var rough = max / 4;
        if (integerOnly && rough < 1) { rough = 1; }
        var mag = Math.pow(10, Math.floor(Math.log(rough) / Math.LN10));
        var norm = rough / mag;
        var step = (norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10) * mag;
        if (integerOnly) { step = Math.max(1, Math.round(step)); }
        var top = Math.ceil(max / step - 1e-9) * step;
        var ticks = [], v;
        for (v = 0; v <= top + step / 1000; v += step) { ticks.push(Math.round(v * 1e6) / 1e6); }
        return { step: step, top: top, ticks: ticks };
    }

    function tickText(v, money, step) {
        if (!money) { return formatInt(v); }
        return PESO + v.toLocaleString('en-US', step < 1 ? { minimumFractionDigits: 2, maximumFractionDigits: 2 } : { maximumFractionDigits: 0 });
    }

    function emptyState(box, title, text) {
        clear(box);
        var wrap = htmlEl('div', 'reports-empty', null, box);
        htmlEl('span', 'reports-empty__title', title, wrap);
        htmlEl('span', 'reports-empty__text', text, wrap);
    }

    // Screen-reader table twin of a chart (the SVG itself is a single labelled image).
    // The table sits inside a clipped 1px wrapper: a clipped <table> alone still adds its
    // rows to the page's scrollable height, which left empty space below the footer.
    function srTable(box, caption, headers, rows) {
        var wrap = htmlEl('div', 'reports-sr-only', null, box);
        var t = htmlEl('table', '', null, wrap);
        htmlEl('caption', '', caption, t);
        var head = htmlEl('tr', '', null, htmlEl('thead', '', null, t));
        var i, j, tr;
        for (i = 0; i < headers.length; i++) { htmlEl('th', '', headers[i], head).setAttribute('scope', 'col'); }
        var body = htmlEl('tbody', '', null, t);
        for (i = 0; i < rows.length; i++) {
            tr = htmlEl('tr', '', null, body);
            for (j = 0; j < rows[i].length; j++) { htmlEl('td', '', rows[i][j], tr); }
        }
    }

    // Draws gridlines, y labels and returns geometry shared by line and bar charts.
    function plotFrame(box, o, labels, vals) {
        var W = Math.max(box.clientWidth || 0, 280);
        var H = o.height;
        var m = { t: 20, r: 18, b: 32, l: o.money ? 68 : 42 };
        var pw = W - m.l - m.r, ph = H - m.t - m.b;
        var max = 0, i;
        for (i = 0; i < vals.length; i++) { if (vals[i] > max) { max = vals[i]; } }
        var nt = niceTicks(max, !o.money);

        var svg = svgEl('svg', { 'class': animate ? 'is-animated' : '', viewBox: '0 0 ' + W + ' ' + H, width: W, height: H, role: 'img', 'aria-label': o.aria, focusable: 'false' });
        var defs = svgEl('defs', {}, svg);
        function y(v) { return m.t + ph - (v / nt.top) * ph; }

        for (i = 0; i < nt.ticks.length; i++) {
            var gy = y(nt.ticks[i]);
            svgEl('line', { 'class': i === 0 ? 'reports-axis-line' : 'reports-grid-line', x1: m.l, x2: m.l + pw, y1: gy, y2: gy }, svg);
            var t = svgEl('text', { 'class': 'reports-axis-text', x: m.l - 10, y: gy + 4, 'text-anchor': 'end' }, svg);
            t.textContent = tickText(nt.ticks[i], o.money, nt.step);
        }
        return { svg: svg, defs: defs, W: W, H: H, m: m, pw: pw, ph: ph, y: y, top: nt.top };
    }

    function gradient(defs, color, topOpacity, bottomOpacity, vertical) {
        var id = 'rg' + (++uid);
        var grad = svgEl('linearGradient', { id: id, x1: 0, y1: 0, x2: vertical ? 0 : 1, y2: vertical ? 1 : 0 }, defs);
        svgEl('stop', { offset: '0%', 'stop-color': color, 'stop-opacity': topOpacity }, grad);
        svgEl('stop', { offset: '100%', 'stop-color': color, 'stop-opacity': bottomOpacity }, grad);
        return 'url(#' + id + ')';
    }

    function drawXLabels(g, labels, xOf, o) {
        var n = labels.length;
        var maxLabels = Math.max(2, Math.floor(g.pw / (o.gran === 'day' && n <= 10 ? 52 : 64)));
        var stride = Math.max(1, Math.ceil(n / maxLabels));
        var i, t, d, text;
        for (i = 0; i < n; i += stride) {
            d = parseIso(labels[i]);
            text = (o.gran === 'day' && n <= 10 && d) ? DAYS[d.getDay()] + ' ' + d.getDate() : shortLabel(labels[i], o.gran, o.years);
            t = svgEl('text', { 'class': 'reports-axis-text', x: xOf(i), y: g.H - 10, 'text-anchor': (o.edgeStart && n > 1 && i === 0) ? 'start' : 'middle' }, g.svg);
            t.textContent = text;
        }
    }

    // Monotone cubic curve through the points: smooth, and never dips below zero between values.
    function curvePath(xs, ys) {
        var n = xs.length, i, d = 'M' + xs[0].toFixed(1) + ' ' + ys[0].toFixed(1);
        if (n === 1) { return d; }
        var dx = [], m = [], tg = [], a, b, s;
        for (i = 0; i < n - 1; i++) { dx.push(xs[i + 1] - xs[i]); m.push((ys[i + 1] - ys[i]) / (dx[i] || 1)); }
        tg.push(m[0]);
        for (i = 1; i < n - 1; i++) { tg.push(m[i - 1] * m[i] <= 0 ? 0 : (m[i - 1] + m[i]) / 2); }
        tg.push(m[n - 2]);
        for (i = 0; i < n - 1; i++) {
            if (m[i] === 0) { tg[i] = 0; tg[i + 1] = 0; continue; }
            a = tg[i] / m[i]; b = tg[i + 1] / m[i]; s = a * a + b * b;
            if (s > 9) { s = 3 / Math.sqrt(s); tg[i] = s * a * m[i]; tg[i + 1] = s * b * m[i]; }
        }
        for (i = 0; i < n - 1; i++) {
            d += 'C' + (xs[i] + dx[i] / 3).toFixed(1) + ' ' + (ys[i] + tg[i] * dx[i] / 3).toFixed(1) + ' ' +
                (xs[i + 1] - dx[i] / 3).toFixed(1) + ' ' + (ys[i + 1] - tg[i + 1] * dx[i] / 3).toFixed(1) + ' ' +
                xs[i + 1].toFixed(1) + ' ' + ys[i + 1].toFixed(1);
        }
        return d;
    }

    /* ---------- line chart (Rental Revenue, Borrowing Expenses) ---------- */

    function lineChart(box, series, o) {
        clear(box);
        var labels = series.labels, vals = series.values, n = labels.length;
        var ne = elapsedCount(labels);                     // only buckets that have started are plotted
        var g = plotFrame(box, o, labels, vals);
        var word = granularityWord(o.gran);
        var x = function (i) { return n === 1 ? g.m.l + g.pw / 2 : g.m.l + (g.pw * i) / (n - 1); };
        var base = g.m.t + g.ph;
        var i, xs = [], ys = [], peak = 0;

        o.edgeStart = true;
        drawXLabels(g, labels, x, o);

        for (i = 0; i < ne; i++) { xs.push(x(i)); ys.push(g.y(vals[i])); if (vals[i] > vals[peak]) { peak = i; } }

        if (ne > 0) {
            var line = curvePath(xs, ys);
            if (ne > 1) {
                svgEl('path', { 'class': 'reports-area', d: line + 'L' + xs[ne - 1].toFixed(1) + ' ' + base + 'L' + xs[0].toFixed(1) + ' ' + base + 'Z', fill: gradient(g.defs, o.color, 0.28, 0.02, true) }, g.svg);
                var drawIn = animate && !reduced;
                var path = svgEl('path', { 'class': 'reports-line' + (drawIn ? ' is-drawing' : ''), d: line, stroke: o.color, pathLength: 1 }, g.svg);
                if (drawIn) { path.addEventListener('animationend', function () { path.setAttribute('class', 'reports-line'); }); }
            }
            if (ne <= 40) {
                for (i = 0; i < ne; i++) {
                    var dot = svgEl('circle', { 'class': 'reports-dot', cx: xs[i], cy: ys[i], r: ne > 20 ? 3 : 4, stroke: o.color }, g.svg);
                    if (animate) { dot.style.animationDelay = (500 + (i * 500) / ne) + 'ms'; }
                }
            }
            if (o.peak && ne > 1 && vals[peak] > 0) {
                var txt = 'Peak ' + formatMoney(vals[peak]);
                var pw2 = txt.length * 6.4 + 16, px = Math.max(g.m.l + pw2 / 2, Math.min(g.W - g.m.r - pw2 / 2, xs[peak]));
                var py = ys[peak] - 26 < 2 ? ys[peak] + 12 : ys[peak] - 26;
                var tag = svgEl('g', { 'class': 'reports-peak' }, g.svg);
                svgEl('rect', { x: px - pw2 / 2, y: py, width: pw2, height: 18, rx: 9, ry: 9, fill: o.color }, tag);
                var tt = svgEl('text', { x: px, y: py + 12.5, 'text-anchor': 'middle' }, tag);
                tt.textContent = txt;
            }
        }

        var cross = svgEl('line', { 'class': 'reports-crosshair', y1: g.m.t, y2: base, visibility: 'hidden' }, g.svg);
        var halo = svgEl('circle', { 'class': 'reports-focus-halo', r: 11, fill: o.color, visibility: 'hidden' }, g.svg);
        var focus = svgEl('circle', { 'class': 'reports-focus', r: 5.5, stroke: o.color, visibility: 'hidden' }, g.svg);
        var hit = svgEl('rect', { 'class': 'reports-hit reports-hit--line', x: g.m.l, y: g.m.t - 6, width: g.pw, height: g.ph + 6 }, g.svg);

        function move(pt) {
            if (ne === 0) { return; }
            var rect = hit.getBoundingClientRect();
            var rel = (pt.clientX - rect.left) / (rect.width || 1);
            var idx = n === 1 ? 0 : Math.round(rel * (n - 1));
            idx = Math.max(0, Math.min(ne - 1, idx));
            var cx = x(idx), cy = g.y(vals[idx]);
            cross.setAttribute('x1', cx); cross.setAttribute('x2', cx); cross.setAttribute('visibility', 'visible');
            halo.setAttribute('cx', cx); halo.setAttribute('cy', cy); halo.setAttribute('visibility', 'visible');
            focus.setAttribute('cx', cx); focus.setAttribute('cy', cy); focus.setAttribute('visibility', 'visible');
            var note = idx > 0 ? deltaNote(vals[idx], vals[idx - 1], word, o.lowerIsBetter) : null;
            var tipRows = [{
                color: o.color, label: o.name, value: o.money ? formatMoney(vals[idx]) : formatInt(vals[idx]),
                note: note ? note.text : null, tone: note ? note.tone : null
            }];
            if (o.detailKind && series.details && series.details[idx]) { tipRows = tipRows.concat(detailRows(series.details[idx], o.detailKind)); }
            showTip(longLabel(labels[idx], o.gran), tipRows, pt);
        }
        function leave() {
            cross.setAttribute('visibility', 'hidden'); halo.setAttribute('visibility', 'hidden'); focus.setAttribute('visibility', 'hidden');
            hideTip();
        }
        hit.addEventListener('mousemove', move);
        hit.addEventListener('mouseleave', leave);
        hit.addEventListener('touchstart', function (e) { var p = pointFromTouch(e); if (p) { move(p); } }, { passive: true });
        hit.addEventListener('touchmove', function (e) { var p = pointFromTouch(e); if (p) { move(p); } }, { passive: true });
        hit.addEventListener('touchend', leave);

        box.appendChild(g.svg);
        var rows = [];
        for (i = 0; i < ne; i++) {
            rows.push([longLabel(labels[i], o.gran), o.money ? formatMoney(vals[i]) : formatInt(vals[i])]);
            if (o.detailKind) { rows[i].push(series.details && series.details[i] ? detailsText(series.details[i], o.detailKind) : ''); }
        }
        srTable(box, o.aria, o.detailKind ? ['Period', o.name, 'Transactions'] : ['Period', o.name], rows);
    }

    /* ---------- vertical bar chart (Borrowing Activity) ---------- */

    function barChart(box, series, o) {
        clear(box);
        var labels = series.labels, vals = series.values, n = labels.length;
        var ne = elapsedCount(labels);
        var g = plotFrame(box, o, labels, vals);
        var word = granularityWord(o.gran);
        var band = g.pw / n;
        var bw = Math.max(3, Math.min(34, band * 0.58));
        var x = function (i) { return g.m.l + band * i + band / 2; };
        var base = g.m.t + g.ph;
        var fill = gradient(g.defs, o.color, 1, 0.62, true);

        drawXLabels(g, labels, x, o);

        var hoverBand = svgEl('rect', { 'class': 'reports-band', x: 0, y: g.m.t, width: band, height: g.ph, rx: 6, ry: 6, fill: o.color, visibility: 'hidden' }, g.svg);

        var i, bars = [];
        for (i = 0; i < ne; i++) {
            var h = (vals[i] / g.top) * g.ph;
            if (h <= 0) { bars.push(null); continue; }
            var x0 = x(i) - bw / 2, yTop = base - h, r = Math.min(5, bw / 2, h);
            var d = 'M' + x0 + ' ' + base + 'V' + (yTop + r) + 'Q' + x0 + ' ' + yTop + ' ' + (x0 + r) + ' ' + yTop +
                'H' + (x0 + bw - r) + 'Q' + (x0 + bw) + ' ' + yTop + ' ' + (x0 + bw) + ' ' + (yTop + r) + 'V' + base + 'Z';
            var bar = svgEl('path', { 'class': 'reports-bar', d: d, fill: fill }, g.svg);
            if (animate) { bar.style.animationDelay = Math.min(i * 40, 600) + 'ms'; }
            bars.push(bar);
            if (n <= 16 && bw >= 14) {
                var lab = svgEl('text', { 'class': 'reports-bar-label', x: x(i), y: yTop - 6, 'text-anchor': 'middle' }, g.svg);
                lab.textContent = formatInt(vals[i]);
                if (animate) { lab.style.animationDelay = (350 + Math.min(i * 40, 600)) + 'ms'; }
            }
        }

        (function (bars) {
            function bind(i) {
                var hit = svgEl('rect', { 'class': 'reports-hit', x: g.m.l + band * i, y: g.m.t, width: band, height: g.ph }, g.svg);
                function over(pt) {
                    box.className = box.className.replace(/\s*is-hovering/g, '') + ' is-hovering';
                    hoverBand.setAttribute('x', g.m.l + band * i); hoverBand.setAttribute('visibility', 'visible');
                    for (var k = 0; k < bars.length; k++) { if (bars[k]) { bars[k].setAttribute('class', 'reports-bar' + (k === i ? ' is-hover' : '')); } }
                    var note = i > 0 ? deltaNote(vals[i], vals[i - 1], word, false) : null;
                    var tipRows = [{
                        color: o.color, label: o.name, value: formatInt(vals[i]),
                        note: note ? note.text : null, tone: note ? note.tone : null
                    }];
                    if (o.detailKind && series.details && series.details[i]) { tipRows = tipRows.concat(detailRows(series.details[i], o.detailKind)); }
                    showTip(longLabel(labels[i], o.gran), tipRows, pt);
                }
                function out() {
                    box.className = box.className.replace(/\s*is-hovering/g, '');
                    hoverBand.setAttribute('visibility', 'hidden');
                    for (var k = 0; k < bars.length; k++) { if (bars[k]) { bars[k].setAttribute('class', 'reports-bar'); } }
                    hideTip();
                }
                hit.addEventListener('mousemove', over);
                hit.addEventListener('mouseleave', out);
                hit.addEventListener('touchstart', function (e) { var p = pointFromTouch(e); if (p) { over(p); } }, { passive: true });
                hit.addEventListener('touchend', out);
            }
            for (var j = 0; j < ne; j++) { bind(j); }
        }(bars));

        box.appendChild(g.svg);
        var rows = [];
        for (i = 0; i < ne; i++) {
            rows.push([longLabel(labels[i], o.gran), formatInt(vals[i])]);
            if (o.detailKind) { rows[i].push(series.details && series.details[i] ? detailsText(series.details[i], o.detailKind) : ''); }
        }
        srTable(box, o.aria, o.detailKind ? ['Period', o.name, 'Items borrowed'] : ['Period', o.name], rows);
    }

    /* ---------- horizontal bars (Most Rented Items) ---------- */

    function topItemsChart(box, items) {
        clear(box);
        var list = htmlEl('ol', 'reports-hbars', null, box);
        var max = 0, total = 0, i, fills = [], widths = [];
        for (i = 0; i < items.length; i++) { total += items[i].quantity; if (items[i].quantity > max) { max = items[i].quantity; } }
        function bind(li, item, rank) {
            function over(e) {
                showTip(item.name, [{
                    label: 'Quantity rented', value: formatInt(item.quantity),
                    note: total > 0 ? Math.round((item.quantity / total) * 100) + '% of the top ' + items.length + ' \u00B7 rank #' + rank : null
                }, { label: 'Completed rental fees', value: formatMoney(item.revenue || 0) }], e);
            }
            li.addEventListener('mousemove', over);
            li.addEventListener('mouseleave', hideTip);
            li.addEventListener('touchstart', function (e) { var p = pointFromTouch(e); if (p) { over(p); } }, { passive: true });
            li.addEventListener('touchend', hideTip);
        }
        for (i = 0; i < items.length; i++) {
            var li = htmlEl('li', 'reports-hbar' + (i === 0 ? ' is-top' : ''), null, list);
            var nameWrap = htmlEl('span', 'reports-hbar__name', null, li);
            htmlEl('span', 'reports-hbar__rank', String(i + 1), nameWrap);
            htmlEl('span', 'reports-hbar__text', items[i].name, nameWrap);
            var track = htmlEl('span', 'reports-hbar__track', null, li);
            var fill = htmlEl('span', 'reports-hbar__fill', null, track);
            var w = (max > 0 ? Math.round((items[i].quantity / max) * 1000) / 10 : 0) + '%';
            if (animate && !reduced) { fill.style.width = '0%'; fill.style.transitionDelay = (i * 60) + 'ms'; fills.push(fill); widths.push(w); }
            else { fill.style.width = w; }
            htmlEl('span', 'reports-hbar__value', formatInt(items[i].quantity), li);
            bind(li, items[i], i + 1);
        }
        if (fills.length) {
            // Two frames so the browser paints width 0 first and then animates to the real width.
            (window.requestAnimationFrame || function (f) { setTimeout(f, 16); })(function () {
                (window.requestAnimationFrame || function (f) { setTimeout(f, 16); })(function () {
                    for (var k = 0; k < fills.length; k++) { fills[k].style.width = widths[k]; }
                });
            });
        }
    }

    /* ---------- donut (Rental Status Distribution) ---------- */

    function donutChart(box, s) {
        clear(box);
        var parts = [
            { label: 'Pending', value: s.pending || 0, color: '#f59e0b' },
            { label: 'Accepted', value: s.accepted || 0, color: '#7c3aed' },
            { label: 'Active', value: s.active, color: COLORS.active },
            { label: 'Completed', value: s.completed, color: COLORS.completed },
            { label: 'Disputed', value: s.disputed, color: COLORS.disputed },
            { label: 'Cancelled', value: s.cancelled || 0, color: '#64748b' },
            { label: 'Rejected', value: s.rejected || 0, color: COLORS.disputed }
        ].filter(function (part) { return part.value > 0; });
        var total = parts.reduce(function (acc, part) { return acc + part.value; }, 0);
        var wrap = htmlEl('div', 'reports-donut', null, box);
        var R = 70, C = 2 * Math.PI * R, GAP = 2, offset = 0, i;
        var segNodes = [], legendNodes = [], pending = [];

        var svg = svgEl('svg', { 'class': 'reports-donut__svg', viewBox: '0 0 190 190', role: 'img', 'aria-label': 'Rental status distribution: ' + total + ' transactions', focusable: 'false' }, wrap);
        svgEl('circle', { 'class': 'reports-donut__track', cx: 95, cy: 95, r: R, fill: 'none', 'stroke-width': 24 }, svg);
        var segs = svgEl('g', { transform: 'rotate(-90 95 95)' }, svg);
        var tt = svgEl('text', { 'class': 'reports-donut__total', x: 95, y: 98, 'text-anchor': 'middle' }, svg);
        var cap = svgEl('text', { 'class': 'reports-donut__caption', x: 95, y: 117, 'text-anchor': 'middle' }, svg);

        function pct(v) { return Math.round((v / total) * 100); }
        function setCenter(p) {
            tt.textContent = p ? formatInt(p.value) : formatInt(total);
            cap.textContent = p ? p.label + ' \u00B7 ' + pct(p.value) + '%' : 'transactions';
        }
        function activate(idx) {
            for (var k = 0; k < parts.length; k++) {
                if (segNodes[k]) { segNodes[k].setAttribute('class', 'reports-seg' + (idx === null ? '' : (k === idx ? ' is-hover' : ' is-dim'))); }
                if (legendNodes[k]) { legendNodes[k].className = 'reports-legend__item' + (idx === k ? ' is-active' : ''); }
            }
            setCenter(idx === null ? null : parts[idx]);
        }
        setCenter(null);

        function bindTip(node, idx) {
            function over(pt) {
                activate(idx);
                showTip(parts[idx].label, [{ color: parts[idx].color, label: 'Transactions', value: formatInt(parts[idx].value), note: pct(parts[idx].value) + '% of ' + formatInt(total) }], pt);
            }
            function out() { activate(null); hideTip(); }
            node.addEventListener('mousemove', over);
            node.addEventListener('mouseleave', out);
            node.addEventListener('touchstart', function (e) { var q = pointFromTouch(e); if (q) { over(q); } }, { passive: true });
            node.addEventListener('touchend', out);
        }

        for (i = 0; i < parts.length; i++) {
            if (parts[i].value <= 0) { continue; }
            var len = (parts[i].value / total) * C;
            var dash = parts[i].value === total ? len : Math.max(len - GAP, 0.5);
            var seg = svgEl('circle', {
                'class': 'reports-seg', cx: 95, cy: 95, r: R, fill: 'none', stroke: parts[i].color, 'stroke-width': 24,
                'stroke-dasharray': (animate && !reduced ? '0 ' + C : dash + ' ' + (C - dash)), 'stroke-dashoffset': -offset
            }, segs);
            if (animate && !reduced) { seg.style.transitionDelay = (i * 140) + 'ms'; pending.push({ node: seg, value: dash + ' ' + (C - dash) }); }
            segNodes[i] = seg;
            offset += len;
            bindTip(seg, i);
        }

        var legend = htmlEl('ul', 'reports-legend', null, wrap);
        for (i = 0; i < parts.length; i++) {
            var li = htmlEl('li', 'reports-legend__item', null, legend);
            var sw = htmlEl('span', 'reports-legend__swatch', null, li);
            sw.style.background = parts[i].color;
            htmlEl('span', 'reports-legend__label', parts[i].label, li);
            htmlEl('span', 'reports-legend__value', formatInt(parts[i].value), li);
            htmlEl('span', 'reports-legend__pct', pct(parts[i].value) + '%', li);
            legendNodes[i] = li;
            (function (idx) {
                if (!segNodes[idx]) { return; }
                li.addEventListener('mouseenter', function () { activate(idx); });
                li.addEventListener('mouseleave', function () { activate(null); });
            }(i));
        }

        if (pending.length) {
            (window.requestAnimationFrame || function (f) { setTimeout(f, 16); })(function () {
                (window.requestAnimationFrame || function (f) { setTimeout(f, 16); })(function () {
                    for (var k = 0; k < pending.length; k++) { pending[k].node.setAttribute('stroke-dasharray', pending[k].value); }
                });
            });
        }
    }

    /* ---------- cards, stats and period label ---------- */

    function setNumber(node, target, fmt) {
        target = Number(target) || 0;
        if (node._raf && window.cancelAnimationFrame) { window.cancelAnimationFrame(node._raf); }
        var from = (animate && !reduced) ? (node._val || 0) : target;
        if (from === target || !window.requestAnimationFrame) { node._val = target; node.textContent = fmt(target); return; }
        var start = null;
        function step(ts) {
            if (start === null) { start = ts; }
            var p = Math.min(1, (ts - start) / 700), e = 1 - Math.pow(1 - p, 3);
            node._val = p === 1 ? target : from + (target - from) * e;
            node.textContent = fmt(node._val);
            if (p < 1) { node._raf = window.requestAnimationFrame(step); }
        }
        node._raf = window.requestAnimationFrame(step);
    }

    function roundInt(v) { return formatInt(Math.round(v)); }

    function setStats(node, items) {
        clear(node);
        if (!items) { return; }
        for (var i = 0; i < items.length; i++) {
            var st = htmlEl('div', 'reports-stat', null, node);
            if (items[i].hint) { st.title = items[i].hint; }
            htmlEl('span', 'reports-stat__label', items[i].label, st);
            htmlEl('strong', 'reports-stat__value', items[i].value, st);
        }
    }

    // Days from the start of the selected period up to today (or its end, if that is earlier).
    function elapsedDays(data) {
        var a = parseIso(data.from), b = parseIso(data.to), t = new Date();
        if (!a || !b) { return 0; }
        t = new Date(t.getFullYear(), t.getMonth(), t.getDate());
        if (t < b) { b = t; }
        return Math.max(0, Math.round((b - a) / 86400000) + 1);
    }

    // Summary figures shown above a chart.
    //  Total   = sum of everything plotted (periods that have not started yet are ignored).
    //  Avg/day = Total / days elapsed in the selected period. Days with no activity count as 0,
    //            whatever the chart groups by (day, week or month), so it is always a per-day figure.
    //  Peak    = the single highest period (only requested for Rental Revenue).
    function seriesStats(series, data, o) {
        var vals = series.values, ne = elapsedCount(series.labels), total = 0, peak = 0, i;
        var days = elapsedDays(data);
        if (!ne || !days) { return null; }
        for (i = 0; i < ne; i++) { total += vals[i]; if (vals[i] > vals[peak]) { peak = i; } }
        var avg = total / days;
        var gran = data.granularity;
        var items = [
            { label: 'Total', value: o.money ? formatMoney(total) : formatInt(total) },
            {
                label: 'Avg / day',
                value: o.money ? formatMoney(avg) : avg.toLocaleString('en-US', { maximumFractionDigits: 1 }),
                hint: o.avgHint + ' \u00F7 ' + days + ' day' + (days === 1 ? '' : 's') + ' so far in this period. Days with no activity count as 0.'
            }
        ];
        if (o.peak) {
            items.push({
                label: 'Peak \u00B7 ' + (gran === 'day' ? '' : granularityWord(gran) + ' of ') + shortLabel(series.labels[peak], gran, spansYears(data)),
                value: o.money ? formatMoney(vals[peak]) : formatInt(vals[peak])
            });
        }
        return items;
    }

    /* ---------- rendering ---------- */

    function emptyPayload() {
        return {
            from: applied.from, to: applied.to, granularity: 'day',
            cards: { totalRevenue: 0, totalRentals: 0, itemsRented: 0, totalBorrowings: 0, pendingTransactions: 0, activeRentals: 0, completedRentals: 0, rejectedOrCancelledTransactions: 0, pendingBorrowingRequests: 0, activeBorrowedRentals: 0, completedBorrowedRentals: 0, cancelledOrRejectedBorrowings: 0 },
            rentalRevenue: { labels: [], values: [] }, rentalRequestsReceived: { labels: [], values: [] }, rentalRequestsSubmitted: { labels: [], values: [] }, mostRentedItems: [],
            rentalStatus: { pending: 0, accepted: 0, active: 0, completed: 0, disputed: 0, cancelled: 0, rejected: 0, notCounted: 0 }, transactions: [],
            borrowingActivity: { labels: [], values: [], details: [] }, borrowingExpenses: { labels: [], values: [], details: [] },
            borrowingsWithoutPayment: 0, borrowingsNotCounted: 0
        };
    }

    function render(p, failure) {
        lastPayload = p;
        lastFailure = failure || null;
        var data = p || emptyPayload();
        var years = spansYears(data);
        var gran = data.granularity;
        var word = granularityWord(gran);
        var none = failure ? 'Report unavailable' : 'No data for this period';

        setNumber(el.cardRevenue, data.cards.totalRevenue, formatMoney);
        setNumber(el.cardRentals, data.cards.totalRentals, roundInt);
        setNumber(el.cardItems, data.cards.itemsRented, roundInt);
        setNumber(el.cardBorrowings, data.cards.totalBorrowings, roundInt);
        setNumber(el.cardPendingRentals, data.cards.pendingTransactions, roundInt);
        setNumber(el.cardActiveRentals, data.cards.activeRentals, roundInt);
        setNumber(el.cardCompletedRentals, data.cards.completedRentals, roundInt);
        setNumber(el.cardClosedRentals, data.cards.rejectedOrCancelledTransactions, roundInt);
        setNumber(el.cardPendingBorrowings, data.cards.pendingBorrowingRequests, roundInt);
        setNumber(el.cardActiveBorrowings, data.cards.activeBorrowedRentals, roundInt);
        setNumber(el.cardCompletedBorrowings, data.cards.completedBorrowedRentals, roundInt);
        setNumber(el.cardClosedBorrowings, data.cards.cancelledOrRejectedBorrowings, roundInt);

        el.revenueSubtitle.textContent = 'Completed rentals only, grouped by ' + word;
        el.activitySubtitle.textContent = 'Borrowing transactions per ' + word + ' \u00B7 hover a bar to see the items';
        el.period.textContent = rangeLabel(data.from, data.to) ? 'Showing ' + rangeLabel(data.from, data.to) + ' \u00B7 grouped by ' + word : '';
        setStats(el.statsRevenue, sum(data.rentalRevenue.values) > 0 ? seriesStats(data.rentalRevenue, data, { money: true, peak: true, avgHint: 'Revenue from completed rentals' }) : null);
        setStats(el.statsActivity, sum(data.borrowingActivity.values) > 0 ? seriesStats(data.borrowingActivity, data, { money: false, avgHint: 'Borrowings requested' }) : null);
        setStats(el.statsExpenses, sum(data.borrowingExpenses.values) > 0 ? seriesStats(data.borrowingExpenses, data, { money: true, avgHint: 'Recorded borrowing payments' }) : null);

        // A. Rental revenue
        if (sum(data.rentalRevenue.values) > 0) {
            lineChart(el.chartRevenue, data.rentalRevenue, { height: 320, money: true, color: COLORS.revenue, gran: gran, years: years, name: 'Revenue', peak: true, aria: 'Rental revenue over time' });
        } else {
            emptyState(el.chartRevenue, failure ? none : 'No completed rentals in this period', failure || 'Revenue appears here once a rental of one of your items is completed within the selected dates.');
        }

        // B. Most rented items
        if (data.mostRentedItems.length) {
            topItemsChart(el.chartTopItems, data.mostRentedItems);
        } else {
            emptyState(el.chartTopItems, failure ? none : 'No rented items yet', failure || 'Your most rented items will be ranked here.');
        }

        // C. Status distribution
        var st = data.rentalStatus;
        if (st.pending + st.accepted + st.active + st.completed + st.disputed + st.cancelled + st.rejected > 0) {
            donutChart(el.chartStatus, st);
        } else {
            emptyState(el.chartStatus, failure ? none : 'No rental transactions', failure || 'Requests and finalized rental statuses will be shown here.');
        }
        el.statusFootnote.textContent = st.notCounted > 0
            ? formatInt(st.notCounted) + ' transaction' + (st.notCounted === 1 ? '' : 's') + ' with an unrecognized status not shown.'
            : '';

        // D. Borrowing activity
        if (sum(data.borrowingActivity.values) > 0) {
            barChart(el.chartActivity, data.borrowingActivity, { height: 260, money: false, color: COLORS.activity, gran: gran, years: years, name: 'Borrowings', detailKind: 'activity', aria: 'Borrowing activity' });
        } else {
            emptyState(el.chartActivity, failure ? none : 'No borrowings in this period', failure || 'Items you borrow from other students will be counted here.');
        }

        // E. Borrowing expenses
        if (sum(data.borrowingExpenses.values) > 0) {
            lineChart(el.chartExpenses, data.borrowingExpenses, { height: 260, money: true, color: COLORS.expenses, gran: gran, years: years, name: 'Spent', detailKind: 'expense', lowerIsBetter: true, aria: 'Borrowing expenses' });
        } else {
            emptyState(el.chartExpenses, failure ? none : 'No payments recorded', failure || 'Expenses appear here once a payment for a borrowed item is recorded.');
        }
        var unpaid = data.borrowingsWithoutPayment;
        el.expensesFootnote.textContent = unpaid > 0
            ? formatInt(unpaid) + ' borrowing' + (unpaid === 1 ? '' : 's') + ' in this period ' + (unpaid === 1 ? 'has' : 'have') + ' no payment recorded and ' + (unpaid === 1 ? 'is' : 'are') + ' not included.'
            : '';

        renderRequestChart(el.chartRequestsReceived, data.rentalRequestsReceived, 'Requests received from borrowers', gran, years);
        renderRequestChart(el.chartRequestsSubmitted, data.rentalRequestsSubmitted, 'Requests you submitted', gran, years);
        renderTransactions(data.transactions || []);
    }

    function renderRequestChart(box, series, label, gran, years) {
        if (!box) { return; }
        if (sum(series.values) > 0) {
            barChart(box, series, { height: 220, money: false, color: COLORS.activity, gran: gran, years: years, name: 'Requests', aria: label });
        } else { emptyState(box, 'No requests in this period', label + ' will appear here.'); }
    }

    function renderTransactions(rows) {
        if (!el.transactions) { return; }
        clear(el.transactions);
        if (!rows.length) {
            var empty = htmlEl('tr', '', null, el.transactions);
            var cell = htmlEl('td', 'reports-table__empty', 'No transactions in this period.', empty);
            cell.colSpan = 6;
            return;
        }
        rows.forEach(function (r) {
            var tr = htmlEl('tr', '', null, el.transactions);
            [r.itemName, r.transactionType, r.date, formatMoney(r.amount), r.status, r.transactionId].forEach(function (v) { htmlEl('td', '', v || '—', tr); });
        });
    }

    /* ---------- notices, loading, validation ---------- */

    function setNotice(message, isError) {
        var text = message || baseNotice;
        el.notice.textContent = text || '';
        el.notice.className = 'reports-notice' + (text ? ' is-visible' : '') + (message && isError ? ' is-error' : '');
    }

    function setFilterError(message) {
        el.error.hidden = !message;
        el.error.textContent = message || '';
        el.from.className = 'reports-input';
        el.to.className = 'reports-input';
        if (message) {
            el.from.className += ' is-invalid';
            el.to.className += ' is-invalid';
        }
    }

    function setLoading(on) {
        var boxes = [el.chartRevenue, el.chartTopItems, el.chartStatus, el.chartActivity, el.chartExpenses, el.chartRequestsReceived, el.chartRequestsSubmitted], i;
        for (i = 0; i < boxes.length; i++) {
            boxes[i].className = boxes[i].className.replace(/\s*is-loading/g, '') + (on ? ' is-loading' : '');
        }
        el.refresh.className = 'reports-btn reports-btn--secondary reports-btn--refresh' + (on ? ' is-loading' : '');
        el.refresh.setAttribute('aria-busy', on ? 'true' : 'false');
    }

    function validateRange(from, to) {
        var a = parseIso(from), b = parseIso(to);
        if (!a || !b) { return 'Please choose both a From and a To date.'; }
        if (a > b) { return 'The From date must be on or before the To date.'; }
        if (Math.round((b - a) / 86400000) + 1 > MAX_DAYS) { return 'Please choose a range of 366 days or fewer.'; }
        return null;
    }

    function query(range) { return '?from=' + encodeURIComponent(range.from) + '&to=' + encodeURIComponent(range.to); }

    /* ---------- quick ranges ---------- */

    // Weeks are recorded Monday to Sunday (the same weeks the server groups by).
    function quickRange(kind) {
        var today = new Date();
        today = new Date(today.getFullYear(), today.getMonth(), today.getDate());
        var monday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - ((today.getDay() + 6) % 7));
        var from, to;
        if (kind === 'lastweek') {
            from = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() - 7);
            to = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() - 1);
        } else if (kind === 'month') {
            from = new Date(today.getFullYear(), today.getMonth(), 1);
            to = today;
        } else {                                           // 'week' = this week, Monday to Sunday
            from = monday;
            to = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + 6);
        }
        return { from: toIso(from), to: toIso(to) };
    }

    function highlightChip(range) {
        var i, kind, q;
        for (i = 0; i < chips.length; i++) {
            kind = chips[i].getAttribute('data-range');
            q = quickRange(kind);
            var on = !!range && q.from === range.from && q.to === range.to;
            chips[i].className = 'reports-chip' + (on ? ' is-active' : '');
            chips[i].setAttribute('aria-pressed', on ? 'true' : 'false');
        }
    }

    /* ---------- data loading ---------- */

    function load(range) {
        var id = ++requestId;
        applied = { from: range.from, to: range.to };
        highlightChip(applied);
        setFilterError(null);
        setLoading(true);

        var xhr = new XMLHttpRequest();
        xhr.open('GET', dataUrl + query(range), true);
        xhr.setRequestHeader('Accept', 'application/json');
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4 || id !== requestId) { return; }
            setLoading(false);
            var body = null;
            try { body = JSON.parse(xhr.responseText); } catch (e) { body = null; }

            if (xhr.status === 200 && body && body.cards) {
                setNotice(null);
                render(body, null);
            } else if (xhr.status === 401) {
                setNotice('Please sign in to see your report.', true);
                render(null, 'Sign in to view your rental and borrowing activity.');
            } else if (xhr.status === 400 && body && body.error) {
                setFilterError(body.error);
                setNotice(null);
            } else {
                setNotice('The report could not be loaded. Please try again.', true);
                render(null, 'Something went wrong while loading the report. Use Refresh to try again.');
            }
        };
        xhr.send();
    }

    function applyFromInputs() {
        var range = { from: el.from.value, to: el.to.value };
        var problem = validateRange(range.from, range.to);
        if (problem) { setFilterError(problem); return false; }
        load(range);
        return true;
    }

    /* ---------- export ---------- */

    function saveBlob(blob, name) {
        if (window.navigator && window.navigator.msSaveOrOpenBlob) { window.navigator.msSaveOrOpenBlob(blob, name); return; }
        var url = (window.URL || window.webkitURL).createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url; a.download = name; a.style.display = 'none';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { (window.URL || window.webkitURL).revokeObjectURL(url); }, 1000);
    }

    function exportReport() {
        var range = { from: el.from.value, to: el.to.value };
        var problem = validateRange(range.from, range.to);
        if (problem) { setFilterError(problem); return; }
        setFilterError(null);
        el.exportBtn.disabled = true;

        var xhr = new XMLHttpRequest();
        xhr.open('GET', exportUrl + query(range), true);
        xhr.responseType = 'blob';
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) { return; }
            el.exportBtn.disabled = false;
            if (xhr.status === 200) {
                saveBlob(xhr.response, 'UniShare-Report_' + range.from + '_to_' + range.to + '.csv');
                setNotice(null);
            } else if (xhr.status === 401) {
                setNotice('Please sign in to export your report.', true);
            } else if (xhr.status === 400) {
                setFilterError('The selected dates could not be exported. Please check them and try again.');
            } else {
                setNotice('The report could not be exported. Please try again.', true);
            }
        };
        xhr.send();
    }

    /* ---------- events ---------- */

    el.form.addEventListener('submit', function (e) { e.preventDefault(); applyFromInputs(); });

    el.reset.addEventListener('click', function () {
        el.from.value = defaultFrom;
        el.to.value = defaultTo;
        load({ from: defaultFrom, to: defaultTo });
    });

    el.refresh.addEventListener('click', function () { load(applied); });

    el.exportBtn.addEventListener('click', exportReport);

    (function bindChips() {
        function bind(chip) {
            chip.addEventListener('click', function () {
                var r = quickRange(chip.getAttribute('data-range'));
                el.from.value = r.from;
                el.to.value = r.to;
                load(r);
            });
        }
        for (var i = 0; i < chips.length; i++) { bind(chips[i]); }
    }());

    el.from.addEventListener('change', function () { setFilterError(null); });
    el.to.addEventListener('change', function () { setFilterError(null); });

    var resizeTimer = null, lastWidth = page.clientWidth;
    window.addEventListener('resize', function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function () {
            if (page.clientWidth === lastWidth) { return; }
            lastWidth = page.clientWidth;
            hideTip();
            animate = false;
            if (lastPayload || lastFailure) { render(lastPayload, lastFailure); }
            animate = true;
        }, 150);
    });

    /* ---------- start ---------- */

    render(null, null);
    load(applied);
}());
