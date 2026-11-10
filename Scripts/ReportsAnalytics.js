/* ==========================================================================
   UniShare - Reports & Analytics Controller (Pure SVG Implementation)
   Matches exact HTML IDs and JSON structure. No external chart libraries.
   ========================================================================== */

(function () {
    'use strict';

    // Helper: format currency
    function formatCurrency(val) {
        var num = Number(val) || 0;
        return '₱' + num.toLocaleString('en-PH', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    // Helper: format integer
    function formatNumber(val) {
        var num = Number(val) || 0;
        return num.toLocaleString('en-US');
    }

    // Tooltip handling
    var tooltipEl = null;

    function initTooltip() {
        tooltipEl = document.getElementById('reportsTooltip');
    }

    function showTooltip(x, y, title, content) {
        if (!tooltipEl) return;
        tooltipEl.innerHTML = '<div class="reports-tooltip__title">' + title + '</div><div class="reports-tooltip__body">' + content + '</div>';
        tooltipEl.removeAttribute('hidden');
        tooltipEl.style.display = 'block';

        var ttWidth = tooltipEl.offsetWidth || 140;
        var ttHeight = tooltipEl.offsetHeight || 50;
        var posX = x + 12;
        var posY = y - ttHeight - 8;

        if (posX + ttWidth > window.innerWidth - 16) {
            posX = x - ttWidth - 12;
        }
        if (posY < 16) {
            posY = y + 16;
        }

        tooltipEl.style.left = posX + 'px';
        tooltipEl.style.top = posY + 'px';
    }

    function hideTooltip() {
        if (!tooltipEl) return;
        tooltipEl.setAttribute('hidden', '');
        tooltipEl.style.display = 'none';
    }

    // SVG Line/Area Chart Renderer (for Revenue & Expenses)
    function renderLineChart(containerId, labels, values, isCurrency, strokeColor, fillColor) {
        var container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';

        if (!labels || labels.length === 0 || !values || values.length === 0) {
            container.innerHTML = '<div class="reports-chart__empty">No data available for this range</div>';
            return;
        }

        var width = container.clientWidth || 600;
        var height = container.clientHeight || (container.classList.contains('reports-chart--tall') ? 300 : 240);
        if (height < 180) height = container.classList.contains('reports-chart--tall') ? 300 : 240;

        var padding = { top: 25, right: 30, bottom: 45, left: 65 };
        var plotWidth = width - padding.left - padding.right;
        var plotHeight = height - padding.top - padding.bottom;

        var maxVal = Math.max.apply(null, values);
        if (maxVal <= 0) maxVal = 100;
        // Add 15% headroom
        maxVal = Math.ceil(maxVal * 1.15);

        var n = values.length;
        var points = [];

        for (var i = 0; i < n; i++) {
            var px = padding.left + (n === 1 ? plotWidth / 2 : (i / (n - 1)) * plotWidth);
            var py = padding.top + plotHeight - (values[i] / maxVal) * plotHeight;
            points.push({ x: px, y: py, label: labels[i], value: values[i] });
        }

        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
        svg.setAttribute('class', 'reports-svg');
        svg.setAttribute('width', '100%');
        svg.setAttribute('height', '100%');

        // Linear Gradient for Area
        var defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
        var gradId = 'grad-' + containerId;
        var grad = document.createElementNS('http://www.w3.org/2000/svg', 'linearGradient');
        grad.setAttribute('id', gradId);
        grad.setAttribute('x1', '0');
        grad.setAttribute('y1', '0');
        grad.setAttribute('x2', '0');
        grad.setAttribute('y2', '1');

        var stop1 = document.createElementNS('http://www.w3.org/2000/svg', 'stop');
        stop1.setAttribute('offset', '0%');
        stop1.setAttribute('stop-color', fillColor);
        stop1.setAttribute('stop-opacity', '0.45');
        grad.appendChild(stop1);

        var stop2 = document.createElementNS('http://www.w3.org/2000/svg', 'stop');
        stop2.setAttribute('offset', '100%');
        stop2.setAttribute('stop-color', fillColor);
        stop2.setAttribute('stop-opacity', '0.02');
        grad.appendChild(stop2);
        defs.appendChild(grad);
        svg.appendChild(defs);

        // Gridlines & Y-Axis Ticks (4 steps)
        var steps = 4;
        for (var s = 0; s <= steps; s++) {
            var stepVal = (maxVal / steps) * s;
            var gy = padding.top + plotHeight - (s / steps) * plotHeight;

            // Gridline
            var line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', padding.left);
            line.setAttribute('y1', gy);
            line.setAttribute('x2', width - padding.right);
            line.setAttribute('y2', gy);
            line.setAttribute('stroke', '#E2E8F0');
            line.setAttribute('stroke-dasharray', s === 0 ? 'none' : '4 4');
            line.setAttribute('stroke-width', '1');
            svg.appendChild(line);

            // Tick Label
            var text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            text.setAttribute('x', padding.left - 10);
            text.setAttribute('y', gy + 4);
            text.setAttribute('text-anchor', 'end');
            text.setAttribute('fill', '#94A3B8');
            text.setAttribute('font-size', '11');
            text.setAttribute('font-family', 'sans-serif');
            text.textContent = isCurrency ? ('₱' + (stepVal >= 1000 ? (stepVal / 1000).toFixed(1) + 'k' : stepVal.toFixed(0))) : stepVal.toFixed(0);
            svg.appendChild(text);
        }

        // Area Path
        if (points.length > 0) {
            var areaD = 'M ' + points[0].x + ' ' + (padding.top + plotHeight);
            for (var p = 0; p < points.length; p++) {
                areaD += ' L ' + points[p].x + ' ' + points[p].y;
            }
            areaD += ' L ' + points[points.length - 1].x + ' ' + (padding.top + plotHeight) + ' Z';

            var area = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            area.setAttribute('d', areaD);
            area.setAttribute('fill', 'url(#' + gradId + ')');
            svg.appendChild(area);

            // Line Path
            var lineD = 'M ' + points[0].x + ' ' + points[0].y;
            for (var l = 1; l < points.length; l++) {
                lineD += ' L ' + points[l].x + ' ' + points[l].y;
            }
            var path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            path.setAttribute('d', lineD);
            path.setAttribute('fill', 'none');
            path.setAttribute('stroke', strokeColor);
            path.setAttribute('stroke-width', '3');
            path.setAttribute('stroke-linecap', 'round');
            path.setAttribute('stroke-linejoin', 'round');
            svg.appendChild(path);
        }

        // X-Axis Labels & Points with Tooltips
        points.forEach(function (pt) {
            // X-axis label
            var xLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            xLabel.setAttribute('x', pt.x);
            xLabel.setAttribute('y', height - 15);
            xLabel.setAttribute('text-anchor', 'middle');
            xLabel.setAttribute('fill', '#64748B');
            xLabel.setAttribute('font-size', '11');
            xLabel.setAttribute('font-family', 'sans-serif');
            // short date format (MM-dd)
            var shortLabel = pt.label.length >= 10 ? pt.label.substring(5) : pt.label;
            xLabel.textContent = shortLabel;
            svg.appendChild(xLabel);

            // Interactive Circle
            var circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', pt.x);
            circle.setAttribute('cy', pt.y);
            circle.setAttribute('r', '5');
            circle.setAttribute('fill', '#FFFFFF');
            circle.setAttribute('stroke', strokeColor);
            circle.setAttribute('stroke-width', '3');
            circle.setAttribute('class', 'reports-chart__point');
            circle.style.cursor = 'pointer';

            circle.addEventListener('mouseenter', function (e) {
                circle.setAttribute('r', '7');
                circle.setAttribute('fill', strokeColor);
                var formattedVal = isCurrency ? formatCurrency(pt.value) : formatNumber(pt.value);
                showTooltip(e.pageX, e.pageY, pt.label, '<strong>' + formattedVal + '</strong>');
            });
            circle.addEventListener('mousemove', function (e) {
                var formattedVal = isCurrency ? formatCurrency(pt.value) : formatNumber(pt.value);
                showTooltip(e.pageX, e.pageY, pt.label, '<strong>' + formattedVal + '</strong>');
            });
            circle.addEventListener('mouseleave', function () {
                circle.setAttribute('r', '5');
                circle.setAttribute('fill', '#FFFFFF');
                hideTooltip();
            });

            svg.appendChild(circle);
        });

        container.appendChild(svg);
    }

    // SVG Vertical Bar Chart Renderer (for Borrowing Activity)
    function renderVerticalBarChart(containerId, labels, values) {
        var container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';

        if (!labels || labels.length === 0 || !values || values.length === 0) {
            container.innerHTML = '<div class="reports-chart__empty">No data available</div>';
            return;
        }

        var width = container.clientWidth || 450;
        var height = container.clientHeight || 240;
        if (height < 180) height = 240;

        var padding = { top: 25, right: 25, bottom: 45, left: 45 };
        var plotWidth = width - padding.left - padding.right;
        var plotHeight = height - padding.top - padding.bottom;

        var maxVal = Math.max.apply(null, values);
        if (maxVal <= 0) maxVal = 10;
        maxVal = Math.ceil(maxVal * 1.2);

        var n = values.length;
        var slotWidth = plotWidth / n;
        var barWidth = Math.min(36, slotWidth * 0.55);

        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
        svg.setAttribute('class', 'reports-svg');
        svg.setAttribute('width', '100%');
        svg.setAttribute('height', '100%');

        // Y Gridlines
        var steps = 4;
        for (var s = 0; s <= steps; s++) {
            var stepVal = Math.round((maxVal / steps) * s);
            var gy = padding.top + plotHeight - (s / steps) * plotHeight;

            var line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', padding.left);
            line.setAttribute('y1', gy);
            line.setAttribute('x2', width - padding.right);
            line.setAttribute('y2', gy);
            line.setAttribute('stroke', '#E2E8F0');
            line.setAttribute('stroke-dasharray', s === 0 ? 'none' : '4 4');
            line.setAttribute('stroke-width', '1');
            svg.appendChild(line);

            var text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            text.setAttribute('x', padding.left - 8);
            text.setAttribute('y', gy + 4);
            text.setAttribute('text-anchor', 'end');
            text.setAttribute('fill', '#94A3B8');
            text.setAttribute('font-size', '11');
            text.textContent = stepVal;
            svg.appendChild(text);
        }

        // Bars
        for (var i = 0; i < n; i++) {
            var cx = padding.left + i * slotWidth + slotWidth / 2;
            var bx = cx - barWidth / 2;
            var barHeight = (values[i] / maxVal) * plotHeight;
            var by = padding.top + plotHeight - barHeight;

            var rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x', bx);
            rect.setAttribute('y', by);
            rect.setAttribute('width', barWidth);
            rect.setAttribute('height', Math.max(2, barHeight));
            rect.setAttribute('rx', '4');
            rect.setAttribute('fill', '#4F46E5');
            rect.style.cursor = 'pointer';
            rect.style.transition = 'opacity 0.2s';

            (function (label, val, el) {
                el.addEventListener('mouseenter', function (e) {
                    el.style.opacity = '0.8';
                    showTooltip(e.pageX, e.pageY, label, '<strong>' + formatNumber(val) + ' borrowings</strong>');
                });
                el.addEventListener('mousemove', function (e) {
                    showTooltip(e.pageX, e.pageY, label, '<strong>' + formatNumber(val) + ' borrowings</strong>');
                });
                el.addEventListener('mouseleave', function () {
                    el.style.opacity = '1';
                    hideTooltip();
                });
            }(labels[i], values[i], rect));

            svg.appendChild(rect);

            // Label
            var xLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            xLabel.setAttribute('x', cx);
            xLabel.setAttribute('y', height - 15);
            xLabel.setAttribute('text-anchor', 'middle');
            xLabel.setAttribute('fill', '#64748B');
            xLabel.setAttribute('font-size', '11');
            var shortLabel = labels[i].length >= 10 ? labels[i].substring(5) : labels[i];
            xLabel.textContent = shortLabel;
            svg.appendChild(xLabel);
        }

        container.appendChild(svg);
    }

    // SVG Horizontal Bar Chart Renderer (for Most Rented Items)
    function renderHorizontalBarChart(containerId, items) {
        var container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';

        if (!items || items.length === 0) {
            container.innerHTML = '<div class="reports-chart__empty">No items rented in this range</div>';
            return;
        }

        var width = container.clientWidth || 450;
        var rowHeight = 38;
        var height = Math.max(180, items.length * rowHeight + 30);

        var padding = { top: 15, right: 55, bottom: 15, left: 140 };
        var plotWidth = width - padding.left - padding.right;

        var maxVal = 0;
        for (var m = 0; m < items.length; m++) {
            if (items[m].quantity > maxVal) maxVal = items[m].quantity;
        }
        if (maxVal <= 0) maxVal = 10;

        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
        svg.setAttribute('class', 'reports-svg');
        svg.setAttribute('width', '100%');
        svg.setAttribute('height', height + 'px');

        items.forEach(function (item, index) {
            var y = padding.top + index * rowHeight;
            var barW = Math.max(4, (item.quantity / maxVal) * plotWidth);

            // Item Name (truncated if long)
            var text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            text.setAttribute('x', padding.left - 10);
            text.setAttribute('y', y + 16);
            text.setAttribute('text-anchor', 'end');
            text.setAttribute('fill', '#1E293B');
            text.setAttribute('font-size', '12');
            text.setAttribute('font-weight', '500');
            var displayName = item.name || 'Item';
            if (displayName.length > 18) displayName = displayName.substring(0, 16) + '...';
            text.textContent = displayName;
            svg.appendChild(text);

            // Bar background track
            var track = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            track.setAttribute('x', padding.left);
            track.setAttribute('y', y + 4);
            track.setAttribute('width', plotWidth);
            track.setAttribute('height', 16);
            track.setAttribute('rx', '4');
            track.setAttribute('fill', '#F1F5F9');
            svg.appendChild(track);

            // Bar filled
            var bar = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            bar.setAttribute('x', padding.left);
            bar.setAttribute('y', y + 4);
            bar.setAttribute('width', barW);
            bar.setAttribute('height', 16);
            bar.setAttribute('rx', '4');
            bar.setAttribute('fill', '#06B6D4');
            bar.style.cursor = 'pointer';

            (function (it, el) {
                el.addEventListener('mouseenter', function (e) {
                    el.setAttribute('fill', '#0891B2');
                    showTooltip(e.pageX, e.pageY, it.name, '<strong>' + formatNumber(it.quantity) + ' units rented</strong>');
                });
                el.addEventListener('mousemove', function (e) {
                    showTooltip(e.pageX, e.pageY, it.name, '<strong>' + formatNumber(it.quantity) + ' units rented</strong>');
                });
                el.addEventListener('mouseleave', function () {
                    el.setAttribute('fill', '#06B6D4');
                    hideTooltip();
                });
            }(item, bar));

            svg.appendChild(bar);

            // Value text
            var valText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            valText.setAttribute('x', padding.left + barW + 8);
            valText.setAttribute('y', y + 16);
            valText.setAttribute('fill', '#64748B');
            valText.setAttribute('font-size', '11');
            valText.setAttribute('font-weight', '600');
            valText.textContent = formatNumber(item.quantity);
            svg.appendChild(valText);
        });

        container.appendChild(svg);
    }

    // SVG Donut Chart Renderer (for Rental Status Distribution)
    function renderDonutChart(containerId, statusData) {
        var container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';

        var active = Number(statusData.active) || 0;
        var completed = Number(statusData.completed) || 0;
        var disputed = Number(statusData.disputed) || 0;
        var total = active + completed + disputed;

        if (total === 0) {
            container.innerHTML = '<div class="reports-chart__empty">No rental records found</div>';
            return;
        }

        var width = container.clientWidth || 380;
        var height = 240;
        var cx = 110;
        var cy = 115;
        var r = 70;
        var strokeWidth = 24;

        var slices = [
            { label: 'Completed', value: completed, color: '#10B981' },
            { label: 'Active', value: active, color: '#4F46E5' },
            { label: 'Disputed', value: disputed, color: '#EF4444' }
        ];

        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
        svg.setAttribute('class', 'reports-svg');
        svg.setAttribute('width', '100%');
        svg.setAttribute('height', height + 'px');

        var circumference = 2 * Math.PI * r;
        var accumulatedPercent = 0;

        slices.forEach(function (slice) {
            if (slice.value <= 0) return;
            var percent = slice.value / total;
            var strokeDasharray = (percent * circumference) + ' ' + circumference;
            var strokeDashoffset = -(accumulatedPercent * circumference);

            var circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', cx);
            circle.setAttribute('cy', cy);
            circle.setAttribute('r', r);
            circle.setAttribute('fill', 'transparent');
            circle.setAttribute('stroke', slice.color);
            circle.setAttribute('stroke-width', strokeWidth);
            circle.setAttribute('stroke-dasharray', strokeDasharray);
            circle.setAttribute('stroke-dashoffset', strokeDashoffset);
            circle.setAttribute('transform', 'rotate(-90 ' + cx + ' ' + cy + ')');
            circle.style.cursor = 'pointer';

            (function (s, p, el) {
                el.addEventListener('mouseenter', function (e) {
                    el.style.opacity = '0.85';
                    var pct = (p * 100).toFixed(1) + '%';
                    showTooltip(e.pageX, e.pageY, s.label, '<strong>' + formatNumber(s.value) + ' (' + pct + ')</strong>');
                });
                el.addEventListener('mousemove', function (e) {
                    var pct = (p * 100).toFixed(1) + '%';
                    showTooltip(e.pageX, e.pageY, s.label, '<strong>' + formatNumber(s.value) + ' (' + pct + ')</strong>');
                });
                el.addEventListener('mouseleave', function () {
                    el.style.opacity = '1';
                    hideTooltip();
                });
            }(slice, percent, circle));

            svg.appendChild(circle);
            accumulatedPercent += percent;
        });

        // Center Total Text
        var centerText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        centerText.setAttribute('x', cx);
        centerText.setAttribute('y', cy - 2);
        centerText.setAttribute('text-anchor', 'middle');
        centerText.setAttribute('fill', '#0F172A');
        centerText.setAttribute('font-size', '18');
        centerText.setAttribute('font-weight', '700');
        centerText.textContent = formatNumber(total);
        svg.appendChild(centerText);

        var centerSub = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        centerSub.setAttribute('x', cx);
        centerSub.setAttribute('y', cy + 16);
        centerSub.setAttribute('text-anchor', 'middle');
        centerSub.setAttribute('fill', '#94A3B8');
        centerSub.setAttribute('font-size', '11');
        centerSub.textContent = 'Total Rentals';
        svg.appendChild(centerSub);

        // Legend on Right Side
        var legendX = 220;
        var legendY = 60;
        slices.forEach(function (slice, idx) {
            var ly = legendY + idx * 36;

            var dot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            dot.setAttribute('cx', legendX);
            dot.setAttribute('cy', ly);
            dot.setAttribute('r', '6');
            dot.setAttribute('fill', slice.color);
            svg.appendChild(dot);

            var lText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            lText.setAttribute('x', legendX + 14);
            lText.setAttribute('y', ly + 4);
            lText.setAttribute('fill', '#1E293B');
            lText.setAttribute('font-size', '12');
            lText.setAttribute('font-weight', '600');
            lText.textContent = slice.label;
            svg.appendChild(lText);

            var lSub = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            lSub.setAttribute('x', legendX + 14);
            lSub.setAttribute('y', ly + 18);
            lSub.setAttribute('fill', '#64748B');
            lSub.setAttribute('font-size', '11');
            var pct = total > 0 ? ((slice.value / total) * 100).toFixed(0) + '%' : '0%';
            lSub.textContent = formatNumber(slice.value) + ' (' + pct + ')';
            svg.appendChild(lSub);
        });

        container.appendChild(svg);
    }

    // Main Controller Logic
    function initReports() {
        var page = document.getElementById('reportsPage');
        if (!page) return;

        initTooltip();

        var dataUrl = page.getAttribute('data-data-url') || '/ReportsAnalytics/Data';
        var defaultFrom = page.getAttribute('data-default-from') || '';
        var defaultTo = page.getAttribute('data-default-to') || '';

        var filterFrom = document.getElementById('filterFrom');
        var filterTo = document.getElementById('filterTo');
        var filterForm = document.getElementById('reportsFilterForm');
        var btnReset = document.getElementById('btnResetFilter');
        var filterError = document.getElementById('filterError');
        var reportsNotice = document.getElementById('reportsNotice');
        var reportsPeriod = document.getElementById('reportsPeriod');

        var cardRevenue = document.getElementById('cardRevenue');
        var cardRentals = document.getElementById('cardRentals');
        var cardItems = document.getElementById('cardItems');
        var cardBorrowings = document.getElementById('cardBorrowings');
        var statsRevenue = document.getElementById('statsRevenue');

        // Set default filter dates
        if (filterFrom && defaultFrom) filterFrom.value = defaultFrom;
        if (filterTo && defaultTo) filterTo.value = defaultTo;

        function showNotice(msg, type) {
            if (!reportsNotice) return;
            reportsNotice.textContent = msg;
            reportsNotice.className = 'reports-notice ' + (type ? 'reports-notice--' + type : '');
            reportsNotice.style.display = msg ? 'block' : 'none';
        }

        function loadReportsData(fromDate, toDate) {
            showNotice('Loading analytics data...', 'info');

            var url = dataUrl + '?from=' + encodeURIComponent(fromDate || '') + '&to=' + encodeURIComponent(toDate || '');

            fetch(url, { method: 'GET', credentials: 'same-origin' })
                .then(function (res) {
                    if (!res.ok) throw new Error('HTTP error ' + res.status);
                    return res.json();
                })
                .then(function (data) {
                    showNotice('', '');

                    // Update Period label
                    if (reportsPeriod && data.from && data.to) {
                        reportsPeriod.textContent = 'Showing data from ' + data.from + ' to ' + data.to;
                    }

                    // Update Overview Cards
                    if (data.cards) {
                        if (cardRevenue) cardRevenue.textContent = formatCurrency(data.cards.totalRevenue);
                        if (cardRentals) cardRentals.textContent = formatNumber(data.cards.totalRentals);
                        if (cardItems) cardItems.textContent = formatNumber(data.cards.itemsRented);
                        if (cardBorrowings) cardBorrowings.textContent = formatNumber(data.cards.totalBorrowings);
                    }

                    // 1. Rental Revenue Over Time
                    if (data.rentalRevenue) {
                        renderLineChart('chartRevenue', data.rentalRevenue.labels, data.rentalRevenue.values, true, '#4F46E5', '#4F46E5');
                        if (statsRevenue && data.rentalRevenue.values && data.rentalRevenue.values.length > 0) {
                            var sum = data.rentalRevenue.values.reduce(function (a, b) { return a + b; }, 0);
                            var avg = sum / data.rentalRevenue.values.length;
                            statsRevenue.innerHTML = '<span class="reports-stat-badge">Total: ' + formatCurrency(sum) + '</span> <span class="reports-stat-badge">Avg: ' + formatCurrency(avg) + '</span>';
                        }
                    }

                    // 2. Most Rented Items
                    if (data.mostRentedItems) {
                        renderHorizontalBarChart('chartTopItems', data.mostRentedItems);
                    }

                    // 3. Rental Status Distribution
                    if (data.rentalStatus) {
                        renderDonutChart('chartStatus', data.rentalStatus);
                    }

                    // 4. Borrowing Activity
                    if (data.borrowingActivity) {
                        renderVerticalBarChart('chartActivity', data.borrowingActivity.labels, data.borrowingActivity.values);
                    }

                    // 5. Borrowing Expenses
                    if (data.borrowingExpenses) {
                        renderLineChart('chartExpenses', data.borrowingExpenses.labels, data.borrowingExpenses.values, true, '#10B981', '#10B981');
                    }
                })
                .catch(function (err) {
                    console.error('Error fetching reports data:', err);
                    showNotice('Unable to load reports data. Please check connection and try again.', 'error');
                });
        }

        // Form Submit
        if (filterForm) {
            filterForm.addEventListener('submit', function (e) {
                e.preventDefault();
                var fVal = filterFrom ? filterFrom.value : '';
                var tVal = filterTo ? filterTo.value : '';

                if (fVal && tVal && fVal > tVal) {
                    if (filterError) {
                        filterError.textContent = 'The "From" date cannot be after the "To" date.';
                        filterError.removeAttribute('hidden');
                        filterError.style.display = 'block';
                    }
                    return;
                }

                if (filterError) {
                    filterError.setAttribute('hidden', '');
                    filterError.style.display = 'none';
                }

                loadReportsData(fVal, tVal);
            });
        }

        // Reset Filter
        if (btnReset) {
            btnReset.addEventListener('click', function () {
                if (filterFrom) filterFrom.value = defaultFrom;
                if (filterTo) filterTo.value = defaultTo;
                if (filterError) {
                    filterError.setAttribute('hidden', '');
                    filterError.style.display = 'none';
                }
                loadReportsData(defaultFrom, defaultTo);
            });
        }

        // Window resize re-render charts
        var resizeTimer;
        window.addEventListener('resize', function () {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(function () {
                var fVal = filterFrom ? filterFrom.value : defaultFrom;
                var tVal = filterTo ? filterTo.value : defaultTo;
                loadReportsData(fVal, tVal);
            }, 250);
        });

        // Initial Load
        loadReportsData(filterFrom ? filterFrom.value : defaultFrom, filterTo ? filterTo.value : defaultTo);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initReports);
    } else {
        initReports();
    }
}());
