(function () {
    'use strict';
    var root = document.getElementById('adminReports');
    if (!root) return;
    var colors = ['#4f46e5', '#0ea5e9', '#10b981', '#ef4444'];
    var ns = 'http://www.w3.org/2000/svg';
    function el(name, attrs, parent, value) { var n = document.createElementNS(ns, name); Object.keys(attrs || {}).forEach(function (k) { n.setAttribute(k, attrs[k]); }); if (value != null) n.textContent = value; if (parent) parent.appendChild(n); return n; }
    function empty(id, text) { var box = document.getElementById(id); box.innerHTML = ''; var n = document.createElement('div'); n.className = 'admin-chart__empty'; n.textContent = text; box.appendChild(n); }
    function setup(id, view) { var box = document.getElementById(id); box.innerHTML = ''; var s = document.createElementNS(ns, 'svg'); s.setAttribute('viewBox', view); s.setAttribute('role', 'img'); box.appendChild(s); return s; }
    function legend(box, labels, fills) { var wrap = document.createElement('div'); wrap.className = 'admin-chart__legend'; labels.forEach(function (x, i) { var item = document.createElement('span'), dot = document.createElement('i'); dot.style.background = fills[i]; item.appendChild(dot); item.appendChild(document.createTextNode(x)); wrap.appendChild(item); }); box.appendChild(wrap); }
    function tooltip(box, node, content) { node.addEventListener('mousemove', function (e) { var t = box.querySelector('.tip'); if (!t) { t = document.createElement('div'); t.className = 'tip'; box.appendChild(t); } t.textContent = content; var r = box.getBoundingClientRect(); t.style.left = Math.max(0, e.clientX - r.left + 10) + 'px'; t.style.top = Math.max(0, e.clientY - r.top - 20) + 'px'; }); node.addEventListener('mouseleave', function () { var t = box.querySelector('.tip'); if (t) t.remove(); }); }
    function lineChart(id, rows) {
        if (!rows.length) return empty(id, 'No rental records with a date are available.');
        var box = document.getElementById(id), s = setup(id, '0 0 640 260'), max = Math.max.apply(null, rows.map(function (x) { return x.count; })) || 1, pad = { l: 42, r: 12, t: 16, b: 42 }, w = 640 - pad.l - pad.r, h = 260 - pad.t - pad.b;
        for (var g = 0; g <= 4; g++) { var y = pad.t + h * g / 4; el('line', { x1: pad.l, y1: y, x2: 628, y2: y, 'class': 'grid' }, s); el('text', { x: pad.l - 8, y: y + 4, 'text-anchor': 'end' }, s, Math.round(max * (4 - g) / 4)); }
        var pts = rows.map(function (r, i) { return [pad.l + (rows.length === 1 ? w / 2 : i * w / (rows.length - 1)), pad.t + h - r.count / max * h]; });
        el('polyline', { points: pts.map(function (p) { return p.join(','); }).join(' '), fill: 'none', stroke: colors[0], 'stroke-width': 3, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' }, s);
        pts.forEach(function (p, i) { var c = el('circle', { cx: p[0], cy: p[1], r: 5, fill: colors[0], tabindex: 0 }, s); tooltip(box, c, rows[i].label + ': ' + rows[i].count + ' rentals'); });
        [0, Math.floor((rows.length - 1) / 2), rows.length - 1].filter(function (v, i, a) { return a.indexOf(v) === i; }).forEach(function (i) { el('text', { x: pts[i][0], y: 248, 'text-anchor': 'middle' }, s, rows[i].label); });
    }
    function donutChart(id, data) {
        var rows = [{ label: 'Pending', value: data.pending, color: '#f59e0b' }, { label: 'Approved', value: data.approved, color: '#10b981' }, { label: 'Rejected', value: data.rejected, color: '#ef4444' }], total = rows.reduce(function (a, x) { return a + x.value; }, 0), box = document.getElementById(id);
        if (!total) return empty(id, 'No listings have been recorded.');
        var s = setup(id, '0 0 420 270'), cx = 210, cy = 130, r = 85, circ = 2 * Math.PI * r, offset = 0;
        rows.forEach(function (row) { if (!row.value) return; var len = circ * row.value / total; var c = el('circle', { cx: cx, cy: cy, r: r, fill: 'none', stroke: row.color, 'stroke-width': 30, 'stroke-dasharray': len + ' ' + (circ - len), 'stroke-dashoffset': -offset, transform: 'rotate(-90 ' + cx + ' ' + cy + ')', tabindex: 0 }, s); tooltip(box, c, row.label + ': ' + row.value + ' (' + Math.round(100 * row.value / total) + '%)'); offset += len; });
        el('text', { x: cx, y: cy - 3, 'text-anchor': 'middle', style: 'font-size:24px;font-weight:700;fill:#0f172a' }, s, total); el('text', { x: cx, y: cy + 17, 'text-anchor': 'middle' }, s, 'listings');
        legend(box, rows.map(function (x) { return x.label + ' ' + x.value + ' (' + Math.round(x.value * 100 / total) + '%)'; }), rows.map(function (x) { return x.color; }));
    }
    function horizontal(id, rows) {
        if (!rows.length) return empty(id, 'No rental transactions are available for ranking.');
        var box = document.getElementById(id), height = Math.max(250, rows.length * 30 + 20), s = setup(id, '0 0 640 ' + height), max = rows[0].count || 1;
        s.setAttribute('viewBox', '0 0 640 ' + height); s.style.height = height + 'px';
        rows.forEach(function (r, i) { var y = 10 + i * 30; el('text', { x: 0, y: y + 15 }, s, r.name.length > 28 ? r.name.substring(0, 26) + '…' : r.name); var bar = el('rect', { x: 235, y: y + 2, width: Math.max(3, 340 * r.count / max), height: 18, rx: 5, fill: colors[0], tabindex: 0 }, s); tooltip(box, bar, r.name + ': ' + r.count + ' rentals'); el('text', { x: 590, y: y + 16, 'text-anchor': 'end' }, s, r.count); });
    }
    function grouped(id, rows) {
        if (!rows.length) return empty(id, 'No user transaction activity is available.');
        var box = document.getElementById(id), shown = rows.slice(0, 8), s = setup(id, '0 0 640 290'), max = Math.max.apply(null, shown.map(function (x) { return Math.max(x.renting, x.borrowing); })) || 1, base = 238;
        for (var g = 0; g <= 4; g++) { var y = 20 + (base - 20) * g / 4; el('line', { x1: 38, y1: y, x2: 632, y2: y, 'class': 'grid' }, s); }
        shown.forEach(function (r, i) { var x = 48 + i * 72, h1 = 175 * r.renting / max, h2 = 175 * r.borrowing / max, a = el('rect', { x: x, y: base - h1, width: 22, height: h1, rx: 3, fill: colors[0], tabindex: 0 }, s), b = el('rect', { x: x + 25, y: base - h2, width: 22, height: h2, rx: 3, fill: colors[1], tabindex: 0 }, s); tooltip(box, a, r.user + ' · Renting: ' + r.renting); tooltip(box, b, r.user + ' · Item owner: ' + r.borrowing); el('text', { x: x + 23, y: 258, 'text-anchor': 'middle' }, s, r.user.length > 9 ? r.user.substring(0, 8) + '…' : r.user); });
        legend(box, ['Renting transactions', 'Item owner transactions'], colors.slice(0, 2));
    }
    function load() {
        document.getElementById('reportError').hidden = true;
        ['rentalChart', 'statusChart', 'itemsChart', 'activityChart'].forEach(function (id) { document.getElementById(id).textContent = 'Loading…'; });
        fetch(root.getAttribute('data-url') + '?period=' + encodeURIComponent(document.getElementById('reportPeriod').value), { credentials: 'same-origin', headers: { Accept: 'application/json' } }).then(function (r) { if (!r.ok) throw new Error('Analytics could not be loaded. Check the database connection and retry.'); return r.json(); }).then(function (d) {
            document.getElementById('platformRevenue').textContent = d.revenueAvailable ? new Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' }).format(d.cards.platformRevenue) : 'Not tracked';
            document.getElementById('revenueNote').textContent = d.revenueNote;
            document.getElementById('totalRentals').textContent = Number(d.cards.totalRentals).toLocaleString(); document.getElementById('totalUsers').textContent = Number(d.cards.totalUsers).toLocaleString(); document.getElementById('totalItems').textContent = Number(d.cards.totalItems).toLocaleString();
            lineChart('rentalChart', d.rentalsByDate || []); donutChart('statusChart', d.listingStatuses); horizontal('itemsChart', d.topItems || []); grouped('activityChart', d.activity || []);
            document.getElementById('activityNote').textContent = d.activityNote;
        }).catch(function (e) { document.getElementById('reportError').textContent = e.message; document.getElementById('reportError').hidden = false; ['rentalChart', 'statusChart', 'itemsChart', 'activityChart'].forEach(function (id) { empty(id, 'Analytics are unavailable.'); }); });
    }
    document.getElementById('reportPeriod').addEventListener('change', load); load();
}());
